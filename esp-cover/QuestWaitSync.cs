using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Quests;
using UnityEngine;
namespace LocalSpt5Esp;

// Quest templates are omitted by the backend while their acceptance delay runs.
// Updating profile timestamps alone cannot add those missing native quest objects.
internal static class QuestWaitSync {
 static Task<string>? snapshot;
 static Il2CppSystem.Threading.Tasks.Task<Il2CppSystem.Collections.Generic.List<QuestTemplate>>? templates;
 static QuestControllerClientBackend? pendingController;
 static QuestControllerClientBackend? snapshotController;
 static JsonElement? queued;
 static string profile="",notice="";
 static float next;
 static DateTime deadline;
 static bool initialized;
 static readonly HashSet<string> waitingIds=new();
 internal static string Notice=>notice;
 static QuestControllerClientBackend? Controller(){
  var app=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>()??UnityEngine.Object.FindObjectOfType<TarkovApplication>();
  return (app?._menuOperation?.QuestController??EFT.UI.ItemUiContext.Instance?.QuestController)?.TryCast<QuestControllerClientBackend>();
 }
 internal static void Queue(JsonElement response){
  if(!response.TryGetProperty("waitSync",out var sync))return;
  if(snapshot!=null||templates!=null){queued=sync.Clone();return;}
  try{var controller=Controller();if(controller==null||!Matches(controller,sync))return;Apply(controller,sync);}
  catch(Exception error){Failed(error);}
 }
 static bool Matches(QuestControllerClientBackend controller,JsonElement sync)=>!ItemSpawner.InRaid()&&!controller.InRaid&&controller.Profile?.Id==sync.GetProperty("profileId").GetString();
 static void Apply(QuestControllerClientBackend controller,JsonElement sync){
  if(!Matches(controller,sync))return;
  int changed=0;waitingIds.Clear();
  foreach(var row in sync.GetProperty("quests").EnumerateArray()){
   string id=row.GetProperty("id").GetString()!;var state=(EQuestStatus)row.GetProperty("state").GetInt32();
   if(state==EQuestStatus.AvailableAfter)waitingIds.Add(id);
   var local=controller.Profile.QuestsData?.FirstOrDefault(q=>q.Id==id);
   if(local==null||!QuestWaitSyncRules.SameQuest(controller.Profile.Id,sync.GetProperty("profileId").GetString()!,controller.InRaid,(int)local.Status,(int)state,local.StartTime,row.GetProperty("startTime").GetInt64()))continue;
   if(state==EQuestStatus.AvailableAfter&&row.TryGetProperty("availableAfter",out var after)&&after.TryGetInt32(out int availableTime)&&QuestWaitSyncRules.Shortens(local.AvailableAfter,availableTime)){local.AvailableAfter=availableTime;changed++;}
   if(local.StatusStartTimestamps==null||!row.TryGetProperty("timers",out var timers)||timers.ValueKind!=JsonValueKind.Object)continue;
   foreach(var timer in timers.EnumerateObject()){
    if(!Enum.TryParse<EQuestStatus>(timer.Name,out var timerState)||timerState!=state||!timer.Value.TryGetDouble(out double time)||!double.IsFinite(time)||time<=0)continue;
    if(local.StatusStartTimestamps.TryGetValue(state,out double old)&&QuestWaitSyncRules.Shortens(old,time)){local.StatusStartTimestamps[state]=time;changed++;}
    var quest=local.Quest;if(quest?.QuestStatus==state&&quest.StatusStartTimestamps?.TryGetValue(state,out double questTime)==true&&QuestWaitSyncRules.Shortens(questTime,time))quest.StatusStartTimestamps[state]=time;
   }
  }
  pendingController=controller;templates=controller.Session.RequestQuestsTemplates(true);deadline=DateTime.UtcNow.AddSeconds(20);
  notice="等待时间已同步，正在刷新原生任务列表…";
  Plugin.Self.Log.LogInfo("Quest wait sync: timestamps="+changed+"; requesting native quest templates");
 }
 internal static void Tick(){try{
  if(ItemSpawner.InRaid()){Reset();return;}
  if((snapshot!=null||templates!=null)&&DateTime.UtcNow>deadline)throw new TimeoutException("Native quest refresh timed out");
  if(snapshot?.IsCompleted==true){var response=snapshot.GetAwaiter().GetResult();snapshot=null;var expected=snapshotController;snapshotController=null;if(expected==null||Controller()?.Pointer!=expected.Pointer)return;using var doc=JsonDocument.Parse(response);var root=doc.RootElement;if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0){notice=root.GetProperty("errmsg").GetString()??"任务同步失败，请刷新。";return;}Queue(CharacterDataPanel.Data(root));}
  if(templates?.IsCompleted==true){
   var controller=pendingController;var completed=templates;templates=null;pendingController=null;
   if(controller==null||Controller()?.Pointer!=controller.Pointer||controller.InRaid)return;
   var incoming=completed.GetAwaiter().GetResult().ToArray();int before=controller.Quests.Count;
   controller.TryAddNewQuestsByTemplate(incoming);
   var expired=new List<object>();long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
   foreach(var data in controller.Profile.QuestsData){var quest=data.Quest;if(quest==null||!waitingIds.Contains(data.Id)||quest.QuestStatus!=EQuestStatus.AvailableAfter||data.AvailableAfter<=0||data.AvailableAfter>now)continue;
    quest.CheckForStatusChange(false,false,true);
    expired.Add(new{id=data.Id,state=quest.QuestStatus.ToString(),visible=quest.IsVisible,availableAfter=data.AvailableAfter});
   }
   notice="原生任务列表已刷新，请重新打开商人任务页。";
   var waiting=waitingIds.Select(id=>{var saved=controller.Profile.QuestsData.FirstOrDefault(q=>q.Id==id);var quest=controller.Quests.GetConditional(id);return new{id,profileState=saved?.Status.ToString(),profileAfter=saved?.AvailableAfter,questState=quest?.QuestStatus.ToString(),visible=quest?.IsVisible,hasTemplate=incoming.Any(t=>t.Id==id),timers=quest?.StatusStartTimestamps==null?"":string.Join(",",quest.StatusStartTimestamps.Select(p=>p.Key+":"+p.Value))};}).ToArray();
   var report=new{utc=DateTime.UtcNow,profileId=controller.Profile.Id,templates=incoming.Length,before,after=controller.Quests.Count,expired,waiting};
   File.WriteAllText(Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","runtime","quest-wait-sync.json"),JsonSerializer.Serialize(report));
   Plugin.Self.Log.LogInfo("Quest wait sync complete: "+JsonSerializer.Serialize(report));
  }
  if(snapshot==null&&templates==null&&queued is {} latest){queued=null;var controller=Controller();if(controller!=null&&Matches(controller,latest))Apply(controller,latest);}
  if(snapshot!=null||templates!=null||Time.unscaledTime<next)return;next=Time.unscaledTime+2;
  var current=Controller();if(current?.Profile==null||current.InRaid)return;
  if(initialized&&profile==current.Profile.Id)return;
  initialized=true;profile=current.Profile.Id;snapshotController=current;deadline=DateTime.UtcNow.AddSeconds(20);snapshot=CharacterDataPanel.Call("/local/gear/story","storyManagement",new(){{"action","status"}});
 }catch(Exception error){Failed(error);}}
 static void Failed(Exception error){snapshot=null;templates=null;pendingController=snapshotController=null;queued=null;notice="任务同步失败，请刷新或重新登录角色。";Plugin.Self.Log.LogWarning("Quest wait sync: "+error);}
 internal static void Reset(){snapshot=null;templates=null;pendingController=snapshotController=null;queued=null;waitingIds.Clear();profile=notice="";initialized=false;next=0;}
}
