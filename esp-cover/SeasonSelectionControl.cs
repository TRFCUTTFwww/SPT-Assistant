using System;
using Comfort.Common;
using EFT;
using EFT.UI;
using EFT.UI.Screens;
using HarmonyLib;
using UnityEngine;
using Selection=EFT.UI.CharacterSelectionScreen.CharacterSelectionScreenController;
namespace LocalSpt5Esp;
// The server deliberately locks pvp-season. Present a PVE-backed entry using
// the existing PVE descriptor; never change server data or create a character.
internal static class SeasonSelectionControl {
 static Selection? observed;static CharacterSelectionScreen? screen;static long aliasSlot;static bool restoring;static float next;static int routed;
 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(CharacterSelectionScreen),nameof(CharacterSelectionScreen.ShowSlot)),prefix:new HarmonyMethod(typeof(SeasonSelectionControl),nameof(Slot)));
  plugin.Patches.Patch(AccessTools.Method(typeof(CharacterSelectionSlotViewBase),nameof(CharacterSelectionSlotViewBase.Show)),prefix:new HarmonyMethod(typeof(SeasonSelectionControl),nameof(Model)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Selection),nameof(Selection.Submit)),prefix:new HarmonyMethod(typeof(SeasonSelectionControl),nameof(Submit)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Selection),nameof(Selection.SubmitAsync)),prefix:new HarmonyMethod(typeof(SeasonSelectionControl),nameof(SubmitAsync)));
 }
 static Selection? Current=>EftScreenManager.Instance?.CurrentScreenController?.TryCast<Selection>();
 static bool Eligible(Selection? controller,out CharacterSelectionProfileData? pve){
  pve=null;if(controller?.Data is not {} data||!data.TryGetValue(EGameMode.Pve,out pve)||pve==null||!data.TryGetValue(EGameMode.PvpSeason,out var season)||season==null)return false;
  return SeasonSelectionRules.CanAlias(SeasonMenuControl.Enabled&&!restoring,ItemSpawner.InRaid(),season.IsLocked,pve.HasProfile,pve.IsSelectable,pve.ProfileId);
 }
 static void Slot(CharacterSelectionScreen __instance,CharacterSelectionSlotViewBase __0,EGameMode __1,ref CharacterSelectionProfileData __2,Selection __3){
  observed=__3;screen=__instance;if(__1!=EGameMode.PvpSeason)return;aliasSlot=0;
  if(Eligible(__3,out var pve)){__2=pve!;aliasSlot=__0.Pointer.ToInt64();}
 }
 static void Model(CharacterSelectionSlotViewBase __instance,EGameMode __0,ref CharacterSelectionProfileData __1,ref CharacterSelectionSlotViewModel __2){
  if(__0!=EGameMode.PvpSeason||__instance.Pointer.ToInt64()!=aliasSlot||!Eligible(observed,out var pve)||__1.ProfileId!=pve!.ProfileId)return;
  __2=CharacterSelectionSlotViewModel.Create(EGameMode.PvpSeason,pve,false,observed!.ExpansionsSettings);
  __2.ActionAvailable=true;__2.ActionText=I18n.Text("选择 PVE 赛季");__2.DisabledTooltipKey="";__2.PurchaseVisible=false;__2.PurchaseAvailable=false;
  __2.ModeType=I18n.Text("赛季 · PVE");__2.CharacterDescription=I18n.Text("使用现有 PVE 角色，共用装备、任务和进度。");
 }
 static bool Route(Selection controller,ref EGameMode mode,ref CharacterSelectionProfileData selected){
  if(mode!=EGameMode.PvpSeason)return true;
  // A genuine supported seasonal profile retains its native path.
  if(controller.Data?.TryGetValue(EGameMode.PvpSeason,out var season)!=true||season?.IsLocked!=true)return true;
  bool eligible=Eligible(controller,out var pve)&&Current?.Pointer==controller.Pointer;
  if(!SeasonSelectionRules.CanSubmit(eligible,pve?.ProfileId??"",selected?.ProfileId??"")){FeatureToasts.Show("PVE 角色尚未就绪，请刷新角色列表。");return false;}
  mode=EGameMode.Pve;selected=pve!;routed++;return true;
 }
 static bool Submit(Selection __instance,ref EGameMode __0,ref CharacterSelectionProfileData __1)=>Route(__instance,ref __0,ref __1);
 static bool SubmitAsync(Selection __instance,ref EGameMode __0,ref CharacterSelectionProfileData __1,ref Il2CppSystem.Threading.Tasks.Task __result){if(Route(__instance,ref __0,ref __1))return true;__result=Il2CppSystem.Threading.Tasks.Task.CompletedTask;return false;}
 internal static void Tick(){if(Time.unscaledTime<next||ItemSpawner.InRaid())return;next=Time.unscaledTime+1;try{var current=Current;if(current==null){observed=null;screen=null;aliasSlot=0;return;}
  bool eligible=Eligible(current,out _);if(observed?.Pointer==current.Pointer&&(eligible?aliasSlot!=0:aliasSlot==0))return;observed=current;screen=UnityEngine.Object.FindObjectOfType<CharacterSelectionScreen>();if(screen!=null)screen.Redraw(current);
  }catch(Exception error){next=Time.unscaledTime+10;Plugin.Self.Error(error);}
 }
 internal static object Diagnostics(){var current=Current;bool eligible=Eligible(current,out var pve);var slot=screen?._seasonSlotView;return new{open=current!=null,eligible,pveHasProfile=pve?.HasProfile,pveSelectable=pve?.IsSelectable,aliasShown=aliasSlot!=0&&slot?.Pointer.ToInt64()==aliasSlot,slotHasProfile=slot?.ProfileData?.HasProfile,slotSelectable=slot?.ProfileData?.IsSelectable,slotLocked=slot?.ProfileData?.IsLocked,buttonInteractable=slot?._actionButton?.Interactable,actionAvailable=slot?._viewModel?.ActionAvailable,actionText=slot?._viewModel?.ActionText,profileMatches=slot?.ProfileData!=null&&slot.ProfileData.ProfileId==pve?.ProfileId,routed};}
 internal static void Refresh(){aliasSlot=0;if(Current is {} current&&(screen??=UnityEngine.Object.FindObjectOfType<CharacterSelectionScreen>())!=null)screen.Redraw(current);}
 internal static void Shutdown(){try{restoring=true;Refresh();}finally{restoring=false;observed=null;screen=null;aliasSlot=0;}}
}
