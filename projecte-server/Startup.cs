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
 public SemanticVersioning.Version Version{get;init;}=new("1.2.0");public SemanticVersioning.Range SptVersion{get;init;}=new("~5.0.0");public bool HasPrepatcher{get;init;}public List<string>? Incompatibilities{get;init;}public Dictionary<string,SemanticVersioning.Range>? ModDependencies{get;init;}public string? Url{get;init;}public string License{get;init;}="MIT";
}
[Injectable(TypePriority=OnLoadOrder.Preload)]
public sealed class Startup(CustomItemService custom,TemplateTable templates,TradersTable traders,Action<string> log,SPTarkov.Server.Core.Utils.JsonUtil json,Assembly? owner=null):IOnLoad {
 public async Task OnLoadAsync(CancellationToken cancellationToken){
  bool profilesLoaded=custom.ProfilesLoaded;
  var state=typeof(CustomItemService).GetField("<ProfilesLoaded>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)??throw new NotSupportedException("Unsupported custom-item registry version");
  if(profilesLoaded)state.SetValue(custom,false);
  try{await Register(cancellationToken);}finally{if(profilesLoaded)state.SetValue(custom,true);}
 }
 void Prepare(string id){templates.Items.Remove(id);templates.Handbook!.Items!.RemoveAll(x=>x.Id.ToString()==id);}
 Task Register(CancellationToken cancellationToken){
  var original=json.Serialize(templates.Items[Swords.BaseTemplate]);
  var baseHandbook=(templates.Handbook?.Items ?? throw new InvalidOperationException("Handbook missing")).Single(i=>i.Id==Swords.BaseTemplate);
  var assort=traders.GetTrader("5c0647fdd443bc2504c2d371")?.Assort ?? throw new InvalidOperationException("Jaeger assort missing"); // Jaeger, LL1.
  foreach(var s in Swords.All){
   Prepare(s.Id);
   var result=custom.CreateItemFromClone(new NewItemFromCloneDetails {
    ItemTplToClone=Swords.BaseTemplate,NewId=s.Id,ParentId=Swords.KnifeParent,NewItemName="projecte_"+s.Key+"_sword",
    OverrideProperties=templates.Items[Swords.BaseTemplate].Properties! with {
     Name=s.English,ShortName=s.English,Description=s.English,ExaminedByDefault=true,
     KnifeHitSlashDam=s.Damage,KnifeHitStabDam=s.Damage,
     Unlootable=false,UnlootableFromSide=[],DiscardLimit=-1,CanSellOnRagfair=true,
     Width=1,Height=2
    },
    AddToHandbook=true,AddToFleaPriceDb=true,AddToWeaponShelf=false,HandbookParentId=baseHandbook.ParentId.ToString(),HandbookPriceRoubles=s.Price,FleaPriceRoubles=s.Price,
    Locales=new(){["en"]=new(){Name=s.English,ShortName=s.English,Description=s.Key=="quantum"?"Modern Industrialization quantum sword. This port kills a local AI on melee contact; finite fallback damage is used for native calculations.":$"ProjectE weapon. Upstream mc1.21.1 uncharged damage: 1 + {s.TierBonus} + {s.SwordBonus} = {s.Damage}. Slash and stab use this base damage; EFT armor and skills still apply."},["ch"]=new(){Name=s.Chinese,ShortName=s.Chinese,Description=s.Key=="quantum"?"Modern Industrialization 量子剑。此移植在近战命中本地 AI 时直接击杀；原生计算使用有限数值兜底伤害。":$"ProjectE 独立近战武器。原模组 mc1.21.1 未充能：玩家基础 1 + 材质 {s.TierBonus} + 剑加成 {s.SwordBonus} = {s.Damage}。劈砍、刺击基础伤害均为 {s.Damage}；实际伤害仍受塔科夫护甲、技能影响。"}}
   },owner??Assembly.GetExecutingAssembly());
   if(!result.Success)throw new InvalidOperationException(string.Join("; ",result.Errors));
   MongoId offer="a7e10000000000000000000"+(Array.IndexOf(Swords.All,s)+1);
   assort.Items.RemoveAll(i=>i.Id==offer);
   assort.Items.Add(new(){Id=offer,Template=s.Id,ParentId="hideout",SlotId="hideout",Upd=new(){StackObjectsCount=999999,UnlimitedCount=true}});
   assort.BarterScheme[offer]=[[new(){Template="5449016a4bdc2d6f028b456f",Count=s.Price}]];
   assort.LoyalLevelItems[offer]=1;
   log($"ProjectE registered {s.English}: {s.Id}, slash/stab={s.Damage}");
  }
  foreach(var gadget in Gadgets.All){
   Prepare(gadget.Id);
   string baseId=Gadgets.Handheld(gadget.Id)?Swords.BaseTemplate:Gadgets.IsBag(gadget.Id)?Gadgets.BagBase:Gadgets.PassiveBase;
   var props=templates.Items[baseId].Properties! with{Name=gadget.English,ShortName=gadget.English,Description=gadget.English,ExaminedByDefault=true,Width=1,Height=1,Weight=.15,KnifeHitSlashDam=0,KnifeHitStabDam=0,Unlootable=false,UnlootableFromSide=[],DiscardLimit=-1,CanSellOnRagfair=true};
   if(Gadgets.IsBag(gadget.Id)){
    var raw=System.Text.Json.Nodes.JsonNode.Parse(json.Serialize(props)!)!;
    var grid=raw["Grids"]![0]!;grid["_id"]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("grid:"+gadget.Id))).ToLowerInvariant()[..24];grid["_parent"]=gadget.Id;
    grid["_props"]!["cellsH"]=13;grid["_props"]!["cellsV"]=8;
    grid["_props"]!["filters"]=System.Text.Json.Nodes.JsonNode.Parse("[{\"Filter\":[\"54009119af1c881c07000029\"],\"ExcludedFilter\":"+JsonSerializer.Serialize(Gadgets.BagIds)+"}]");
    T Decode<T>(string text,T sample)=>json.Deserialize<T>(text)!;
    props=Decode(raw.ToJsonString(),props);
   }
   string parent=Gadgets.Handheld(gadget.Id)?Swords.KnifeParent:Gadgets.IsBag(gadget.Id)?"5795f317245977243854e041":"57864a3d24597754843f8721";
   var book=templates.Handbook!.Items!.Single(x=>x.Id==baseId);
   var result=custom.CreateItemFromClone(new NewItemFromCloneDetails{
    ItemTplToClone=baseId,NewId=gadget.Id,ParentId=parent,NewItemName="projecte_"+gadget.Key,OverrideProperties=props,
    AddToHandbook=true,AddToFleaPriceDb=true,AddToWeaponShelf=false,HandbookParentId=book.ParentId.ToString(),HandbookPriceRoubles=gadget.Price,FleaPriceRoubles=gadget.Price,
    Locales=new(){["ch"]=new(){Name=gadget.Chinese,ShortName=gadget.Chinese,Description=Gadgets.Description(gadget.Id)},["en"]=new(){Name=gadget.English,ShortName=gadget.English,Description=Gadgets.Description(gadget.Id)}}
   },owner??Assembly.GetExecutingAssembly());
   if(!result.Success)throw new InvalidOperationException(string.Join("; ",result.Errors));
   MongoId offer="a7e1000000000000000001"+(Array.IndexOf(Gadgets.All,gadget)+1).ToString("D2");
   assort.Items.RemoveAll(i=>i.Id==offer);assort.Items.Add(new(){Id=offer,Template=gadget.Id,ParentId="hideout",SlotId="hideout",Upd=new(){StackObjectsCount=999999,UnlimitedCount=true}});
   assort.BarterScheme[offer]=[[new(){Template=EmcRules.Ruble,Count=gadget.Price}]];assort.LoyalLevelItems[offer]=1;
  }
  if(json.Serialize(templates.Items[Swords.BaseTemplate])!=original)throw new InvalidOperationException("Original gladius was modified");
  foreach(var s in Swords.All){
   var p=templates.Items[s.Id].Properties!;var source=templates.Items[Swords.BaseTemplate].Properties!;
   if(p.KnifeHitSlashDam!=s.Damage||p.KnifeHitStabDam!=s.Damage||p.StackMaxSize!=1||p.Prefab?.Path!=source.Prefab?.Path||p.PrimaryDistance!=source.PrimaryDistance)throw new InvalidOperationException("Sword registration validation failed: "+s.Key);
  }
  string folder=Path.Combine(AppContext.BaseDirectory,"user","mods","ProjectE");Directory.CreateDirectory(folder);
  File.WriteAllText(Path.Combine(folder,"registration-status.json"),JsonSerializer.Serialize(new {version="1.2.0",jaeger="5c0647fdd443bc2504c2d371",serverPid=Environment.ProcessId,utc=DateTime.UtcNow,originalGladiusUnchanged=true,swords=Swords.All.Select(s=>new{s.Id,s.English,s.Chinese,s.Damage,s.Price,width=1,height=2}),utilities=Gadgets.All.Select(g=>new{g.Id,g.Key,g.English,g.Chinese,g.Price,width=1,height=1})},new JsonSerializerOptions{WriteIndented=true}));
  new Scp3XRegistration(custom,templates,traders,json,owner??Assembly.GetExecutingAssembly()).Register();
  log("ProjectE and 3-X registered; Developer catalog; original donor templates unchanged");
  return Task.CompletedTask;
 }
}
