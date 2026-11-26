using System.Text.Json;
using HarmonyLib;
using LocalSpt.Shared;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Services.InRaid;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
// Receipts live in the same profile save as the mail, and survive retries,
// module reloads, restart, collection of the reward and raid settlement.
internal static class BtrInstantDelivery {
 const string Key="localBtrDeliveredItems";
 static SaveServer saves=null!;static MailSendService? sender;
 internal static void Install(SaveServer server,MailSendService? mail){
  saves=server;sender=mail;var harmony=new Harmony("local.spt5.btr-live-delivery");harmony.UnpatchSelf();
  harmony.Patch(AccessTools.Method(typeof(BtrDeliveryService),nameof(BtrDeliveryService.HandleItemTransferEvent)),prefix:new HarmonyMethod(typeof(BtrInstantDelivery),nameof(FilterSettlement)));
  harmony.Patch(AccessTools.Method(typeof(LocationLifecycleService),"HandleInsuredItemLostEvent"),prefix:new HarmonyMethod(typeof(BtrInstantDelivery),nameof(FilterInsurance)));
 }
 internal static HashSet<string> Receipts(SptProfile profile)=>profile.ExtensionData?.TryGetValue(Key,out var value)==true?JsonSerializer.Deserialize<HashSet<string>>(JsonSerializer.Serialize(value))??[]:[];
 internal static void FilterSettlement(MongoId __0,EndLocalRaidRequestData __1){
  var gate=GearMailService.SessionLock(__0);gate.Wait();try{
   var profile=saves.GetProfile(__0);var receipts=Receipts(profile);FilterTransfers(__1,receipts);
   // If an HTTP request failed or arrived after settlement, this same receipt
   // path supplies the fallback; a late retry cannot mail the cargo twice.
   if(!Instant()||sender==null||__1.TransferItems==null)return;
   var cargo=Cargo(__1);
   if(cargo.Count==0)return;
   Deliver(__0,profile,cargo,sender,receipts);
   saves.SaveProfileAsync(__0).GetAwaiter().GetResult();FilterTransfers(__1,receipts);
  }finally{gate.Release();}
 }
 internal static List<Item> Cargo(EndLocalRaidRequestData request)=>(request.TransferItems??[]).Where(p=>BtrKey(p.Key)).SelectMany(p=>p.Value??[]).Where(i=>i.Id!=Traders.BTR).ToList();
 static bool BtrKey(string key)=>key.EndsWith("_btr",StringComparison.Ordinal)||key.EndsWith("_BtrItemsDelivery",StringComparison.Ordinal);
 static bool Instant(){
  try{using var config=JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalRaidControls","settings.json")));return config.RootElement.TryGetProperty("InstantBtrDelivery",out var enabled)&&enabled.GetBoolean();}catch{return false;}
 }
 internal static void FilterTransfers(EndLocalRaidRequestData __1,HashSet<string> ids){
  if(ids.Count==0)return;
  if(__1.TransferItems!=null)foreach(string key in __1.TransferItems.Keys.Where(BtrKey).ToArray())
   __1.TransferItems[key]=__1.TransferItems[key].Where(i=>!ids.Contains(i.Id.ToString())).ToArray();
  if(__1.LostInsuredItems!=null)__1.LostInsuredItems=__1.LostInsuredItems.Where(i=>!ids.Contains(i.Id.ToString())).ToArray();
 }
 static void FilterInsurance(MongoId __0,PmcData __1,EndLocalRaidRequestData __2){
  var ids=Receipts(saves.GetProfile(__0));
  if(Instant()&&__2.TransferItems!=null)ids.UnionWith(Cargo(__2).Select(i=>i.Id.ToString()));
  if(ids.Count==0)return;
  if(__2.LostInsuredItems!=null)__2.LostInsuredItems=__2.LostInsuredItems.Where(i=>!ids.Contains(i.Id.ToString())).ToArray();
  if(__1.InsuredItems!=null)__1.InsuredItems=__1.InsuredItems.Where(i=>!ids.Contains(i.ItemId.ToString()??"")).ToList();
 }
 internal static List<Item> Prepare(IEnumerable<Item> source,HashSet<string> delivered,TemplateTable templates){
  var all=source.ToList();var ids=new HashSet<string>(StringComparer.Ordinal);
  foreach(var item in all)if(!ids.Add(item.Id.ToString())||!templates.Items.ContainsKey(item.Template))throw new ArgumentException("BTR 托运物品编号或模板无效。");
  var nodes=all.ToDictionary(i=>i.Id.ToString(),StringComparer.Ordinal);
  foreach(var item in all){var seen=new HashSet<string>{item.Id.ToString()};var node=item;
   while(node.ParentId!=null&&nodes.TryGetValue(node.ParentId,out var parent)){if(!seen.Add(parent.Id.ToString()))throw new ArgumentException("BTR 托运物品层级循环。");node=parent;}
   if(delivered.Contains(item.Id.ToString())!=delivered.Contains(node.Id.ToString()))throw new ArgumentException("BTR 托运收据不完整。");
  }
  var fresh=all.Where(i=>!delivered.Contains(i.Id.ToString())).ToList();var freshIds=fresh.Select(i=>i.Id.ToString()).ToHashSet();
  // Root placement belongs to the mailbox; child locations and all upd data stay intact.
  foreach(var item in fresh)if(item.ParentId==null||!freshIds.Contains(item.ParentId)){item.ParentId=null;item.SlotId=null;item.Location=null;}
  return fresh.OrderBy(i=>i.ParentId==null?0:1).ToList();
 }
 static void Deliver(MongoId session,SptProfile profile,List<Item> items,MailSendService mail,HashSet<string> receipts){
  var originalIds=items.Select(i=>i.Id.ToString()).ToArray();
  mail.SendDirectNpcMessageToPlayer(session,Traders.BTR.ToString(),MessageType.BtrItemsDelivery,"BTR 托运物品已送达。",items,MailLifetime());
  receipts.UnionWith(originalIds);profile.ExtensionData??=[];profile.ExtensionData[Key]=JsonSerializer.SerializeToElement(receipts);
  var pmc=profile.CharacterData!.PmcData!;
  if(pmc.Inventory?.Items!=null)pmc.Inventory.Items=pmc.Inventory.Items.Where(i=>!receipts.Contains(i.Id.ToString())).ToList();
  if(pmc.InsuredItems!=null)pmc.InsuredItems=pmc.InsuredItems.Where(i=>!receipts.Contains(i.ItemId.ToString()??"")).ToList();
 }
 static long MailLifetime(){
  using var config=JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"SPT_Data","configs","trader.json")));
  long hours=config.RootElement.GetProperty("fence").GetProperty("btrDeliveryExpireHours").GetInt64();
  return hours<0?-1:checked(hours*3600);
 }
 internal static async ValueTask<string> Handle(CharacterDataRequest request,MongoId session,SptProfile profile,SaveServer server,JsonUtil json,HttpResponseUtil http,TemplateTable templates,MailSendService mail){
  var raw=request.StashItems??throw new ArgumentException("缺少 BTR 托运物品。");
  var normalized=RaidStashJson.Normalize(raw.GetRawText(),out _,out _);
  var source=json.Deserialize<List<Item>>(normalized.ToJsonString())??throw new ArgumentException("BTR 托运物品格式无效。");
  var receipts=Receipts(profile);var items=Prepare(source,receipts,templates);
  if(items.Count>0)Deliver(session,profile,items,mail,receipts);
  // A failed disk write keeps the mail and receipt together in memory. Retrying
  // persists the same mail without sending another reward or notification.
  await server.SaveProfileAsync(session);
  return http.GetBody(new{ok=true,delivered=items.Count,acknowledged=source.Select(i=>i.Id.ToString()).ToArray()});
 }
}
