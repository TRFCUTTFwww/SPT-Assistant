using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
namespace LocalGearMail;
public class GearAttributes {
 [JsonPropertyName("durability")] public double? Durability{get;set;}
 [JsonPropertyName("maxDurability")] public double? MaxDurability{get;set;}
 [JsonPropertyName("medicalResource")] public double? MedicalResource{get;set;}
 [JsonPropertyName("foodResource")] public double? FoodResource{get;set;}
 [JsonPropertyName("keyUses")] public int? KeyUses{get;set;}
 [JsonPropertyName("keyRemaining")] public int? KeyRemaining{get;set;}
 [JsonPropertyName("poisonUses")] public double? PoisonUses{get;set;}
 [JsonPropertyName("resource")] public double? Resource{get;set;}
 [JsonPropertyName("repairKitResource")] public double? RepairKitResource{get;set;}
 [JsonPropertyName("toggledOn")] public bool? ToggledOn{get;set;}
 [JsonPropertyName("foundInRaid")] public bool? FoundInRaid{get;set;}
}
public static class GearAttributeRules {
 // The client fields are Float32; this is a transport boundary, not a verified gameplay ceiling.
 public const double StorageMaximum=(double)float.MaxValue;
 static double Number(double value,double min,double max,string name){if(!double.IsFinite(value)||value<min||value>max)throw new ArgumentException("Invalid "+name);return value;}
 public static void Apply(Upd upd,GearAttributes? attributes,JsonElement props,string templateId=""){
  if(attributes is not GearAttributes a)return;
  if(a.Durability.HasValue||a.MaxDurability.HasValue){
   if(upd.Repairable==null)throw new ArgumentException("This item has no durability");
   double max=Number(a.MaxDurability??upd.Repairable.MaxDurability??0,.01,StorageMaximum,"maximum durability");
   double current=Number(a.Durability??Math.Min(upd.Repairable.Durability??max,max),0,max,"durability");
   upd.Repairable.MaxDurability=max;upd.Repairable.Durability=current;
  }
  if(a.MedicalResource is double medical){
   if(upd.MedKit==null||!props.TryGetProperty("MaxHpResource",out var cap))throw new ArgumentException("This item has no medical resource");
   upd.MedKit.HpResource=Number(medical,0,StorageMaximum,"medical resource");
  }
  if(a.FoodResource is double food){
   if(upd.FoodDrink==null||!props.TryGetProperty("MaxResource",out var cap))throw new ArgumentException("This item has no food resource");
   upd.FoodDrink.HpPercent=Number(food,0,StorageMaximum,"food resource");
  }
  if(a.KeyUses is int uses){
   if(!props.TryGetProperty("MaximumNumberOfUsage",out var cap))throw new ArgumentException("This item is not a key");
   Number(uses,0,cap.GetDouble()>0?cap.GetDouble():int.MaxValue,"key uses");upd.Key??=new UpdKey();upd.Key.NumberOfUsages=uses;
  }
  if(a.KeyRemaining is int remaining){
   if(!props.TryGetProperty("MaximumNumberOfUsage",out var maximum))throw new ArgumentException("This item is not a key");
   Number(remaining,0,int.MaxValue,"remaining key uses");upd.Key??=new UpdKey();upd.Key.NumberOfUsages=checked(maximum.GetInt32()-remaining);
  }
  if(a.PoisonUses is double poison){
   if(upd.SideEffect==null&&templateId!="5fc64ea372b0dd78d51159dc")throw new ArgumentException("This item has no poison charges");
   upd.SideEffect??=new UpdSideEffect();upd.SideEffect.Value=Number(poison,0,StorageMaximum,"poison charges");
  }
  if(a.Resource is double resource){
   if(upd.Resource==null&&!Positive(props,"Resource"))throw new ArgumentException("This item has no resource");
   double value=Number(resource,0,StorageMaximum,"resource");upd.Resource??=new UpdResource{UnitsConsumed=0};upd.Resource.Value=value;
  }
  if(a.RepairKitResource is double repair){
   if(upd.RepairKit==null&&!Positive(props,"MaxRepairResource"))throw new ArgumentException("This item is not a repair kit");
   double value=Number(repair,0,StorageMaximum,"repair kit resource");upd.RepairKit??=new UpdRepairKit();upd.RepairKit.Resource=value;
  }
  if(a.ToggledOn is bool on){if(upd.Togglable==null)throw new ArgumentException("This item has no toggle state");upd.Togglable.On=on;}
  if(a.FoundInRaid is bool found)upd.SpawnedInSession=found;
 }
 static bool Positive(JsonElement props,string name)=>props.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.Number&&value.GetDouble()>0;
}
