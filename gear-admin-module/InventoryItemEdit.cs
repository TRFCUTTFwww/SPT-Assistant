using System.Text.Json;
using LocalSpt.Shared;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace LocalGearMail;

public static class InventoryItemEdit {
 public static Item Apply(BotBaseInventory inventory,string id,string templateId,GearAttributes? attributes,TemplateTable templates,JsonUtil json,DogtagEdit? dogtag=null){
  var item=inventory.Items?.SingleOrDefault(candidate=>candidate.Id.ToString()==id);
  if(item==null||item.Template.ToString()!=templateId)
   throw new ArgumentException("该物品已不在当前角色库存中。");
  if(!templates.Items.TryGetValue(item.Template,out var template))
   throw new ArgumentException("找不到该物品的模板。");
  var props=JsonSerializer.Deserialize<JsonElement>(json.Serialize(template)!).GetProperty("_props");
  Apply(item,attributes,props,json,SupportsToggle(template,templates),dogtag);
  return item;
 }

 static bool SupportsToggle(TemplateItem template,TemplateTable templates){
  var visited=new HashSet<string>();
  for(var parent=template.Parent;visited.Add(parent.ToString());){
   if(parent.ToString() is "5a2c3a9486f774688b05e574" // Night vision
    or "5d21f59b6dbe99052b54ef83" // Thermal vision
    or "55818b084bdc2d5b648b4571" // Flashlights
    or "55818b0e4bdc2dde698b456e" // Light/laser devices
    or "55818b164bdc2ddc698b456c")return true; // Tactical combinations
   if(!templates.Items.TryGetValue(parent,out var ancestor))break;
   parent=ancestor.Parent;
  }
  return false;
 }

 // Only publish the updated component data once every selected field has passed validation.
 public static void Apply(Item item,GearAttributes? attributes,JsonElement props,JsonUtil json,bool supportsToggle=false,DogtagEdit? dogtag=null){
  attributes??=new GearAttributes();
  if(attributes.KeyUses!=null||(dogtag==null
   &&attributes.Durability==null&&attributes.MaxDurability==null&&attributes.MedicalResource==null
   &&attributes.FoodResource==null&&attributes.KeyRemaining==null&&attributes.PoisonUses==null
   &&attributes.Resource==null&&attributes.RepairKitResource==null&&attributes.ToggledOn==null
   &&attributes.FoundInRaid==null))
   throw new ArgumentException("请至少选择一项可修改的物品参数。");

  var updated=item.Upd==null?new Upd():json.Deserialize<Upd>(json.Serialize(item.Upd)!)!;
  if(dogtag!=null){
   if(!props.TryGetProperty("DogTagQualities",out var tagProperties)||tagProperties.ValueKind!=JsonValueKind.True)
    throw new ArgumentException("只有狗牌可以修改狗牌参数。");
   var edit=dogtag.Validate();
   var tag=updated.Dogtag??=new UpdDogtag();
   if(edit.Nickname!=null)tag.Nickname=edit.Nickname;
   if(edit.Time!=null)tag.Time=edit.Time;
   if(edit.Side!=null)tag.Side=edit.Side=="Usec"?DogtagSide.Usec:DogtagSide.Bear;
   if(edit.Level is int level)tag.Level=level;
   if(edit.Status!=null)tag.Status=edit.Status;
   if(edit.WeaponName!=null)tag.WeaponName=edit.WeaponName;
  }
  // Some untouched items omit their UPD components and use the template defaults.
  if((attributes.Durability!=null||attributes.MaxDurability!=null)&&updated.Repairable==null&&Positive("MaxDurability") is double max)
   updated.Repairable=new UpdRepairable{MaxDurability=max,Durability=Positive("Durability")??max};
  if(attributes.MedicalResource!=null&&updated.MedKit==null&&Positive("MaxHpResource") is double health)
   updated.MedKit=new UpdMedKit{HpResource=health};
  if(attributes.FoodResource!=null&&updated.FoodDrink==null&&Positive("MaxResource") is double food)
   updated.FoodDrink=new UpdFoodDrink{HpPercent=food};
  if(attributes.ToggledOn!=null&&supportsToggle)updated.Togglable??=new UpdTogglable();
  GearAttributeRules.Apply(updated,attributes,props,item.Template.ToString());
  item.Upd=updated;

  double? Positive(string key)=>props.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.Number
   &&value.TryGetDouble(out double number)&&number>0?number:null;
 }
}
