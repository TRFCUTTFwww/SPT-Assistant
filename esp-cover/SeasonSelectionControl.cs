using System;
using EFT;
using EFT.UI;
using EFT.UI.Screens;
using HarmonyLib;
using UnityEngine;
using Selection=EFT.UI.CharacterSelectionScreen.CharacterSelectionScreenController;
namespace LocalSpt5Esp;
// The server supplies an independent seasonal descriptor. Never alias PVE or
// rewrite the submitted mode: the native reload must request pvp-season.
internal static class SeasonSelectionControl {
 static Selection? observed;static CharacterSelectionScreen? screen;static bool restoring;static int submitted;
 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(CharacterSelectionScreen),nameof(CharacterSelectionScreen.ShowSlot)),prefix:new HarmonyMethod(typeof(SeasonSelectionControl),nameof(Slot)));
  plugin.Patches.Patch(AccessTools.Method(typeof(CharacterSelectionSlotViewBase),nameof(CharacterSelectionSlotViewBase.Show)),prefix:new HarmonyMethod(typeof(SeasonSelectionControl),nameof(Model)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Selection),nameof(Selection.Submit)),prefix:new HarmonyMethod(typeof(SeasonSelectionControl),nameof(Submit)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Selection),nameof(Selection.SubmitAsync)),prefix:new HarmonyMethod(typeof(SeasonSelectionControl),nameof(SubmitAsync)));
 }
 static Selection? Current=>EftScreenManager.Instance?.CurrentScreenController?.TryCast<Selection>();
 static void Slot(CharacterSelectionScreen __instance,CharacterSelectionSlotViewBase __0,EGameMode __1,CharacterSelectionProfileData __2,Selection __3){observed=__3;screen=__instance;}
 static void Model(CharacterSelectionSlotViewBase __instance,EGameMode __0,CharacterSelectionProfileData __1,CharacterSelectionSlotViewModel __2){
  if(restoring||__0!=EGameMode.PvpSeason||__1.IsLocked)return;
  __2.ActionAvailable=SeasonMenuControl.Enabled&&!ItemSpawner.InRaid();__2.ActionText=I18n.Text(__1.IsEmpty?"新建独立赛季角色":"选择独立赛季角色");__2.DisabledTooltipKey="";__2.PurchaseVisible=false;__2.PurchaseAvailable=false;
  __2.ModeType=I18n.Text("赛季 · 独立 PVE");__2.CharacterDescription=I18n.Text("独立的新角色和存档，仓库、任务与进度独立保存；对局沿用 PVE。选择 PVE 可返回原角色。");
 }
 static bool Route(Selection controller,EGameMode mode,CharacterSelectionProfileData selected){
  if(mode!=EGameMode.PvpSeason)return true;
  bool current=Current?.Pointer==controller.Pointer;
  CharacterSelectionProfileData? season=null;bool found=controller.Data?.TryGetValue(EGameMode.PvpSeason,out season)==true;
  if(!SeasonSelectionRules.CanSubmit(SeasonMenuControl.Enabled,ItemSpawner.InRaid(),current,found&&season!=null&&!season.IsLocked,found&&season?.Pointer==selected?.Pointer)){
   FeatureToasts.Show("独立赛季档案尚未就绪，请刷新角色列表并检查服务端。");return false;
  }
  submitted++;return true;
 }
 static bool Submit(Selection __instance,EGameMode __0,CharacterSelectionProfileData __1)=>Route(__instance,__0,__1);
 static bool SubmitAsync(Selection __instance,EGameMode __0,CharacterSelectionProfileData __1,ref Il2CppSystem.Threading.Tasks.Task __result){if(Route(__instance,__0,__1))return true;__result=Il2CppSystem.Threading.Tasks.Task.CompletedTask;return false;}
 internal static void Tick(){if(Current==null){observed=null;screen=null;}}
 internal static object Diagnostics(){var current=Current;var slot=screen?._seasonSlotView;return new{open=current!=null,independent=true,slotHasProfile=slot?.ProfileData?.HasProfile,slotSelectable=slot?.ProfileData?.IsSelectable,slotLocked=slot?.ProfileData?.IsLocked,buttonInteractable=slot?._actionButton?.Interactable,actionAvailable=slot?._viewModel?.ActionAvailable,actionText=slot?._viewModel?.ActionText,profileMatches=slot?.ProfileData?.ProfileId==current?.ProfileId,submitted};}
 internal static void Refresh(){if(Current is {} current&&(screen??=UnityEngine.Object.FindObjectOfType<CharacterSelectionScreen>())!=null)screen.Redraw(current);}
 internal static void Shutdown(){try{restoring=true;Refresh();}finally{restoring=false;observed=null;screen=null;}}
}
