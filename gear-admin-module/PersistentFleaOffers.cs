using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Services.Ragfair;

namespace LocalGearMail;

internal sealed class PersistentFleaOffers(FleaOfferStore store,RagfairOfferService service,Func<string,CustomFleaOffers.Draft,RagfairOffer> create,Func<string,CustomFleaOffers.Draft,RagfairOffer,RagfairOffer>? revise=null){
 internal const string SavedMarker="localAssistantSavedQuote";
 readonly object gate=new();
 internal SavedFleaOffer Publish(string template,CustomFleaOffers.Draft draft,string? id=null,SavedFleaOffer? restoring=null,RagfairOffer? source=null,long? expiry=null,long? pausedSeconds=null){
  lock(gate){
   var previous=id==null?null:store.Find(id);
   var effective=restoring==null?draft:draft with{Stock=restoring.Stock};
   var offer=Create(template,effective,source,restoring??previous);
   string savedId=id??Guid.NewGuid().ToString("N");
   offer.ExtensionData??=[];offer.ExtensionData[SavedMarker]=savedId;
   if(restoring!=null)Lifetime(offer,draft,restoring.EndTime,restoring.PausedRemainingSeconds);
   else if(expiry!=null)Lifetime(offer,draft,expiry.Value,pausedSeconds);
   else if(previous?.State=="withdrawn"&&previous.PausedRemainingSeconds is {} withdrawnPaused&&draft.Paused&&draft.RemainingMinutes==previous.Draft.RemainingMinutes)CustomFleaOffers.Pause(offer,withdrawnPaused);
   var next=new SavedFleaOffer(savedId,template,draft,offer.Id.ToString(),offer.Quantity,offer.EndTime!.Value,"active",FleaOfferStore.ItemJson.Serialize(offer.Items),CustomFleaOffers.PausedRemaining(offer));
   store.Put(next);
   try{
    service.AddOffer(offer);
    if(!service.DoesOfferExist(offer.Id))throw new ArgumentException("原生报价池未接受自定义报价。");
    if(previous!=null&&MongoId.IsValidMongoId(previous.OfferId)&&service.DoesOfferExist(new(previous.OfferId)))service.RemoveOfferById(new(previous.OfferId));
    if(previous==null&&source!=null)service.RemoveOfferById(source.Id);
   }catch{
    if(service.DoesOfferExist(offer.Id))service.RemoveOfferById(offer.Id);
    if(previous==null)store.Delete(savedId);else store.Put(previous);
    throw;
   }
   return next;
  }
 }
 internal void Edit(string id,string template,CustomFleaOffers.Draft draft,bool preserveExpiry){
  lock(gate){
   var previous=store.Find(id);
   var sourceId=previous?.OfferId??id;
   var source=MongoId.IsValidMongoId(sourceId)?service.GetOfferByOfferId(new(sourceId)):null;
   if(previous==null&&source==null)throw new ArgumentException("报价已售罄、到期或移除，请刷新后重试。");
   // Player and trader offers have separate inventory / assortment bookkeeping.
   if(source!=null&&source.CreatedBy!=SPTarkov.Server.Core.Models.Enums.OfferCreator.FakePlayer)throw new ArgumentException("商人和玩家自身报价请使用原生管理功能；此编辑器支持市场生成报价及自定义报价。");
   long? frozen=previous?.PausedRemainingSeconds??CustomFleaOffers.PausedRemaining(source);
   long? expiry=preserveExpiry?(frozen is {} seconds?DateTimeOffset.UtcNow.ToUnixTimeSeconds()+seconds:previous?.EndTime??source?.EndTime):null;
   if(draft.Persistent){
    Publish(template,draft,previous?.Id,source:source,expiry:expiry,pausedSeconds:frozen);
    return;
   }
   var next=Create(template,draft,source,previous);
   if(expiry!=null)Lifetime(next,draft,expiry.Value,frozen);
   try{
    service.AddOffer(next);
    if(!service.DoesOfferExist(next.Id))throw new ArgumentException("原生报价池未接受修改后的报价。");
    if(previous!=null)store.Delete(previous.Id);
    if(source!=null)service.RemoveOfferById(source.Id);
   }catch{
    if(service.DoesOfferExist(next.Id))service.RemoveOfferById(next.Id);
    if(previous!=null)store.Put(previous);
    throw;
   }
  }
 }
 static void Lifetime(RagfairOffer offer,CustomFleaOffers.Draft draft,long expiry,long? frozen){
  if(draft.Paused&&draft.RemainingMinutes>=0)CustomFleaOffers.Pause(offer,frozen??Math.Max(0,expiry-DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
  else offer.EndTime=expiry;
 }
 RagfairOffer Create(string template,CustomFleaOffers.Draft draft,RagfairOffer? source,SavedFleaOffer? saved){
  // Only server-owned item trees enter storage; clients submit scalar draft fields.
  if(source==null&&saved?.Template==template&&saved.ItemTree is {} json){
   var items=FleaOfferStore.ItemJson.Deserialize<List<SPTarkov.Server.Core.Models.Eft.Common.Tables.Item>>(json);
   if(items is {Count:>0})source=new RagfairOffer{Items=items,Root=items[0].Id,User=new()};
  }
  return source!=null&&revise!=null?revise(template,draft,source):create(template,draft);
 }
 internal void Restore(){
  lock(gate)foreach(var row in store.List().Where(r=>r.State=="active")){
   if(row.Stock<=0){store.Put(row with{Stock=0,State="sold"});continue;}
   if(row.EndTime<=DateTimeOffset.UtcNow.ToUnixTimeSeconds()){store.Put(row with{State="expired"});continue;}
   if(MongoId.IsValidMongoId(row.OfferId)&&service.DoesOfferExist(new(row.OfferId)))continue;
   try{Publish(row.Template,row.Draft,row.Id,row);}catch(ArgumentException){store.Put(row with{State="unavailable"});}
  }
 }
 internal void Capture(string offerId){
  lock(gate){
   var row=store.Find(offerId);if(row==null||row.State!="active")return;
   var live=MongoId.IsValidMongoId(offerId)?service.GetOfferByOfferId(new(offerId)):null;
   int remaining=Math.Max(0,live?.Quantity??0);
   store.Put(row with{Stock=remaining,State=remaining==0?"sold":"active"});
  }
 }
 internal SavedFleaOffer Recreate(string id){lock(gate){var row=store.Find(id)??throw new ArgumentException("持久化报价不存在。");return Publish(row.Template,row.Draft,row.Id);}}
 internal void Withdraw(string id){
  lock(gate){
   var previous=store.Find(id);string nativeId=previous?.OfferId??id;
   // Read the underlying pool so market oversupply does not persist synthetic Int32.MaxValue stock.
   var offer=service.GetOffers().FirstOrDefault(o=>o.Id.ToString()==nativeId);
   if(offer!=null&&offer.CreatedBy!=SPTarkov.Server.Core.Models.Enums.OfferCreator.FakePlayer)throw new ArgumentException("商人和玩家自身报价请使用原生下架功能。");
   var row=previous??(offer!=null?Snapshot(offer):throw new ArgumentException("报价已售罄、到期或移除，请刷新后重试。"));
   var next=row with{State="withdrawn",Stock=offer?.Quantity??row.Stock};store.Put(next);
   try{if(offer!=null)service.RemoveOfferById(offer.Id);}catch{if(previous==null)store.Delete(row.Id);else store.Put(previous);throw;}
  }
 }
 internal void Delete(string id){
  lock(gate){var row=store.Find(id);if(row==null){RemoveNative(id);return;}store.Delete(row.Id);try{RemoveNative(row.OfferId);}catch{store.Put(row);throw;}}
 }
 void RemoveNative(string id){
  if(!MongoId.IsValidMongoId(id))return;
  var offer=service.GetOfferByOfferId(new(id));
  if(offer!=null){if(!CustomFleaOffers.IsCustom(offer))throw new ArgumentException("只能操作自定义报价。");service.RemoveOfferById(offer.Id);}
 }
 static SavedFleaOffer Snapshot(RagfairOffer offer){
  var root=offer.Items!.First(i=>i.Id==offer.Root);long? frozen=CustomFleaOffers.PausedRemaining(offer);
  long seconds=frozen??Math.Max(0,(offer.EndTime??0)-DateTimeOffset.UtcNow.ToUnixTimeSeconds());
  var draft=new CustomFleaOffers.Draft(offer.User.Nickname??"Player",checked((int)(offer.RequirementsCost??1)),offer.Quantity,
   CustomFleaOffers.Permanent(offer)?-1:checked((int)((seconds+59)/60)),offer.User.Rating??0,root.Upd?.Repairable?.Durability,root.Upd?.Repairable?.MaxDurability,true,frozen!=null);
  return new SavedFleaOffer(Guid.NewGuid().ToString("N"),root.Template.ToString(),draft,offer.Id.ToString(),offer.Quantity,offer.EndTime??0,"active",
   FleaOfferStore.ItemJson.Serialize(offer.Items!.OrderBy(i=>i.Id==offer.Root?0:1).ToList()),frozen);
 }
 internal void Promote(string id){
  lock(gate){
   if(store.Find(id)!=null)return;
   var offer=service.GetOffers().FirstOrDefault(o=>o.Id.ToString()==id&&CustomFleaOffers.IsCustom(o))??throw new ArgumentException("自定义报价已售罄、到期或移除。");
   var row=Snapshot(offer);
   store.Put(row);offer.ExtensionData??=[];offer.ExtensionData[SavedMarker]=row.Id;
  }
 }
 internal object[] List(){
  lock(gate){
   var rows=store.List();var savedIds=rows.Select(r=>r.OfferId).ToHashSet(StringComparer.Ordinal);
   var list=new List<object>();long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
   foreach(var row in rows){
    var live=MongoId.IsValidMongoId(row.OfferId)?service.GetOfferByOfferId(new(row.OfferId)):null;
    bool paused=row.PausedRemainingSeconds.HasValue;
    bool active=row.State=="active"&&row.Stock>0&&(paused||row.EndTime>now)&&live!=null;
    string status=active?"active":row.State=="active"?(row.Stock<=0?"sold":!paused&&row.EndTime<=now?"expired":"pending"):row.State;
    list.Add(new{id=row.Id,offerId=row.OfferId,template=row.Template,playerName=row.Draft.PlayerName,unitPrice=row.Draft.UnitPrice,stock=active&&MiscService.Options.UnlimitedMarketStock?int.MaxValue:row.Stock,
     endTime=paused?now+row.PausedRemainingSeconds!.Value:row.EndTime,rating=row.Draft.Rating,durability=row.Draft.Durability,maxDurability=row.Draft.MaxDurability,persistent=true,active,status,permanent=!paused&&row.Draft.RemainingMinutes<0,pausedRemainingSeconds=row.PausedRemainingSeconds});
   }
   foreach(var offer in service.GetOffers().Where(CustomFleaOffers.IsCustom).Where(o=>!savedIds.Contains(o.Id.ToString())&&o.EndTime>now))list.Add(CustomFleaOffers.Describe(offer));
   return list.ToArray();
  }
 }
}
