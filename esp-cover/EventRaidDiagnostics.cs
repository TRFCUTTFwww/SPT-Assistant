using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using EFT.AssetsManager;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class EventRaidDiagnostics {
 static string Home=>Path.Combine(BepInEx.Paths.PluginPath,"HalloweenEventPort");
 static DateTime observed;static float nextPoll;static Il2CppSystem.Threading.Tasks.Task? task;
 static System.Threading.Tasks.Task<object>? serverTask;
 static TarkovApplication App()=>Singleton<TarkovApplication>.Instance??UnityEngine.Object.FindObjectOfType<TarkovApplication>()??throw new InvalidOperationException("Application unavailable");
 static void Write(string stage,object? details){File.WriteAllText(Path.Combine(Home,"launch-diagnostics.json"),JsonSerializer.Serialize(new{stage,details,utc=DateTime.UtcNow},new JsonSerializerOptions{WriteIndented=true}));}
 internal static void Tick(){try{
  if(serverTask?.IsCompleted==true){var completed=serverTask;serverTask=null;Write("server-controls-state",completed.GetAwaiter().GetResult());}
  EventAudioProbe.Tick();
  EventMapTrace.Tick();
  if(task?.IsCompleted==true){var completed=task;task=null;Write(completed.IsFaulted?"native-task-failed":"native-task-complete",new{error=completed.Exception?.ToString()});}
  if(Time.unscaledTime<nextPoll)return;nextPoll=Time.unscaledTime+.5f;string file=Path.Combine(Home,"launch-diagnostics-request.json");if(!File.Exists(file))return;var stamp=File.GetLastWriteTimeUtc(file);if(stamp==observed)return;observed=stamp;
  using var doc=JsonDocument.Parse(File.ReadAllText(file));string action=doc.RootElement.GetProperty("action").GetString()??"";var app=App();
  if(action=="inspect")Write("application-state",new{screen=EFT.UI.Screens.EftScreenManager.Instance?.CurrentScreenController?.ScreenType.ToString(),world=Singleton<GameWorld>.Instantiated,menuOperation=app._menuOperation!=null,matchmakerOperation=app._localMatchmakerOperation!=null,raidSettings=app._raidSettings!=null,weather=app._raidSettings?.TimeAndWeatherSettings!=null,localSettings=app._localRaidSettings!=null,location=app._raidSettings?.SelectedLocation?.Id,mode=app._raidSettings?.RaidMode.ToString(),profile=app.GetProfileForLocalGame()!=null,aiTestStatus=AiSpawnService.Status});
  else if(action=="server-controls-state"){if(serverTask!=null)throw new InvalidOperationException("Server check already running");serverTask=ServerControlPanel.Diagnostics();}
  else if(action=="hideout-craft-state")Write("hideout-craft-state",HideoutCraftControl.Diagnostics());
  else if(action=="coordinates-state")Write("coordinates-state",CoordinateReader.Diagnostics());
  else if(action=="visual-lifecycle-state")Write("visual-lifecycle-state",PlayerVisualGuard.Diagnostics());
  else if(action=="chat-selection-state")Write("chat-selection-state",ChatSelectionGuard.Diagnostics());
  else if(action=="chat-refresh-check"){task=app.GetClientBackEndSession().SocialNetwork.UpdateDialogueList();Write("chat-refresh-requested",null);}
  else if(action=="quest-locations-state")Write("quest-locations-state",QuestLocations.Diagnostics());
  else if(action=="scav-equipment-state")Write("scav-equipment-state",ScavEquipmentSync.Diagnostics());
  else if(action=="scav-equipment-check")Write("scav-equipment-check",ScavEquipmentSync.Check());
  else if(action=="scav-secure-sync"){ScavEquipmentSync.BeginSecure(MiscPanel.Options.ScavSecureTemplate);Write("scav-secure-sync",ScavEquipmentSync.Diagnostics());}
  else if(action=="universal-keys-state")Write("universal-keys-state",UniversalKeys.Diagnostics());
  else if(action=="passcode-override-check")Write("passcode-override-check",PasscodeHints.CheckOverrideScopes());
  else if(action=="passcode-hint-state")Write("passcode-hint-state",PasscodeHints.Diagnostics());
#if EFT_48558
  else if(action=="entry-key-state")Write("entry-key-state",ArmorCare.EntryDiagnostics(app));
#endif
  else if(action=="i18n-state")Write("i18n-state",I18n.Diagnostics());
  else if(action=="bitcoin-state")Write("bitcoin-state",BitcoinControl.Diagnostics(app));
  else if(action=="event-loot-state")Write("event-loot-state",EventLootDiagnostics.Read());
  else if(action=="record-clue-anchor"){var anchor=EventLootDiagnostics.Anchor();File.WriteAllText(Path.Combine(Home,"clue-anchor.json"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,anchor},new JsonSerializerOptions{WriteIndented=true}));Write("clue-anchor-recorded",anchor);}
  else if(action=="warm-infection-backups"){var zombie=Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController?.EventsController?.BotHalloweenWithZombies??throw new InvalidOperationException("No infection controller");if(zombie._spawner==null)throw new InvalidOperationException("Infection spawner not ready");File.WriteAllText(file,"{\"action\":\"lab-ai-state\"}");zombie.AddBackupProfiles();Write("infection-backups-requested",null);}
  else if(action=="dump-infection-code"){EventInfectionCode.Dump(Home);Write("infection-code-saved",null);}
  else if(action=="infection-pool-state"){var pools=Singleton<ObjectsFactory>.Instance?.GetPools(ObjectsFactory.PoolsCategory.Raid);var pool=pools?.PlayerZombieAssetPool;Write("infection-pool-state",new{raidPools=pools?.Pointer.ToInt64(),pool=pool?.Pointer.ToInt64(),initialized=pool?._initialized,disposed=pool?.IsDisposed,role=pool?.RoleModelAssetPoolObject!=null,gameObject=pool?.RoleModelGameObject!=null,parent=pool?.ParentTransform!=null,poolSize=pool?.CurrentPoolSize,key=pool?.ResourceKey?.path,physical=Singleton<GameWorld>.Instance?.MainPlayer?.Physical!=null,timer=Singleton<AbstractGame>.Instance?.GameTimer?.Status.ToString()});}
  else if(action=="probe-infection-activation"){if(Singleton<GameWorld>.Instance?.MainPlayer?.HealthController?.IsAlive!=true)throw new InvalidOperationException("No living player");InfectionActivationProbe.Install(Home);File.WriteAllText(file,"{\"action\":\"zombie-combat-state\"}");task=Singleton<AbstractGame>.Instance.TryCast<IBotGame>().BotsController.BotSpawner.SpawnBotByTypeForce(1,WildSpawnType.infectedAssault,BotDifficulty.normal,new BotSpawnParams());Write("infection-activation-probe-started",null);}
  else if(action=="infection-errors"){
   var assembly=Array.Find(AppDomain.CurrentDomain.GetAssemblies(),a=>a.GetName().Name=="Local.HalloweenEventPort.InfectionCompat"&&a.GetName().Version==new System.Version(0,1,8,0))??throw new InvalidOperationException("Spawn tracker missing");var type=assembly.GetType("HalloweenEventPort.InfectionCompat.SpawnTrace",true)!;var entries=(System.Collections.IEnumerable)type.GetField("entries",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;var failures=new List<object>();foreach(var entry in entries){var task=(Il2CppSystem.Threading.Tasks.Task)entry.GetType().GetProperty("Task")!.GetValue(entry)!;if(task.IsFaulted){failures.Add(new{stage=entry.GetType().GetProperty("Stage")!.GetValue(entry),error=task.Exception?.ToString()});if(failures.Count==2)break;}}Write("infection-errors",failures);
  }
  else if(action=="retry-failed-infection-wave"){
   var current=Singleton<GameWorld>.Instance??throw new InvalidOperationException("No raid");var spawner=Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController?.BotSpawner??throw new InvalidOperationException("No spawner");var pending=spawner.BossSpawner._spawnProcess;
   if(current.MainPlayer?.HealthController?.IsAlive!=true||pending==null||pending.Count==0||spawner._inSpawnProcess!=pending.Count||spawner._botCreator.BotsLoading!=0||spawner._botCreator.BundlesLoading!=0)throw new InvalidOperationException("Spawn work has not settled");
   if(Comfort.Common.Singleton<ObjectsFactory>.Instance?.GetPools(ObjectsFactory.PoolsCategory.Raid)?.PlayerZombieAssetPool?.RoleModelAssetPoolObject==null)throw new InvalidOperationException("Zombie pool is not ready");
   foreach(var actor in current.AllAlivePlayersList){string role=actor?.Profile?.Info?.Settings?.Role.ToString()??"";if(actor?.HealthController?.IsAlive==true&&role.StartsWith("infected",StringComparison.OrdinalIgnoreCase)&&role!="infectedTagilla")throw new InvalidOperationException("Ordinary zombies already exist");}
   var waves=new List<BossLocationSpawn>();foreach(var item in pending){if(item.Wave?.BossName is not "infectedAssault" and not "infectedPmc")throw new InvalidOperationException("Pending request is not an ordinary zombie");waves.Add(item.Wave);}
   var traceAssembly=Array.Find(AppDomain.CurrentDomain.GetAssemblies(),a=>a.GetName().Name=="Local.HalloweenEventPort.InfectionCompat"&&a.GetName().Version==new System.Version(0,1,8,0))??throw new InvalidOperationException("Failure evidence missing");var entries=(System.Collections.IEnumerable)traceAssembly.GetType("HalloweenEventPort.InfectionCompat.SpawnTrace",true)!.GetField("entries",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;int failed=0;foreach(var entry in entries){string stage=entry.GetType().GetProperty("Stage")!.GetValue(entry)?.ToString()??"";if(stage is "player:infectedAssault" or "player:infectedPmc"){var task=(Il2CppSystem.Threading.Tasks.Task)entry.GetType().GetProperty("Task")!.GetValue(entry)!;if(!task.IsFaulted)throw new InvalidOperationException("Some player creation task is still live");failed++;}}if(failed!=waves.Count)throw new InvalidOperationException("Failed task count differs from pending requests");
   File.WriteAllText(file,"{\"action\":\"lab-ai-state\"}");pending.Clear();spawner._inSpawnProcess=0;foreach(var wave in waves)spawner.ActivateBotsByWave(wave);Write("infection-failed-wave-retried",new{count=waves.Count});
  }
  else if(action=="infection-loader-state"){
   var controller=Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController??throw new InvalidOperationException("No bots controller");var spawner=controller.BotSpawner;var blocked=new List<string>();if(spawner?._blockedRoles!=null)foreach(var role in spawner._blockedRoles)blocked.Add(role.ToString());var pending=new List<object>();if(spawner?.BossSpawner?._spawnProcess!=null)foreach(var item in spawner.BossSpawner._spawnProcess)pending.Add(new{role=item.Wave?.BossName,zone=item.BotZone?.name,point=item.PotentialSpawnPoint!=null});
   var backups=new DebugBotProfilesStructContainer();spawner?._botCreator?.FillBackupProfilesData(backups);
   Write("infection-loader-state",new{blocked,pending,backups=backups.Struct?.MessageInfo(),botsLoading=spawner?._botCreator?.BotsLoading,bundlesLoading=spawner?._botCreator?.BundlesLoading,canSpawn=controller.CanSpawn,gameEnd=spawner?._gameEnd,deleted=spawner?._deletedPlayers?.Count,added=spawner?._addedPlayers?.Count,allBots=spawner?._allBotsCount,spawning=spawner?._inSpawnProcess});
  }
  else if(action=="lab-ai-state"){
   var events=Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController?.EventsController;var zombie=events?.BotHalloweenWithZombies;
   var config=app.GetClientBackEndSession().BackEndConfig?.Config?.SeasonEventsConfig?.InfectionHalloweenConfig;
   var assembly=Array.Find(AppDomain.CurrentDomain.GetAssemblies(),a=>a.GetName().Name=="Local.HalloweenEventPort.LabModes");var flags=assembly?.GetType("HalloweenEventPort.LabModes.InfectionFlags");object? Flag(string name)=>flags?.GetField(name,BindingFlags.NonPublic|BindingFlags.Static)?.GetValue(null);
   var controller=Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController;var scenario=Singleton<AbstractGame>.Instance?.TryCast<LocalGame>()?.BossSpawnScenario;var waves=new List<object>();if(scenario?.BossSpawnWaves!=null)foreach(var wave in scenario.BossSpawnWaves){var values=new Dictionary<string,object?>();foreach(var p in typeof(BossLocationSpawn).GetProperties(BindingFlags.Public|BindingFlags.Instance))if(p.PropertyType.IsPrimitive||p.PropertyType.IsEnum||p.PropertyType==typeof(string))try{values[p.Name]=p.GetValue(wave)?.ToString();}catch{}waves.Add(values);}
   Write("lab-ai-state",new{coreActive=BotInternalSettingsController.Core?.ACTIVE_HALLOWEEN_ZOMBIES_EVENT,configEnabled=config?.Enabled,configured=Flag("configured"),enabled=Flag("enabled"),sawWorld=Flag("sawWorld"),hasZombieController=zombie!=null,zombieInitialized=zombie?._isInited,infection=zombie?.InfectionPercentage,crowd=zombie?._crowdSpawn!=null,crowdPlayers=zombie?._crowdSpawn?._playersInfo?.Count,midSessionSpawned=zombie?._midSessionSpawned,pursuits=zombie?._activePursuits?.Count,zombieSpawner=zombie?._spawner!=null,crowdSpawner=zombie?._crowdSpawn?._spawner!=null,actualSpawner=controller?.BotSpawner!=null,botsLoading=controller?.BotSpawner?._botCreator?.BotsLoading,bundlesLoading=controller?.BotSpawner?._botCreator?.BundlesLoading,spawnPending=controller?.BotSpawner?._inSpawnProcess,profilesLoaded=controller?.BotSpawner?.IsProfilesLoaded,subscribed=scenario?._isSubscribed,waves});
  }
  else if(action=="zombie-combat-state"){
   var world=Singleton<GameWorld>.Instance??throw new InvalidOperationException("No raid");var local=world.MainPlayer??throw new InvalidOperationException("No player");var actors=new List<object>();
   foreach(var actor in world.AllAlivePlayersList){if(actor==null||actor.IsYourPlayer||actor.HealthController?.IsAlive!=true)continue;string role=actor.Profile?.Info?.Settings?.Role.ToString()??"";if(!role.StartsWith("infected",StringComparison.OrdinalIgnoreCase))continue;
    var owner=actor.AIData?.BotOwner;var memory=owner?.Memory;var group=owner?.BotsGroup;
    actors.Add(new{role,side=actor.Profile?.Side.ToString(),distance=Vector3.Distance(actor.Position,local.Position),owner=owner!=null,botState=owner?.BotState.ToString(),playerClass=actor.GetIl2CppType()?.Name,active=actor.gameObject.activeInHierarchy,paused=AiTestControl.IsPaused(actor),locked=ModelPoseLock.Locked(actor),individualIgnore=owner!=null&&AiActorControl.Ignored(owner,local),groupEnemy=group?.Enemies?.ContainsKey(local),remembered=owner?.EnemiesController?.EnemyInfos?.ContainsKey(local),goalIsPlayer=memory?.GoalEnemy?.Person?.ProfileId==local.ProfileId,peace=memory?.IsPeace,hands=actor.HandsController?.GetIl2CppType()?.Name,handsItem=actor.HandsController?.Item?.StringTemplateId});
   }Write("zombie-combat-state",new{ignore=Plugin.Self.IgnoreAI.Value,localSide=local.Profile?.Side.ToString(),actors});
  }
  else if(action=="trigger-infection-start"){
   var world=Singleton<GameWorld>.Instance??throw new InvalidOperationException("No active raid");if(world.MainPlayer?.HealthController?.IsAlive!=true||!AiSpawnService.GameStarted)throw new InvalidOperationException("Raid not ready");
   var zombie=Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController?.EventsController?.BotHalloweenWithZombies??throw new InvalidOperationException("Native infection controller missing");
   if(!zombie._isInited||BotInternalSettingsController.Core?.ACTIVE_HALLOWEEN_ZOMBIES_EVENT!=true)throw new InvalidOperationException("Native infection disabled");
   File.WriteAllText(file,"{\"action\":\"lab-ai-state\"}");zombie.SpawnAction();Plugin.Self.Log.LogInfo("Requested one native initial infection spawn action for validation");Write("infection-start-requested",new{world=world.Pointer.ToInt64()});
  }
  else if(action=="retry-local"){task=app.LocalGameMatching(app._raidSettings.TimeAndWeatherSettings,false);Write("native-task-started",new{action});}
  else if(action=="ready-match"){task=app.OnReadyToStartMatchingAsync();Write("native-task-started",new{action});}
  else if(action=="recover-menu"){if(Singleton<GameWorld>.Instance?.MainPlayer!=null)throw new InvalidOperationException("Finish the active raid before returning to menu");task=app.ComebackToMainMenu(false,false);Write("returning-to-menu",null);}
  else if(action=="prepare-port"){
   var port=AppDomain.CurrentDomain.GetAssemblies();var assembly=Array.Find(port,a=>a.GetName().Name=="Local.HalloweenEventPort.Client")??throw new InvalidOperationException("Port assembly is unavailable");
   var runtime=assembly.GetType("HalloweenEventPort.PortRuntime",true)!;var instance=runtime.GetField("Instance",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null);runtime.GetMethod("EnsureBundleLoaded",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(instance,null);
   if(Singleton<GameWorld>.Instantiated||app._menuOperation==null)throw new InvalidOperationException("Return to the main menu before resetting the failed launch");
   var launcher=assembly.GetType("HalloweenEventPort.RaidLauncher",true)!;launcher.GetField("matching",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,null);launcher.GetField("levels",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,null);launcher.GetField("requested",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,"");
   var bridge=assembly.GetType("HalloweenEventPort.RaidSceneBridge",true)!;var presets=(System.Collections.IDictionary)bridge.GetField("presets",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;int cached=presets.Count;presets.Clear();Write("port-prepared",new{clearedPresets=cached});
  }
  else if(action=="load-state"){
   var assembly=Array.Find(AppDomain.CurrentDomain.GetAssemblies(),a=>a.GetName().Name=="Local.HalloweenEventPort.Client")!;var matching=(Il2CppSystem.Threading.Tasks.Task?)assembly.GetType("HalloweenEventPort.RaidLauncher",true)!.GetField("matching",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null);
   var grid=Koenigz.PerfectCulling.EFT.PerfectCullingAdaptiveGrid.Instance;var sampler=Koenigz.PerfectCulling.EFT.PerfectCullingCrossSceneSampler.Instance;
   Write("load-state",new{matching=matching?.Status.ToString(),error=matching?.Exception?.ToString(),grid=grid!=null,hash=grid?.GridHash,packed=grid?.PackedData!=null,valid=grid?.PackedData?.IsValid,cells=grid?.PackedData?.NumCells,mapping=grid?.RuntimeGroupMapping?.Count,bvh=grid?.RuntimeBVH!=null,sampler=sampler!=null,visibility=sampler?.IsUpdatingVisibility,adaptiveTask=sampler?._adaptiveGridUpdateTask?.Status.ToString(),adaptiveError=sampler?._adaptiveGridUpdateTask?.Exception?.ToString(),pendingSceneLoads=assembly.GetType("HalloweenEventPort.RaidSceneBridge",true)!.GetField("pending",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null) is System.Collections.IList pending?pending.Count:-1});
  }
  else if(action=="install-completion-fix"){var fix=Assembly.Load(File.ReadAllBytes(Path.Combine(Home,"Local.HalloweenEventPort.LiveFix.dll")));fix.GetType("HalloweenEventPort.LiveFix.CompletionFix",true)!.GetMethod("Install")!.Invoke(null,new object[]{Home});Write("completion-fix-installed",null);}
  else if(action=="install-wait-probe"){var fix=Assembly.Load(File.ReadAllBytes(Path.Combine(Home,"Local.HalloweenEventPort.WaitProbe.dll")));fix.GetType("HalloweenEventPort.WaitProbe.Probe",true)!.GetMethod("Install")!.Invoke(null,new object[]{Home});Write("wait-probe-installed",null);}
  else if(action=="dump-load-code"){EventLoadCode.Dump(Home);Write("load-code-saved",null);}
  else if(action=="resolve-load-code"){EventLoadCode.Resolve(Home);Write("load-symbol-scan-started",null);}
  else if(action=="watch-audio"){EventAudioProbe.Install();Write("audio-probe-installed",null);}
  else if(action=="trace-map"){EventMapTrace.Install();EventAudioProbe.Install();Write("map-trace-installed",null);}
  else if(action=="restore-state-machine"){Write("state-machine-restored",EventLoadCode.RestoreStateMachine());}
  else if(action=="skeleton-state"){
   var output=new List<object>();foreach(var skeleton in UnityEngine.Object.FindObjectsOfType<Diz.Skinning.Skeleton>(true)){var keys=new List<string>();if(skeleton.Bones!=null)foreach(var pair in skeleton.Bones)if(pair.Key.Contains("Spine",StringComparison.Ordinal)||pair.Key.EndsWith("Pelvis",StringComparison.Ordinal))keys.Add(pair.Key);output.Add(new{name=skeleton.name,scene=skeleton.gameObject.scene.name,bones=skeleton.Bones?.Count,keys});}Write("skeleton-state",output);
  }
  else if(action=="character-state"||action=="select-existing-character"){
   var controller=EFT.UI.Screens.EftScreenManager.Instance?.CurrentScreenController?.TryCast<EFT.UI.CharacterSelectionScreen.CharacterSelectionScreenController>()??throw new InvalidOperationException("Character selection is not open");
   var slots=new List<object>();var available=new List<(EGameMode Mode,CharacterSelectionProfileData Profile)>();
   foreach(var pair in controller.Data){slots.Add(new{mode=pair.Key.ToString(),hasProfile=pair.Value?.HasProfile,selectable=pair.Value?.IsSelectable,status=pair.Value?.Status.ToString()});if(pair.Value?.HasProfile==true&&pair.Value.IsSelectable)available.Add((pair.Key,pair.Value));}
   if(action=="character-state")Write("character-state",new{slots});
   else{if(available.Count!=1)throw new InvalidOperationException("Expected one existing selectable character; found "+available.Count);task=controller.SubmitAsync(available[0].Mode,available[0].Profile);Write("character-selected",new{mode=available[0].Mode.ToString()});}
  }
  else if(action=="scene-operations"||action=="release-scene-operations"){
   var cache=(ConcurrentDictionary<IntPtr,WeakReference<Il2CppObjectBase>>)typeof(Il2CppInterop.Runtime.Runtime.Il2CppObjectPool).GetField("s_cache",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
   var operations=new List<LoadSceneOperation>();var parents=new List<object>();
   foreach(var pair in cache)if(pair.Value.TryGetTarget(out var value)){
    if(value is LoadSceneOperation scene&&scene.Info?.StartsWith("HalloweenEventPort:")==true)operations.Add(scene);
    if(value is LoadScenesFromPresetOperation parent){parents.Add(new{parent.Completed,parent.Succeed,parent.Error,count=parent._loadSceneOperations?.Count});if(parent._loadSceneOperations!=null)foreach(var child in parent._loadSceneOperations)if(child.Info?.StartsWith("HalloweenEventPort:")==true&&!operations.Exists(o=>o.Pointer==child.Pointer))operations.Add(child);}
   }
   var scenes=new List<object>();foreach(var operation in operations){var unity=operation.AsyncOperation;scenes.Add(new{operation.Info,operation.Completed,operation.Succeed,operation.Error,progress=unity?.progress,done=unity?.isDone,activate=unity?.allowSceneActivation});if(action=="release-scene-operations"&&unity!=null&&(unity.isDone||unity.progress>=.9f)){operation.field_Private_Boolean_0=true;operation.RaiseCompletedEvent();}}
   Write("scene-operations",new{scenes,parents});
  }
  else throw new ArgumentException("Unknown launch diagnostic action");
 }catch(Exception error){task=null;Write("diagnostic-error",new{error=error.ToString()});}}
}
