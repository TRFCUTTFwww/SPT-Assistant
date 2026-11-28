using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LocalSpt.Shared;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Ws;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;

public class CharacterDataRequest:IRequestData {
 [JsonPropertyName("categories")] public string[] Categories{get;set;}=[];
 [JsonPropertyName("preview")] public string Preview{get;set;}="";
 [JsonPropertyName("settings")] public Dictionary<string,System.Text.Json.JsonElement> Settings{get;set;}=[];
 [JsonPropertyName("revision")] public string Revision{get;set;}="";
 [JsonPropertyName("jsonToken")] public string JsonToken{get;set;}="";
 [JsonPropertyName("jsonPath")] public string JsonPath{get;set;}="$";
 [JsonPropertyName("jsonOffset")] public int JsonOffset{get;set;}
 [JsonPropertyName("jsonQuery")] public string JsonQuery{get;set;}="";
 [JsonPropertyName("jsonAddField")] public bool JsonAddField{get;set;}
 [JsonPropertyName("jsonAppend")] public bool JsonAppend{get;set;}
 [JsonPropertyName("expectedValue")] public double? ExpectedValue{get;set;}
 [JsonPropertyName("stashItems")] public System.Text.Json.JsonElement? StashItems{get;set;}
 [JsonPropertyName("stashRevision")] public string StashRevision{get;set;}="";
 [JsonPropertyName("apiKey")] public string ApiKey{get;set;}="";
 [JsonPropertyName("action")] public string Action{get;set;}="status";
 [JsonPropertyName("id")] public string Id{get;set;}="";
 [JsonPropertyName("value")] public double Value{get;set;}
 [JsonPropertyName("unlocked")] public bool Unlocked{get;set;}
 [JsonPropertyName("listingAllowed")] public bool? ListingAllowed{get;set;}
 [JsonPropertyName("fleaPresetName")] public string? FleaPresetName{get;set;}
 [JsonPropertyName("fleaTargetName")] public string? FleaTargetName{get;set;}
 [JsonPropertyName("fleaJson")] public string? FleaJson{get;set;}
 [JsonPropertyName("fleaRules")] public System.Text.Json.JsonElement? FleaRules{get;set;}
 [JsonPropertyName("fleaOffer")] public System.Text.Json.JsonElement? FleaOffer{get;set;}
 [JsonPropertyName("batchOp")] public string BatchOp{get;set;}="";
 [JsonPropertyName("template")] public string Template{get;set;}="";
 [JsonPropertyName("attributes")] public GearAttributes? Attributes{get;set;}
 [JsonPropertyName("dogtag")] public DogtagEdit? Dogtag{get;set;}
 [JsonPropertyName("expectedBalance")] public int ExpectedBalance{get;set;}
 [JsonPropertyName("expectedCoins")] public long ExpectedCoins{get;set;}
 [JsonPropertyName("currency")] public string Currency{get;set;}="";
 [JsonPropertyName("count")] public int Count{get;set;}
 [JsonPropertyName("sellInOnePiece")] public bool SellInOnePiece{get;set;}
 [JsonPropertyName("deleteNode")] public bool DeleteNode{get;set;}
 [JsonPropertyName("childKey")] public string ChildKey{get;set;}="";
}
public class CharacterDataService {
 readonly SaveServer saves;
 readonly JsonUtil json;
 readonly HttpResponseUtil http;
 readonly TemplateTable templates;
 readonly NotificationSendHelper notifications;
 readonly MailSendService? mail;

 public CharacterDataService(SaveServer saves,JsonUtil json,HttpResponseUtil http,TemplateTable templates,NotificationSendHelper notifications)
  :this(saves,json,http,templates,notifications,null){}

 public CharacterDataService(SaveServer saves,JsonUtil json,HttpResponseUtil http,TemplateTable templates,NotificationSendHelper notifications,MailSendService? mail){
  this.saves=saves;
  this.json=json;
  this.http=http;
  this.templates=templates;
  this.notifications=notifications;
  this.mail=mail;
  MiscService.Install(saves,json,templates);
  ArmoredCaseRewards.Install(templates);
  RaidHistoryService.Install(saves,json);
  BtrInstantDelivery.Install(saves,mail);
  // Older hot-loaded modules registered these patches under a separate Harmony ID.
  new HarmonyLib.Harmony("local.spt5.independent-season").UnpatchSelf();
 }
 public async ValueTask<string> Handle(CharacterDataRequest request,MongoId session,CancellationToken ct){
  var gate=GearMailService.SessionLock(session);await gate.WaitAsync(ct);SptProfile? snapshot=null;bool committed=false,jsonWriteStarted=false;
  try{
   var key=File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","api-key.txt")).Trim();
   if(!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(request.ApiKey??"")))throw new ArgumentException("认证失败。");
   var profile=saves.GetProfile(session)??throw new ArgumentException("当前角色不存在。");var pmc=profile.CharacterData?.PmcData??throw new ArgumentException("当前角色尚未创建。");
   if(request.Action=="btrInstantDeliver")return await BtrInstantDelivery.Handle(request,session,profile,saves,json,http,templates,mail??throw new ArgumentException("邮件服务尚未就绪。"));
   if(request.Action=="emergencyBackup")return EmergencyBackupService.Handle(request,session,profile,json,http,templates);
   if(request.Action is "raidHistoryList" or "raidHistoryDetail" or "raidHistoryExport")return RaidHistoryService.Handle(request,session,http);
   if(request.Action is "raidStash" or "saveRaidStash")return await RaidStashService.Handle(request,session,profile,saves,json,http,ct);
   if(request.Action is "onlineOfferQuote" or "onlineAddOffer")return await OnlineTradingService.Handle(request,session,profile,saves,json,http,templates,ct);
   if(request.Action=="fleaRaidBuy"){
    try{return await FleaPrices.HandleRaidBuy(request,profile,()=>saves.SaveProfileAsync(session),http,templates);}
    catch(ArgumentException error){return http.GetBody(new{ok=false,rejected=true},(BackendErrorCodes)228,error.Message);}
   }
   if(request.Action is "miscStatus" or "miscSave")return await MiscService.Handle(request,session,http,ct);
   if(request.Action.StartsWith("flea",StringComparison.Ordinal)&&request.Action!="fleaRating")return FleaPrices.Handle(request,templates,http,pmc);
   if(request.Action is "status" or "healthStatus")return Status(profile);
   if(request.Action=="serverStatus")return ServerSettingsService.Handle(request,http,pmc);
   var location=profile.InraidData?.Location;if(!string.IsNullOrEmpty(location)&&location!="none"&&location!="hideout")throw new ArgumentException("请先退出战局。");
   if(request.Action.StartsWith("serverJson",StringComparison.Ordinal))return ServerJsonEditorService.Handle(request,session.ToString(),http,ct);
   if(request.Action=="raidHistoryMail"){
    if(mail==null)throw new ArgumentException("请更新并重启服务端。");
    return await RaidHistoryMail.Send(request,session,profile,saves,json,http,templates,mail);
   }
   if(request.Action is "backupList" or "backupCreate" or "backupPreview" or "backupRestore" or "backupDelete" or "backupExport" or "backupImport" or "backupSettings")return await ProfileBackupService.Handle(request,session,profile,saves,json,http,templates,ct);
   if(request.Action is "profileJsonRead" or "profileJsonSave")return await ProfileJsonEditorService.Handle(request,session,profile,saves,json,http,templates,notifications,ct);
   if(request.Action=="serverSave")return ServerSettingsService.Handle(request,http,pmc);
   if(request.Action is "finishCrafts" or "craftStatus" or "finishUpgrades")return await HideoutCraftService.Handle(request,session,profile,saves,json,http,ct);
   if(request.Action is "cultistStatus" or "finishCultist")return await CultistCircleService.Handle(request,session,profile,saves,json,http,ct);
   if(request.Action=="repairRaidStash")return await RaidStashService.Handle(request,session,profile,saves,json,http,ct);
   if(request.Action=="inventoryItemJsonRead"){
    var item=InventoryItemJson.Find(Inventory(),request.Id,request.Template);
    return http.GetBody(new{ok=true,item,revision=InventoryItemJson.Revision(item,json)});
   }
   if(request.Action=="inventoryItemTreeJsonRead"){
    var items=InventoryItemJson.Tree(Inventory(),request.Id,request.Template);
    return http.GetBody(new{ok=true,items,revision=InventoryItemJson.TreeRevision(items,json)});
   }
   snapshot=json.Deserialize<SptProfile>(json.Serialize(profile))!;
   var directory=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","character-data");Directory.CreateDirectory(directory);
   await File.WriteAllTextAsync(System.IO.Path.Combine(directory,$"{session}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.json"),json.Serialize(profile,true),ct);
   Item? editedItem=null;
   List<Item>? editedTree=null;
   List<Item>? gridAddedItems=null;
   switch(request.Action){
    case "inventoryGridAddItems":
     gridAddedItems=InventoryItemJson.AddItemsToGrid(Inventory(),request.Preview,json);break;
    case "inventoryItemTreeJsonSave":
     editedTree=InventoryItemJson.ApplyTree(Inventory(),request.Id,request.Template,request.Preview,request.Revision,json);break;
    case "inventoryItemJsonSave":
     editedItem=InventoryItemJson.Apply(Inventory(),request.Id,request.Template,request.Preview,request.Revision,json);break;
    case "itemFoundInRaid":
     ItemFirRules.Apply(Inventory(),request.Id,request.Unlocked,request.ExpectedValue);break;
    case "editInventoryItem":
     editedItem=InventoryItemEdit.Apply(Inventory(),request.Id,request.Template,request.Attributes,templates,json);break;
    case "editInventoryDogtag":
     if(request.Dogtag==null)throw new ArgumentException("请至少选择一项狗牌参数。");
     editedItem=InventoryItemEdit.Apply(Inventory(),request.Id,request.Template,request.Attributes,templates,json,request.Dogtag);break;
    case "fleaRating":KarmaRules.ApplyFlea(pmc,request.Value,request.ExpectedValue);break;
    case "karma":var bounds=ServerSettingsService.ScavBounds();KarmaRules.Apply(pmc,request.Id,request.Value,request.ExpectedValue,bounds.Min,bounds.Max);break;
    case "deposit":CharacterDataRules.Deposit(pmc,request.ExpectedCoins,request.ExpectedBalance);break;
    case "wallet":if(!double.IsFinite(request.Value)||request.Value!=Math.Truncate(request.Value)||request.Value<int.MinValue||request.Value>int.MaxValue)throw new ArgumentException("请输入 Int32 范围内的整数余额。");CharacterDataRules.Wallet(pmc,(int)request.Value,request.ExpectedBalance);break;
    case "bodyHealth":{
     var health=pmc.Health??throw new ArgumentException("当前角色没有生命值数据。");
     string[] allowed=["Head","Chest","Stomach","LeftArm","RightArm","LeftLeg","RightLeg"];
     if(request.Settings.Count is not (7 or 9)||allowed.Any(part=>!request.Settings.ContainsKey(part))||request.Settings.Keys.Any(key=>!allowed.Contains(key)&&key is not ("Energy" or "Hydration")))throw new ArgumentException("必须提供全部七个部位的生命值。");
     var raw=JsonNode.Parse(json.Serialize(health)!)!;
     foreach(string part in allowed){var value=request.Settings[part];double maximum=value.GetProperty("maximum").GetDouble(),current=value.GetProperty("current").GetDouble();
      if(!double.IsFinite(maximum)||!double.IsFinite(current)||maximum<=0)throw new ArgumentException("部位最大血量须大于零，且数值须为有限数字。");
      var state=raw["BodyParts"]![part]!["Health"]!;state["Maximum"]=maximum;state["Current"]=current;
     }
     foreach(string vital in new[]{"Energy","Hydration"})if(request.Settings.TryGetValue(vital,out var value)){
      double maximum=value.GetProperty("maximum").GetDouble(),current=value.GetProperty("current").GetDouble();
      if(!double.IsFinite(maximum)||!double.IsFinite(current)||maximum<=0)throw new ArgumentException("能量和水分最大值须大于零，且数值须为有限数字。");
      if(raw[vital] is not JsonObject state)throw new ArgumentException("存档缺少能量或水分数据。");state["Maximum"]=maximum;state["Current"]=current;
     }
     T Decode<T>(string text,T sample)=>json.Deserialize<T>(text)!;
     pmc.Health=Decode(raw.ToJsonString(),health);break;
    }
    case "skill":CharacterDataRules.Skill(pmc,request.Id,request.Value);break;
    case "achievement":CharacterDataRules.Achievement(pmc,request.Id,request.Unlocked,(templates.Achievements??[]).Concat(templates.CustomAchievements??[]),DateTimeOffset.UtcNow.ToUnixTimeSeconds());break;
    default:throw new ArgumentException("未知数据操作。");
   }
   jsonWriteStarted=request.Action is "inventoryItemJsonSave" or "inventoryItemTreeJsonSave" or "inventoryGridAddItems";
   await saves.SaveProfileAsync(session);
   if(gridAddedItems!=null){
    committed=true;
    return http.GetBody(new{ok=true,items=gridAddedItems});
   }
   if(editedTree!=null){
    var path=System.IO.Path.Combine(AppContext.BaseDirectory,"user","profiles",session+".json");
    var persisted=json.Deserialize<SptProfile>(await File.ReadAllTextAsync(path,CancellationToken.None));
    var character=request.Categories[0]=="scav"?persisted?.CharacterData?.ScavData:persisted?.CharacterData?.PmcData;
    string savedRootId=editedTree[0].Id.ToString();
    var saved=InventoryItemJson.Tree(character?.Inventory??throw new IOException("物品参数写入核验失败。"),savedRootId);
    if(InventoryItemJson.TreeRevision(saved,json)!=InventoryItemJson.TreeRevision(editedTree,json))throw new IOException("物品参数写入核验失败。");
    committed=true;
    return http.GetBody(new{ok=true,items=saved,revision=InventoryItemJson.TreeRevision(saved,json)});
   }
   if(editedItem!=null){
    var path=System.IO.Path.Combine(AppContext.BaseDirectory,"user","profiles",session+".json");
    var persisted=json.Deserialize<SptProfile>(await File.ReadAllTextAsync(path,CancellationToken.None));
    var character=request.Categories[0]=="scav"?persisted?.CharacterData?.ScavData:persisted?.CharacterData?.PmcData;
    var saved=character?.Inventory?.Items?.SingleOrDefault(item=>item.Id==editedItem.Id&&item.Template==editedItem.Template);
    if(saved==null||!JsonNode.DeepEquals(JsonNode.Parse(json.Serialize(saved)!),JsonNode.Parse(json.Serialize(editedItem)!)))
     throw new IOException("物品参数写入核验失败。");
    committed=true;
    return http.GetBody(new{ok=true,item=saved,revision=InventoryItemJson.Revision(saved,json)});
   }
   committed=true;
   if(request.Action is "deposit" or "wallet")try{await notifications.SendMessageAsync(session,new WsExpansionsBalance{EventType=NotificationEventType.UpdateAccountTarcoinBalance,Balance=pmc.TarCoinBalance??0});}catch{/* The durable balance is returned even if the client disconnected. */}
   return Status(profile);
   BotBaseInventory Inventory(){
    if(request.Categories.Length!=1||request.Categories[0] is not ("pmc" or "scav"))throw new ArgumentException("请选择当前角色库存。");
    return (request.Categories[0]=="scav"?profile.CharacterData?.ScavData?.Inventory:pmc.Inventory)??throw new ArgumentException("角色库存尚未创建。");
   }
  }catch(Exception error){
   if(snapshot!=null&&!committed){
    LiveProfileState.Replace(saves.GetProfile(session),snapshot);
    if(jsonWriteStarted)try{await saves.SaveProfileAsync(session,CancellationToken.None);}catch{error=new IOException("物品 JSON 保存失败，请使用角色数据备份恢复。");}
   }
   return http.GetBody(new{ok=false},(BackendErrorCodes)228,request.Action.StartsWith("serverJson",StringComparison.Ordinal)&&error is System.Text.Json.JsonException?"JSON 语法无效。":error is ArgumentException||(request.Action.StartsWith("backup",StringComparison.Ordinal)||request.Action.StartsWith("profileJson",StringComparison.Ordinal)||request.Action.StartsWith("serverJson",StringComparison.Ordinal)||request.Action is "inventoryItemJsonSave" or "inventoryItemTreeJsonSave" or "inventoryGridAddItems")&&error is IOException?error.Message:"保存失败，请刷新角色数据后重试。");
  }
  finally{gate.Release();}
 }
 string Status(SptProfile profile){var pmc=profile.CharacterData!.PmcData!;return http.GetBody(new{ok=true,balance=pmc.TarCoinBalance??0,coins=CharacterDataRules.PhysicalCoins(pmc),health=pmc.Health,skills=pmc.Skills?.Common?.Select(skill=>new{id=skill.Id.ToString(),level=skill.Progress/100}),achievements=(templates.Achievements??[]).Concat(templates.CustomAchievements??[]).DistinctBy(entry=>entry.Id).Select(entry=>new{id=entry.Id.ToString(),unlocked=pmc.Achievements?.ContainsKey(entry.Id)==true})});}
}
