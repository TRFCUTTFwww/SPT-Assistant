using System.Text.Json;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
namespace ProjectE.Client;
public sealed partial class ProjectModule {
 ConfigEntry<bool> artifactsEnabled=null!,watchEnabled=null!,repairEnabled=null!;
 ConfigEntry<int> watchCharge=null!;
 readonly Queue<TableRequest> artifactQueue=[];
 readonly Dictionary<IntPtr,ICharacterController> slowed=[];
 Task<TableReply>? artifactPending;TableRequest? artifactRequest;string artifactProfile="";IntPtr artifactActor;
 float nextArtifacts,watchUntil,nextWatchScan,nextArtifactWarning;
 sealed record WatchMove(ICharacterController Controller,float Previous,float Applied);
 void InitializeArtifacts(Harmony harmony){
  artifactsEnabled=settingsFile!.Bind("Artifacts","Stones enabled",true,"随身携带的身之/灵魂/生命宝石自动生效，从转化桌钱包消耗 EMC。");
  watchEnabled=settingsFile.Bind("Artifacts","Time watch enabled",true,"携带时间洪流怀表时，消耗 EMC 减慢 8 米内 AI 的水平移动，不影响玩家与全局时间。");
  watchCharge=settingsFile.Bind("Artifacts","Time watch charge",0,new ConfigDescription("怀表充能等级 0/1/2；每秒消耗 20/30/40 EMC。",new AcceptableValueRange<int>(0,2)));
  repairEnabled=settingsFile.Bind("Artifacts","Repair talisman enabled",true,"修复护符每秒恢复 1 点当前耐久，不能提高耐久上限。");
  foreach(var type in new[]{typeof(SimpleCharacterController),typeof(PlayerCharacterController),typeof(ImpostorCharacterController),typeof(PlayerSpiritCharacterController)}){
   var method=type.GetMethod("Move",[typeof(Vector3),typeof(float)])??throw new MissingMethodException(type.Name,"Move");
   harmony.Patch(method,prefix:new HarmonyMethod(typeof(ProjectModule),nameof(WatchBeforeMove)),finalizer:new HarmonyMethod(typeof(ProjectModule),nameof(WatchAfterMove)));
  }
  context!.OnShutdown(()=>{slowed.Clear();watchUntil=0;artifactQueue.Clear();});
 }
 static async Task<TableReply> ArtifactCall(TableRequest request){
  Exception? failure=null;for(int attempt=0;attempt<2;attempt++)try{return TableProtocol.Read(await SPTushonka.Common.Http.RequestHandler.PostJsonAsync("/local/projecte/table",JsonSerializer.Serialize(request)).WaitAsync(TimeSpan.FromSeconds(15)));}catch(Exception e){failure=e;}
  throw new IOException("宝石 EMC 请求未确认。",failure);
 }
 void QueueArtifact(Player p,string template,int flags){artifactQueue.Enqueue(new(){Action="artifact",Profile=p.ProfileId,Operation=Guid.NewGuid().ToString("N"),Raid=true,Template=template,Count=flags,Quality=template==Gadgets.Watch?watchCharge.Value:0});}
 void TickArtifacts(Player? player){
  if(artifactPending?.IsCompleted==true){var task=artifactPending;var effect=artifactRequest!;artifactPending=null;artifactRequest=null;
   try{var reply=task.GetAwaiter().GetResult();if(!reply.Ok)throw new InvalidOperationException(reply.Error);
    if(player?.Pointer==artifactActor&&player.ProfileId==effect.Profile&&player.HealthController?.IsAlive==true&&CarriedInventory.Items(player).Any(i=>i.StringTemplateId==effect.Template))ApplyArtifact(player,effect);
   }catch(Exception e){if(Time.unscaledTime>=nextArtifactWarning){nextArtifactWarning=Time.unscaledTime+20;Say(e.Message);}}
  }
  if(player==null||player.HealthController?.IsAlive!=true){artifactQueue.Clear();slowed.Clear();watchUntil=0;return;}
  if(artifactActor!=player.Pointer||artifactProfile!=player.ProfileId){artifactQueue.Clear();slowed.Clear();watchUntil=0;artifactActor=player.Pointer;artifactProfile=player.ProfileId;}
  if(Time.unscaledTime>=nextArtifacts){
   nextArtifacts=Time.unscaledTime+1;var carried=CarriedInventory.Items(player);var ids=carried.Select(i=>i.StringTemplateId).ToHashSet();
   if(repairEnabled.Value&&ids.Contains(Gadgets.Repair))foreach(var item in carried){var repair=item.GetItemComponent<RepairableComponent>();if(repair==null)continue;float value=ArtifactRules.Repair(repair.Durability,repair.MaxDurability);if(value>repair.Durability){repair.Durability=value;item.RaiseRefreshEvent(false,true);}}
   if(!ids.Contains(Gadgets.Watch)||!watchEnabled.Value){watchUntil=0;slowed.Clear();}
   if(artifactPending==null&&artifactQueue.Count==0&&table?.Open!=true&&table?.Busy!=true){
    bool hungry=player.ActiveHealthController.Energy.Current<player.ActiveHealthController.Energy.Maximum-.01f;
    bool hurt=bodyParts.Any(part=>{var h=player.ActiveHealthController.GetBodyPartHealth(part,false);return h.Current>0&&h.Current<h.Maximum-.01f;});
    if(artifactsEnabled.Value){if(ids.Contains(Gadgets.Life)){int flags=(hungry?1:0)|(hurt?2:0);if(flags!=0)QueueArtifact(player,Gadgets.Life,flags);}
     else{if(hungry&&ids.Contains(Gadgets.Body))QueueArtifact(player,Gadgets.Body,1);if(hurt&&ids.Contains(Gadgets.Soul))QueueArtifact(player,Gadgets.Soul,2);}}
    if(watchEnabled.Value&&ids.Contains(Gadgets.Watch))QueueArtifact(player,Gadgets.Watch,0);
   }
  }
  if(artifactPending==null&&artifactQueue.Count>0){artifactRequest=artifactQueue.Dequeue();artifactPending=ArtifactCall(artifactRequest);}
  if(Time.unscaledTime<watchUntil&&Time.unscaledTime>=nextWatchScan){nextWatchScan=Time.unscaledTime+.2f;slowed.Clear();
   var actors=Singleton<GameWorld>.Instance?.AllAlivePlayersList;if(actors!=null)foreach(var actor in actors){if(actor==null||!actor.IsAI||actor.IsYourPlayer||actor.HealthController?.IsAlive!=true||Vector3.Distance(actor.Position,player.Position)>8)continue;var controller=actor.CharacterController;if(controller!=null)slowed[controller.Pointer]=controller;}
  }else if(Time.unscaledTime>=watchUntil)slowed.Clear();
 }
 void ApplyArtifact(Player p,TableRequest effect){
  var health=p.ActiveHealthController;if(effect.Template==Gadgets.Watch){if(watchEnabled.Value)watchUntil=Time.unscaledTime+1.3f;return;}
  if(!artifactsEnabled.Value)return;
  if((effect.Count&1)!=0)health.ChangeEnergy(Math.Min(2,Math.Max(0,health.Energy.Maximum-health.Energy.Current)));
  if((effect.Count&2)!=0){float remaining=2;foreach(var part in bodyParts.OrderBy(part=>{var h=health.GetBodyPartHealth(part,false);return h.Maximum>0?h.Current/h.Maximum:1;})){
   var h=health.GetBodyPartHealth(part,false);if(h.Current<=0||h.Current>=h.Maximum)continue;float amount=Math.Min(remaining,h.Maximum-h.Current);health.ChangeHealth(part,amount,new DamageInfo());remaining-=amount;if(remaining<=0)break;
  }}
 }
 static void WatchBeforeMove(Il2CppObjectBase __instance,ref Vector3 __0,out WatchMove? __state){
  __state=null;var a=active;if(a?.context?.IsActive!=true||!a.watchEnabled.Value||Time.unscaledTime>=a.watchUntil||RaidPlayer()?.Pointer!=a.artifactActor||!a.slowed.TryGetValue(__instance.Pointer,out var controller))return;
  float factor=ArtifactRules.Slowdown(a.watchCharge.Value);float old=controller.SpeedLimit;
  if(!float.IsFinite(old)||!float.IsFinite(__0.x)||!float.IsFinite(__0.z))return;
  float applied=old>0?old*factor:old;__state=new(controller,old,applied);controller.SpeedLimit=applied;__0.x*=factor;__0.z*=factor;
 }
 static void WatchAfterMove(WatchMove? __state){if(__state!=null&&__state.Controller.SpeedLimit==__state.Applied)__state.Controller.SpeedLimit=__state.Previous;}
}
