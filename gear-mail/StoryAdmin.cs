using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public class StoryRequest:IRequestData {
 [JsonPropertyName("apiKey")] public string ApiKey{get;set;}="";
 [JsonPropertyName("action")] public string Action{get;set;}="status";
 [JsonPropertyName("id")] public string Id{get;set;}="";
 [JsonPropertyName("state")] public int State{get;set;}=2;
 [JsonPropertyName("revision")] public string Revision{get;set;}="";
}
public class StoryService(SaveServer saves,JsonUtil json,HttpResponseUtil http,TemplateTable templates,MainStoryChapterHelper chapters,QuestVariableHelper variables,QuestConfig config) {
 void RewindDialogue(SptProfile profile,ISet<MongoId> affected){
  var pmc=profile.CharacterData!.PmcData!;pmc.Variables??=[];
  using var tree=JsonDocument.Parse(json.Serialize(templates.Dialogue)!);
  foreach(var dialog in tree.RootElement.GetProperty("elements").EnumerateArray()){
   var taskIds=affected.Select(id=>id.ToString()).ToHashSet();
   bool HasQuest(JsonElement element){if(element.ValueKind==JsonValueKind.Object)return element.EnumerateObject().Any(p=>(p.Name.Equals("QuestId",StringComparison.OrdinalIgnoreCase)&&p.Value.ValueKind==JsonValueKind.String&&taskIds.Contains(p.Value.GetString()!))||HasQuest(p.Value));return element.ValueKind==JsonValueKind.Array&&element.EnumerateArray().Any(HasQuest);}
   var lines=dialog.GetProperty("Lines").EnumerateArray().Where(HasQuest).ToArray();if(lines.Length==0)continue;
   var nodeIds=lines.Select(line=>line.GetProperty("Id").GetString()!).ToHashSet();var dialogId=dialog.GetProperty("Id").GetString()!;
   profile.DialogueProgress?.RemoveAll(path=>path.DialogueId?.ToString()==dialogId&&path.NodeId.HasValue&&nodeIds.Contains(path.NodeId.Value.ToString()));
   foreach(var line in lines)foreach(var action in line.GetProperty("Actions").EnumerateArray())if(action.TryGetProperty("type",out var type)&&type.GetString()=="SetVariable"&&action.TryGetProperty("variableId",out var variable)&&action.TryGetProperty("saveScope",out var scope)){
    var key=new MongoId(variable.GetString()!);if(scope.GetString()=="Profile")pmc.Variables.Remove(key);
    else if(scope.GetString()=="Dialogue"&&action.TryGetProperty("value",out var value)&&value.TryGetInt32(out int expected)&&pmc.Variables.GetValueOrDefault(key)==expected)pmc.Variables.Remove(key);
   }
  }
 }
 string Revision(SptProfile profile)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.Serialize(new{profile.CharacterData!.PmcData!.Quests,profile.CharacterData.PmcData.Variables,profile.CharacterData.PmcData.Ending,profile.DialogueProgress})!)));
 public async ValueTask<string> Handle(StoryRequest request,MongoId session,CancellationToken ct){
   var gate=GearMailService.SessionLock(session);await gate.WaitAsync(ct);SptProfile? snapshot=null;bool committed=false;
  try{
   var key=File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","api-key.txt")).Trim();
   if(!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(request.ApiKey??"")))throw new ArgumentException("认证失败。");
   var profile=saves.GetProfile(session)??throw new ArgumentException("当前角色不存在。");var pmc=profile.CharacterData?.PmcData??throw new ArgumentException("当前角色尚未创建。");
   if(request.Action=="status")return Status(profile);
   var location=profile.InraidData?.Location;if(!string.IsNullOrEmpty(location)&&location!="none"&&location!="hideout")throw new ArgumentException("请先退出战局。");
   if(Revision(profile)!=request.Revision)throw new ArgumentException("剧情进度已变化，请刷新后重试。");
   var id=string.IsNullOrEmpty(request.Id)?default:new MongoId(request.Id);bool ending=request.Action=="ending";
   if(request.Action is "replay" or "state"&&!templates.Quests.ContainsKey(id))throw new ArgumentException("未知任务。");
   if(request.Action=="preview"){
    if(!templates.Quests.ContainsKey(id))throw new ArgumentException("未知任务。");
    var affected=StoryRules.Dependents(StoryRules.Alternatives(id,templates).Append(id),templates);
    return http.GetBody(new{ok=true,preview=true,revision=Revision(profile),affected=affected.Select(key=>new{id=key.ToString(),name=QuestName(key)})});
   }
   if(request.Action is not ("replay" or "state" or "ending" or "waits" or "wait"))throw new ArgumentException("未知剧情操作。");
   snapshot=json.Deserialize<SptProfile>(json.Serialize(profile))!;var directory=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","story");Directory.CreateDirectory(directory);
   await File.WriteAllTextAsync(System.IO.Path.Combine(directory,$"{session}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json"),json.Serialize(profile,true),ct);long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
   if(request.Action=="wait")StoryRules.SkipWait(pmc,templates,id,now);
   else if(request.Action=="waits")StoryRules.SkipWaits(pmc,templates,config,now);
   else if(ending){
    var selected=templates.Endings.Elements.SingleOrDefault(e=>e.Id==id)??throw new ArgumentException("未知结局。");
    var affected=new HashSet<MongoId>();foreach(var e in templates.Endings.Elements)foreach(var condition in e.Conditions??[])if(condition.ConditionType=="Quest")foreach(string target in StoryRules.Targets(condition))if(templates.Quests.ContainsKey(new MongoId(target))){affected.Add(new MongoId(target));StoryRules.Set(pmc,new MongoId(target),QuestStatusEnum.Fail,now);}
    foreach(var condition in selected.Conditions??[])if(condition.ConditionType=="Quest")foreach(string target in StoryRules.Targets(condition))if(templates.Quests.ContainsKey(new MongoId(target)))StoryRules.Set(pmc,new MongoId(target),QuestStatusEnum.AvailableForFinish,now);
    pmc.Ending??=new();pmc.Ending.Current=id.ToString();pmc.Ending.Display=id.ToString();pmc.Ending.Achieved??=[];if(!pmc.Ending.Achieved.Contains(id.ToString()))pmc.Ending.Achieved.Add(id.ToString());pmc.Ending.Available??=[];pmc.Ending.Available["pve"]=[id.ToString()];pmc.Ending.Available["regular"]=[id.ToString()];
    StoryRules.SyncVariables(pmc,templates,variables,affected);
   }else{
    if(request.State is not (0 or 2 or 4 or 5))throw new ArgumentException("未知任务状态。");
    var alternatives=StoryRules.Alternatives(id,templates);var affected=StoryRules.Dependents(alternatives.Append(id),templates);
    foreach(var target in affected)if(pmc.Quests?.Any(q=>q.QId==target)==true)StoryRules.Set(pmc,target,QuestStatusEnum.Locked,now);
    foreach(var target in alternatives)StoryRules.Set(pmc,target,QuestStatusEnum.Fail,now);
    StoryRules.Set(pmc,id,request.Action=="replay"?QuestStatusEnum.Started:(QuestStatusEnum)request.State,now);
    RewindDialogue(profile,affected);StoryRules.SyncVariables(pmc,templates,variables,affected);
   }
   await saves.SaveProfileAsync(session);committed=true;return Status(profile);
  }catch(Exception error){if(snapshot!=null&&!committed)saves.GetProfiles()[session]=snapshot;return http.GetBody(new{ok=false},(BackendErrorCodes)228,error is ArgumentException?error.Message:committed?"已保存，请刷新剧情进度。":"保存失败，已恢复角色数据。");}
  finally{gate.Release();}
 }
 string QuestName(MongoId id){if(!templates.Quests.TryGetValue(id,out var quest))return id.ToString();if(quest.Localization?.TryGetValue("ch",out var ch)==true&&ch.TryGetValue(id+" name",out var name)&&!string.IsNullOrWhiteSpace(name))return name;return quest.Name??id.ToString();}
 string Status(SptProfile profile){var pmc=profile.CharacterData!.PmcData!;var story=StoryRules.StoryTasks(templates);return http.GetBody(new{ok=true,revision=Revision(profile),chapters=templates.MainQuests.Chapters.Select(chapter=>new{id=chapter.ChapterId.ToString(),name=QuestName(chapter.ChapterId),state=(int)(pmc.Quests?.SingleOrDefault(q=>q.QId==chapter.ChapterId)?.Status??QuestStatusEnum.Locked)}),quests=templates.Quests.Select(pair=>new{id=pair.Key.ToString(),name=QuestName(pair.Key),story=story.Contains(pair.Key),chapter=chapters.ChapterOf(pair.Key)?.ToString(),state=(int)(pmc.Quests?.SingleOrDefault(q=>q.QId==pair.Key)?.Status??QuestStatusEnum.Locked),wait=Math.Max(0,(pmc.Quests?.SingleOrDefault(q=>q.QId==pair.Key)?.AvailableAfter??0)-DateTimeOffset.UtcNow.ToUnixTimeSeconds()),alternatives=StoryRules.Alternatives(pair.Key,templates).Select(id=>id.ToString())}),endings=templates.Endings.Elements.Select(e=>new{id=e.Id.ToString(),name=e.SystemName,current=pmc.Ending?.Current==e.Id.ToString(),achieved=pmc.Ending?.Achieved?.Contains(e.Id.ToString())==true})});}
}
