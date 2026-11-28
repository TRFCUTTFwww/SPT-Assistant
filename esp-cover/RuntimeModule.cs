using LocalSpt5Esp.HotReload;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
namespace LocalSpt5Esp;
public sealed class RuntimeModule:IRuntimeModule {
 const string ReloadStampKey="LocalSpt5Esp.ReloadStartTimestamp";
 Plugin? plugin;
 Overlay? overlay;
 public int ApiVersion=>1;
 internal const string ReleaseVersion="2.4";
 internal static readonly string BuildNumber=typeof(RuntimeModule).Assembly
  .GetCustomAttributes<AssemblyMetadataAttribute>()
  .First(attribute=>attribute.Key=="AssistantBuildNumber").Value!;
 public string Version=>ReleaseVersion;
 // Discovered dynamically by Host >= 1.7 for zero-flicker time-budgeted swaps;
 // ignored harmlessly by older 1.5/1.6 hosts.
 public double ReloadFrameBudgetMs=>25.0;
 public bool SilentFastReload=>true;

 public void PrepareInBackground(){I18n.Prepare();ItemCatalog.PrepareEsp();}
 public bool PrepareForReload(){if(!BtrInstantTransfer.PrepareForReload()||!ItemFirContext.CanReload||!FleaMarketPanel.CanReload||!FunModuleManager.CanReload||!WorldEditor.PrepareContainersForReload()||!WorldItemPlacement.PrepareForReload()||!RaidItemPoolGuard.PrepareForReload()||!RaidStash.PrepareForReload()||!HideoutCraftControl.PrepareForReload()||!CultistCircleControl.PrepareForReload()||!ProfileAdminPanel.PrepareForReload()||!ProfileJsonEditorPanel.PrepareForReload()||!ServerJsonEditorPanel.PrepareForReload())return false;RaidClock.PreserveTimeoutForReload();ReloadInputRecovery.BeforeReload();return true;}
 public void Initialize(IHostServices host){foreach(var step in InitializeSteps(host))step();}

 static IEnumerable<Action> BatchSteps(IEnumerable<Action> steps,int batchSize)=>LocalSpt.Shared.LifecycleSteps.Batch(steps,batchSize);

 IEnumerable<Action> RawInitializeSteps(IHostServices host){
  yield return ()=>{FeatureRetirement.Apply(host.Config,host.PluginDirectory,host.Log);plugin=new Plugin(host);};
  yield return ()=>DiagnosticsPanel.Initialize(host);
  yield return ()=>PlayerVisualGuard.Initialize(plugin!);
  foreach(var step in plugin!.LoadSteps())yield return step;
  yield return Hotkeys.Initialize;
  yield return OptionalCombatIntegration.Initialize;
  yield return ()=>UpdateChecker.Initialize(host);
  yield return WorldEditor.Initialize;
  yield return RaidChat.Initialize;
  yield return ()=>InterfaceMenu.Initialize(plugin,host.PluginDirectory);
  yield return PerformanceHud.Initialize;
  yield return ()=>RaidNotifications.Initialize(plugin);
  yield return KillEffects.Initialize;
  yield return ()=>I18n.Initialize(plugin);
  yield return ()=>overlay=new Overlay();
  yield return ReloadInputRecovery.Arm;
  // Config binding restores saved values and emits SettingChanged. Listen only after startup is complete.
  yield return ()=>FeatureToasts.Initialize(plugin!);
  yield return ()=>{
   int reused=plugin!.Patches.ReusedCount,created=plugin.Patches.CreatedCount;
   if(AppDomain.CurrentDomain.GetData(ReloadStampKey) is long started){
    AppDomain.CurrentDomain.SetData(ReloadStampKey,null);
    double elapsedMs=Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    plugin.Log.LogInfo($"Fast hot-reload finished in {elapsedMs:F1}ms (reusedHooks={reused}, newHooks={created})");
    FeatureToasts.Show($"热更新完成 · {Math.Max(1,(int)Math.Round(elapsedMs))} ms");
   }else{
    plugin.Log.LogInfo($"Hook router initialized: reusedHooks={reused}, newHooks={created}");
   }
  };
 }

 public IEnumerable<Action> InitializeSteps(IHostServices host)=>BatchSteps(RawInitializeSteps(host),20);

 public void Update(){long stamp=RuntimePerformance.Start();try{ScopeSticker.CaptureInput();ReloadInputRecovery.Tick();RuntimePerformance.Frame();long featureStamp=RuntimePerformance.Start();RaidChat.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.Chat);RaidClock.Tick();MagazineAmmoCompatibility.Tick();RaidStartClock.Tick();DiagnosticsPanel.Tick();WorldEditor.CaptureInput();WorldItemPlacement.Tick();ContainerLibrary.Tick();MineLibrary.Tick();WorldEditor.TickContainers();ContainerRendering.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.EditorUpdate);AiFreePlacement.Tick(Comfort.Common.Singleton<EFT.GameWorld>.Instance);AiDifficultyControl.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.DifficultyUpdate);QuestWaitSync.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.QuestUpdate);HideoutCraftControl.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.CraftUpdate);CultistCircleControl.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.CultistUpdate);CornerVersionControl.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.CornerUpdate);ProfileAdminPanel.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.ScavUpdate);ProfileBackupPanel.Tick();ProfileJsonEditorPanel.Tick();ServerJsonEditorPanel.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.BackupUpdate);RaidStash.Tick();BtrInstantTransfer.Tick();RaidItemPoolGuard.Tick();WeaponLifecycleGuard.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.StashUpdate);RuntimeCosts.Mark(ref featureStamp,CostStage.ProfileUpdate);GraphicsRepair.Tick();CorpseRevival.Tick(Comfort.Common.Singleton<EFT.GameWorld>.Instance);KillEffects.Tick(Comfort.Common.Singleton<EFT.GameWorld>.Instance);EventRaidLauncher.Tick();EventRaidDiagnostics.Tick();FloatingStatus.Update();RuntimeCosts.Mark(ref featureStamp,CostStage.EffectsUpdate);ConfigProfiles.Keys();InterfaceMenu.Update();RuntimeCosts.Mark(ref featureStamp,CostStage.MenuUpdate);HomeDashboard.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.HomeUpdate);ItemFirContext.Tick();ItemInventoryEditor.Tick();GridCellItemCreator.Tick();FleaOfferEditor.Tick();NativeFleaOfferEdit.Tick();OnlineTrading.Tick();MiscPanel.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.MiscUpdate);MiscNativeProtection.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.SniperUpdate);FleaFees.Tick();FleaClientHooks.Tick();FleaMarketShortcut.Tick();FleaMarketPanel.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.Market);ScavDiplomacy.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.AIProtection);DirectorStage.Update();EntertainmentControl.Update();OptionalCombatIntegration.Tick();FunModuleManager.Tick();RuntimeCosts.Mark(ref featureStamp,CostStage.FunItems);UpdateChecker.Tick();LaserEyes.CaptureInput();HitSounds.Tick();long costStamp=RuntimePerformance.Start();overlay?.Update();RuntimeCosts.Mark(ref costStamp,CostStage.Scan);PasscodeHints.Tick();CoordinateReader.Tick();}finally{RuntimePerformance.UpdateDone(stamp);}}
 public void LateUpdate(){long stamp=RuntimePerformance.Start();try{overlay?.LateUpdate();WorldEditor.LateContainerBuild();long costStamp=RuntimePerformance.Start();BodyTint.Tick();OpticZoom.Tick();ScopeSticker.Tick();RuntimeCosts.Mark(ref costStamp,CostStage.Tint);RaidSelectionClock.LateUpdate();InterfaceMenu.KeepCursor();}finally{RuntimePerformance.LateDone(stamp);}}
 public void OnGUI(){long stamp=RuntimePerformance.Start(),guiStage=stamp;ClickSelect.BeginFrame();try{if(FleaOfferEditor.Open){FleaOfferEditor.Draw();return;}if(ItemInventoryEditor.Open){ItemInventoryEditor.Draw();return;}if(GridCellItemCreator.Open){GridCellItemCreator.Draw();return;}if(WorldEditor.ModalOpen&&!InterfaceMenu.Open){WorldEditor.Draw();RuntimeCosts.Mark(ref guiStage,CostStage.EditorGUI);RaidChat.Draw();return;}if(!DirectorStage.HudHidden){overlay?.OnGUI();FloatingStatus.Draw();RaidNotifications.Draw();PerformanceHud.Draw();FeatureToasts.Draw();PasscodeHints.Draw();ScopeSticker.DrawRange();KillEffects.Draw(EFT.CameraControl.CameraManager.Instance?.Camera);GrenadeArc.Draw(EFT.CameraControl.CameraManager.Instance?.Camera);}RuntimeCosts.Mark(ref guiStage,CostStage.OverlayGUI);WorldEditor.Draw();RuntimeCosts.Mark(ref guiStage,CostStage.EditorGUI);RaidSelectionClock.Draw();FleaClientHooks.Draw();FleaMarketShortcut.Draw();InterfaceMenu.Draw();ScopeSticker.DrawEditor();RuntimeCosts.Mark(ref guiStage,CostStage.MenuGUI);RaidChat.Draw();RuntimeCosts.Mark(ref guiStage,CostStage.OtherGUI);}finally{ClickSelect.EndFrame();RuntimePerformance.GuiDone(stamp);}}
 public void Unload(){foreach(var step in UnloadSteps())step();}
 public IEnumerable<Action> UnloadSteps(){
  AppDomain.CurrentDomain.SetData(ReloadStampKey,Stopwatch.GetTimestamp());
  var actions=new List<Action>{FeatureToasts.Shutdown,DiagnosticsPanel.Reset,UpdateChecker.Reset,BtrServices.Reset,BtrInstantTransfer.Reset,FunModuleManager.Reset,OptionalCombatIntegration.Reset,RaidChat.Reset,WorldEditor.Reset,ContainerLibrary.Reset,MineLibrary.Reset,RaidParameterPanel.Reset,AiServerInfo.Reset,WorldItemPlacement.Reset,RaidItemPoolGuard.Reset,AiFreePlacement.Reset,AiDifficultyControl.Shutdown,I18n.Shutdown,QuestWaitSync.Reset,HideoutCraftControl.Shutdown,CultistCircleControl.Shutdown,CornerVersionControl.Shutdown,ProfileAdminPanel.Reset,ProfileBackupPanel.Reset,ProfileJsonEditorPanel.Reset,ServerJsonEditorPanel.Reset,RaidHistoryPanel.Reset,HomeDashboard.Reset,ItemFirContext.Reset,ItemInventoryEditor.Reset,PlayerWeaponPresets.Reset,WeaponEditor.ShutdownStash,OnlineTrading.Reset,MiscPanel.Reset,MagazineAmmoCompatibility.Reset,NightVisionAssist.Shutdown,ScopeSticker.Shutdown,OpticZoom.Shutdown,MiscNativeProtection.Reset,FleaFees.Reset,FleaClientHooks.Reset,FleaMarketPanel.Reset,FleaOfferEditor.Reset,NativeFleaOfferEdit.Reset,FleaMarketShortcut.Reset,ScavDiplomacy.Reset,UniversalKeys.Reset,LaserEyes.Reset,RaidClock.Shutdown,RaidStash.Shutdown,GrenadeArc.Dispose,KillEffects.Reset,PasscodeHints.Reset,CommandoConsole.Reset,RuntimePerformance.Shutdown,RaidNotifications.Reset,FloatingStatus.Shutdown,InterfaceMenu.Shutdown};
  if(overlay!=null)actions.AddRange(overlay.UnloadSteps());
  // Deactivate all persistent bridge delegates in O(N) pointer clears (<0.1ms)
  // without triggering Harmony.Unpatch / DobbyDetour native recompilation.
  if(plugin?.Patches is {} router)actions.Add(router.DeactivateAll);
  actions.Add(LootDisplay.Shutdown);
  actions.Add(()=>{overlay=null;plugin=null;Plugin.Self=null!;});
  var errors=new List<Exception>();
  var guarded=actions.Select<Action,Action>(action=>()=>{try{action();}catch(Exception error){errors.Add(error);plugin?.Log.LogWarning(error);}});
  foreach(var batch in BatchSteps(guarded,28))yield return batch;
  yield return ()=>{if(errors.Count>0)throw new AggregateException("辅助模块未完整卸载，需重启以避免重复 Hook。",errors);};
 }
}
