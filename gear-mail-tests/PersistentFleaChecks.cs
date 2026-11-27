using System.Reflection;
using SPTarkov.Server.Core.Utils;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using LocalGearMail;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Ragfair;
using SPTarkov.Server.Core.Models.Spt.Dialog;
using Path=System.IO.Path;

internal static class PersistentFleaChecks {
 static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static RagfairOfferService Service(){
  var holder=new RagfairOfferHolder(DispatchProxy.Create<ISptLogger<RagfairOfferHolder>,FleaQuietLog>(),null!,null!,null!);
  var service=(RagfairOfferService)RuntimeHelpers.GetUninitializedObject(typeof(RagfairOfferService));
  typeof(RagfairOfferService).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(RagfairOfferHolder)).SetValue(service,holder);
  return service;
 }
 static RagfairOffer Create(string template,CustomFleaOffers.Draft draft){
  if(template=="missing")throw new ArgumentException("Missing template");
  var root=new Item{Id=new MongoId(),Template=new(template),ParentId="hideout",SlotId="hideout"};
  var child=new Item{Id=new MongoId(),Template=new MongoId(),ParentId=root.Id.ToString(),SlotId="mod_magazine",Upd=new(){StackObjectsCount=1}};
  var offer=new RagfairOffer{Id=new MongoId(),InternalId=1,Root=root.Id,Items=[root,child],User=new(){Id=new MongoId()},CreatedBy=OfferCreator.FakePlayer};
  CustomFleaOffers.Apply(offer,draft);return offer;
 }
 static RagfairOffer Revise(string template,CustomFleaOffers.Draft draft,RagfairOffer source){
  if(template=="missing")throw new ArgumentException("Missing template");
  if(source.Items![0].Template.ToString()!=template)return Create(template,draft);
  var items=FleaOfferStore.ItemJson.Deserialize<List<Item>>(FleaOfferStore.ItemJson.Serialize(source.Items))!;
  var ids=items.ToDictionary(i=>i.Id.ToString(),_=>new MongoId());
  foreach(var item in items){item.Id=ids[item.Id.ToString()];if(item.ParentId is {} parent&&ids.TryGetValue(parent,out var next))item.ParentId=next.ToString();}
  var offer=new RagfairOffer{Id=new MongoId(),InternalId=1,Root=items[0].Id,Items=items,User=new(){Id=new MongoId()},CreatedBy=OfferCreator.FakePlayer};
  CustomFleaOffers.Apply(offer,draft);return offer;
 }
 internal static void Run(string[] args){
  string dir=args[1],phase=args[2];Directory.CreateDirectory(dir);
  var store=new FleaOfferStore(Path.Combine(dir,"quotes.sqlite"));var service=Service();
  var manager=new PersistentFleaOffers(store,service,Create,Revise);
  var nativeHooks=new Harmony("assistant.persistence-offer-tests");CustomFleaOffers.InstallNativeOffers(nativeHooks);MarketSupply.Install(nativeHooks);
  if(phase=="seed"){
   string template=new MongoId().ToString();var draft=new CustomFleaOffers.Draft("SQLite Seller",12345,37,1440,-8,150,100);
   var finite=manager.Publish(template,draft);service.ReduceOfferQuantity(new(finite.OfferId),7);manager.Capture(finite.OfferId);
   var permanent=manager.Publish(template,draft with{RemainingMinutes=-123,Stock=5});
   var paused=manager.Publish(template,draft with{Stock=11,RemainingMinutes=17,Paused=true});
   var sold=manager.Publish(template,draft with{Stock=2});service.ReduceOfferQuantity(new(sold.OfferId),2);manager.Capture(sold.OfferId);
   var withdrawn=manager.Publish(template,draft);manager.Withdraw(withdrawn.Id);
   var expired=manager.Publish(template,draft);store.Put(expired with{EndTime=1});service.RemoveOfferById(new(expired.OfferId));
   store.Put(new("missing", "missing",draft,"",1,CustomFleaOffers.PermanentEndTime,"active"));
   File.WriteAllText(Path.Combine(dir,"expected.json"),JsonSerializer.Serialize(new{finite,permanent,paused,sold,withdrawn,expired}));
   Check(store.Find(finite.Id)!.Stock==30,"partial purchases are written to SQLite");
   Check(File.ReadAllBytes(Path.Combine(dir,"quotes.sqlite")).Take(15).SequenceEqual("SQLite format 3"u8.ToArray()),"offer storage is an actual SQLite database");
   return;
  }
  using var expected=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"expected.json")));
  SavedFleaOffer Saved(string name)=>expected.RootElement.GetProperty(name).Deserialize<SavedFleaOffer>()!;
  manager.Restore();var finiteOld=Saved("finite");var finiteNow=store.Find(finiteOld.Id)!;var permanentNow=store.Find(Saved("permanent").Id)!;
  Check(service.GetOffers().Count()==3&&finiteNow.OfferId!=finiteOld.OfferId,"fresh process restores active offers to a new native offer pool");
  var live=service.GetOfferByOfferId(new(finiteNow.OfferId))!;
  Check(live.Quantity==30&&live.EndTime==finiteOld.EndTime&&live.Items!.Count==2&&live.Items[1].Upd!.StackObjectsCount==1&&live.Items[1].Template==FleaOfferStore.ItemJson.Deserialize<List<Item>>(finiteOld.ItemTree!)![1].Template&&live.Items[1].Id!=FleaOfferStore.ItemJson.Deserialize<List<Item>>(finiteOld.ItemTree!)![1].Id&&live.Items[1].ParentId==live.Root.ToString(),"restart preserves remaining stock, original expiry and attachment contents");
  Check(CustomFleaOffers.Permanent(service.GetOfferByOfferId(new(permanentNow.OfferId))!),"permanent expiry survives a process restart");
  Check(store.Find(Saved("sold").Id)!.State=="sold"&&store.Find(Saved("expired").Id)!.State=="expired"&&store.Find(Saved("withdrawn").Id)!.State=="withdrawn"&&store.Find("missing")!.State=="unavailable","sold, expired, withdrawn and missing-template records remain saved without becoming active");
  var pausedNow=store.Find(Saved("paused").Id)!;var pausedLive=service.GetOfferByOfferId(new(pausedNow.OfferId))!;
  Check(pausedNow.PausedRemainingSeconds==1020&&CustomFleaOffers.PausedRemaining(pausedLive)==1020&&!CustomFleaOffers.Permanent(pausedLive),"paused countdown survives a real process restart separately from permanent lifetime");
  foreach(string lookup in new[]{pausedNow.Id,pausedNow.OfferId}){
   var status=JsonSerializer.SerializeToElement(manager.Find(lookup));
   Check(status.GetProperty("id").GetString()==pausedNow.Id&&status.GetProperty("offerId").GetString()==pausedNow.OfferId&&status.GetProperty("pausedRemainingSeconds").GetInt64()==1020&&status.GetProperty("persistent").GetBoolean()&&!status.GetProperty("permanent").GetBoolean(),"editor state resolves the same paused saved quote by stable or native id: "+lookup);
  }
  service.ReduceOfferQuantity(live.Id,1);
  Check(JsonSerializer.SerializeToElement(manager.Find(finiteNow.Id)).GetProperty("stock").GetInt32()==29,"editor state uses current native stock even before a stored snapshot is updated");manager.Capture(finiteNow.OfferId);
  var withdrawnState=JsonSerializer.SerializeToElement(manager.Find(Saved("withdrawn").Id));
  Check(!withdrawnState.GetProperty("active").GetBoolean()&&withdrawnState.GetProperty("status").GetString()=="withdrawn","editor state preserves an inactive saved offer for explicit republishing");
  using(var pausedList=JsonDocument.Parse(JsonSerializer.Serialize(manager.List()))){
   var description=pausedList.RootElement.EnumerateArray().Single(o=>o.GetProperty("id").GetString()==pausedNow.Id);
   Check(description.GetProperty("endTime").GetInt64()<CustomFleaOffers.PermanentEndTime&&!description.GetProperty("permanent").GetBoolean(),"persistent paused offer description exposes projected expiry rather than PermanentEndTime");
   var lifetime=new LocalFleaShared.FleaOfferLifetime(description.GetProperty("endTime").GetInt64(),description.GetProperty("permanent").GetBoolean(),description.GetProperty("pausedRemainingSeconds").GetInt64());
   long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
   Check(lifetime.EditorMinutes(now)=="17"&&lifetime.Clock(now)=="00:17:00"&&lifetime.Clock(now+3600)=="00:17:00","authoritative paused metadata shows the original duration in editor and native timer after restart");
   Check((lifetime with{Permanent=true}).EditorMinutes(now)=="17","frozen duration takes priority over a stale native permanent flag instead of displaying -1");
  }
  manager.Edit(pausedNow.Id,pausedNow.Template,pausedNow.Draft with{Paused=false},true);
  var resumed=store.Find(pausedNow.Id)!;long resumedSeconds=resumed.EndTime-DateTimeOffset.UtcNow.ToUnixTimeSeconds();
  Check(JsonSerializer.SerializeToElement(manager.Find(resumed.Id)).GetProperty("pausedRemainingSeconds").ValueKind==JsonValueKind.Null&&manager.Find(pausedNow.OfferId)==null,"reopening after resume reads the new native quote and drops its old pause state");
  Check(resumed.PausedRemainingSeconds==null&&resumedSeconds is >=1018 and <=1020,"resuming starts from frozen seconds instead of counting server downtime");
  var resumedDisplay=new LocalFleaShared.FleaOfferLifetime(resumed.EndTime,false);
  Check(!resumedDisplay.Paused&&resumedDisplay.RemainingSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()+60)<resumedSeconds,"resumed quote display counts down from the frozen duration");
  Check(new LocalFleaShared.FleaOfferLifetime(CustomFleaOffers.PermanentEndTime,true).EditorMinutes(DateTimeOffset.UtcNow.ToUnixTimeSeconds())=="-1","only a permanent quote without frozen seconds displays -1");
  var pauseOptions=MiscService.Options;var pauseOptionsProperty=typeof(MiscService).GetProperty(nameof(MiscService.Options))!;
  try{
   pauseOptionsProperty.SetValue(null,new LocalMiscShared.MiscSettings{UnlimitedMarketStock=true});
   foreach(bool keepSaved in new[]{true,false}){
    var flowDraft=finiteOld.Draft with{PlayerName="Unlimited pause flow "+keepSaved,Stock=3,RemainingMinutes=17,Paused=true};
    var flow=manager.Publish(finiteOld.Template,flowDraft);
    var before=JsonSerializer.SerializeToElement(manager.Find(flow.Id));
    Check(before.GetProperty("pausedRemainingSeconds").GetInt64()==1020&&!before.GetProperty("permanent").GetBoolean(),"editor reads paused duration with unlimited stock enabled");
    manager.Edit(flow.OfferId,flow.Template,flowDraft with{Paused=false,Persistent=keepSaved},true);
    var after=service.GetOffers().Single(o=>o.User.Nickname==flowDraft.PlayerName);
    Check(CustomFleaOffers.PausedRemaining(after)==null&&!CustomFleaOffers.Permanent(after)&&after.EndTime-DateTimeOffset.UtcNow.ToUnixTimeSeconds() is >=1018 and <=1020,"resuming with unlimited stock starts a finite countdown; persistent="+keepSaved);
    manager.Delete(keepSaved?flow.Id:after.Id.ToString());
   }
  }finally{pauseOptionsProperty.SetValue(null,pauseOptions);}
  manager.Edit(resumed.Id,resumed.Template,resumed.Draft with{Paused=true},true);var pausedAgain=store.Find(resumed.Id)!;
  Check(pausedAgain.PausedRemainingSeconds is >=1018 and <=1020,"pausing an active countdown captures its exact remaining seconds");
  manager.Edit(resumed.Id,resumed.Template,resumed.Draft with{Paused=true,RemainingMinutes=9},false);
  Check(store.Find(resumed.Id)!.PausedRemainingSeconds==540,"editing paused minutes explicitly replaces the frozen duration");
  var recreated=manager.Recreate(finiteNow.Id);Check(service.GetOffers().Count()==3&&store.List().Count(r=>r.Id==recreated.Id)==1&&service.GetOfferByOfferId(new(recreated.OfferId))!.Quantity==37,"recreation resets initial stock without duplicate offers or records");
  manager.Withdraw(permanentNow.Id);manager.Restore();Check(!service.DoesOfferExist(new(permanentNow.OfferId)),"withdrawn permanent offers stay withdrawn");
  var restoredPermanent=manager.Recreate(permanentNow.Id);Check(CustomFleaOffers.Permanent(service.GetOfferByOfferId(new(restoredPermanent.OfferId))!),"explicit recreation retains permanent expiry");
  var promoted=Create(finiteOld.Template,finiteOld.Draft with{RemainingMinutes=-2});service.AddOffer(promoted);manager.Promote(promoted.Id.ToString());
  Check(store.Find(promoted.Id.ToString())!.Draft.RemainingMinutes==-1,"promoting a temporary permanent offer preserves its lifetime");
  manager.Delete(recreated.Id);manager.Restore();Check(store.Find(recreated.Id)==null&&!service.DoesOfferExist(new(recreated.OfferId)),"deleted offers and records do not return");
  var editing=manager.Publish(finiteOld.Template,finiteOld.Draft);
  service.ReduceOfferQuantity(new(editing.OfferId),9);manager.Capture(editing.OfferId);
  var changes=finiteOld.Draft with{PlayerName="Edited seller",UnitPrice=76543,Stock=28,Rating=-12,Durability=175,MaxDurability=190};
  manager.Edit(editing.OfferId,finiteOld.Template,changes,true);
  var edited=store.Find(editing.Id)!;var editedLive=service.GetOfferByOfferId(new(edited.OfferId))!;
  Check(edited.Id==editing.Id&&edited.OfferId!=editing.OfferId&&!service.DoesOfferExist(new(editing.OfferId))&&store.List().Count(r=>r.Id==editing.Id)==1,"edit by native id retains the stable SQLite id and replaces the native offer exactly once");
  Check(editedLive.Quantity==28&&editedLive.User.Nickname=="Edited seller"&&editedLive.RequirementsCost==76543&&editedLive.User.Rating==-12&&editedLive.Items!.First(i=>i.Id==editedLive.Root).Upd!.Repairable!.Durability==175&&editedLive.EndTime==editing.EndTime,"editing applies seller, price, remaining stock, rating and durability while retaining unchanged expiry");
  try{manager.Edit(editing.Id,"missing",changes,false);throw new Exception("invalid edit accepted");}catch(ArgumentException){}
  Check(store.Find(editing.Id)==edited&&service.DoesOfferExist(new(edited.OfferId)),"failed edit preserves the previous saved row and active native offer");
  manager.Edit(editing.Id,finiteOld.Template,changes with{RemainingMinutes=-777},false);
  edited=store.Find(editing.Id)!;
  Check(CustomFleaOffers.Permanent(service.GetOfferByOfferId(new(edited.OfferId))!),"edited negative lifetime uses permanent native expiry");
  service=Service();manager=new PersistentFleaOffers(store,service,Create,Revise);manager.Restore();
  var restartEdit=store.Find(editing.Id)!;var restartLive=service.GetOfferByOfferId(new(restartEdit.OfferId))!;
  Check(restartLive.User.Nickname=="Edited seller"&&restartEdit.Stock==28&&restartLive.Items![1].Template==editedLive.Items![1].Template,"edited settings, current stock and non-default attachments survive restoration to a fresh native pool");
  var legacy=finiteOld with{Id="legacy-no-items",OfferId=new MongoId().ToString(),ItemTree=null};store.Put(legacy);manager.Restore();
  Check(service.GetOfferByOfferId(new(store.Find(legacy.Id)!.OfferId))!=null,"pre-item-tree SQLite rows remain readable and restore through default creation");manager.Delete(legacy.Id);
  manager.Recreate(editing.Id);edited=store.Find(editing.Id)!;
  manager.Edit(editing.Id,finiteOld.Template,changes with{Persistent=false},true);
  Check(store.Find(editing.Id)==null&&!service.DoesOfferExist(new(edited.OfferId))&&service.GetOffers().Any(o=>o.User.Nickname=="Edited seller"&&CustomFleaOffers.IsCustom(o)),"turning persistence off removes the saved row and keeps one edited temporary quote");
  var temporary=Create(finiteOld.Template,changes with{Persistent=false});service.AddOffer(temporary);
  manager.Edit(temporary.Id.ToString(),finiteOld.Template,changes,true);
  var converted=store.List().Single(r=>r.Draft.PlayerName=="Edited seller");
  Check(!service.DoesOfferExist(temporary.Id)&&service.DoesOfferExist(new(converted.OfferId)),"temporary quote can be edited and saved without duplicating the original");
  manager.Withdraw(converted.Id);manager.Edit(converted.Id,finiteOld.Template,changes,false);
  var reactivated=service.GetOfferByOfferId(new(store.Find(converted.Id)!.OfferId))!;
  Check(store.Find(converted.Id)!.State=="active"&&reactivated.Items![1].Template==temporary.Items![1].Template,"editing an inactive saved quote republishes settings and retains its saved attachments");
  var beforeRecreate=reactivated.Items![1].Template;var republished=manager.Recreate(converted.Id);
  Check(service.GetOfferByOfferId(new(republished.OfferId))!.Items![1].Template==beforeRecreate,"explicit recreation retains non-default saved attachments");
  manager.Delete(converted.Id);Check(store.Find(converted.Id)==null,"edited quote and SQLite row can be deleted together");
  var native=Create(finiteOld.Template,changes with{PlayerName="Withdraw native",Stock=19,RemainingMinutes=17,Paused=true,Persistent=false});
  native.ExtensionData!.Remove(CustomFleaOffers.Marker);native.Items!.Reverse();service.AddOffer(native);
  var priorOptions=MiscService.Options;var optionProperty=typeof(MiscService).GetProperty(nameof(MiscService.Options))!;
  try{optionProperty.SetValue(null,new LocalMiscShared.MiscSettings{UnlimitedMarketStock=true});Check(service.GetOfferByOfferId(native.Id)!.Quantity==int.MaxValue,"withdrawal test exercises the native unlimited-market projection");manager.Withdraw(native.Id.ToString());}
  finally{optionProperty.SetValue(null,priorOptions);}
  var nativeRow=store.Find(native.Id.ToString())!;
  Check(nativeRow.State=="withdrawn"&&nativeRow.Stock==19&&nativeRow.PausedRemainingSeconds==1020&&!service.DoesOfferExist(native.Id),"native generated offers can be withdrawn by native id and retained with current stock and paused duration");
  manager.Withdraw(native.Id.ToString());manager.Restore();
  Check(store.List().Count(r=>r.Id==nativeRow.Id)==1&&!service.DoesOfferExist(native.Id),"repeated withdrawal is idempotent and restoration does not relist withdrawn native offers");
  var returned=manager.Recreate(nativeRow.Id);var returnedLive=service.GetOfferByOfferId(new(returned.OfferId))!;
  Check(returnedLive.Quantity==19&&returnedLive.Items![0].Template.ToString()==finiteOld.Template&&returnedLive.Items[1].Template==native.Items!.First(i=>i.Id!=native.Root).Template&&CustomFleaOffers.PausedRemaining(returnedLive)==1020,"withdrawn native offers can be recreated with complete attachments and normalized root ordering");
  manager.Withdraw(returned.OfferId);Check(store.Find(returned.Id)!.State=="withdrawn"&&!service.DoesOfferExist(new(returned.OfferId)),"persisted offers also accept native ids for withdrawal");manager.Delete(returned.Id);
  var temporaryWithdraw=Create(finiteOld.Template,changes with{PlayerName="Withdraw temporary",Persistent=false});service.AddOffer(temporaryWithdraw);manager.Withdraw(temporaryWithdraw.Id.ToString());
  Check(store.Find(temporaryWithdraw.Id.ToString()) is {State:"withdrawn"}&&!service.DoesOfferExist(temporaryWithdraw.Id),"temporary custom quotes are saved before native withdrawal and remain available to recreate");manager.Delete(store.Find(temporaryWithdraw.Id.ToString())!.Id);
  foreach(var creator in new[]{OfferCreator.Player,OfferCreator.Trader}){
   var protectedOffer=Create(finiteOld.Template,changes) with{CreatedBy=creator};service.AddOffer(protectedOffer);
   try{manager.Withdraw(protectedOffer.Id.ToString());throw new Exception("Protected offer withdrawn");}catch(ArgumentException){}
   Check(service.DoesOfferExist(protectedOffer.Id)&&store.Find(protectedOffer.Id.ToString())==null,"withdrawal preserves native inventory bookkeeping for "+creator);service.RemoveOfferById(protectedOffer.Id);
  }
  var h=new Harmony("assistant.permanent-mail-tests");
  try{
   PermanentMailExpiry.Install(h);var config=new InsuranceConfig{StorageTimeOverrideSeconds=-42};
   Check(config.StorageTimeOverrideSeconds>100000000000,"negative insurance storage uses a permanent native lifetime");
   config.StorageTimeOverrideSeconds=73;Check(config.StorageTimeOverrideSeconds==73,"positive insurance storage remains exact");
   var details=new SendMessageDetails{ItemsMaxStorageLifetimeSeconds=-999};PermanentMailExpiry.Normalize(details);
   Check(details.ItemsMaxStorageLifetimeSeconds>100000000000,"negative quest and reward mail expiry becomes permanent");
  }finally{h.UnpatchSelf();}
 }
}
