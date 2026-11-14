using System.Reflection;
using Path=System.IO.Path;
using System.Text.Json;
using ProjectE;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
namespace ProjectE.Server;
public record Metadata:IModMetadata {
 public string ModGuid{get;init;}="local.projecte";
 public string Name{get;init;}="ProjectE Swords";public string Author{get;init;}="Local";public List<string>? Contributors{get;init;}
 public SemanticVersioning.Version Version{get;init;}=new("1.1.0");public SemanticVersioning.Range SptVersion{get;init;}=new("~5.0.0");public bool HasPrepatcher{get;init;}public List<string>? Incompatibilities{get;init;}public Dictionary<string,SemanticVersioning.Range>? ModDependencies{get;init;}public string? Url{get;init;}public string License{get;init;}="MIT";
}
[Injectable(TypePriority=OnLoadOrder.Preload)]
public sealed class Startup(CustomItemService custom,TemplateTable templates,TradersTable traders,ISptLogger<Startup> log,SPTarkov.Server.Core.Utils.JsonUtil json):IOnLoad {
 public Task OnLoadAsync(CancellationToken cancellationToken){
  var original=json.Serialize(templates.Items[Swords.BaseTemplate]);
  var baseHandbook=(templates.Handbook?.Items ?? throw new InvalidOperationException("Handbook missing")).Single(i=>i.Id==Swords.BaseTemplate);
  var assort=traders.GetTrader("5c0647fdd443bc2504c2d371")?.Assort ?? throw new InvalidOperationException("Jaeger assort missing"); // Jaeger, LL1.
  foreach(var s in Swords.All){
   var result=custom.CreateItemFromClone(new NewItemFromCloneDetails {
    ItemTplToClone=Swords.BaseTemplate,NewId=s.Id,ParentId=Swords.KnifeParent,NewItemName="projecte_"+s.Key+"_sword",
    OverrideProperties=templates.Items[Swords.BaseTemplate].Properties! with {
     Name=s.English,ShortName=s.English,Description=s.English,ExaminedByDefault=true,
     KnifeHitSlashDam=s.Damage,KnifeHitStabDam=s.Damage,
     Unlootable=false,UnlootableFromSide=[],DiscardLimit=-1,CanSellOnRagfair=true,
     Width=1,Height=2
    },
    AddToHandbook=true,AddToFleaPriceDb=true,AddToWeaponShelf=false,HandbookParentId=baseHandbook.ParentId.ToString(),HandbookPriceRoubles=s.Price,FleaPriceRoubles=s.Price,
    Locales=new(){["en"]=new(){Name=s.English,ShortName=s.English,Description=$"ProjectE weapon. Upstream mc1.21.1 uncharged damage: 1 + {s.TierBonus} + {s.SwordBonus} = {s.Damage}. Slash and stab use this base damage; EFT armor and skills still apply."},["ch"]=new(){Name=s.Chinese,ShortName=s.Chinese,Description=$"ProjectE 独立近战武器。原模组 mc1.21.1 未充能：玩家基础 1 + 材质 {s.TierBonus} + 剑加成 {s.SwordBonus} = {s.Damage}。劈砍、刺击基础伤害均为 {s.Damage}；实际伤害仍受塔科夫护甲、技能影响。"}}
   },Assembly.GetExecutingAssembly());
   if(!result.Success)throw new InvalidOperationException(string.Join("; ",result.Errors));
   MongoId offer="a7e10000000000000000000"+(Array.IndexOf(Swords.All,s)+1);
   assort.Items.RemoveAll(i=>i.Id==offer);
   assort.Items.Add(new(){Id=offer,Template=s.Id,ParentId="hideout",SlotId="hideout",Upd=new(){StackObjectsCount=999999,UnlimitedCount=true}});
   assort.BarterScheme[offer]=[[new(){Template="5449016a4bdc2d6f028b456f",Count=s.Price}]];
   assort.LoyalLevelItems[offer]=1;
   log.Info($"ProjectE registered {s.English}: {s.Id}, slash/stab={s.Damage}");
  }
  foreach(var gadget in Gadgets.All){
   string use=gadget.Id==Gadgets.Tablet?"手持右键打开转化桌。":gadget.Id==Gadgets.Terminal?"手持右键打开连接仓库的战局 Tab；需要辅助 MOD 的战局仓库模块。":"战局中手持时抵挡一次致命伤并消耗，触发图腾动画及短暂恢复效果。放在背包或近战槽但未手持时不生效。";
   var result=custom.CreateItemFromClone(new NewItemFromCloneDetails{
    ItemTplToClone=Swords.BaseTemplate,NewId=gadget.Id,ParentId=Swords.KnifeParent,NewItemName="projecte_"+gadget.Key,
    OverrideProperties=templates.Items[Swords.BaseTemplate].Properties! with{Name=gadget.English,ShortName=gadget.English,Description=gadget.English,ExaminedByDefault=true,Width=1,Height=1,Weight=.15,KnifeHitSlashDam=0,KnifeHitStabDam=0,Unlootable=false,UnlootableFromSide=[],DiscardLimit=-1,CanSellOnRagfair=true},
    AddToHandbook=true,AddToFleaPriceDb=true,AddToWeaponShelf=false,HandbookParentId=baseHandbook.ParentId.ToString(),HandbookPriceRoubles=gadget.Price,FleaPriceRoubles=gadget.Price,
    Locales=new(){["ch"]=new(){Name=gadget.Chinese,ShortName=gadget.Chinese,Description=use+" 占用 1×1 格；装备近战槽后按近战键持握。"},["en"]=new(){Name=gadget.English,ShortName=gadget.English,Description="1x1 handheld utility. Equip in the melee slot and draw it. Tablet/terminal: right click to open. Totem: consumed to prevent one fatal injury while held."}}
   },Assembly.GetExecutingAssembly());
   if(!result.Success)throw new InvalidOperationException(string.Join("; ",result.Errors));
   MongoId offer="a7e10000000000000000010"+(Array.IndexOf(Gadgets.All,gadget)+1);
   assort.Items.RemoveAll(i=>i.Id==offer);assort.Items.Add(new(){Id=offer,Template=gadget.Id,ParentId="hideout",SlotId="hideout",Upd=new(){StackObjectsCount=999999,UnlimitedCount=true}});
   assort.BarterScheme[offer]=[[new(){Template=EmcRules.Ruble,Count=gadget.Price}]];assort.LoyalLevelItems[offer]=1;
  }
  if(json.Serialize(templates.Items[Swords.BaseTemplate])!=original)throw new InvalidOperationException("Original gladius was modified");
  foreach(var s in Swords.All){
   var p=templates.Items[s.Id].Properties!;var source=templates.Items[Swords.BaseTemplate].Properties!;
   if(p.KnifeHitSlashDam!=s.Damage||p.KnifeHitStabDam!=s.Damage||p.StackMaxSize!=1||p.Prefab?.Path!=source.Prefab?.Path||p.PrimaryDistance!=source.PrimaryDistance)throw new InvalidOperationException("Sword registration validation failed: "+s.Key);
  }
  string folder=Path.Combine(AppContext.BaseDirectory,"user","mods","ProjectE");Directory.CreateDirectory(folder);
  File.WriteAllText(Path.Combine(folder,"registration-status.json"),JsonSerializer.Serialize(new {version="1.1.0",jaeger="5c0647fdd443bc2504c2d371",serverPid=Environment.ProcessId,utc=DateTime.UtcNow,originalGladiusUnchanged=true,swords=Swords.All.Select(s=>new{s.Id,s.English,s.Chinese,s.Damage,s.Price,width=1,height=2}),utilities=Gadgets.All.Select(g=>new{g.Id,g.Key,g.English,g.Chinese,g.Price,width=1,height=1})},new JsonSerializerOptions{WriteIndented=true}));
  log.Success("ProjectE: 3 two-cell weapons and 3 one-cell handheld utilities; Jaeger LL1; original gladius unchanged");
  return Task.CompletedTask;
 }
}
