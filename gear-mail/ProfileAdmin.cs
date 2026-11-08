using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Enums.Hideout;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.InRaid;
using SPTarkov.Server.Core.Utils;
using Path=System.IO.Path;
namespace LocalGearMail;
public class AdminRequest:IRequestData {
 [JsonPropertyName("apiKey")] public string ApiKey{get;set;}="";
 [JsonPropertyName("action")] public string Action{get;set;}="status";
 [JsonPropertyName("traderId")] public string TraderId{get;set;}="";
 [JsonPropertyName("standing")] public double Standing{get;set;}
 [JsonPropertyName("areaType")] public int AreaType{get;set;}
 [JsonPropertyName("level")] public int Level{get;set;}
 [JsonPropertyName("source")] public string Source{get;set;}="pmc";
 [JsonPropertyName("mode")] public string Mode{get;set;}="once";
 [JsonPropertyName("equipment")] public Dictionary<string,string> Equipment{get;set;}=[];
 [JsonPropertyName("contents")] public Dictionary<string,List<ScavContentSpec>> Contents{get;set;}=[];
 [JsonPropertyName("sourceRevision")] public string SourceRevision{get;set;}="";
}
public record ScavPreset(string Mode,LoadoutSnapshot Loadout);
[Injectable]
public class ProfileAdminRouter:StaticRouter {
 public ProfileAdminRouter(JsonUtil json,ProfileAdminService service):base(json,[new RouteAction<AdminRequest>("/local/gear/profile",(_,r,s,_,ct)=>service.Handle(r,s,ct))]){}
}
[Injectable]
public class ProfileAdminService {
 const string PresetKey="localEspScavPreset";
 static ProfileAdminService? instance;
 readonly SaveServer saves;readonly JsonUtil json;readonly HttpResponseUtil http;readonly HideoutTable hideout;readonly HideoutController controller;readonly EventOutputHolder output;readonly TraderHelper traders;readonly TemplateTable templates;readonly ItemHelper items;readonly PresetHelper presets;readonly ISptLogger<ProfileAdminService> log;
 public ProfileAdminService(SaveServer saves,JsonUtil json,HttpResponseUtil http,HideoutTable hideout,HideoutController controller,EventOutputHolder output,TraderHelper traders,TemplateTable templates,ItemHelper items,PresetHelper presets,ISptLogger<ProfileAdminService> log){
  this.saves=saves;this.json=json;this.http=http;this.hideout=hideout;this.controller=controller;this.output=output;this.traders=traders;this.templates=templates;this.items=items;this.presets=presets;this.log=log;instance=this;
  var harmony=new Harmony("local.spt5.scav-loadout");
  harmony.Patch(typeof(PlayerScavGenerator).GetMethod("Generate"),postfix:new HarmonyMethod(typeof(ProfileAdminService),nameof(Generated)));
  harmony.Patch(typeof(LocationLifecycleService).GetMethod("StartLocalRaidAsync"),postfix:new HarmonyMethod(typeof(ProfileAdminService),nameof(RaidStarted)));
 }
 static void Generated(MongoId __0,ref PmcData __result){instance?.ApplyPreset(__0,__result);}
 static void RaidStarted(MongoId __0,StartLocalRaidRequestData __1,ref Task<StartLocalRaidResponseData> __result){if(instance!=null&&IsScav(__1.PlayerSide))__result=instance.ConsumeAfterStart(__0,__result);}
 static bool IsScav(string? side)=>string.Equals(side,"Savage",StringComparison.OrdinalIgnoreCase)||string.Equals(side,"Scav",StringComparison.OrdinalIgnoreCase);
 async Task<StartLocalRaidResponseData> ConsumeAfterStart(MongoId session,Task<StartLocalRaidResponseData> task){var response=await task;try{var profile=saves.GetProfile(session);if(Preset(profile)?.Mode=="once"){profile.ExtensionData!.Remove(PresetKey);await saves.SaveProfileAsync(session);}}catch(Exception error){log.Error("Could not clear one-time Scav preset: "+error);}return response;}
 ScavPreset? Preset(SptProfile profile)=>profile.ExtensionData?.TryGetValue(PresetKey,out var value)==true?json.Deserialize<ScavPreset>(json.Serialize(value)):null;
 void ApplyPreset(MongoId session,PmcData scav){
  try{var profile=saves.GetProfile(session);var preset=Preset(profile);if(preset==null||scav.Inventory==null)return;ScavLoadoutRules.Apply(scav.Inventory,preset.Loadout);profile.CharacterData!.ScavData=scav;saves.SaveProfileAsync(session).GetAwaiter().GetResult();}
  catch(Exception error){log.Error("Scav preset application failed: "+error);}
 }
 public async ValueTask<string> Handle(AdminRequest request,MongoId session,CancellationToken ct){
  var gate=GearMailService.SessionLock(session);await gate.WaitAsync(ct);SptProfile? snapshot=null;
  try{
   string key=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","api-key.txt")).Trim();
   if(!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(request.ApiKey??"")))throw new ArgumentException("API key invalid");
   var profile=saves.GetProfile(session)??throw new ArgumentException("当前角色不存在。");var pmc=profile.CharacterData?.PmcData??throw new ArgumentException("当前角色尚未创建。");
   if(request.Action=="status")return Status(profile,session);
   var activeLocation=profile.InraidData?.Location;if(!string.IsNullOrEmpty(activeLocation)&&!activeLocation.Equals("none",StringComparison.OrdinalIgnoreCase)&&!activeLocation.Equals("hideout",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("请先退出战局后再修改角色。");
   snapshot=json.Deserialize<SptProfile>(json.Serialize(profile))!;
   string directory=Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","profile-admin");Directory.CreateDirectory(directory);
   await File.WriteAllTextAsync(Path.Combine(directory,$"{session}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json"),json.Serialize(profile,true),ct);
   switch(request.Action){
    case "trader":
     if(!double.IsFinite(request.Standing)||request.Standing is <-20 or >100)throw new ArgumentException("好感请输入 -20～100。");
     var id=new MongoId(request.TraderId);if(pmc.TradersInfo==null||!pmc.TradersInfo.TryGetValue(id,out var info))throw new ArgumentException("未找到该商人的关系数据。");
     traders.AddStandingToTrader(session,id,request.Standing-(info.Standing??0));traders.ValidateTraderStandingsAndPlayerLevelForProfile(session);break;
    case "hideout":
     var type=(HideoutAreas)request.AreaType;var definition=hideout.Areas.SingleOrDefault(area=>area.Type==type&&area.IsEnabled)??throw new ArgumentException("未知藏身处区域。");
     var area=pmc.Hideout?.Areas?.SingleOrDefault(area=>area.Type==type)??throw new ArgumentException("该区域尚无角色数据。");
     int current=area.Level??0;if(request.Level<current||request.Level>20||definition.Stages==null||!definition.Stages.ContainsKey(request.Level.ToString()))throw new ArgumentException("请选择当前等级到该区域最大等级之间的等级。");
     while((area.Level??0)<request.Level){int before=area.Level??0;area.Constructing=true;area.CompleteTime=0;controller.UpgradeComplete(pmc,new HideoutUpgradeCompleteRequestData{AreaType=type,Timestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds()},session,output.GetOutput(session));if((area.Level??0)<=before)throw new ArgumentException("该区域无法完成升级。");}
     output.ResetOutput(session);break;
    case "scav-save":
     if(request.Mode is not ("once" or "always"))throw new ArgumentException("未知 Scav 预设模式。");
     var source=request.Source=="pmc"?pmc:request.Source=="scav"?profile.CharacterData!.ScavData:throw new ArgumentException("未知装备模板来源。");
     var inventory=source?.Inventory??throw new ArgumentException("该角色装备尚未生成。");
     var baseLoadout=request.Source=="scav"?Preset(profile)?.Loadout??ScavLoadoutRules.Capture(inventory):ScavLoadoutRules.Capture(inventory);
     if((request.Contents.Count>0||request.SourceRevision.Length>0)&&request.SourceRevision!=Revision(baseLoadout))throw new ArgumentException("来源装备已变化，请刷新后重新编辑。");
     var loadout=json.Deserialize<LoadoutSnapshot>(json.Serialize(baseLoadout))!;
     ReplaceEquipment(loadout,inventory,request.Equipment);
     ScavContentsRules.Apply(loadout,request.Contents,templates,items,presets,json);
     profile.ExtensionData??=[];profile.ExtensionData[PresetKey]=new ScavPreset(request.Mode,loadout);
     if(profile.CharacterData!.ScavData?.Inventory!=null)ScavLoadoutRules.Apply(profile.CharacterData.ScavData.Inventory,json.Deserialize<LoadoutSnapshot>(json.Serialize(loadout))!);break;
    case "scav-clear":profile.ExtensionData?.Remove(PresetKey);break;
    default:throw new ArgumentException("未知角色管理操作。");
   }
   await saves.SaveProfileAsync(session);return Status(profile,session);
  }catch(Exception error){if(snapshot!=null)saves.GetProfiles()[session]=snapshot;if(error is not ArgumentException)log.Error("Profile management failed: "+error);return http.GetBody(new{ok=false},(BackendErrorCodes)228,error is ArgumentException?error.Message:"操作失败，已恢复内存中的原角色数据。");}
  finally{if(request.Action=="hideout")output.ResetOutput(session);gate.Release();}
 }
 string Status(SptProfile profile,MongoId session){
  var pmc=profile.CharacterData!.PmcData!;var preset=Preset(profile);var loadout=preset?.Loadout;
  if(loadout==null&&profile.CharacterData.ScavData?.Inventory!=null)try{loadout=ScavLoadoutRules.Capture(profile.CharacterData.ScavData.Inventory);}catch(ArgumentException){}
  var inventory=pmc.Inventory??throw new ArgumentException("PMC 库存尚未就绪。");var equipment=inventory.Items!.Single(item=>item.Id==inventory.Equipment);
  using var equipmentDefinition=JsonDocument.Parse(json.Serialize(templates.Items[equipment.Template])!);
  var pmcLoadout=ScavLoadoutRules.Capture(pmc.Inventory!);
  return http.GetBody(new{ok=true,scavContentsEditing=true,pmcContents=ScavContentsRules.Describe(pmcLoadout),scavContents=loadout==null?null:ScavContentsRules.Describe(loadout),pmcRevision=Revision(pmcLoadout),scavRevision=loadout==null?"":Revision(loadout),
   areas=hideout.Areas.Where(area=>area.IsEnabled&&area.Stages?.Count>0&&pmc.Hideout?.Areas?.Any(saved=>saved.Type==area.Type)==true).Select(area=>new{type=(int)area.Type,name=area.Type.ToString(),level=pmc.Hideout!.Areas!.Single(saved=>saved.Type==area.Type).Level??0,maximum=area.Stages!.Keys.Select(key=>int.TryParse(key,out int value)?value:0).Max()}),
   traders=(pmc.TradersInfo??[]).Select(pair=>new{id=pair.Key.ToString(),name=traders.GetTrader(pair.Key,session)?.Nickname??pair.Key.ToString(),standing=pair.Value.Standing??0,loyalty=pair.Value.LoyaltyLevel??1}),
   scavMode=preset?.Mode??"off",scavItems=loadout?.Items.Where(item=>item.ParentId==loadout.EquipmentRoot).Select(item=>new{slot=item.SlotId,template=item.Template.ToString()}),slots=ScavLoadoutRules.Slots,
   pmcItems=ScavLoadoutRules.Capture(pmc.Inventory!).Items.Where(item=>item.ParentId==pmc.Inventory!.Equipment?.ToString()).Select(item=>new{slot=item.SlotId,template=item.Template.ToString()}),
   slotFilters=equipmentDefinition.RootElement.GetProperty("_props").GetProperty("Slots").EnumerateArray().Where(slot=>ScavLoadoutRules.Slots.Contains(slot.GetProperty("_name").GetString()!)).Select(slot=>new{slot=slot.GetProperty("_name").GetString(),filters=slot.GetProperty("_props").GetProperty("filters")}),
   note="已写入当前角色；请重新进入角色以刷新游戏原生界面。"});
 }
 string Revision(LoadoutSnapshot loadout)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.Serialize(loadout)!)));
 void ReplaceEquipment(LoadoutSnapshot loadout,BotBaseInventory source,Dictionary<string,string> replacements){
  var equipment=(source.Items??throw new ArgumentException("角色没有库存数据。")).Single(item=>item.Id==source.Equipment);
  using var tree=JsonDocument.Parse(json.Serialize(templates.Items[equipment.Template])!);
  foreach(var pair in replacements){
   if(!ScavLoadoutRules.Slots.Contains(pair.Key))throw new ArgumentException("无效装备槽位。");
   if(pair.Key=="Pockets"&&pair.Value.Length==0)throw new ArgumentException("Scav 必须保留口袋容器。");
   var remove=loadout.Items.Where(item=>item.ParentId==loadout.EquipmentRoot&&item.SlotId==pair.Key).Select(item=>item.Id.ToString()).ToHashSet();bool changed;do{changed=false;foreach(var item in loadout.Items)if(item.ParentId!=null&&remove.Contains(item.ParentId)&&remove.Add(item.Id.ToString()))changed=true;}while(changed);loadout.Items.RemoveAll(item=>remove.Contains(item.Id.ToString()));
   if(pair.Value.Length==0)continue;
   var templateId=new MongoId(pair.Value);if(!templates.Items.TryGetValue(templateId,out var template))throw new ArgumentException("未知物品模板。");
   var slot=tree.RootElement.GetProperty("_props").GetProperty("Slots").EnumerateArray().FirstOrDefault(slot=>slot.GetProperty("_name").GetString()==pair.Key);
   if(slot.ValueKind==JsonValueKind.Undefined||!SlotAllowed(templateId,slot))throw new ArgumentException("该物品不能装备到 "+pair.Key);
   var preset=presets.GetDefaultPreset(templateId);
   if(preset?.Items?.Count>0){
    var parts=json.Deserialize<List<Item>>(json.Serialize(preset.Items))!;var root=parts.FirstOrDefault(item=>item.Id==preset.Parent)??parts[0];var ids=parts.ToDictionary(item=>item.Id.ToString(),_=>new MongoId());
    loadout.Items.AddRange(parts.Select(item=>item with{Id=ids[item.Id.ToString()],ParentId=item.Id==root.Id?loadout.EquipmentRoot:item.ParentId!=null&&ids.TryGetValue(item.ParentId,out var parent)?parent.ToString():throw new ArgumentException("默认装备预设结构错误。"),SlotId=item.Id==root.Id?pair.Key:item.SlotId,Upd=item.Upd??items.GenerateUpdForItem(templates.Items[item.Template])}));
   }else loadout.Items.Add(new Item{Id=new MongoId(),Template=templateId,ParentId=loadout.EquipmentRoot,SlotId=pair.Key,Upd=items.GenerateUpdForItem(template)});
  }
 }
 bool SlotAllowed(MongoId template,JsonElement slot){
  var ancestry=new HashSet<string>();while(ancestry.Add(template.ToString())&&templates.Items.TryGetValue(template,out var entry)){template=entry.Parent;}
  return slot.GetProperty("_props").GetProperty("filters").EnumerateArray().Any(filter=>filter.TryGetProperty("Filter",out var allowed)&&allowed.EnumerateArray().Any(value=>ancestry.Contains(value.GetString()!))&&(!filter.TryGetProperty("ExcludedFilter",out var excluded)||!excluded.EnumerateArray().Any(value=>ancestry.Contains(value.GetString()!))));
 }
}
