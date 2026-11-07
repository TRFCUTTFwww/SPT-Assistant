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
 readonly bool containerRewardsReady=ArmoredCaseRewards.Install(templates);
 public async ValueTask<string> Handle(CharacterDataRequest request,MongoId session,CancellationToken ct){
  var gate=GearMailService.SessionLock(session);await gate.WaitAsync(ct);SptProfile? snapshot=null;bool committed=false;
  try{
   var key=File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","api-key.txt")).Trim();
   if(!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(request.ApiKey??"")))throw new ArgumentException("认证失败。");
   var profile=saves.GetProfile(session)??throw new ArgumentException("当前角色不存在。");var pmc=profile.CharacterData?.PmcData??throw new ArgumentException("当前角色尚未创建。");
   if(request.Action is "raidStash" or "saveRaidStash")return await RaidStashService.Handle(request,session,profile,saves,json,http,ct);
   if(request.Action=="status")return Status(profile);
   if(request.Action=="serverStatus")return ServerSettingsService.Handle(request,http,pmc);
   var location=profile.InraidData?.Location;if(!string.IsNullOrEmpty(location)&&location!="none"&&location!="hideout")throw new ArgumentException("请先退出战局。");
   if(request.Action=="serverSave")return ServerSettingsService.Handle(request,http,pmc);
   if(request.Action is "finishCrafts" or "craftStatus")return await HideoutCraftService.Handle(request,session,profile,saves,json,http,ct);
   if(request.Action=="repairRaidStash")return await RaidStashService.Handle(request,session,profile,saves,json,http,ct);
   snapshot=json.Deserialize<SptProfile>(json.Serialize(profile))!;
   var directory=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","character-data");Directory.CreateDirectory(directory);
   await File.WriteAllTextAsync(System.IO.Path.Combine(directory,$"{session}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json"),json.Serialize(profile,true),ct);
   switch(request.Action){
    case "karma":var bounds=ServerSettingsService.ScavBounds();KarmaRules.Apply(pmc,request.Id,request.Value,request.ExpectedValue,bounds.Min,bounds.Max);break;
    case "deposit":CharacterDataRules.Deposit(pmc,request.ExpectedCoins,request.ExpectedBalance);break;
    case "wallet":if(!double.IsFinite(request.Value)||request.Value!=Math.Truncate(request.Value)||request.Value<0||request.Value>int.MaxValue)throw new ArgumentException("请输入有效的非负整数余额。");CharacterDataRules.Wallet(pmc,(int)request.Value,request.ExpectedBalance);break;
    case "skill":CharacterDataRules.Skill(pmc,request.Id,request.Value);break;
    case "achievement":CharacterDataRules.Achievement(pmc,request.Id,request.Unlocked,(templates.Achievements??[]).Concat(templates.CustomAchievements??[]),DateTimeOffset.UtcNow.ToUnixTimeSeconds());break;
    default:throw new ArgumentException("未知数据操作。");
   }
   await saves.SaveProfileAsync(session);committed=true;
   if(request.Action is "deposit" or "wallet")try{await notifications.SendMessageAsync(session,new WsExpansionsBalance{EventType=NotificationEventType.UpdateAccountTarcoinBalance,Balance=pmc.TarCoinBalance??0});}catch{/* The durable balance is returned even if the client disconnected. */}
   return Status(profile);
  }catch(Exception error){if(snapshot!=null&&!committed)saves.GetProfiles()[session]=snapshot;return http.GetBody(new{ok=false},(BackendErrorCodes)228,error is ArgumentException?error.Message:"保存失败，请刷新角色数据后重试。");}
  finally{gate.Release();}
 }
 string Status(SptProfile profile){var pmc=profile.CharacterData!.PmcData!;return http.GetBody(new{ok=true,balance=pmc.TarCoinBalance??0,coins=CharacterDataRules.PhysicalCoins(pmc),skills=pmc.Skills?.Common?.Select(skill=>new{id=skill.Id.ToString(),level=skill.Progress/100}),achievements=(templates.Achievements??[]).Concat(templates.CustomAchievements??[]).DistinctBy(entry=>entry.Id).Select(entry=>new{id=entry.Id.ToString(),unlocked=pmc.Achievements?.ContainsKey(entry.Id)==true})});}
}
