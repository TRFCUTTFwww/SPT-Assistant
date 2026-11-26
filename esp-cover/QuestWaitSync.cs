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

// Quest templates are omitted by the backend while their acceptance delay runs or while prerequisites are unmet.
// Synchronizing Profile.QuestsData + RequestQuestsTemplates + live Quest.SetStatus applies story/quest mutations without relogging.
internal static class QuestWaitSync {
 static Task<string>? snapshot;
 static Il2CppSystem.Threading.Tasks.Task<Il2CppSystem.Collections.Generic.List<QuestTemplate>>? templates;
 static QuestControllerClientBackend? pendingController;
 static QuestControllerClientBackend? snapshotController;
 static JsonElement? queued;
 static bool queuedFull,activeFull;
 static string profile="",notice="";
 static float next;
 static DateTime deadline;
 static bool initialized;
 static readonly HashSet<string> waitingIds=new(StringComparer.Ordinal);
 static readonly Dictionary<string,(EQuestStatus State,long StartTime,int AvailableAfter)> fullTargets=new(StringComparer.Ordinal);
 internal static string Notice=>notice;
 static TarkovApplication? application;static float nextApplication;

 static QuestControllerClientBackend? Controller(bool search=false){
  if(EFT.UI.ItemUiContext.Instance?.QuestController?.TryCast<QuestControllerClientBackend>() is {} available)return available;
  var app=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>()??application;
  if(search&&app==null&&Time.unscaledTime>=nextApplication){nextApplication=Time.unscaledTime+10;app=UnityEngine.Object.FindObjectOfType<TarkovApplication>();}
  application=app;
  return (app?._menuOperation?.QuestController??EFT.UI.ItemUiContext.Instance?.QuestController)?.TryCast<QuestControllerClientBackend>();
 }

 internal static void Queue(JsonElement response,bool fullSync=false){
  if(!response.TryGetProperty("waitSync",out var sync))return;
  if(snapshot!=null||templates!=null){queued=sync.Clone();queuedFull|=fullSync;return;}
  try{var controller=Controller(true);if(controller==null||!Matches(controller,sync))return;Apply(controller,sync,fullSync);}
  catch(Exception error){Failed(error);}
 }

 static bool Matches(QuestControllerClientBackend controller,JsonElement sync)=>
  !ItemSpawner.InRaid()&&!controller.InRaid&&controller.Profile?.Id==sync.GetProperty("profileId").GetString();

 static void Apply(QuestControllerClientBackend controller,JsonElement sync,bool fullSync){
  if(!Matches(controller,sync))return;
  int changed=0;waitingIds.Clear();fullTargets.Clear();activeFull=fullSync;
  var questsData=controller.Profile.QuestsData;
  var localMap=new Dictionary<string,QuestStatusData>(StringComparer.Ordinal);
  if(questsData!=null)foreach(var q in questsData)if(q!=null&&!string.IsNullOrEmpty(q.Id))localMap[q.Id]=q;

  foreach(var row in sync.GetProperty("quests").EnumerateArray()){
   string id=row.GetProperty("id").GetString()!;
   var state=(EQuestStatus)row.GetProperty("state").GetInt32();
   long startTime=row.TryGetProperty("startTime",out var st)&&st.TryGetInt64(out long sv)?sv:0;
   int availableTime=row.TryGetProperty("availableAfter",out var after)&&after.TryGetInt32(out int av)?av:0;
   if(state==EQuestStatus.AvailableAfter)waitingIds.Add(id);
   localMap.TryGetValue(id,out var local);

   if(fullSync){
    fullTargets[id]=(state,startTime,availableTime);
    if(local==null&&questsData!=null&&state!=EQuestStatus.Locked){
     local=new QuestStatusData{
      Id=id,
      StartTime=(int)Math.Clamp(startTime,0,int.MaxValue),
      Status=state,
      AvailableAfter=availableTime,
      CompletedConditions=new Il2CppSystem.Collections.Generic.HashSet<MongoID>(),
      StatusStartTimestamps=new Il2CppSystem.Collections.Generic.Dictionary<EQuestStatus,double>()
     };
     local.StatusStartTimestamps[state]=startTime>0?startTime:DateTimeOffset.UtcNow.ToUnixTimeSeconds();
     questsData.Add(local);
     localMap[id]=local;
     changed++;
    }else if(local!=null){
     if(local.Status!=state||local.AvailableAfter!=availableTime){
      if(state is EQuestStatus.Locked or EQuestStatus.AvailableForStart or EQuestStatus.Started)local.CompletedConditions?.Clear();
      local.Status=state;
      local.StartTime=(int)Math.Clamp(startTime,0,int.MaxValue);
      local.AvailableAfter=availableTime;
      changed++;
     }
     local.StatusStartTimestamps??=new Il2CppSystem.Collections.Generic.Dictionary<EQuestStatus,double>();
     if(row.TryGetProperty("timers",out var tObj)&&tObj.ValueKind==JsonValueKind.Object){
      foreach(var timer in tObj.EnumerateObject())
       if(Enum.TryParse<EQuestStatus>(timer.Name,out var ts)&&timer.Value.TryGetDouble(out double tv)&&double.IsFinite(tv)&&tv>0)
        local.StatusStartTimestamps[ts]=tv;
     }
    }
    continue;
   }

   if(local==null||!QuestWaitSyncRules.SameQuest(controller.Profile.Id,sync.GetProperty("profileId").GetString()!,controller.InRaid,(int)local.Status,(int)state,local.StartTime,startTime))continue;
   if(local.Status==EQuestStatus.AvailableAfter&&state==EQuestStatus.AvailableForStart){
    waitingIds.Add(id);local.AvailableAfter=0;
    if(local.Quest is {} ready)ready.SetStatus(EQuestStatus.AvailableForStart,true,true);
    else local.Status=EQuestStatus.AvailableForStart;
    changed++;
   }
   if(state==EQuestStatus.AvailableAfter&&QuestWaitSyncRules.Shortens(local.AvailableAfter,availableTime)){local.AvailableAfter=availableTime;changed++;}
   if(local.StatusStartTimestamps==null||!row.TryGetProperty("timers",out var timers)||timers.ValueKind!=JsonValueKind.Object)continue;
   foreach(var timer in timers.EnumerateObject()){
    if(!Enum.TryParse<EQuestStatus>(timer.Name,out var timerState)||timerState!=state||!timer.Value.TryGetDouble(out double time)||!double.IsFinite(time)||time<=0)continue;
    if(local.StatusStartTimestamps.TryGetValue(state,out double old)&&QuestWaitSyncRules.Shortens(old,time)){local.StatusStartTimestamps[state]=time;changed++;}
    var quest=local.Quest;if(quest?.QuestStatus==state&&quest.StatusStartTimestamps?.TryGetValue(state,out double questTime)==true&&QuestWaitSyncRules.Shortens(questTime,time))quest.StatusStartTimestamps[state]=time;
   }
  }

  if(fullSync&&questsData!=null){
   foreach(var pair in localMap){
    if(fullTargets.ContainsKey(pair.Key)||pair.Value.Status==EQuestStatus.Locked)continue;
    pair.Value.Status=EQuestStatus.Locked;pair.Value.AvailableAfter=0;pair.Value.StartTime=0;pair.Value.CompletedConditions?.Clear();
    fullTargets[pair.Key]=(EQuestStatus.Locked,0,0);changed++;
   }
  }

  pendingController=controller;templates=controller.Session.RequestQuestsTemplates(true);deadline=DateTime.UtcNow.AddSeconds(20);
  notice=fullSync?"正在实时同步任务与剧情状态…":"等待时间已同步，正在刷新原生任务列表…";
  Plugin.Self.Log.LogInfo($"Quest sync (full={fullSync}): changes={changed}; requesting native quest templates");
 }

 static void ApplyLiveStatuses(QuestControllerClientBackend controller){
  int applied=0;
  foreach(var pair in fullTargets){
   var target=pair.Value;
   var saved=controller.Profile.QuestsData?.FirstOrDefault(q=>q.Id==pair.Key);
   if(saved!=null){
    saved.Status=target.State;
    saved.AvailableAfter=target.AvailableAfter;
    saved.StartTime=(int)Math.Clamp(target.StartTime,0,int.MaxValue);
    if(target.State is EQuestStatus.Locked or EQuestStatus.AvailableForStart or EQuestStatus.Started)saved.CompletedConditions?.Clear();
   }
   var quest=controller.Quests.GetConditional(pair.Key)??saved?.Quest;
   if(quest==null)continue;
   if(target.State is EQuestStatus.Locked or EQuestStatus.AvailableForStart or EQuestStatus.Started)quest.CompletedConditions?.Clear();
   if(quest.QuestStatus!=target.State){
    try{quest.SetStatus(target.State,true,true);}catch{}
    if(quest.QuestStatus!=target.State){
     quest._QuestStatus_k__BackingField=target.State;
     if(saved!=null)saved.Status=target.State;
     try{quest.CallOnStatusChangedEvent(quest,true);}catch{}
    }
    applied++;
   }
  }
  Plugin.Self.Log.LogInfo($"Quest live status sync applied={applied}, totalTracked={fullTargets.Count}");
 }

 internal static void Tick(){try{
  if(ItemSpawner.InRaid()){Reset();return;}
  if((snapshot!=null||templates!=null)&&DateTime.UtcNow>deadline)throw new TimeoutException("Native quest refresh timed out");
  if(snapshot?.IsCompleted==true){
   var response=snapshot.GetAwaiter().GetResult();snapshot=null;var expected=snapshotController;snapshotController=null;
   if(expected==null||Controller()?.Pointer!=expected.Pointer)return;
   using var doc=JsonDocument.Parse(response);var root=doc.RootElement;
   if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0){notice=root.GetProperty("errmsg").GetString()??"任务同步失败，请刷新。";return;}
   Queue(CharacterDataPanel.Data(root),false);
  }
  if(templates?.IsCompleted==true){
   var controller=pendingController;var completed=templates;bool wasFull=activeFull;
   templates=null;pendingController=null;activeFull=false;
   if(controller==null||Controller()?.Pointer!=controller.Pointer||controller.InRaid)return;
   var incoming=completed.GetAwaiter().GetResult().ToArray();int before=controller.Quests.Count;
   var missing=incoming.GroupBy(t=>t.Id.ToString()).Select(g=>g.First()).Where(t=>controller.Quests.GetConditional(t.Id)==null).ToArray();
   if(missing.Length>0)controller.TryAddNewQuestsByTemplate(missing);
   var expired=new List<object>();long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
   foreach(var data in controller.Profile.QuestsData){
    var quest=data.Quest;
    if(quest==null||!waitingIds.Contains(data.Id)||quest.QuestStatus!=EQuestStatus.AvailableAfter||data.AvailableAfter<=0||data.AvailableAfter>now)continue;
    quest.CheckForStatusChange(false,false,true);
    expired.Add(new{id=data.Id,state=quest.QuestStatus.ToString(),visible=quest.IsVisible,availableAfter=data.AvailableAfter});
   }
   if(wasFull){
    ApplyLiveStatuses(controller);
    notice="任务与剧情已实时生效（若正打开商人任务界面，切换页签即可看到最新状态）。";
    FeatureToasts.Show("任务与剧情状态已实时生效");
   }else{
    notice="原生任务列表已刷新，请重新打开商人任务页。";
   }
   var waiting=waitingIds.Select(id=>{var saved=controller.Profile.QuestsData.FirstOrDefault(q=>q.Id==id);var quest=controller.Quests.GetConditional(id);return new{id,profileState=saved?.Status.ToString(),profileAfter=saved?.AvailableAfter,questState=quest?.QuestStatus.ToString(),visible=quest?.IsVisible,hasTemplate=incoming.Any(t=>t.Id==id),timers=quest?.StatusStartTimestamps==null?"":string.Join(",",quest.StatusStartTimestamps.Select(p=>p.Key+":"+p.Value))};}).ToArray();
   var report=new{utc=DateTime.UtcNow,profileId=controller.Profile.Id,fullSync=wasFull,templates=incoming.Length,missing=missing.Length,before,after=controller.Quests.Count,expired,waiting};
   File.WriteAllText(Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","runtime","quest-wait-sync.json"),JsonSerializer.Serialize(report));
   Plugin.Self.Log.LogInfo("Quest wait sync complete: "+JsonSerializer.Serialize(report));
  }
  if(snapshot==null&&templates==null&&queued is {} latest){bool nextFull=queuedFull;queued=null;queuedFull=false;var controller=Controller();if(controller!=null&&Matches(controller,latest))Apply(controller,latest,nextFull);}
  if(snapshot!=null||templates!=null||Time.unscaledTime<next)return;next=Time.unscaledTime+2;
  var current=Controller();if(current?.Profile==null||current.InRaid)return;
  if(initialized&&profile==current.Profile.Id)return;
  initialized=true;profile=current.Profile.Id;snapshotController=current;deadline=DateTime.UtcNow.AddSeconds(20);
  snapshot=CharacterDataPanel.Call("/local/gear/story","storyManagement",new(){{"action","status"}});
 }catch(Exception error){Failed(error);}}

 static void Failed(Exception error){snapshot=null;templates=null;pendingController=snapshotController=null;queued=null;queuedFull=activeFull=false;fullTargets.Clear();notice="任务同步失败，请刷新或重新登录角色。";Plugin.Self.Log.LogWarning("Quest wait sync: "+error);}
 internal static void Reset(){snapshot=null;templates=null;pendingController=snapshotController=null;queued=null;queuedFull=activeFull=false;waitingIds.Clear();fullTargets.Clear();profile=notice="";initialized=false;next=0;application=null;nextApplication=0;}
}
