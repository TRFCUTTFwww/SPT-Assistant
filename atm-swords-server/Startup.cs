using System.Reflection;
using Path=System.IO.Path;
using System.Text.Json;
using AllTheModium;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
namespace AllTheModium.Server;
#if !SPT_ASSISTANT_SERVER
public record Metadata:IModMetadata {
 public string ModGuid{get;init;}="local.allthemodium.swords";
 public string Name{get;init;}="AllTheModium Swords";public string Author{get;init;}="Local";public List<string>? Contributors{get;init;}
 public SemanticVersioning.Version Version{get;init;}=new("1.1.0");public SemanticVersioning.Range SptVersion{get;init;}=new("~5.0.0");public bool HasPrepatcher{get;init;}public List<string>? Incompatibilities{get;init;}public Dictionary<string,SemanticVersioning.Range>? ModDependencies{get;init;}public string? Url{get;init;}public string License{get;init;}="MIT";
}
#endif
[Injectable(TypePriority=OnLoadOrder.Preload)]
public sealed class Startup(CustomItemService custom,TemplateTable templates,TradersTable traders,ISptLogger<Startup> log,SPTarkov.Server.Core.Utils.JsonUtil json):IOnLoad {
 public Task OnLoadAsync(CancellationToken cancellationToken){
  var original=json.Serialize(templates.Items[Swords.BaseTemplate]);
  var baseHandbook=(templates.Handbook?.Items ?? throw new InvalidOperationException("Handbook missing")).Single(i=>i.Id==Swords.BaseTemplate);
  var assort=traders.GetTrader("5c0647fdd443bc2504c2d371")?.Assort ?? throw new InvalidOperationException("Jaeger assort missing"); // Jaeger, LL1.
  foreach(var s in Swords.All){
   var result=custom.CreateItemFromClone(new NewItemFromCloneDetails {
    ItemTplToClone=Swords.BaseTemplate,NewId=s.Id,ParentId=Swords.KnifeParent,NewItemName="atm_"+s.Key+"_sword",
    OverrideProperties=templates.Items[Swords.BaseTemplate].Properties! with {
     Name=s.English,ShortName=s.English,Description=s.English,ExaminedByDefault=true,
     KnifeHitSlashDam=s.Damage,KnifeHitStabDam=s.Damage,
     Unlootable=false,UnlootableFromSide=[],DiscardLimit=-1,CanSellOnRagfair=true,
     Width=1,Height=2
    },
    AddToHandbook=true,AddToFleaPriceDb=true,AddToWeaponShelf=false,HandbookParentId=baseHandbook.ParentId.ToString(),HandbookPriceRoubles=s.Price,FleaPriceRoubles=s.Price,
    Locales=new(){["en"]=new(){Name=s.English,ShortName=s.English,Description=$"AllTheModium pixel sword. Upstream 1.21.x damage: 1 + {s.TierBonus} + {s.SwordBonus} = {s.Damage}. Slash and stab use this base damage; EFT armor and skills still apply."},["ch"]=new(){Name=s.Chinese,ShortName=s.Chinese,Description=$"AllTheModium 独立像素剑。原模组 1.21.x：玩家基础 1 + 材质 {s.TierBonus} + 剑加成 {s.SwordBonus} = {s.Damage}。劈砍、刺击基础伤害均为 {s.Damage}；实际伤害仍受塔科夫护甲、技能影响。"}}
   },Assembly.GetExecutingAssembly());
   if(!result.Success)throw new InvalidOperationException(string.Join("; ",result.Errors));
   MongoId offer="a7a10000000000000000000"+(Array.IndexOf(Swords.All,s)+1);
   assort.Items.RemoveAll(i=>i.Id==offer);
   assort.Items.Add(new(){Id=offer,Template=s.Id,ParentId="hideout",SlotId="hideout",Upd=new(){StackObjectsCount=999999,UnlimitedCount=true}});
   assort.BarterScheme[offer]=[[new(){Template="5449016a4bdc2d6f028b456f",Count=s.Price}]];
   assort.LoyalLevelItems[offer]=1;
   log.Info($"AllTheModium registered {s.English}: {s.Id}, slash/stab={s.Damage}");
  }
    if(json.Serialize(templates.Items[Swords.BaseTemplate])!=original)throw new InvalidOperationException("Original gladius was modified");
  foreach(var s in Swords.All){
   var p=templates.Items[s.Id].Properties!;var source=templates.Items[Swords.BaseTemplate].Properties!;
   if(p.KnifeHitSlashDam!=s.Damage||p.KnifeHitStabDam!=s.Damage||p.StackMaxSize!=1||p.Prefab?.Path!=source.Prefab?.Path||p.PrimaryDistance!=source.PrimaryDistance)throw new InvalidOperationException("Sword registration validation failed: "+s.Key);
  }
  string folder=Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","Minecraft","AllTheModium");Directory.CreateDirectory(folder);
  File.WriteAllText(Path.Combine(folder,"registration-status.json"),JsonSerializer.Serialize(new {version="1.1.0",jaeger="5c0647fdd443bc2504c2d371",serverPid=Environment.ProcessId,utc=DateTime.UtcNow,originalGladiusUnchanged=true,swords=Swords.All.Select(s=>new{s.Id,s.English,s.Chinese,s.Damage,s.Price,width=1,height=2})},new JsonSerializerOptions{WriteIndented=true}));
  log.Success("AllTheModium Swords: 4 independent melee items validated; Jaeger LL1; original gladius unchanged");
  return Task.CompletedTask;
 }
}
