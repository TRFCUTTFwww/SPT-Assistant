using System.Reflection;
using System.Reflection.Emit;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Utils;
using System.Text.Json;
using HarmonyLib;
using SPTarkov.Server.Core.Generators.Ragfair;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Ragfair;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Utils.Cloners;

namespace LocalGearMail;

internal static partial class CustomFleaOffers {
 internal const string Marker="localAssistantCustomOffer";
 internal const long PermanentEndTime=253402300799; // Last full Unix second supported by DateTimeOffset.
 internal const string PausedMarker="localAssistantPausedSeconds";
 internal static long? PausedRemaining(RagfairOffer? offer)=>offer?.ExtensionData?.TryGetValue(PausedMarker,out var raw)==true?raw switch{long n=>n,int n=>n,JsonElement e when e.TryGetInt64(out var n)=>n,_=>null}:null;
 internal static void Pause(RagfairOffer offer,long seconds){offer.ExtensionData??=[];offer.ExtensionData[PausedMarker]=Math.Max(0,seconds);offer.EndTime=PermanentEndTime;}
 internal static bool Permanent(RagfairOffer offer)=>offer.EndTime==PermanentEndTime&&PausedRemaining(offer)==null;
 internal static RagfairOffer ProjectForClient(RagfairOffer offer)=>PausedRemaining(offer) is {} frozen?offer with{EndTime=DateTimeOffset.UtcNow.ToUnixTimeSeconds()+frozen}:offer;
 internal sealed record Draft(string PlayerName="",int UnitPrice=1,int Stock=1,int RemainingMinutes=1440,double Rating=1,double? Durability=null,double? MaxDurability=null,bool Persistent=true,bool Paused=false);
 static readonly JsonSerializerOptions Options=new(){PropertyNameCaseInsensitive=true};
 static T Dependency<T>(RagfairOfferGenerator generator)=>(T)typeof(RagfairOfferGenerator).GetFields(BindingFlags.Instance|BindingFlags.NonPublic).Single(f=>f.FieldType==typeof(T)).GetValue(generator)!;
 internal static void InstallNativeOffers(Harmony h)=>h.Patch(AccessTools.Method(typeof(RagfairOfferHolder),nameof(RagfairOfferHolder.AddOffer)),transpiler:new HarmonyMethod(typeof(CustomFleaOffers),nameof(OfferLimit)));
 static bool LimitedFakePlayer(RagfairOffer offer)=>!IsCustom(offer)&&offer.IsFakePlayerOffer();
 static IEnumerable<CodeInstruction> OfferLimit(IEnumerable<CodeInstruction> instructions){
  bool first=true;var target=AccessTools.Method(typeof(RagfairOfferExtensions),nameof(RagfairOfferExtensions.IsFakePlayerOffer));
  foreach(var instruction in instructions){if(first&&instruction.Calls(target)){first=false;instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(CustomFleaOffers),nameof(LimitedFakePlayer));}yield return instruction;}
 }
 internal static bool IsCustom(RagfairOffer offer)=>offer.ExtensionData?.ContainsKey(Marker)==true;

 internal static Draft Validate(JsonElement? raw,TemplateItem item){
  Draft d;
  try{d=raw is {} value?value.Deserialize<Draft>(Options)??throw new ArgumentException("缺少自定义报价参数。"):throw new ArgumentException("缺少自定义报价参数。");}
  catch(JsonException){throw new ArgumentException("自定义报价参数格式无效。");}
  d=d with{PlayerName=(d.PlayerName??"").Trim()};
  if(d.PlayerName.Length is <1 or >32||d.PlayerName.Any(char.IsControl))throw new ArgumentException("玩家名需为 1～32 个字符，且不能包含控制字符。");
  if(!double.IsFinite(d.Rating))throw new ArgumentException("市场信誉请输入有限数值。");
  double limit=item.Properties?.MaxDurability??0;
  if(d.Durability.HasValue!=d.MaxDurability.HasValue)throw new ArgumentException("请同时填写当前耐久和最大耐久，或同时留空。");
  if(d.Durability is {} current&&d.MaxDurability is {} maximum){
   if(limit<=0)throw new ArgumentException("该物品没有耐久组件，请清空耐久字段。");
   if(!double.IsFinite(current)||!double.IsFinite(maximum))
    throw new ArgumentException("耐久请输入有限数值。");
  }
  if(d.Paused&&d.RemainingMinutes<0)throw new ArgumentException("暂停剩余时间不能使用负数（负数表示永久）；请填写非负分钟数或关闭暂停。");
  return d;
 }

 internal static RagfairOffer Add(TemplateItem template,Draft draft,RagfairOfferGenerator generator,RagfairOfferService service){var offer=Create(template,draft,generator);service.AddOffer(offer);return offer;}
 internal static RagfairOffer Create(TemplateItem template,Draft draft,RagfairOfferGenerator generator,RagfairOffer? source=null){
  var preset=Dependency<PresetHelper>(generator).GetDefaultPreset(template.Id);
  var items=source?.Items?.Count>0&&source.Items.First(i=>i.Id==source.Root).Template==template.Id?Dependency<ICloner>(generator).Clone(source.Items)!:preset?.Items?.Count>0?Dependency<ICloner>(generator).Clone(preset.Items)!:new List<Item>{new(){Id=new MongoId(),Template=template.Id}};
  var ids=items.ToDictionary(i=>i.Id.ToString(),_=>new MongoId());
  foreach(var item in items){
   item.Id=ids[item.Id.ToString()];
   if(item.ParentId is {} parent&&ids.TryGetValue(parent,out var newParent))item.ParentId=newParent.ToString();
  }
  var root=items.First(i=>i.Template==template.Id&&(i.ParentId==null||!ids.Values.Any(id=>id.ToString()==i.ParentId)));
  items.Remove(root);items.Insert(0,root);root.ParentId="hideout";root.SlotId="hideout";root.Location=null;
  root.Upd??=new();root.Upd.StackObjectsCount=draft.Stock;root.Upd.UnlimitedCount=false;
  if(root.Upd.Repairable==null&&template.Properties?.MaxDurability is >0 and var maximum)root.Upd.Repairable=new(){Durability=maximum,MaxDurability=maximum};
  // Build a native fake-player offer before registering it, so the holder sees
  // its final stock, price and expiry from the first observable moment.
  var details=new CreateFleaOfferDetails{UserId=new MongoId(),Time=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),Items=items,
   BarterScheme=[new(){Template=new MongoId("5449016a4bdc2d6f028b456f"),Count=draft.UnitPrice}],Quantity=draft.Stock,Creator=OfferCreator.FakePlayer,SellInOnePiece=false,LoyalLevel=1};
  RagfairOffer offer;
  try{offer=(RagfairOffer)AccessTools.Method(typeof(RagfairOfferGenerator),"CreateOffer").Invoke(generator,[details])!;}
  catch(TargetInvocationException error) when(error.InnerException!=null){throw new ArgumentException("无法生成该物品报价："+error.InnerException.Message);}
  Apply(offer,draft);
  return offer;
 }

 internal static void Apply(RagfairOffer offer,Draft draft){
  offer.CreatedBy=OfferCreator.FakePlayer;
  offer.User.Nickname=draft.PlayerName;offer.User.Rating=draft.Rating;offer.User.IsRatingGrowing=true;
  offer.User.MemberType=MemberCategory.Default;offer.User.SelectedMemberCategory=MemberCategory.Default;
  offer.StartTime=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
  offer.EndTime=draft.RemainingMinutes<0?PermanentEndTime:offer.StartTime+draft.RemainingMinutes*60L;
  offer.ExtensionData?.Remove(PausedMarker);
  if(draft.Paused&&draft.RemainingMinutes>=0)Pause(offer,draft.RemainingMinutes*60L);
  offer.Quantity=draft.Stock;offer.UnlimitedCount=false;offer.SellInOnePiece=false;
  offer.Requirements=[new(){TemplateId=new MongoId("5449016a4bdc2d6f028b456f"),Count=draft.UnitPrice}];
  offer.RequirementsCost=draft.UnitPrice;offer.SummaryCost=draft.UnitPrice;
  var root=offer.Items!.First(i=>i.Id==offer.Root);
  root.Upd??=new();root.Upd.StackObjectsCount=draft.Stock;root.Upd.UnlimitedCount=false;
  if(draft.Durability is {} current&&draft.MaxDurability is {} maximum)root.Upd.Repairable=new(){Durability=current,MaxDurability=maximum};
  offer.ExtensionData??=[];offer.ExtensionData[Marker]=true;
 }

 internal static object Describe(RagfairOffer original){
  var o=MiscService.Options.UnlimitedMarketStock?MarketSupply.Copy(original):original;
  var root=o.Items!.First(i=>i.Id==o.Root);
  long? paused=PausedRemaining(o);long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
  return (object)new{id=o.Id.ToString(),template=root.Template.ToString(),playerName=o.User.Nickname,unitPrice=o.RequirementsCost,stock=o.Quantity,
   endTime=paused is {} seconds?now+seconds:o.EndTime,rating=o.User.Rating,durability=root.Upd?.Repairable?.Durability,maxDurability=root.Upd?.Repairable?.MaxDurability,persistent=false,active=true,status="active",permanent=paused==null&&Permanent(o),pausedRemainingSeconds=paused};
 }
 internal static object[] List(RagfairOfferService? service)=>persistence?.List()??service?.GetOffers().Where(IsCustom).Where(o=>o.EndTime>DateTimeOffset.UtcNow.ToUnixTimeSeconds()).Select(Describe).ToArray()??[];
}
