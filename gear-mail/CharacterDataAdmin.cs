using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Ws;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;

public class CharacterDataRequest:IRequestData {
 [JsonPropertyName("categories")] public string[] Categories{get;set;}=[];
 [JsonPropertyName("preview")] public string Preview{get;set;}="";
 [JsonPropertyName("settings")] public Dictionary<string,System.Text.Json.JsonElement> Settings{get;set;}=[];
 [JsonPropertyName("revision")] public string Revision{get;set;}="";
 [JsonPropertyName("expectedValue")] public double? ExpectedValue{get;set;}
 [JsonPropertyName("stashItems")] public System.Text.Json.JsonElement? StashItems{get;set;}
 [JsonPropertyName("stashRevision")] public string StashRevision{get;set;}="";
 [JsonPropertyName("apiKey")] public string ApiKey{get;set;}="";
 [JsonPropertyName("action")] public string Action{get;set;}="status";
 [JsonPropertyName("id")] public string Id{get;set;}="";
 [JsonPropertyName("value")] public double Value{get;set;}
 [JsonPropertyName("unlocked")] public bool Unlocked{get;set;}
 [JsonPropertyName("expectedBalance")] public int ExpectedBalance{get;set;}
 [JsonPropertyName("expectedCoins")] public long ExpectedCoins{get;set;}
}
public class CharacterDataService(SaveServer saves,JsonUtil json,HttpResponseUtil http,TemplateTable templates,NotificationSendHelper notifications) {
 readonly bool seasonsReady=SeasonProfiles.Install(saves,json);
 readonly bool miscReady=MiscService.Install(saves,json,templates);
 readonly bool containerRewardsReady=ArmoredCaseRewards.Install(templates);
 public async ValueTask<string> Handle(CharacterDataRequest request,MongoId session,CancellationToken ct){
  var gate=GearMailService.SessionLock(session);await gate.WaitAsync(ct);SptProfile? snapshot=null;bool committed=false;
  try{
   var key=File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","api-key.txt")).Trim();
   if(!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(request.ApiKey??"")))throw new ArgumentException("认证失败。");
   var profile=saves.GetProfile(session)??throw new ArgumentException("当前角色不存在。");var pmc=profile.CharacterData?.PmcData??throw new ArgumentException("当前角色尚未创建。");
   if(request.Action=="emergencyBackup")return EmergencyBackupService.Handle(request,session,profile,json,http,templates);
   if(request.Action is "raidStash" or "saveRaidStash")return await RaidStashService.Handle(request,session,profile,saves,json,http,ct);
   if(request.Action is "miscStatus" or "miscSave")return await MiscService.Handle(request,session,http,ct);
   if(request.Action is "status" or "healthStatus")return Status(profile);
   if(request.Action=="serverStatus")return ServerSettingsService.Handle(request,http,pmc);
   var location=profile.InraidData?.Location;if(!string.IsNullOrEmpty(location)&&location!="none"&&location!="hideout")throw new ArgumentException("请先退出战局。");
   if(request.Action is "backupList" or "backupCreate" or "backupPreview" or "backupRestore")return await ProfileBackupService.Handle(request,session,profile,saves,json,http,templates,ct);
   if(request.Action=="serverSave")return ServerSettingsService.Handle(request,http,pmc);
   if(request.Action is "finishCrafts" or "craftStatus")return await HideoutCraftService.Handle(request,session,profile,saves,json,http,ct);
   if(request.Action is "cultistStatus" or "finishCultist")return await CultistCircleService.Handle(request,session,profile,saves,json,http,ct);
   if(request.Action=="repairRaidStash")return await RaidStashService.Handle(request,session,profile,saves,json,http,ct);
   snapshot=json.Deserialize<SptProfile>(json.Serialize(profile))!;
   var directory=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","character-data");Directory.CreateDirectory(directory);
   await File.WriteAllTextAsync(System.IO.Path.Combine(directory,$"{session}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json"),json.Serialize(profile,true),ct);
   switch(request.Action){
    case "itemFoundInRaid":
     if(request.Categories.Length!=1||request.Categories[0] is not ("pmc" or "scav"))throw new ArgumentException("请选择当前角色库存。");
     ItemFirRules.Apply((request.Categories[0]=="scav"?profile.CharacterData?.ScavData?.Inventory:pmc.Inventory)??throw new ArgumentException("角色库存尚未创建。"),request.Id,request.Unlocked,request.ExpectedValue);break;
    case "fleaRating":KarmaRules.ApplyFlea(pmc,request.Value,request.ExpectedValue);break;
    case "karma":var bounds=ServerSettingsService.ScavBounds();KarmaRules.Apply(pmc,request.Id,request.Value,request.ExpectedValue,bounds.Min,bounds.Max);break;
    case "deposit":CharacterDataRules.Deposit(pmc,request.ExpectedCoins,request.ExpectedBalance);break;
    case "wallet":if(!double.IsFinite(request.Value)||request.Value!=Math.Truncate(request.Value)||request.Value<0||request.Value>int.MaxValue)throw new ArgumentException("请输入有效的非负整数余额。");CharacterDataRules.Wallet(pmc,(int)request.Value,request.ExpectedBalance);break;
    case "bodyHealth":{
     var health=pmc.Health??throw new ArgumentException("当前角色没有生命值数据。");
     string[] allowed=["Head","Chest","Stomach","LeftArm","RightArm","LeftLeg","RightLeg"];
     if(request.Settings.Count is not (7 or 9)||allowed.Any(part=>!request.Settings.ContainsKey(part))||request.Settings.Keys.Any(key=>!allowed.Contains(key)&&key is not ("Energy" or "Hydration")))throw new ArgumentException("必须提供全部七个部位的生命值。");
     var raw=System.Text.Json.Nodes.JsonNode.Parse(json.Serialize(health)!)!;
     foreach(string part in allowed){var value=request.Settings[part];double maximum=value.GetProperty("maximum").GetDouble(),current=value.GetProperty("current").GetDouble();
      if(!double.IsFinite(maximum)||!double.IsFinite(current)||maximum<.01||maximum>100000000||current<0||current>maximum)throw new ArgumentException("血量须满足：0 ≤ 当前值 ≤ 最大值，且最大值大于零。");
      var state=raw["BodyParts"]![part]!["Health"]!;state["Maximum"]=maximum;state["Current"]=current;
     }
     foreach(string vital in new[]{"Energy","Hydration"})if(request.Settings.TryGetValue(vital,out var value)){
      double maximum=value.GetProperty("maximum").GetDouble(),current=value.GetProperty("current").GetDouble();
      if(!double.IsFinite(maximum)||!double.IsFinite(current)||maximum<.01||maximum>100000000||current<0||current>maximum)throw new ArgumentException("能量和水分须满足 0 ≤ 当前值 ≤ 最大值，最大值大于零。");
      if(raw[vital] is not System.Text.Json.Nodes.JsonObject state)throw new ArgumentException("存档缺少能量或水分数据。");state["Maximum"]=maximum;state["Current"]=current;
     }
     T Decode<T>(string text,T sample)=>json.Deserialize<T>(text)!;
     pmc.Health=Decode(raw.ToJsonString(),health);break;
    }
    case "skill":CharacterDataRules.Skill(pmc,request.Id,request.Value);break;
    case "achievement":CharacterDataRules.Achievement(pmc,request.Id,request.Unlocked,(templates.Achievements??[]).Concat(templates.CustomAchievements??[]),DateTimeOffset.UtcNow.ToUnixTimeSeconds());break;
    default:throw new ArgumentException("未知数据操作。");
   }
   await saves.SaveProfileAsync(session);committed=true;
   if(request.Action is "deposit" or "wallet")try{await notifications.SendMessageAsync(session,new WsExpansionsBalance{EventType=NotificationEventType.UpdateAccountTarcoinBalance,Balance=pmc.TarCoinBalance??0});}catch{/* The durable balance is returned even if the client disconnected. */}
   return Status(profile);
  }catch(Exception error){if(snapshot!=null&&!committed)saves.GetProfiles()[session]=snapshot;return http.GetBody(new{ok=false},(BackendErrorCodes)228,error is ArgumentException||request.Action.StartsWith("backup",StringComparison.Ordinal)&&error is IOException?error.Message:"保存失败，请刷新角色数据后重试。");}
  finally{gate.Release();}
 }
 string Status(SptProfile profile){var pmc=profile.CharacterData!.PmcData!;return http.GetBody(new{ok=true,balance=pmc.TarCoinBalance??0,coins=CharacterDataRules.PhysicalCoins(pmc),health=pmc.Health,skills=pmc.Skills?.Common?.Select(skill=>new{id=skill.Id.ToString(),level=skill.Progress/100}),achievements=(templates.Achievements??[]).Concat(templates.CustomAchievements??[]).DistinctBy(entry=>entry.Id).Select(entry=>new{id=entry.Id.ToString(),unlocked=pmc.Achievements?.ContainsKey(entry.Id)==true})});}
}
