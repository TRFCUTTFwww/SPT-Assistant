using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Passcodes;
using EFT.UI;
using HarmonyLib;
using UnityEngine;
using NativeCodes=Il2CppSystem.Collections.Generic.Dictionary<string,string>;
namespace LocalSpt5Esp;
internal static class PasscodeHints {
 static ConfigEntry<bool> enabled=null!,acceptAny=null!;
 static bool hookInstalled;
 static IntPtr worldPointer,panelPointer;
 static string terminalId="",code="",text="";
 static float nextRead,nextError;
 static int overriddenAttempts;
 static bool lastAccepted;
 sealed class OverrideScope {
  readonly NativeCodes codes;readonly string id;readonly bool existed;readonly string previous;
  bool restored;
  internal OverrideScope(NativeCodes dictionary,string key){codes=dictionary;id=key;existed=codes.TryGetValue(id,out var value);previous=value;}
  internal void Apply(string input)=>codes[id]=input;
  internal void Restore(){if(restored)return;if(existed)codes[id]=previous;else codes.Remove(id);restored=true;}
 }
 internal static void Initialize(Plugin plugin){
  enabled=FeatureConfig.Bind("13. Raid info","Passcode hints","密码输入时提示正确密码",true,"打开实验室密码终端时，显示本局当前终端的密码或任意密码通过状态。");
  acceptAny=FeatureConfig.Bind("13. Raid info","Accept any laboratory passcode","实验室任意密码通过",true,"实验室终端提交任意非空密码即可通过，保留终端原有输入格式与确认操作。");
  try{var method=AccessTools.Method(typeof(ClientLocalPasscodeController),"HandleSubmitPasscode",new[]{typeof(Player),typeof(PasscodeTerminal),typeof(string)})??throw new MissingMethodException("Local passcode validation missing");plugin.Patches.Patch(method,prefix:new HarmonyMethod(typeof(PasscodeHints),nameof(BeforeSubmit)),postfix:new HarmonyMethod(typeof(PasscodeHints),nameof(AfterSubmit)),finalizer:new HarmonyMethod(typeof(PasscodeHints),nameof(FinishSubmit)));hookInstalled=true;plugin.Log.LogInfo("Laboratory passcode assistance ready: scoped local validation override, native submission retained");}catch(Exception error){plugin.Log.LogError("Laboratory passcode assistance could not initialize: "+error);}
 }
 static bool InLab(Player? player)=>player!=null&&player.Location is "laboratory" or "laboratory_dark";
 static void BeforeSubmit(ClientLocalPasscodeController __instance,Player __0,PasscodeTerminal __1,string __2,out OverrideScope? __state){
  __state=null;
  try{
   var world=Singleton<GameWorld>.Instance;var local=world?.MainPlayer;
   if(!acceptAny.Value||!RaidClock.Active||!InLab(local)||__0==null||__0.Pointer!=local!.Pointer||__instance.Pointer!=world!.PasscodeController?.Pointer||__1==null||string.IsNullOrEmpty(__1.PasscodeId)||string.IsNullOrWhiteSpace(__2))return;
   var codes=__instance._generatedPasscodes;if(codes==null){Warn(new InvalidOperationException("Local passcode table is unavailable"));return;}
   __state=new OverrideScope(codes,__1.PasscodeId);__state.Apply(__2);
  }catch(Exception error){try{__state?.Restore();}catch(Exception restoreError){Warn(restoreError);}__state=null;Warn(error);}
 }
 static void AfterSubmit(bool __result,OverrideScope? __state){if(__state==null)return;overriddenAttempts++;lastAccepted=__result;try{__state.Restore();}catch(Exception error){Warn(error);}}
 static void FinishSubmit(OverrideScope? __state){try{__state?.Restore();}catch(Exception error){Warn(error);}}
 internal static void Reset(){worldPointer=panelPointer=IntPtr.Zero;terminalId=code=text="";}
 static PasscodeInputPanel? CurrentPanel(){
  var world=Singleton<GameWorld>.Instance;var local=world?.MainPlayer;
  if(!enabled.Value||!RaidClock.Active||!InLab(local))return null;
  var panel=GameUI.Instance?.PasscodeInputPanel;
  if(panel==null||!panel._shown||!panel.gameObject.activeInHierarchy||panel._player?.Pointer!=local!.Pointer||panel.Terminal==null||panel._controller==null||panel._controller.Pointer!=world!.PasscodeController?.Pointer)return null;
  return panel;
 }
 static void Read(){
  Reset();var panel=CurrentPanel();if(panel==null)return;
  worldPointer=Singleton<GameWorld>.Instance.Pointer;panelPointer=panel.Pointer;terminalId=panel.Terminal.PasscodeId??"";
  if(acceptAny.Value&&hookInstalled){text="任意密码通过已开启";return;}
  var controller=panel._controller.TryCast<ClientLocalPasscodeController>();
  if(controller==null){text="当前战局未提供终端密码";return;}
  var generated=controller._generatedPasscodes;
  if(terminalId.Length==0||generated==null||!generated.TryGetValue(terminalId,out var current)||string.IsNullOrEmpty(current)){text="本局未生成终端密码";return;}
  code=current;text="当前终端密码："+code;
 }
 static void Warn(Exception error){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Log.LogWarning("Passcode hints: "+error.Message);}
 internal static void Tick(){if(Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+.1f;try{Read();}catch(Exception error){Reset();Warn(error);}}
 internal static void Draw(){
  if(!enabled.Value||text.Length==0||Event.current.type!=EventType.Repaint)return;
  try{
   var panel=CurrentPanel();if(panel==null||Singleton<GameWorld>.Instance.Pointer!=worldPointer||panel.Pointer!=panelPointer||panel.Terminal.PasscodeId!=terminalId){Reset();return;}
   float width=Math.Min(520,Screen.width-24);var box=new Rect((Screen.width-width)/2,Math.Max(16,Screen.height*.15f),width,78);
   var previous=GUI.color;try{GUI.color=new Color(.035f,.055f,.075f,.96f);GUI.DrawTexture(box,Texture2D.whiteTexture);}finally{GUI.color=previous;}
   bool bypass=acceptAny.Value&&hookInstalled;
   InterfaceMenu.Label(new Rect(box.x+16,box.y+9,box.width-32,32),text,code.Length>0||bypass?new Color(.45f,1,.8f):new Color(1,.8f,.35f),22);
   InterfaceMenu.Label(new Rect(box.x+16,box.y+44,box.width-32,24),bypass?"输入任意密码后按终端确认":code.Length>0?"在终端输入后确认":"可在辅助 mod 中开启任意密码通过",null,13);
  }catch(Exception error){Reset();Warn(error);}
 }
 internal static void DrawOption(Rect rect){
  float half=(rect.width-12)/2;
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y,half,rect.height),"密码提示："+(enabled.Value?"开启":"关闭"),enabled.Value)){enabled.Value=!enabled.Value;if(!enabled.Value)Reset();}
  if(InterfaceMenu.Button(new Rect(rect.x+half+12,rect.y,half,rect.height),"任意密码通过："+(acceptAny.Value?"开启":"关闭"),acceptAny.Value)){acceptAny.Value=!acceptAny.Value;Reset();}
 }
 internal static object Diagnostics(){Read();var world=Singleton<GameWorld>.Instance;var controller=world?.PasscodeController?.TryCast<ClientLocalPasscodeController>();return new{enabled=enabled.Value,acceptAny=acceptAny.Value,hookInstalled,panelVisible=panelPointer!=IntPtr.Zero,terminalId,hintAvailable=code.Length>0,codeLength=code.Length,generatedCount=controller?._generatedPasscodes?.Count,overriddenAttempts,lastAccepted};}
 internal static object CheckOverrideScopes(){
  // Isolated native dictionaries only: no terminal submission, world changes or unlock events.
  var codes=new NativeCodes();codes["existing"]="001234";
  var scope=new OverrideScope(codes,"existing");scope.Apply("999999");if(codes["existing"]!="999999")throw new InvalidOperationException("Override was not applied");scope.Restore();scope.Restore();if(codes["existing"]!="001234")throw new InvalidOperationException("Existing code not restored");
  var missing=new OverrideScope(codes,"missing");try{missing.Apply("000000");throw new InvalidOperationException("expected test exception");}catch(InvalidOperationException){}finally{missing.Restore();}if(codes.ContainsKey("missing"))throw new InvalidOperationException("Temporary code leaked");
  var outer=new OverrideScope(codes,"existing");outer.Apply("111111");var inner=new OverrideScope(codes,"existing");inner.Apply("222222");inner.Restore();if(codes["existing"]!="111111")throw new InvalidOperationException("Nested code not restored");outer.Restore();if(codes["existing"]!="001234")throw new InvalidOperationException("Nested original lost");
  return new{passed=true,existingCodeRestored=true,missingEntryRemovedAfterException=true,nestedScopesRestored=true,worldChanged=false,terminalSubmitted=false};
 }
}