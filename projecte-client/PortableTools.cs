using System.Reflection;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using Local.ModHost;
using UnityEngine;
namespace ProjectE.Client;

public sealed partial class ProjectModule {
 GamePlayerOwner? gameOwner;float nextToolUse,terminalUntil,messageUntil,nextTerminalCheck;string toolMessage="";McGui? toolGui;bool openingTerminal;
 ConfigEntry<KeyCode> chargeKey=null!,skillKey=null!,modeKey=null!;
 ConfigEntry<bool> totemEnabled=null!;
 void InitializeTools(Harmony harmony){
  chargeKey=settingsFile!.Bind("Weapons","Charge key",KeyCode.R,"手持 PE 武器：充能；Shift + 此键降级。");
  skillKey=settingsFile.Bind("Weapons","Ability key",KeyCode.G,"手持 PE 武器：发动范围技能（取代此帧原生按键动作）。");
  modeKey=settingsFile.Bind("Weapons","Mode key",KeyCode.M,"红物质剑/拳剑切换仅敌对 AI / 全部 AI，不攻击玩家。");
  totemEnabled=settingsFile.Bind("Totem","Enabled",true,"手持不死图腾时，消耗一次以阻止本地 PMC 致命伤。");
  harmony.Patch(AccessTools.Method(typeof(GamePlayerOwner),nameof(GamePlayerOwner.TranslateCommand)),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(ToolCommands)){priority=Priority.First});
  harmony.Patch(AccessTools.Method(typeof(Player.KnifeController),nameof(Player.KnifeController.MakeAlternativeKick)),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(ToolRightClick)){priority=Priority.First});
  harmony.Patch(AccessTools.Method(typeof(Player.KnifeController),nameof(Player.KnifeController.MakeKnifeKick)),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(ToolLeftClick)){priority=Priority.First});
  InitializeMatter(harmony);InitializeTotem(harmony);
  context!.OnShutdown(()=>{toolGui?.Clear();gameOwner=null;terminalUntil=0;});
 }
 static Player? RaidPlayer(){var game=Singleton<AbstractGame>.Instance;var player=Singleton<GameWorld>.Instance?.MainPlayer;return game?.InRaid==true&&game.TryCast<HideoutGame>()==null&&player?.IsYourPlayer==true&&player.Side!=EPlayerSide.Savage?player:null;}
 static bool Playing(Player? player)=>player?.HealthController?.IsAlive==true&&!player.IsInventoryOpened&&Application.isFocused&&!Cursor.visible&&Time.timeScale>0&&active?.table?.Open!=true;
 static bool OwnHands(Player.KnifeController hands)=>active?.context?.IsActive==true&&RaidPlayer() is {} player&&hands._player?.Pointer==player.Pointer;
 static bool ToolCommands(GamePlayerOwner __instance,ref InputNode.ETranslateResult __result){
  var a=active;if(a?.context?.IsActive!=true||__instance.Player?.IsYourPlayer!=true)return true;a.gameOwner=__instance;if(a.openingTerminal)return true;
  var p=RaidPlayer();if(!Playing(p))return true;string id=p!.HandsController?.GetItem()?.StringTemplateId??"";
  bool use=Gadgets.Find(id)!=null&&(Input.GetMouseButton(0)||Input.GetMouseButton(1));
  bool weapon=Swords.Find(id)!=null&&(Input.GetKeyDown(a.chargeKey.Value)||Input.GetKeyDown(a.skillKey.Value)||Input.GetKeyDown(a.modeKey.Value));
  if(!use&&!weapon)return true;__result=InputNode.ETranslateResult.BlockAll;return false;
 }
 static bool ToolRightClick(Player.KnifeController __instance){
  if(!OwnHands(__instance)||Gadgets.Find(__instance.GetItem()?.StringTemplateId??"")==null)return true;
  if(Playing(RaidPlayer()))active!.UseTool(__instance.GetItem().StringTemplateId);return false;
 }
 static bool ToolLeftClick(Player.KnifeController __instance)=>!OwnHands(__instance)||Gadgets.Find(__instance.GetItem()?.StringTemplateId??"")==null;
 void Say(string message){toolMessage=message;messageUntil=Time.unscaledTime+6;context!.Log(LogLevel.Info,message);}
 void UseTool(string id){
  if(Time.unscaledTime<nextToolUse)return;nextToolUse=Time.unscaledTime+.35f;
  if(id==Gadgets.Tablet)table!.SetOpen(true);
  else if(id==Gadgets.Terminal){terminalUntil=Time.unscaledTime+20;TryTerminal();}
  else Say("不死图腾已握持：受到致命伤时自动消耗一次。");
 }
 void TryTerminal(){
  var p=RaidPlayer();if(p==null||p.HealthController?.IsAlive!=true||p.HandsController?.GetItem()?.StringTemplateId!=Gadgets.Terminal){terminalUntil=0;return;}
  try{
   // Resolve the active core on each use: its assembly changes on hot reload.
   var type=AppDomain.CurrentDomain.GetAssemblies().Reverse().Select(a=>a.GetType("LocalSpt5Esp.RaidStash",false)).FirstOrDefault(t=>t!=null);
   if(type==null)throw new InvalidOperationException("无线终端需要辅助 MOD 的战局仓库模块。");
   var flags=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
   if(type.GetField("enabled",flags)?.GetValue(null) is not ConfigEntry<bool> enabled)throw new InvalidOperationException("辅助 MOD 的战局仓库尚未初始化。");
   enabled.Value=true;type.GetMethod("Tick",flags)!.Invoke(null,null);
   if(type.GetField("ready",flags)?.GetValue(null) is not true){if(Time.unscaledTime>=terminalUntil){terminalUntil=0;Say("仓库连接超时，请检查辅助 MOD 的战局仓库提示。");}return;}
   if(gameOwner==null||gameOwner.Player?.Pointer!=p.Pointer)gameOwner=p.GetComponent<GamePlayerOwner>();
   if(gameOwner==null)throw new InvalidOperationException("库存输入控制器尚未就绪，请再次右键。");
   if(!p.IsInventoryOpened){openingTerminal=true;try{gameOwner.TranslateCommand(ECommand.ToggleInventory);}finally{openingTerminal=false;}}
   terminalUntil=0;Say(p.IsInventoryOpened?"无线终端已打开战局仓库；物品读写由辅助 MOD 管理。":"仓库已连接，当前操作暂时阻止打开；请松开鼠标后再次右键。");
  }catch(Exception e){terminalUntil=0;Say(e.GetBaseException().Message);}
 }
 void TickTools(){
  var p=RaidPlayer();TickTotem(p);if(terminalUntil>0&&Time.unscaledTime>=nextTerminalCheck){nextTerminalCheck=Time.unscaledTime+.25f;TryTerminal();}
  if(!Playing(p))return;string id=p!.HandsController?.GetItem()?.StringTemplateId??"";
  if(Gadgets.Find(id)!=null&&Input.GetMouseButtonDown(1))UseTool(id);
  TickMatter(p);
 }
 void DrawTools(){
  DrawTotem();if(table?.Open==true)return;
  var p=RaidPlayer();string id=p?.HandsController?.GetItem()?.StringTemplateId??"";
  string hint=Time.unscaledTime<messageUntil?toolMessage:Gadgets.Find(id) is {} tool?tool.Chinese+(id==Gadgets.Totem?" · 手持时自动抵挡一次致命伤":" · 右键打开"):MatterHint(id);
  if(hint.Length==0||p?.IsInventoryOpened==true)return;
  toolGui??=new(resources!);toolGui.InitializeFont();var old=GUI.color;int depth=GUI.depth;GUI.depth=-7000;GUI.color=Color.white;
  try{float width=Math.Min(720,Screen.width-32);var r=new Rect((Screen.width-width)/2,Screen.height-140,width,42);McGui.Fill(r,new(.05f,.05f,.07f,.85f));toolGui.Label(new(r.x+10,r.y+2,r.width-20,r.height-4),hint,Color.white,14,true);}finally{GUI.color=old;GUI.depth=depth;}
 }
}
