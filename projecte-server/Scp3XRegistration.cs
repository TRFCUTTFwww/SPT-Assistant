using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;
using SPTarkov.Server.Core.Utils;
using Scp3X;
namespace ProjectE.Server;
public sealed class Scp3XRegistration(CustomItemService custom,TemplateTable templates,TradersTable traders,JsonUtil json,Assembly owner) {
 public static void EnsureCatalog(TemplateTable table){
  var handbook=table.Handbook??throw new InvalidOperationException("3-X handbook unavailable");
  var book=handbook.Items!.ToList();
  var prices=table.Prices.ToDictionary(x=>x.Key,x=>x.Value);
  foreach(var entry in new[]{(DisruptorRules.Weapon,DisruptorRules.RifleCategorySource,DisruptorRules.WeaponPrice),(DisruptorRules.Magazine,DisruptorRules.DonorMagazine,DisruptorRules.MagazinePrice),(DisruptorRules.Ammo,DisruptorRules.DonorAmmo,DisruptorRules.AmmoPrice)}){
   var source=book.Single(x=>x.Id.ToString()==entry.Item2);
   book.RemoveAll(x=>x.Id.ToString()==entry.Item1);book.Add(new HandbookItem{Id=entry.Item1,ParentId=source.ParentId,Price=entry.Item3});prices[entry.Item1]=entry.Item3;
   if(!book.Any(x=>x.Id.ToString()==entry.Item1))throw new InvalidOperationException("3-X handbook ID mismatch: "+entry.Item1+" -> "+book[^1].Id.ToString());
  }
  // Publish a new record rather than mutating a getter snapshot. The live
  // server can wrap table reads while the isolated server returns raw lists.
  typeof(TemplateTable).GetProperty(nameof(TemplateTable.Handbook))!.SetValue(table,handbook with{Items=book});
  typeof(TemplateTable).GetProperty(nameof(TemplateTable.Prices))!.SetValue(table,prices);
 }
 static string Id(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("scp3x:"+value))).ToLowerInvariant()[..24];
 public void Register(){
  var originals=new[]{DisruptorRules.DonorWeapon,DisruptorRules.DonorMagazine,DisruptorRules.DonorAmmo}.ToDictionary(x=>x,x=>json.Serialize(templates.Items[x]));
  var ammo=Properties(DisruptorRules.DonorAmmo);ammo["Damage"]=800;ammo["ArmorDamage"]=100;ammo["PenetrationPower"]=100;ammo["InitialSpeed"]=2500;ammo["FragmentationChance"]=0;ammo["RicochetChance"]=0;ammo["PenetrationChanceObstacle"]=0;ammo["ShowBullet"]=false;ammo["Tracer"]=false;ammo["StackMaxSize"]=50;ammo["Weight"]=.02;
  Add(DisruptorRules.DonorAmmo,DisruptorRules.Ammo,ammo,"3-X fusion charge","3-X 聚变充能弹",DisruptorRules.AmmoPrice,"Fusion charge for the 3-X Particle Disruptor.","3-X 粒子分解器专用充能弹。");
  var mag=Properties(DisruptorRules.DonorMagazine);mag["Weight"]=.35;mag["Width"]=1;mag["Height"]=1;
  RemapSlots(mag,"Cartridges",DisruptorRules.Magazine,DisruptorRules.Ammo,DisruptorRules.Capacity-1);
  Add(DisruptorRules.DonorMagazine,DisruptorRules.Magazine,mag,"3-X fusion cell","3-X 聚变充能匣",DisruptorRules.MagazinePrice,"Four reserve charges plus one in the weapon chamber.","匣内 4 发，加上枪膛 1 发，共 5 次充能。");
  var gun=Properties(DisruptorRules.DonorWeapon);gun["Width"]=5;gun["Height"]=2;gun["Weight"]=4.5;gun["weapClass"]="assaultRifle";gun["Ergonomics"]=70;gun["RecoilForceUp"]=15;gun["RecoilForceBack"]=20;gun["RecoilCamera"]=.02;gun["CenterOfImpact"]=.001;gun["BaseMalfunctionChance"]=0;gun["DurabilityBurnRatio"]=0;gun["HeatFactorByShot"]=0;gun["AllowJam"]=false;gun["AllowMisfire"]=false;gun["AllowFeed"]=false;gun["AllowSlide"]=false;gun["defAmmo"]=DisruptorRules.Ammo;gun["defMagType"]=DisruptorRules.Magazine;
  RemapSlots(gun,"Slots",DisruptorRules.Weapon,DisruptorRules.Magazine);
  RemapSlots(gun,"Chambers",DisruptorRules.Weapon,DisruptorRules.Ammo);
  Add(DisruptorRules.DonorWeapon,DisruptorRules.Weapon,gun,"3-X Particle Disruptor","3-X 粒子分解器",DisruptorRules.WeaponPrice,"SCP 3-X Particle Disruptor. Charge before firing; middle mouse switches burst/disintegration. Confirmed kills play a body disintegration effect. Original SCP assets adapted to EFT; native inventory and ammunition transactions.","SCP 3-X 粒子分解器。开火前蓄能，鼠标中键切换连束／分解模式；确认击杀后播放尸体粒子分解效果。使用 SCP 原模型、贴图、声音与转换动作，物品和弹药由塔科夫原生库存管理。");
  var assort=traders.GetTrader("5c0647fdd443bc2504c2d371")!.Assort;
  foreach(var tpl in DisruptorRules.Templates){
   string offer=Id("offer:"+tpl);var old=TerminalStorageRules.Tree(assort.Items,offer).Select(x=>x.Id).ToHashSet();assort.Items.RemoveAll(x=>old.Contains(x.Id));
   assort.Items.Add(new(){Id=offer,Template=tpl,ParentId="hideout",SlotId="hideout",Upd=new(){StackObjectsCount=999999,UnlimitedCount=true}});
   if(tpl==DisruptorRules.Weapon){
    string magazine=Id("loaded-cell");assort.Items.Add(new(){Id=magazine,Template=DisruptorRules.Magazine,ParentId=offer,SlotId="mod_magazine"});
    assort.Items.Add(new(){Id=Id("loaded-reserve"),Template=DisruptorRules.Ammo,ParentId=magazine,SlotId="cartridges",Location=0,Upd=new(){StackObjectsCount=DisruptorRules.Capacity-1}});
    assort.Items.Add(new(){Id=Id("loaded-chamber"),Template=DisruptorRules.Ammo,ParentId=offer,SlotId="patron_in_weapon"});
   }
   assort.BarterScheme[offer]=[[new(){Template=EmcRules.Ruble,Count=tpl==DisruptorRules.Weapon?DisruptorRules.WeaponPrice:tpl==DisruptorRules.Magazine?DisruptorRules.MagazinePrice:DisruptorRules.AmmoPrice}]];assort.LoyalLevelItems[offer]=1;
  }
  foreach(var pair in originals)if(json.Serialize(templates.Items[pair.Key])!=pair.Value)throw new InvalidOperationException("3-X modified donor "+pair.Key);
  EnsureCatalog(templates);
 }
 JsonNode Properties(string donor)=>JsonNode.Parse(json.Serialize(templates.Items[donor].Properties)!)!;
 void RemapSlots(JsonNode props,string key,string parent,string filter,int? capacity=null){
  if(props[key] is not JsonArray slots)return;
  foreach(var slot in slots){slot!["_id"]=Id(parent+":"+key+":"+slot["_name"]);slot["_parent"]=parent;
   if(capacity.HasValue)slot["_max_count"]=capacity.Value;
   if(slot["_props"]?["filters"] is JsonArray filters)foreach(var f in filters)f!["Filter"]=new JsonArray(filter);
  }
 }
 void Add(string donor,string id,JsonNode props,string en,string ch,int price,string enDescription,string chDescription){
  props["Name"]=en;props["ShortName"]=en;props["Description"]=enDescription;props["ExaminedByDefault"]=true;props["CanSellOnRagfair"]=true;props["Unlootable"]=false;props["DiscardLimit"]=-1;
  templates.Items.Remove(id);templates.Handbook!.Items!.RemoveAll(x=>x.Id.ToString()==id);
  string categorySource=id==DisruptorRules.Weapon?DisruptorRules.RifleCategorySource:donor;
  var handbook=templates.Handbook.Items.Single(x=>x.Id.ToString()==categorySource);
  string parent=JsonNode.Parse(json.Serialize(templates.Items[categorySource])!)!["_parent"]!.GetValue<string>();
  var result=custom.CreateItemFromClone(new NewItemFromCloneDetails{ItemTplToClone=donor,NewId=id,ParentId=parent,NewItemName="scp3x_"+id,OverrideProperties=json.Deserialize<TemplateItemProperties>(props.ToJsonString())!,AddToHandbook=true,AddToFleaPriceDb=true,AddToWeaponShelf=false,HandbookParentId=handbook.ParentId.ToString(),HandbookPriceRoubles=price,FleaPriceRoubles=price,Locales=new(){["en"]=new(){Name=en,ShortName=id==DisruptorRules.Weapon?"3-X":en,Description=enDescription},["ch"]=new(){Name=ch,ShortName=id==DisruptorRules.Weapon?"3-X":ch,Description=chDescription}}},owner);
  if(!result.Success)throw new InvalidOperationException("3-X registration: "+string.Join("; ",result.Errors));
  // Existing server services can retain handbook entries across a late item
  // registration. Publish the current canonical entry before rebuilding traders.
  templates.Handbook.Items.RemoveAll(x=>x.Id.ToString()==id);
  templates.Handbook.Items.Add(new HandbookItem{Id=id,ParentId=handbook.ParentId,Price=price});
  templates.Prices[id]=price;
 }
}
