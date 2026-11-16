using LocalSpt5Esp.HotReload;
using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
public sealed class RuntimeModule:IRuntimeModule {
 Plugin? plugin;
 Overlay? overlay;
 public int ApiVersion=>1;
 internal const string ReleaseVersion="2.3.1";
 public string Version=>ReleaseVersion;
 // Optional methods are discovered by the 1.5 host. API 1 still loads on older
 // hosts, allowing the core to be installed before the one-time host restart.
 public void PrepareInBackground()=>I18n.Prepare();
 public bool PrepareForReload(){if(!MinecraftBridge.CanReload||!WorldItemPlacement.PrepareForReload()||!RaidStash.PrepareForReload()||!HideoutCraftControl.PrepareForReload()||!ProfileAdminPanel.PrepareForReload())return false;ReloadInputRecovery.BeforeReload();return true;}
 public void Initialize(IHostServices host){foreach(var step in InitializeSteps(host))step();}
 public IEnumerable<Action> InitializeSteps(IHostServices host){
  yield return ()=>{FeatureRetirement.Apply(host.Config,host.PluginDirectory,host.Log);plugin=new Plugin(host);};
  yield return ()=>PlayerVisualGuard.Initialize(plugin!);
  foreach(var step in plugin!.LoadSteps())yield return step;
  yield return Hotkeys.Initialize;
  yield return BlackwallBridge.Initialize;
  yield return MinecraftBridge.Initialize;
  yield return UpdateChecker.Initialize;
  yield return WorldEditor.Initialize;
  yield return RaidChat.Initialize;
  yield return ()=>InterfaceMenu.Initialize(plugin);
  yield return PerformanceHud.Initialize;
  yield return ()=>FeatureToasts.Initialize(plugin);
  yield return KillEffects.Initialize;
  yield return ()=>I18n.Initialize(plugin);
  yield return ()=>overlay=new Overlay();
  yield return ReloadInputRecovery.Arm;
 }
 public void Update(){long stamp=RuntimePerformance.Start();try{ReloadInputRecovery.Tick();RuntimePerformance.Frame();RaidChat.Tick();RaidClock.Tick();WorldEditor.CaptureInput();WorldItemPlacement.Tick();AiFreePlacement.Tick(Comfort.Common.Singleton<EFT.GameWorld>.Instance);AiDifficultyControl.Tick();QuestWaitSync.Tick();HideoutCraftControl.Tick();ProfileAdminPanel.Tick();ProfileBackupPanel.Tick();RaidStash.Tick();GraphicsRepair.Tick();CorpseRevival.Tick(Comfort.Common.Singleton<EFT.GameWorld>.Instance);GrenadeArc.Tick(Comfort.Common.Singleton<EFT.GameWorld>.Instance);KillEffects.Tick(Comfort.Common.Singleton<EFT.GameWorld>.Instance);EventRaidLauncher.Tick();EventRaidDiagnostics.Tick();FloatingStatus.Update();ConfigProfiles.Keys();InterfaceMenu.Update();HomeDashboard.Tick();MiscPanel.Tick();MiscNativeProtection.Tick();FleaFees.Tick();ScavDiplomacy.Tick();DirectorStage.Update();EntertainmentControl.Update();BlackwallBridge.Tick();MinecraftBridge.Tick();UpdateChecker.Tick();LaserEyes.CaptureInput();HitSounds.Tick();long costStamp=RuntimePerformance.Start();overlay?.Update();RuntimeCosts.Mark(ref costStamp,CostStage.Scan);PasscodeHints.Tick();CoordinateReader.Tick();}finally{RuntimePerformance.UpdateDone(stamp);}}
 public void LateUpdate(){long stamp=RuntimePerformance.Start();try{overlay?.LateUpdate();long costStamp=RuntimePerformance.Start();BodyTint.Tick();BlackwallBridge.Late();MinecraftBridge.Late();RuntimeCosts.Mark(ref costStamp,CostStage.Tint);InterfaceMenu.KeepCursor();}finally{RuntimePerformance.LateDone(stamp);}}
 public void OnGUI(){long stamp=RuntimePerformance.Start();try{BlackwallBridge.DrawEffects();MinecraftBridge.Draw();if(WorldEditor.ModalOpen&&!InterfaceMenu.Open){WorldEditor.Draw();RaidChat.Draw();return;}if(!DirectorStage.HudHidden){overlay?.OnGUI();FloatingStatus.Draw();PerformanceHud.Draw();FeatureToasts.Draw();PasscodeHints.Draw();KillEffects.Draw(EFT.CameraControl.CameraManager.Instance?.Camera);GrenadeArc.Draw(EFT.CameraControl.CameraManager.Instance?.Camera);}WorldEditor.Draw();InterfaceMenu.Draw();RaidChat.Draw();}finally{RuntimePerformance.GuiDone(stamp);}}
 public void Unload(){foreach(var step in UnloadSteps())step();}
 public IEnumerable<Action> UnloadSteps(){
  var actions=new List<Action>{UpdateChecker.Reset,BtrServices.Reset,MinecraftBridge.Reset,FunItemClient.Reset,BlackwallBridge.Reset,RaidChat.Reset,WorldEditor.Reset,WorldItemPlacement.Reset,AiFreePlacement.Reset,AiDifficultyControl.Shutdown,I18n.Shutdown,QuestWaitSync.Reset,HideoutCraftControl.Shutdown,ProfileAdminPanel.Reset,ProfileBackupPanel.Reset,HomeDashboard.Reset,MiscPanel.Reset,MiscNativeProtection.Reset,FleaFees.Reset,ScavDiplomacy.Reset,UniversalKeys.Reset,LaserEyes.Reset,RaidClock.Reset,RaidStash.Shutdown,GrenadeArc.Reset,KillEffects.Reset,PasscodeHints.Reset,CommandoConsole.Reset,RuntimePerformance.Shutdown,FeatureToasts.Shutdown,FloatingStatus.Shutdown,InterfaceMenu.Shutdown};
  if(overlay!=null)actions.AddRange(overlay.UnloadSteps());
  // Unpatch one original at a time; UnpatchSelf can otherwise rebuild every
  // native trampoline in a single frame. Capture the list before unpatching.
  if(plugin?.Patches is {} harmony)foreach(var original in harmony.GetPatchedMethods()){
   var method=original;actions.Add(()=>harmony.Unpatch(method,HarmonyLib.HarmonyPatchType.All,harmony.Id));
  }
  actions.Add(LootDisplay.Shutdown);actions.Add(()=>{overlay=null;plugin=null;Plugin.Self=null!;});
  foreach(var action in actions)yield return ()=>{try{action();}catch(Exception error){plugin?.Log.LogWarning(error);}};
 }
}
