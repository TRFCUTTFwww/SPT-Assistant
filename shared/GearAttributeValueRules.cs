using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
namespace LocalGearMail;
internal static class GearAttributeValueRules {
 public const double StorageMaximum=(double)float.MaxValue;
 static double Number(double value,string name){if(!double.IsFinite(value))throw new ArgumentException("Invalid "+name);return value;}
 public static void Apply(Upd upd,GearAttributes? attributes,JsonElement props,string templateId=""){
  if(attributes is not GearAttributes a)return;
  if(a.Durability.HasValue||a.MaxDurability.HasValue){
   upd.Repairable??=new UpdRepairable();
   double max=Number(a.MaxDurability??upd.Repairable.MaxDurability??100,"maximum durability");
   double current=Number(a.Durability??upd.Repairable.Durability??max,"durability");
   upd.Repairable.MaxDurability=max;upd.Repairable.Durability=current;
  }
  if(a.MedicalResource is double medical){
   upd.MedKit??=new UpdMedKit();
   upd.MedKit.HpResource=Number(medical,"medical resource");
  }
  if(a.FoodResource is double food){
   upd.FoodDrink??=new UpdFoodDrink();
   upd.FoodDrink.HpPercent=Number(food,"food resource");
  }
  if(a.KeyUses is int uses){
   Number(uses,"key uses");upd.Key??=new UpdKey();upd.Key.NumberOfUsages=uses;
  }
  if(a.KeyRemaining is int remaining){
   int maxUsage=props.TryGetProperty("MaximumNumberOfUsage",out var maximum)&&maximum.ValueKind==JsonValueKind.Number?maximum.GetInt32():0;
   upd.Key??=new UpdKey();upd.Key.NumberOfUsages=checked(maxUsage-remaining);
  }
  if(a.PoisonUses is double poison){
   upd.SideEffect??=new UpdSideEffect();upd.SideEffect.Value=Number(poison,"poison charges");
  }
  if(a.Resource is double resource){
   double value=Number(resource,"resource");upd.Resource??=new UpdResource{UnitsConsumed=0};upd.Resource.Value=value;
  }
  if(a.RepairKitResource is double repair){
   double value=Number(repair,"repair kit resource");upd.RepairKit??=new UpdRepairKit();upd.RepairKit.Resource=value;
  }
  if(a.ToggledOn is bool on){upd.Togglable??=new UpdTogglable();upd.Togglable.On=on;}
  if(a.FoundInRaid is bool found)upd.SpawnedInSession=found;
 }
}
