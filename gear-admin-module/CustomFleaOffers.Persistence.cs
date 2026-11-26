using HarmonyLib;
using Path=System.IO.Path;
using SPTarkov.Server.Core.Generators.Ragfair;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Ragfair;

namespace LocalGearMail;

internal static partial class CustomFleaOffers {
 static FleaOfferStore? store;
 static TemplateTable? persistentTemplates;
 static PersistentFleaOffers? persistence;
 static bool restored;
 internal static void InstallPersistence(Harmony h,TemplateTable templates){
  store=new FleaOfferStore(Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","flea-prices","custom-offers.sqlite"));
  InstallNativeOffers(h);
  persistentTemplates=templates;persistence=null;restored=false;
  h.Patch(AccessTools.Method(typeof(RagfairOfferService),nameof(RagfairOfferService.ReduceOfferQuantity)),postfix:new HarmonyMethod(typeof(CustomFleaOffers),nameof(AfterReduction)));
 }
 internal static void Attach(RagfairOfferService service,RagfairOfferGenerator generator){
  var templates=persistentTemplates;if(store==null||templates==null)return;
  TemplateItem Resolve(string id){
   if(!MongoId.IsValidMongoId(id)||!templates.Items.TryGetValue(new(id),out var template)||template.Type!="Item"||template.Properties?.QuestItem==true)
    throw new ArgumentException("持久化报价物品不存在或已变为任务物品。");
   return template;
  }
  persistence??=new PersistentFleaOffers(store,service,(id,draft)=>{var template=Resolve(id);return Create(template,Validate(System.Text.Json.JsonSerializer.SerializeToElement(draft),template),generator);},
   (id,draft,source)=>{var template=Resolve(id);return Create(template,Validate(System.Text.Json.JsonSerializer.SerializeToElement(draft),template),generator,source);});
  if(!restored){persistence.Restore();restored=true;}
 }
 static void AfterReduction(MongoId __0,bool __runOriginal){if(__runOriginal&&!MiscService.Options.UnlimitedMarketStock)persistence?.Capture(__0.ToString());}
 internal static void Publish(TemplateItem template,Draft draft,RagfairOfferGenerator generator,RagfairOfferService service){
  if(!draft.Persistent){Add(template,draft,generator,service);return;}
  (persistence??throw new ArgumentException("持久化报价后台尚未就绪，请稍候重试。")).Publish(template.Id.ToString(),draft);
 }
 internal static void Edit(string id,TemplateItem template,Draft draft,bool preserveExpiry)=>
  (persistence??throw new ArgumentException("报价后台尚未就绪，请稍候重试。")).Edit(id,template.Id.ToString(),draft,preserveExpiry);
 internal static void Manage(string operation,string id){
  var manager=persistence??throw new ArgumentException("持久化报价后台尚未就绪，请稍候重试。");
  switch(operation){case "fleaOfferDelete":manager.Delete(id);break;case "fleaOfferRecreate":manager.Recreate(id);break;case "fleaOfferWithdraw":manager.Withdraw(id);break;case "fleaOfferPersist":manager.Promote(id);break;}
 }
}
