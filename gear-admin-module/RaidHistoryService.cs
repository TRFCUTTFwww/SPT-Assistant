using System.Text.Json;
using HarmonyLib;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.InRaid;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public static class RaidHistoryService {
 static SaveServer saves=null!;static JsonUtil json=null!;static RaidHistoryStore? store;static readonly object Gate=new();
 public static bool Ready{get;private set;}public static string Error{get;private set;}="";
 public static bool Install(SaveServer server,JsonUtil serializer){try{lock(Gate){saves=server;json=serializer;store=new(Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","history","raids.sqlite"));var h=new Harmony("local.spt5.raid-history");h.UnpatchSelf();h.Patch(AccessTools.Method(typeof(LocationLifecycleService),"StartLocalRaidAsync"),prefix:new(typeof(RaidHistoryService),nameof(BeforeStart)),postfix:new(typeof(RaidHistoryService),nameof(AfterStart)));h.Patch(AccessTools.Method(typeof(LocationLifecycleService),"EndLocalRaidAsync"),prefix:new(typeof(RaidHistoryService),nameof(BeforeEnd)),postfix:new(typeof(RaidHistoryService),nameof(AfterEnd)));Ready=true;Error="";}return true;}catch{Ready=false;Error="战局记录暂不可用";return false;}}
 static void Safe(Action action){try{lock(Gate){action();Error="";}}catch{Error="战局记录写入失败";}}
 static JsonElement Encode(object? value){using var doc=JsonDocument.Parse(json.Serialize(value)??"null");return doc.RootElement.Clone();}
 static PmcData? Profile(MongoId session,bool pmc){var data=saves.GetProfile(session)?.CharacterData;return pmc?data?.PmcData:data?.ScavData;}
 static bool Pmc(string side)=>!side.Equals("savage",StringComparison.OrdinalIgnoreCase)&&!side.Equals("scav",StringComparison.OrdinalIgnoreCase);
 static void BeforeStart(MongoId __0,StartLocalRaidRequestData __1,out RaidHistoryRecord? __state){RaidHistoryRecord? value=null;Safe(()=>{var profile=Profile(__0,Pmc(__1.PlayerSide??""));value=new(){Map=__1.Location??"",Side=Pmc(__1.PlayerSide??"")?"PMC":"Scav",Nickname=profile?.Info?.Nickname??"",Started=DateTime.UtcNow.ToString("O"),EntryKnown=profile?.Inventory!=null,Entry=RaidHistoryRules.Inventory(Encode(profile?.Inventory))};});__state=value;}
 static void AfterStart(MongoId __0,RaidHistoryRecord? __state,ref Task<StartLocalRaidResponseData> __result){__result=CompleteStart(__result,__0.ToString(),__state);}
 static async Task<StartLocalRaidResponseData> CompleteStart(Task<StartLocalRaidResponseData> task,string session,RaidHistoryRecord? start){var response=await task;Safe(()=>{if(start==null||string.IsNullOrEmpty(response.ServerId))return;string id=RaidHistoryRules.Id(session,response.ServerId);if(store!.Find(session,id)==null)store.Put(session,start with{Id=id,ServerId=response.ServerId});});return response;}
 static void BeforeEnd(MongoId __0,EndLocalRaidRequestData __1,out RaidHistoryRecord? __state){RaidHistoryRecord? value=null;Safe(()=>{if(string.IsNullOrEmpty(__1.ServerId)||__1.Results==null)return;var session=__0.ToString();string id=RaidHistoryRules.Id(session,__1.ServerId);var old=store!.Find(session,id);if(old?.Settled==true){value=old;return;}
  var result=__1.Results;string map=__1.ServerId.Split('.')[0];bool pmc=__1.ServerId.Split('.').Skip(1).FirstOrDefault()?.Contains("pmc",StringComparison.OrdinalIgnoreCase)==true;
  value=(old??new RaidHistoryRecord{Id=id,ServerId=__1.ServerId,Map=map,Side=pmc?"PMC":"Scav",Nickname=result.Profile?.Info?.Nickname??"",Started=""}) with{Ended=DateTime.UtcNow.ToString("O"),Status=result.Result?.ToString()??"UNKNOWN",Exit=result.ExitName??"",Seconds=result.PlayTime is double time&&double.IsFinite(time)&&time>=0?time:null,Final=RaidHistoryRules.Inventory(Encode(result.Profile?.Inventory)),Kills=RaidHistoryRules.Kills(Encode(result.Profile?.Stats))};
  store.Put(session,value);});__state=value;}
 static void AfterEnd(MongoId __0,RaidHistoryRecord? __state,ref Task __result){__result=CompleteEnd(__result,__0,__state);}
 static async Task CompleteEnd(Task task,MongoId session,RaidHistoryRecord? raid){try{await task;}catch{Safe(()=>{if(raid!=null&&!raid.Settled)store!.Put(session.ToString(),raid with{Status="SAVE_FAILED"});});throw;}
  Safe(()=>{if(raid==null||raid.Settled)return;bool survived=raid.Status is "SURVIVED" or "RUNNER" or "TRANSIT";var saved=RaidHistoryRules.Inventory(Encode(Profile(session,raid.Side=="PMC")?.Inventory));var returned=survived?raid.Final:raid.Side=="PMC"?RaidHistoryRules.Returned(raid.Final,saved):SavedScavSecure(session,raid.Final);store!.Put(session.ToString(),raid with{Returned=returned,Loot=raid.EntryKnown?RaidHistoryRules.Loot(raid.Entry,returned):[],Settled=true});});
 }
 static HistoryItem[] SavedScavSecure(MongoId session,HistoryItem[] final){var extension=saves.GetProfile(session)?.ExtensionData;if(extension?.TryGetValue("localMiscScavSecure",out var value)!=true)return [];var array=Encode(value);if(array.ValueKind!=JsonValueKind.Array)return [];var ids=array.EnumerateArray().Select(i=>RaidHistoryRules.Text(i,"_id")).ToHashSet();return final.Where(i=>ids.Contains(i.Id)).ToArray();}
 public static string Handle(CharacterDataRequest request,MongoId session,HttpResponseUtil http){lock(Gate){if(!Ready||store==null)throw new ArgumentException("战局记录暂不可用");if(request.Action=="raidHistoryDetail")return http.GetBody(new{ok=true,raid=store.Find(session.ToString(),request.Id)??throw new ArgumentException("战局记录不存在")});string Filter(string key)=>request.Settings.TryGetValue(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";return http.GetBody(new{ok=true,history=store.List(session.ToString(),double.IsFinite(request.Value)?(int)Math.Clamp(request.Value,0,100000):0,Filter("map"),Filter("status")),warning=Error});}}
}
