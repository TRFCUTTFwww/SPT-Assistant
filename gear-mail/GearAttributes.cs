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
 public const double StorageMaximum=(double)float.MaxValue;
 public static void Apply(Upd upd,GearAttributes? attributes,JsonElement props,string templateId="")=>GearAttributeValueRules.Apply(upd,attributes,props,templateId);
}
