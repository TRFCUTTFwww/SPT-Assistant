using System.Reflection;
using LocalSpt.Shared;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Utils;

namespace LocalGearMail;

public static class OnlineTradingService {
 const string Rub="5449016a4bdc2d6f028b456f";
 const string Usd="5696686a4bdc2da3298b456a";
 const string Eur="569668774bdc2da2298b4568";

 static readonly FieldInfo taxField=Field<RagfairTaxService>();
 static readonly FieldInfo offerServiceField=Field<RagfairOfferService>();
 static readonly FieldInfo configField=Field<RagfairConfig>();
 static readonly FieldInfo globalField=Field<GlobalTable>();
 static readonly MethodInfo calcRubMethod=typeof(RagfairController).GetMethod("CalculateRequirementsPriceInRub",BindingFlags.Instance|BindingFlags.NonPublic)!;
 static readonly MethodInfo createOfferMethod=typeof(RagfairController).GetMethod("CreatePlayerOffer",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!;

 static FieldInfo Field<T>()=>typeof(RagfairController).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(T));

 static bool ValidCurrency(string currency)=>currency is Rub or Usd or Eur;

 static int MaxOffers(RagfairController controller,PmcData pmc){
  if(MiscService.Options.FreeTrade)return int.MaxValue;
  var globals=(GlobalTable)globalField.GetValue(controller)!;
  double rating=pmc.RagfairInfo?.Rating??0;
  var entries=globals.Configuration?.RagFair?.MaxActiveOfferCount;
  if(entries==null||entries.Count==0)return 2;
  foreach(var entry in entries)if(rating>=entry.From&&rating<=entry.To)return entry.Count;
  return entries.Last().Count;
 }

 static double CalculateTax(RagfairController controller,Item root,PmcData pmc,string currency,double unitPrice,int count,bool sellInOnePiece){
  var settings=(RagfairConfig)configField.GetValue(controller)!;
  if(MiscService.Options.FreeFlea||settings.OfferListingTaxMultiplier==0||settings.Sell?.Fees==false)return 0;
  if(!double.IsFinite(unitPrice)||unitPrice<1)return 0;
  var reqs=new List<Requirement>{new(){Template=new MongoId(currency),Count=unitPrice,OnlyFunctional=false}};
  double rubPerUnit=(double)calcRubMethod.Invoke(controller,[reqs])!;
  double totalRub=sellInOnePiece?rubPerUnit:rubPerUnit*Math.Max(1,count);
  var taxService=(RagfairTaxService)taxField.GetValue(controller)!;
  double raw=taxService.CalculateTax(root,pmc,totalRub,Math.Max(1,count),sellInOnePiece);
  return double.IsFinite(raw)&&raw>0?Math.Ceiling(raw):0;
 }

 static List<Item> ParseAndValidate(CharacterDataRequest request,JsonUtil json,TemplateTable templates,out Item root,out int stack){
  var raw=request.StashItems??throw new ArgumentException("缺少物品数据。");
  var normalized=RaidStashJson.Normalize(raw.GetRawText(),out _,out _);
  var items=json.Deserialize<List<Item>>(normalized.ToJsonString())??throw new ArgumentException("物品格式无效。");
  if(items.Count==0)throw new ArgumentException("物品数据为空。");
  root=items[0];
  if(!templates.Items.TryGetValue(root.Template,out var tpl)||tpl.Properties?.QuestItem==true)
   throw new ArgumentException("任务物品或未知物品无法上架跳蚤市场。");
  if(FleaPrices.HasBlockedItem(items))throw new ArgumentException(FleaPrices.ListingBlocked);
  stack=Math.Max(1,(int)(root.Upd?.StackObjectsCount??1));
  return items;
 }

 static List<Item> RemapIds(List<Item> items){
  var map=new Dictionary<string,MongoId>(items.Count,StringComparer.Ordinal);
  foreach(var item in items)map[item.Id.ToString()]=new MongoId();
  var remapped=new List<Item>(items.Count);
  foreach(var item in items){
   var copy=item with{Id=map[item.Id.ToString()]};
   if(!string.IsNullOrEmpty(copy.ParentId)&&map.TryGetValue(copy.ParentId,out var mappedParent))
    copy.ParentId=mappedParent.ToString();
   remapped.Add(copy);
  }
  return remapped;
 }

 internal static long CountRubInStash(PmcData pmc){
  var items=pmc.Inventory?.Items;
  if(items==null)return 0;
  long available=0;
  foreach(var item in items){
   if(item.Template.ToString()!=Rub)continue;
   long count=(long)(item.Upd?.StackObjectsCount??1);
   if(count>0)available+=count;
  }
  return available;
 }

 internal static long DeductRubFromStash(PmcData pmc,long amount,string purpose="跳蚤手续费"){
  var items=pmc.Inventory?.Items??throw new ArgumentException("仓库尚未就绪。");
  long available=0;
  var stacks=new List<Item>();
  foreach(var item in items){
   if(item.Template.ToString()!=Rub)continue;
   long count=(long)(item.Upd?.StackObjectsCount??1);
   if(count<=0)continue;
   available+=count;
   stacks.Add(item);
  }
  if(amount<=0)return available;
  if(available<amount)throw new ArgumentException($"仓库卢布不足以支付{purpose}（需要 {amount:N0} ₽，当前 {available:N0} ₽）。");
  long remaining=amount;
  foreach(var stack in stacks){
   if(remaining<=0)break;
   long count=(long)(stack.Upd?.StackObjectsCount??1);
   if(count<=remaining){
    remaining-=count;
    items.Remove(stack);
   }else{
    stack.Upd??=new Upd();
    stack.Upd.StackObjectsCount=count-remaining;
    remaining=0;
   }
  }
  return available-amount;
 }

 public static async ValueTask<string> Handle(CharacterDataRequest request,MongoId session,SptProfile profile,SaveServer saves,JsonUtil json,HttpResponseUtil http,TemplateTable templates,CancellationToken ct){
  if(!MiscService.Options.OnlineTrading)throw new ArgumentException("请先在杂项设置中开启「线上交易」。");
  var controller=FleaAutomation.Controller??throw new ArgumentException("跳蚤控制器尚未就绪，请先打开一次跳蚤市场或等待后台刷新。");
  var pmc=profile.CharacterData?.PmcData??throw new ArgumentException("当前角色尚未创建。");
  var items=ParseAndValidate(request,json,templates,out var root,out int stack);
  string currency=ValidCurrency(request.Currency)?request.Currency:Rub;
  int count=request.Count>0?Math.Clamp(request.Count,1,stack):stack;
  if(items.Count>1)count=stack;
  double price=double.IsFinite(request.Value)&&request.Value>=1?Math.Truncate(request.Value):0;
  int activeOffers=pmc.RagfairInfo?.Offers?.Count??0;
  int maxOffers=MaxOffers(controller,pmc);
  double tax=CalculateTax(controller,root,pmc,currency,price,count,request.SellInOnePiece);

  if(request.Action=="onlineOfferQuote"){
   var market=controller.GetItemMinAvgMaxFleaPriceValues(new GetMarketPriceRequestData{TemplateId=root.Template},false);
   return http.GetBody(new{
    ok=true,
    min=market.Min,
    avg=market.Avg??0,
    max=market.Max,
    tax,
    stack,
    hasChildren=items.Count>1,
    activeOffers,
    maxOffers,
    freeTrade=MiscService.Options.FreeTrade,
    freeFlea=MiscService.Options.FreeFlea
   });
  }

  if(price<1)throw new ArgumentException("请输入有效的报价金额（至少为 1）。");
  if(!ValidCurrency(request.Currency))throw new ArgumentException("不支持的货币类型。");
  if(items.Count>1&&count!=stack)throw new ArgumentException("包含子配件或内部物品的容器必须整件上架。");
  if(!MiscService.Options.FreeTrade&&activeOffers>=maxOffers)
   throw new ArgumentException($"已达到当前跳蚤信誉的最大挂单上限（{activeOffers}/{maxOffers}）。");

  var cloned=RemapIds(items);
  var offerRoot=cloned[0];
  offerRoot.Upd??=new Upd();
  if(items.Count==1)offerRoot.Upd.StackObjectsCount=count;

  long taxCost=(long)Math.Ceiling(tax);
  if(taxCost>0)DeductRubFromStash(pmc,taxCost);

  var requirements=new List<Requirement>{new(){Template=new MongoId(currency),Count=price,OnlyFunctional=false}};
  var offer=(RagfairOffer)createOfferMethod.Invoke(controller,[session,requirements,cloned,request.SellInOnePiece])!;
  pmc.RagfairInfo??=new RagfairInfo();
  pmc.RagfairInfo.Offers??=[];
  if(!pmc.RagfairInfo.Offers.Any(o=>o.Id==offer.Id))pmc.RagfairInfo.Offers.Add(offer);
  var offerService=(RagfairOfferService)offerServiceField.GetValue(controller)!;
  if(!offerService.DoesOfferExist(offer.Id))offerService.AddOffer(offer);

  bool instantSold=MiscService.Options.InstantFleaSales;
  if(instantSold){
   int amount=offer.SellInOnePiece==true?1:offer.Quantity;
   if(amount>0)FleaAutomation.CompleteAsAssistant(controller,session,offer,amount);
  }

  await saves.SaveProfileAsync(session);
  return http.GetBody(new{ok=true,offerId=offer.Id.ToString(),tax=taxCost,instantSold});
 }
}
