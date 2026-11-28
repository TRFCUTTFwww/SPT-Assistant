using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace LocalSpt5Esp;
internal enum LootCategory { 全部,武器,弹药,配件,防具装备,医疗,食物饮料,钥匙,交易材料,其他,任务物品 }
internal static class ItemCatalog {
 const string CashParent="543be5dd4bdc2deb348b4569",SeasonDocumentParent="6a28212a0368f4438b0d0a45";
 const string AmmoBoxParent="543be5cb4bdc2deb348b4568";
 sealed record Node(string Parent,string Name,bool QuestItem,AmmoStats? Ammo);
 internal sealed record AmmoStats(string Id,string Caliber,double Damage,double Penetration,
  double? ArmorDamage,double? Speed,double? Recoil,double Projectiles);
 internal readonly record struct AmmoNeighbors(AmmoStats? Lower,AmmoStats? Higher);
 static readonly Dictionary<string,AmmoNeighbors> ammoNeighbors=new(StringComparer.Ordinal);
 internal readonly record struct EspStyle(LootCategory Category,float? AmmoStrength=null,bool Cash=false,bool SeasonDocument=false){
  internal bool HasEstimate=>!Cash&&!SeasonDocument;
 }
 static readonly Lazy<Dictionary<string,Node>> database=new(ReadDatabase);
 static Dictionary<string,Node> nodes=>database.Value;
 static readonly Dictionary<string,LootCategory> categories=new();
 static readonly Dictionary<string,HashSet<string>> ancestry=new();
 static readonly Dictionary<string,EspStyle> styles=new();
 static bool espUnavailable;
 internal static string DatabasePath {
  get {
#if PROBE
   return Path.Combine(Environment.GetEnvironmentVariable("SPT_GAME_ROOT")!,"SPT_Runtime","SPT_Data","database","templates","items.json");
#else
   return Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","SPT_Data","database","templates","items.json");
#endif
  }
 }
 static Dictionary<string,Node> ReadDatabase(){
  const string CacheKey="LocalSpt5Esp.ItemCatalogRaw.v1";
  string dbPath=DatabasePath;
  long ticks=File.GetLastWriteTimeUtc(dbPath).Ticks;
  string stamp=dbPath+"|"+ticks;
  (string Id,string Parent,string Name,bool Quest,bool HasAmmo,string Caliber,double Dmg,double Pen,double? ArmorDmg,double? Speed,double? Recoil,double Proj)[] rawRows;
  if(AppDomain.CurrentDomain.GetData(CacheKey) is ValueTuple<string,(string,string,string,bool,bool,string,double,double,double?,double?,double?,double)[]> cached&&cached.Item1==stamp){
   rawRows=cached.Item2;
  }else{
   using var stream=File.OpenRead(dbPath);
   using var json=JsonDocument.Parse(stream);
   var list=new List<(string,string,string,bool,bool,string,double,double,double?,double?,double?,double)>(4096);
   foreach(var entry in json.RootElement.EnumerateObject()){
    var template=entry.Value;
    string parent=template.TryGetProperty("_parent",out var p)?p.GetString()??"":"";
    string name=template.TryGetProperty("_name",out var n)?n.GetString()??"":"";
    bool quest=false,hasAmmo=false;
    string cal="";double dmg=0,pen=0,proj=1;double? ad=null,spd=null,rec=null;
    if(template.TryGetProperty("_props",out var props)&&props.ValueKind==JsonValueKind.Object){
     quest=props.TryGetProperty("QuestItem",out var q)&&q.ValueKind==JsonValueKind.True;
     if(template.TryGetProperty("_type",out var type)&&type.ValueEquals("Item")&&
        props.TryGetProperty("Caliber",out var caliber)&&caliber.ValueKind==JsonValueKind.String&&
        caliber.GetString() is { Length: >0 } nameOfCaliber&&Number(props,"Damage") is double damage&&
        Number(props,"PenetrationPower") is double penetration){
      double pellets=Number(props,"buckshotBullets")??0;
      hasAmmo=true;cal=nameOfCaliber;dmg=damage;pen=penetration;
      ad=Number(props,"ArmorDamage");spd=Number(props,"InitialSpeed");rec=Number(props,"ammoRec");
      proj=pellets>0?pellets:Math.Max(1,Number(props,"ProjectileCount")??1);
     }
    }
    list.Add((entry.Name,parent,name,quest,hasAmmo,cal,dmg,pen,ad,spd,rec,proj));
   }
   rawRows=list.ToArray();
   AppDomain.CurrentDomain.SetData(CacheKey,(stamp,rawRows));
  }
  var result=new Dictionary<string,Node>(rawRows.Length,StringComparer.Ordinal);
  for(int i=0;i<rawRows.Length;i++){
   var r=rawRows[i];
   AmmoStats? ammo=r.HasAmmo?new(r.Id,r.Caliber,r.Dmg,r.Pen,r.ArmorDmg,r.Speed,r.Recoil,r.Proj):null;
   result[r.Id]=new(r.Parent,r.Name,r.Quest,ammo);
  }
  foreach(var caliber in result.Values.Select(node=>node.Ammo).OfType<AmmoStats>().GroupBy(round=>round.Caliber)){
   var tiers=caliber.OrderBy(round=>round.Penetration).ThenBy(round=>round.Damage)
    .ThenBy(round=>round.Id,StringComparer.Ordinal).GroupBy(round=>(round.Penetration,round.Damage)).ToArray();
   for(int i=0;i<tiers.Length;i++){
    var pair=new AmmoNeighbors(i>0?tiers[i-1].First():null,i+1<tiers.Length?tiers[i+1].First():null);
    foreach(var round in tiers[i])ammoNeighbors[round.Id]=pair;
   }
  }
  return result;
 }
 static double? Number(JsonElement props,string key)=>ItemDetails.TryNumber(props,key,out var value)?value:null;
 internal static AmmoStats? Ammo(string id)=>!espUnavailable&&nodes.TryGetValue(id,out var node)?node.Ammo:null;
 internal static bool IsAmmoBox(string id)=>!espUnavailable&&IsChildOf(id,AmmoBoxParent);
 internal static float Strength(AmmoStats round)=>AmmoStrength((float)round.Damage,(float)round.Penetration);
 internal static string FormatCaliber(string value){
  string code=value.StartsWith("Caliber",StringComparison.Ordinal)?value[7..]:value;
  int separator=code.IndexOf('x');
  if(separator<0)return code.EndsWith('g')?code[..^1]+" ga":code;
  string bore=code[..separator];
  if(bore is "46" or "57" or "58" or "68" or "86" or "93")bore=bore.Insert(1,".");
  else if(bore is "545" or "556" or "762" or "784" or "1143" or "127")
   bore=bore.Insert(bore is "1143" or "127"?2:1,".");
  return bore+"×"+code[(separator+1)..];
 }
 internal static AmmoNeighbors Neighbors(AmmoStats ammo)=>ammoNeighbors[ammo.Id];
 static float AmmoStrength(float damage,float penetration)=>
  .4f*Math.Clamp(damage/100f,0,1)+.6f*Math.Clamp(penetration/65f,0,1);
 internal static void Load()=>_=nodes;
 internal static void PrepareEsp(){
  try{Load();}
  catch(Exception error) when(error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException){espUnavailable=true;}
 }
 internal static EspStyle ForEsp(string id){
  if(styles.TryGetValue(id,out var style))return style;
  if(espUnavailable)return new(LootCategory.其他,Cash:HomeOverviewRules.Currency(id)>=0);
  var category=Category(id);
  bool cash=IsChildOf(id,CashParent),document=IsChildOf(id,SeasonDocumentParent);
  float? ammo=category==LootCategory.弹药&&Ammo(id) is {} round?Strength(round):null;
  return styles[id]=new(category,ammo,cash,document);
 }
 internal static bool IsChildOf(string id,string parent){Load();if(!ancestry.TryGetValue(id,out var seen)){seen=new HashSet<string>();string current=id;while(seen.Add(current)){if(!nodes.TryGetValue(current,out var n)||n.Parent.Length==0)break;current=n.Parent;}ancestry[id]=seen;}return seen.Contains(parent);}
 internal static LootCategory Category(string id){Load();if(categories.TryGetValue(id,out var value))return value;string initial=id;var seen=new HashSet<string>();value=LootCategory.其他;while(seen.Add(id)&&nodes.TryGetValue(id,out var n)){if(n.QuestItem){value=LootCategory.任务物品;break;}value=n.Name switch{"Weapon"=>LootCategory.武器,"Ammo" or "AmmoBox" or "ThrowWeap"=>LootCategory.弹药,"Mod"=>LootCategory.配件,"Armor" or "ArmoredEquipment" or "ArmorPlate" or "Headwear" or "Headphones" or "Backpack" or "Vest" or "Equipment"=>LootCategory.防具装备,"Medical" or "MedKit" or "Drugs" or "Stimulator"=>LootCategory.医疗,"Food" or "Drink" or "FoodDrink"=>LootCategory.食物饮料,"KeyMechanical" or "Keycard" or "Key"=>LootCategory.钥匙,"BarterItem" or "Fuel" or "Electronics" or "Battery" or "BuildingMaterial" or "HouseholdGoods" or "Jewelry" or "Lubricant" or "MedicalSupplies" or "Tool" or "Info" or "Money"=>LootCategory.交易材料,_=>LootCategory.其他};if(value!=LootCategory.其他)break;id=n.Parent;}categories[initial]=value;return value;}
}
