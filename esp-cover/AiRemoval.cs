using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AiRemoval {
 sealed class Record {internal IntPtr World,Pointer;internal string Profile="",Name="";internal bool PlayerDisposeCalled;}
 static readonly Dictionary<(IntPtr World,IntPtr Actor,string Profile),Record> records=new();static IntPtr world;
 internal static string Status{get;private set;}="";
 static T? Read<T>(Func<T?> read)where T:class{try{return read();}catch{return null;}}
 internal static Player[] Gather(GameWorld current,bool history=false){var result=new Dictionary<IntPtr,Player>();void Add(Player? player){if(ReferenceEquals(player,null)||player.Pointer==current.MainPlayer?.Pointer)return;try{if(player.IsYourPlayer||!(player.IsAI||player.AIData?.BotOwner!=null))return;result[player.Pointer]=player;}catch{}}
  try{var alive=current.AllAlivePlayersList;if(alive!=null)for(int i=0;i<alive.Count;i++)Add(alive[i]);}catch{}
  try{var registered=current.RegisteredPlayers;if(registered!=null)for(int i=0;i<registered.Count;i++)Add(registered[i]?.TryCast<Player>());}catch{}
  var controller=Read(()=>Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController);try{if(controller?.Bots?.BotOwners is {} owners)foreach(var owner in owners)Add(Read(()=>owner?.GetPlayer));}catch{}
  try{var spawner=controller?.GetSpawner();if(spawner!=null)for(int i=0;i<spawner.PlayersCount;i++)Add(Read(()=>spawner.GetPlayer(i)));}catch{}
  try{if(history&&current.AllPlayersEverExisted is {} all)foreach(var actor in all)Add(actor);}catch{}return result.Values.ToArray();
 }
 static bool Registered(GameWorld current,Player player,BotsController? controller){
  try{var list=current.AllAlivePlayersList;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i]?.Pointer==player.Pointer)return true;}catch{}
  try{var list=current.RegisteredPlayers;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i]?.TryCast<Player>()?.Pointer==player.Pointer)return true;}catch{}
  try{if(controller?.Bots?.BotOwners is {} owners)foreach(var owner in owners)if(Read(()=>owner?.GetPlayer)?.Pointer==player.Pointer)return true;}catch{}
  try{var spawner=controller?.GetSpawner();if(spawner!=null)for(int i=0;i<spawner.PlayersCount;i++)if(Read(()=>spawner.GetPlayer(i))?.Pointer==player.Pointer)return true;}catch{}return false;
 }
 internal static int Delete(GameWorld current,IEnumerable<Player> selected,bool report=true){
  if(world!=current.Pointer){records.Clear();world=current.Pointer;}
  var controller=Read(()=>Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController);var spawner=Read(()=>controller?.GetSpawner());
  var targets=selected.Where(p=>!ReferenceEquals(p,null)).GroupBy(p=>p.Pointer).Select(g=>g.First()).ToArray();int removed=0,absent=0,protectedCount=0;var errors=new List<string>();
  foreach(var player in targets){
   try{
    if(player.Pointer==current.MainPlayer?.Pointer||player.IsYourPlayer){protectedCount++;continue;}
    string id=Read(()=>player.ProfileId)??"";var prior=records.Values.FirstOrDefault(r=>r.World==current.Pointer&&r.Pointer==player.Pointer&&(id.Length==0||r.Profile==id));if(id.Length==0)id=prior?.Profile??AiActorControl.CleanupIdentity(player.Pointer,current.Pointer);
    string known=AiActorControl.CleanupIdentity(player.Pointer,current.Pointer);if(known.Length>0&&known!=id){protectedCount++;continue;}
    var owner=Read(()=>player.AIData?.BotOwner);bool ai=prior!=null||known.Length>0;try{ai|=player.IsAI||owner!=null;}catch{}
    bool belongs=false;try{belongs=player.GameWorld?.Pointer==current.Pointer;}catch{}bool present=Registered(current,player,controller);belongs|=present||prior!=null||AiActorControl.CleanupIdentity(player.Pointer,current.Pointer)==id&&id.Length>0;
    if(!belongs&&current._allPlayersEverExistedByProfileId?.TryGetValue(id,out var historical)==true)belongs=historical?.Pointer==player.Pointer;
    if(!AiCleanupRules.Allowed(false,ai,belongs?current.Pointer.ToInt64():0,current.Pointer.ToInt64(),id,id)){protectedCount++;continue;}
    var key=(current.Pointer,player.Pointer,id);if(!records.TryGetValue(key,out var record))records[key]=record=new(){World=current.Pointer,Pointer=player.Pointer,Profile=id,Name=Read(()=>player.Profile?.Nickname)??id};
    var go=Read(()=>player.gameObject);
    var failures=AiCleanupRules.AllSteps(new (string,Action)[]{
     ("清理动作控制",()=>{ModelPoseLock.Release(player,false);AiSceneDirector.Remove(player);AiFreePlacement.Release(player);}),
     ("清理暂停与管理状态",()=>{AiTestControl.ForgetActor(id);AiActorControl.CleanupTracking(player.Pointer,id,owner?.Pointer??IntPtr.Zero);}),
     ("停止 AI",()=>{if(owner!=null&&owner.BotState!=EBotState.Disposed)owner.Deactivate();}),
     ("从生成器移除",()=>{spawner?.DeletePlayer(player);}),
     ("从 AI 注册表移除控制器",()=>{if(owner!=null)controller?.Bots?.Remove(owner);}),
     ("从 AI 注册表移除人物",()=>{controller?.Bots?.RemovePlayer(player);}),
     ("注销世界人物",()=>current.UnregisterPlayer(player.Cast<IPlayer>())),
     ("释放 AI 资源",()=>{if(owner!=null&&owner.BotState!=EBotState.Disposed)owner.Dispose(true);}),
     ("释放人物资源",()=>{if(go!=null&&!record.PlayerDisposeCalled&&Read(()=>player.HealthController)!=null){record.PlayerDisposeCalled=true;player.Dispose();}}),
     ("移除模型",()=>{if(go!=null){go.SetActive(false);UnityEngine.Object.Destroy(go);}})
    });
    bool remains=Registered(current,player,controller);if(!present)absent++;else if(!remains)removed++;
    if(remains)errors.Add(record.Name+"：注册表仍有残留");
    if(failures.Length>0)Plugin.Self.Log.LogWarning("AI cleanup "+record.Name+" ["+id+"]: "+string.Join("；",failures));
   }catch(Exception error){errors.Add(error.Message);}
  }
  Status=$"逐个清理 {targets.Length} 名：确认移除 {removed}，原已不在注册表 {absent}，保护／过期对象 {protectedCount}，残留／失败 {errors.Count}。";
  if(errors.Count>0)Status+=" "+string.Join("；",errors.Take(2));if(report||errors.Count>0)Plugin.Self.Log.LogInfo(Status);return removed;
 }
 internal static void Reset(){records.Clear();world=IntPtr.Zero;Status="";}
}
