using System.Text.Json;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
namespace LocalGearMail;

public static class StoryRules {
 public static void SkipWait(PmcData pmc,TemplateTable templates,MongoId id,long now){
  if(!templates.Quests.TryGetValue(id,out var quest)||quest.IsStoryQuest!=true)throw new ArgumentException("请选择剧情等待任务。");
  var saved=pmc.Quests?.SingleOrDefault(q=>q.QId==id);if(saved?.Status!=QuestStatusEnum.AvailableAfter)throw new ArgumentException("该任务目前不处于等待状态。");
  var updates=new List<(QuestStatus Quest,QuestStatusEnum State,double Time)>();
  foreach(var condition in quest.Conditions?.AvailableForStart??[]){
   if(condition.ConditionType!="Quest"||condition.AvailableAfter is not >0)continue;
   foreach(string target in Targets(condition)){
    var prior=pmc.Quests?.SingleOrDefault(q=>q.QId.ToString()==target);
    if(prior==null||condition.Status?.Contains(prior.Status)!=true||prior.StatusTimers?.TryGetValue(prior.Status,out double time)!=true)throw new ArgumentException("该任务的前置条件尚未满足。");
    if(time+condition.AvailableAfter>now)updates.Add((prior,prior.Status,now-condition.AvailableAfter.Value-1));
   }
  }
  foreach(var update in updates)update.Quest.StatusTimers[update.State]=update.Time;
  saved.AvailableAfter=now-1;
 }
 public static IEnumerable<string> Targets(QuestCondition condition)=>condition.Target==null?[]:condition.Target.IsList?condition.Target.List??[]:condition.Target.IsItem&&condition.Target.Item!=null?[condition.Target.Item]:[];
 public static HashSet<MongoId> StoryTasks(TemplateTable templates){var found=templates.Quests.Where(p=>p.Value.IsStoryQuest==true).Select(p=>p.Key).ToHashSet();foreach(var id in found.ToArray())foreach(var condition in templates.Quests[id].Conditions?.AvailableForFinish??[])if(condition.ConditionType=="Quest")foreach(string target in Targets(condition))if(templates.Quests.ContainsKey(new MongoId(target)))found.Add(new MongoId(target));return found;}
 public static HashSet<MongoId> Alternatives(MongoId id,TemplateTable templates){
  var found=new HashSet<MongoId>();
  foreach(var condition in templates.Quests[id].Conditions?.Fail??[])if(condition.ConditionType=="Quest"&&condition.Status?.Contains(QuestStatusEnum.Success)==true)foreach(string target in Targets(condition))if(templates.Quests.ContainsKey(new MongoId(target)))found.Add(new MongoId(target));
  foreach(var pair in templates.Quests)if(pair.Key!=id&&(pair.Value.Conditions?.Fail??[]).Any(c=>c.ConditionType=="Quest"&&c.Status?.Contains(QuestStatusEnum.Success)==true&&Targets(c).Contains(id.ToString())))found.Add(pair.Key);
  found.Remove(id);return found;
 }
 public static HashSet<MongoId> Dependents(IEnumerable<MongoId> seeds,TemplateTable templates){
  var affected=seeds.ToHashSet();bool changed;do{changed=false;foreach(var pair in templates.Quests.Where(pair=>!affected.Contains(pair.Key)))if((pair.Value.Conditions?.AvailableForStart??[]).Any(c=>c.ConditionType=="Quest"&&Targets(c).Any(target=>affected.Any(id=>id.ToString()==target)))){affected.Add(pair.Key);changed=true;}}while(changed);return affected;
 }
 public static void Set(PmcData pmc,MongoId id,QuestStatusEnum status,long now){
  pmc.Quests??=[];var saved=pmc.Quests.SingleOrDefault(q=>q.QId==id);if(saved==null){saved=new QuestStatus{QId=id,StartTime=0,Status=status,StatusTimers=[]};pmc.Quests.Add(saved);}
  saved.Status=status;saved.StartTime=status==QuestStatusEnum.Locked?0:now;saved.AvailableAfter=0;saved.CompletedConditions=[];saved.StatusTimers=status==QuestStatusEnum.Locked?[]:new(){{status,now}};
  if(pmc.TaskConditionCounters!=null)foreach(var key in pmc.TaskConditionCounters.Where(pair=>pair.Value.SourceId==id).Select(pair=>pair.Key).ToArray())pmc.TaskConditionCounters.Remove(key);
 }
 public static void SyncVariables(PmcData pmc,TemplateTable templates,QuestVariableHelper variables,ISet<MongoId> affected){
  // Native SyncAll only sets bits; clear generated group bits before rebuilding after a rewind.
  pmc.Variables??=[];foreach(var group in (templates.QuestVariables??[]).Values.Where(group=>(group.Quests?.Keys.Any(affected.Contains)==true)||(group.Fillers?.Any(f=>(f.Quests??[]).Any(affected.Contains))==true))){
   foreach(var id in (group.Quests??[]).Values)pmc.Variables.Remove(id);
   foreach(var variableGroup in templates.VariableGroups??[])if(variableGroup.Id==group.Id)foreach(var id in variableGroup.Variables??[])pmc.Variables.Remove(id);
  }
  variables.SyncAll(pmc);
 }
 public static int SkipWaits(PmcData pmc,TemplateTable templates,QuestConfig config,long now,bool storyQuests=true){
  var story=StoryTasks(templates);var selected=templates.Quests.Keys.Where(id=>story.Contains(id)==storyQuests).ToHashSet();
  int count=0;foreach(var saved in pmc.Quests??[])if(selected.Contains(saved.QId)&&saved.Status==QuestStatusEnum.AvailableAfter&&saved.AvailableAfter>now){saved.AvailableAfter=now-1;count++;}
  foreach(var pair in templates.Quests.Where(pair=>selected.Contains(pair.Key)))foreach(var condition in pair.Value.Conditions?.AvailableForStart??[]){
   if(condition.ConditionType!="Quest"||condition.AvailableAfter is not >0)continue;
   foreach(string target in Targets(condition)){var saved=pmc.Quests?.SingleOrDefault(q=>q.QId.ToString()==target);if(saved?.StatusTimers==null||condition.Status?.Contains(saved.Status)!=true)continue;if(saved.StatusTimers.TryGetValue(saved.Status,out double time)&&time+condition.AvailableAfter>now){saved.StatusTimers[saved.Status]=now-condition.AvailableAfter.Value-1;count++;}}
  }
  foreach(var pair in config.HiddenVariableGates??[]){if(!selected.Contains(pair.Key)||pair.Value.Delay is not >0)continue;
   foreach(var id in pair.Value.Quests??[]){var saved=pmc.Quests?.SingleOrDefault(q=>q.QId==id);if(saved?.Status==QuestStatusEnum.Success&&saved.StatusTimers?.TryGetValue(QuestStatusEnum.Success,out double time)==true&&time+pair.Value.Delay>now){saved.StatusTimers[QuestStatusEnum.Success]=now-pair.Value.Delay.Value-1;count++;}}
  }return count;
 }
}
