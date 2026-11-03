using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace LocalSpt5Esp;
internal enum LootCategory { 全部,武器,弹药,配件,防具装备,医疗,食物饮料,钥匙,交易材料,其他 }
internal static class ItemCatalog {
 sealed record Node(string Parent,string Name);
 static readonly Dictionary<string,Node> nodes=new();
 static readonly Dictionary<string,LootCategory> categories=new();
 static readonly Dictionary<string,HashSet<string>> ancestry=new();
 static bool loaded;
 internal static void Load(){if(loaded)return;using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","SPT_Data","database","templates","items.json")));foreach(var entry in json.RootElement.EnumerateObject())nodes[entry.Name]=new(entry.Value.TryGetProperty("_parent",out var p)?p.GetString()??"":"",entry.Value.TryGetProperty("_name",out var n)?n.GetString()??"":"");loaded=true;}
 internal static bool IsChildOf(string id,string parent){Load();if(!ancestry.TryGetValue(id,out var seen)){seen=new HashSet<string>();string current=id;while(seen.Add(current)){if(!nodes.TryGetValue(current,out var n)||n.Parent.Length==0)break;current=n.Parent;}ancestry[id]=seen;}return seen.Contains(parent);}
 internal static LootCategory Category(string id){Load();if(categories.TryGetValue(id,out var value))return value;string initial=id;var seen=new HashSet<string>();value=LootCategory.其他;while(seen.Add(id)&&nodes.TryGetValue(id,out var n)){value=n.Name switch{"Weapon"=>LootCategory.武器,"Ammo" or "AmmoBox" or "ThrowWeap"=>LootCategory.弹药,"Mod"=>LootCategory.配件,"Armor" or "ArmoredEquipment" or "ArmorPlate" or "Headwear" or "Headphones" or "Backpack" or "Vest" or "Equipment"=>LootCategory.防具装备,"Medical" or "MedKit" or "Drugs" or "Stimulator"=>LootCategory.医疗,"Food" or "Drink" or "FoodDrink"=>LootCategory.食物饮料,"KeyMechanical" or "Keycard" or "Key"=>LootCategory.钥匙,"BarterItem" or "Fuel" or "Electronics" or "Battery" or "BuildingMaterial" or "HouseholdGoods" or "Jewelry" or "Lubricant" or "MedicalSupplies" or "Tool" or "Info" or "Money"=>LootCategory.交易材料,_=>LootCategory.其他};if(value!=LootCategory.其他)break;id=n.Parent;}categories[initial]=value;return value;}
}
