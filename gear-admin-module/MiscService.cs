using Path=System.IO.Path;
using System.Security.Cryptography;
using System.Text.Json;
using HarmonyLib;
using LocalMiscShared;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.InRaid;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;

public static class MiscService {
 const string SecureKey="localMiscScavSecure";
 static readonly string Home=Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail"),FilePath=Path.Combine(Home,"misc-settings.json");
 static readonly SemaphoreSlim Gate=new(1,1);
 public static MiscSettings Options {get;private set;}=new();
 static SaveServer saves=null!;static JsonUtil json=null!;
 static string[] secureTemplates=[];
 public static bool Install(SaveServer server,JsonUtil serializer,TemplateTable templates){
  saves=server;json=serializer;Options=File.Exists(FilePath)?JsonSerializer.Deserialize<MiscSettings>(File.ReadAllText(FilePath))??new():new();
  secureTemplates=templates.Items.Values.Where(t=>t.Type=="Item"&&t.Parent.ToString()=="5448bf274bdc2dfc2f8b456a"&&t.Properties?.Grids?.Any()==true).Select(t=>t.Id.ToString()).OrderBy(id=>id==ScavSecureRules.Alpha?0:1).ThenBy(id=>id).ToArray();
  var h=new Harmony("local.spt5.misc");h.UnpatchSelf();
  void Patch(Type type,string name,string? prefix=null,string? postfix=null){var method=AccessTools.Method(type,name)??throw new MissingMethodException(type.Name,name);h.Patch(method,prefix:prefix==null?null:new HarmonyMethod(typeof(MiscService),prefix),postfix:postfix==null?null:new HarmonyMethod(typeof(MiscService),postfix));}
  Patch(typeof(InsuranceService),"GetRoublePriceToInsureItemWithTrader",postfix:nameof(Premium));
  Patch(typeof(InsuranceController),"FindItemsToDelete",nameof(KeepInsured));
  Patch(typeof(InsuranceController),"IsMapLabsAndInsuranceDisabled",postfix:nameof(MapInsurance));
  Patch(typeof(InsuranceController),"IsMapLabyrinthAndInsuranceDisabled",postfix:nameof(MapInsurance));
  Patch(typeof(LocationBase),"get_Insurance",postfix:nameof(LocationInsurance));
  Patch(typeof(LocationBase),"get_DisabledForScav",postfix:nameof(ScavAccess));
  Patch(typeof(InsuranceService),"GetInsuranceReturnTimestamp",postfix:nameof(ReturnTime));
  Patch(typeof(RagfairTaxService),"CalculateTax",postfix:nameof(FleaTax));
  Patch(typeof(PlayerScavGenerator),"Generate",postfix:nameof(Generated));
  Patch(typeof(LocationLifecycleService),"HandlePostRaidPlayerScavAsync",nameof(BeforeScavEnd),nameof(AfterScavEnd));
  Patch(typeof(TradeController),"GetScavItemsToSell",postfix:nameof(SaleItems));
  return true;
 }
 static string Revision()=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(Options)));
 public static async ValueTask<string> Handle(CharacterDataRequest request,MongoId session,HttpResponseUtil http,CancellationToken ct){
  if(request.Action=="miscStatus")return http.GetBody(new{ok=true,settings=Options,revision=Revision(),secureTemplates,ready=true});
  var profile=saves.GetProfile(session);var location=profile.InraidData?.Location;
  if(!string.IsNullOrEmpty(location)&&location is not "none" and not "hideout")throw new ArgumentException("请退出战局后修改杂项规则。");
  await Gate.WaitAsync(ct);var previous=Options;SptProfile? snapshot=null;bool settingsWritten=false;string? oldSettings=null;
  try{
   if(request.Revision!=Revision())throw new ArgumentException("设置已改变，请刷新后重试。");
   var properties=typeof(MiscSettings).GetProperties().Where(p=>p.PropertyType==typeof(bool)).ToDictionary(p=>p.Name);
   var next=Options with{};
   foreach(var pair in request.Settings){
    if(pair.Key==nameof(MiscSettings.ScavSecureTemplate)){if(pair.Value.ValueKind!=JsonValueKind.String||!secureTemplates.Contains(pair.Value.GetString()))throw new ArgumentException("请选择有效的原生安全箱。");next=next with{ScavSecureTemplate=pair.Value.GetString()!};continue;}
    if(!properties.TryGetValue(pair.Key,out var property)||pair.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))throw new ArgumentException("未知杂项设置。");property.SetValue(next,pair.Value.GetBoolean());
   }
   var current=profile.CharacterData?.ScavData?.Inventory is {} bag?ScavSecureRules.Capture(bag):[];
   var retained=current.Count>0?current:Stored(profile);
   if(next.ScavSecure||next.ScavSecureTemplate!=Options.ScavSecureTemplate)retained=ScavSecureRules.Select(retained,next.ScavSecureTemplate);
   snapshot=json.Deserialize<SptProfile>(json.Serialize(profile))!;
   Directory.CreateDirectory(Home);oldSettings=File.Exists(FilePath)?File.ReadAllText(FilePath):null;
   var backupDir=Path.Combine(Home,"backups","misc");Directory.CreateDirectory(backupDir);await File.WriteAllTextAsync(Path.Combine(backupDir,$"{session}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json"),json.Serialize(profile,true),ct);
   // Preserve stored contents when the feature is disabled. Never copy a PMC item.
   if(profile.CharacterData?.ScavData?.Inventory is {} inventory){
    if(next.ScavSecure)ScavSecureRules.Attach(inventory,retained,next.ScavSecureTemplate);
   }
   if(retained.Count>0)Store(profile,retained);
   await saves.SaveProfileAsync(session,ct);
   if(oldSettings!=null)File.WriteAllText(FilePath+".bak",oldSettings);
   File.WriteAllText(FilePath+".new",JsonSerializer.Serialize(next));File.Move(FilePath+".new",FilePath,true);settingsWritten=true;Options=next;
   return http.GetBody(new{ok=true,settings=Options,revision=Revision(),secureTemplates,ready=true});
  }catch{
   Options=previous;
   if(settingsWritten){if(oldSettings==null)File.Delete(FilePath);else File.WriteAllText(FilePath,oldSettings);}
   if(snapshot!=null){saves.GetProfiles()[session]=snapshot;await saves.SaveProfileAsync(session,CancellationToken.None);}
   throw;
  }finally{Gate.Release();}
 }
 static void Premium(ref double __result){if(Options.FreeInsurance)__result=0;}
 static void FleaTax(ref double __result){if(Options.FreeFlea)__result=0;}
 static bool KeepInsured(ref HashSet<MongoId> __result){if(!Options.GuaranteedInsurance)return true;__result=[];return false;}
 static void MapInsurance(ref bool __result){if(Options.GuaranteedInsurance)__result=false;}
 static void LocationInsurance(ref bool __result){if(Options.GuaranteedInsurance)__result=true;}
 static void ScavAccess(LocationBase __instance,ref bool __result){if(Options.Connections&&MiscSettings.RestrictedMap(__instance.Id))__result=false;}
 static void ReturnTime(ref double __result){if(Options.ExpressInsurance)__result=DateTimeOffset.UtcNow.ToUnixTimeSeconds();}
 static List<Item> Stored(SptProfile profile)=>profile.ExtensionData?.TryGetValue(SecureKey,out var value)==true?json.Deserialize<List<Item>>(json.Serialize(value))??[]:[];
 static void Store(SptProfile profile,List<Item> items){profile.ExtensionData??=[];profile.ExtensionData[SecureKey]=json.Deserialize<List<Item>>(json.Serialize(items))!;}
 [HarmonyPriority(Priority.Last)]
 static void Generated(MongoId __0,PmcData __result){
  if(!Options.ScavSecure||__result.Inventory==null)return;var profile=saves.GetProfile(__0);ScavSecureRules.Attach(__result.Inventory,Stored(profile),Options.ScavSecureTemplate);
 }
 static void BeforeScavEnd(MongoId __0,PmcData __2,EndLocalRaidRequestData __6){
  var posted=__6.Results?.Profile;if(posted==null)return;
  // A previously created secure container remains protected even if its toggle was
  // disabled after the raid. Only a Scav settlement enters this hook.
  if(posted.Inventory is not {} inventory)return;var items=ScavSecureRules.Capture(inventory);if(items.Count==0)return;
  var profile=saves.GetProfile(__0);Store(profile,items);var protectedIds=items.Select(i=>i.Id.ToString()).ToHashSet();
  inventory.Items!.RemoveAll(i=>protectedIds.Contains(i.Id.ToString()));
  if(__6.TransferItems!=null)foreach(string key in __6.TransferItems.Keys.ToArray())__6.TransferItems[key]=__6.TransferItems[key].Where(i=>!protectedIds.Contains(i.Id.ToString())).ToArray();
 }
 static void AfterScavEnd(MongoId __0,ref Task __result)=>__result=FinishScav(__0,__result);
 static async Task FinishScav(MongoId session,Task native){await native;var profile=saves.GetProfile(session);if(Options.ScavSecure&&profile.CharacterData?.ScavData?.Inventory is {} inventory){if(ScavSecureRules.Capture(inventory).Count==0)ScavSecureRules.Attach(inventory,Stored(profile),Options.ScavSecureTemplate);}await saves.SaveProfileAsync(session);}
 static void SaleItems(BotBaseInventory __0,ref IEnumerable<Item> __result)=>__result=ScavSecureRules.Sellable(__0,__result);
}
