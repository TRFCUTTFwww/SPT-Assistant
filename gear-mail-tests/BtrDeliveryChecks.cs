using System.Text.Json;
using LocalGearMail;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Tables;
internal static class BtrDeliveryChecks {
 internal static void Run(){
  var template=new MongoId("5447a9cd4bdc2dbd208b4567");var templates=(TemplateTable)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TemplateTable));typeof(TemplateTable).GetProperty(nameof(TemplateTable.Items))!.SetValue(templates,new Dictionary<MongoId,TemplateItem>{[template]=new(){Id=template,Properties=new()}});
  var bag=new MongoId();var child=new MongoId();var outside=new MongoId();
  List<Item> Batch()=>[new(){Id=child,Template=template,ParentId=bag.ToString(),SlotId="mod_scope",Upd=new(){Repairable=new(){Durability=17,MaxDurability=53}}},new(){Id=bag,Template=template,ParentId=outside.ToString(),SlotId="main"}];
  var fresh=BtrInstantDelivery.Prepare(Batch(),[],templates);
  Check(fresh.Count==2&&fresh[0].Id==bag&&fresh[0].ParentId==null&&fresh[0].SlotId==null,"mail roots sorted and detached from transfer stash");
  Check(fresh[1].ParentId==bag.ToString()&&fresh[1].SlotId=="mod_scope"&&fresh[1].Upd?.Repairable?.Durability==17,"attachment hierarchy and upd preserved");
  var receipt=new HashSet<string>{bag.ToString(),child.ToString()};
  Check(BtrInstantDelivery.Prepare(Batch(),receipt,templates).Count==0,"retry after delivery creates no new reward");
  var second=new MongoId();var batch=Batch();batch.Add(new(){Id=second,Template=template,ParentId=outside.ToString(),SlotId="main"});
  Check(BtrInstantDelivery.Prepare(batch,receipt,templates).Single().Id==second,"a later handoff sends only fresh roots");
  var settlement=new SPTarkov.Server.Core.Models.Eft.Match.EndLocalRaidRequestData{TransferItems=new(){["driver_btr"]=Batch(),["driver_transit"]=Batch()},LostInsuredItems=Batch()};
  var virtualRoot=new Item{Id=SPTarkov.Server.Core.Models.Enums.Traders.BTR,Template=template};
  settlement.TransferItems["driver_btr"]=settlement.TransferItems["driver_btr"].Prepend(virtualRoot).ToList();
  Check(BtrInstantDelivery.Cargo(settlement).Count==2,"native BTR virtual stash and transit cargo are excluded from delivery");
  BtrInstantDelivery.FilterTransfers(settlement,receipt);
  Check(settlement.TransferItems["driver_btr"].Single().Id==virtualRoot.Id&&settlement.TransferItems["driver_transit"].Count()==2&&!settlement.LostInsuredItems.Any(),"settlement and insurance exclude delivered BTR items but preserve transit cargo");
  var profile=new SptProfile{ExtensionData=new(){["localBtrDeliveredItems"]=JsonSerializer.SerializeToElement(receipt)}};
  Check(BtrInstantDelivery.Receipts(JsonSerializer.Deserialize<SptProfile>(JsonSerializer.Serialize(profile))!).SetEquals(receipt),"receipts survive profile serialization and restart");
  Reject(()=>BtrInstantDelivery.Prepare(Batch(),[bag.ToString()],templates),"partial attachment receipt");
  var loop=Batch();loop[1].ParentId=child.ToString();Reject(()=>BtrInstantDelivery.Prepare(loop,[],templates),"parent cycle");
  var duplicate=Batch();duplicate.Add(duplicate[0]);Reject(()=>BtrInstantDelivery.Prepare(duplicate,[],templates),"duplicate ID");
  var unknown=Batch();unknown[0].Template=new MongoId();Reject(()=>BtrInstantDelivery.Prepare(unknown,[],templates),"unknown template");
  Console.WriteLine("PASS BTR in-raid delivery: hierarchy, parameters, retries, subsequent handoffs, persistent receipts and invalid trees");
 }
 static void Check(bool value,string name){if(!value)throw new Exception(name);}
 static void Reject(Action action,string name){try{action();}catch(ArgumentException){return;}throw new Exception("Accepted "+name);}
}
