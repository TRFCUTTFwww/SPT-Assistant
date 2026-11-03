using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal sealed class ItemComponentParameters {
 public ItemComponentParameters(){}
 public float? Durability{get;set;}
 public float? MaxDurability{get;set;}
 public float? MedicalResource{get;set;}
 public float? FoodResource{get;set;}
 public int? KeyRemaining{get;set;}
 public float? PoisonUses{get;set;}
 public float? Resource{get;set;}
 public float? RepairKitResource{get;set;}
 public bool? ToggledOn{get;set;}
 public bool? FoundInRaid{get;set;}
 // Storage boundary only; native effect ceilings are verified by reading back writes.
 internal const float StorageMaximum=float.MaxValue;
 internal Dictionary<string,object?> Attributes(){var result=new Dictionary<string,object?>();void Add(string key,object? value){if(value!=null)result[key]=value;}Add("durability",Durability);Add("maxDurability",MaxDurability);Add("medicalResource",MedicalResource);Add("foodResource",FoodResource);Add("keyRemaining",KeyRemaining);Add("poisonUses",PoisonUses);Add("resource",Resource);Add("repairKitResource",RepairKitResource);Add("toggledOn",ToggledOn);Add("foundInRaid",FoundInRaid);return result;}
 internal void Apply(Item item,bool? inheritedFir=null){
  if(Durability.HasValue||MaxDurability.HasValue){var component=item.GetItemComponent<RepairableComponent>()??throw new ArgumentException("物品没有耐久属性。");float maximum=MaxDurability??component.MaxDurability;float current=Durability??Math.Min(component.Durability,maximum);Check(maximum,.01f,StorageMaximum);Check(current,0,maximum);component.MaxDurability=maximum;component.Durability=current;}
  if(MedicalResource is float medical){Check(medical,0,StorageMaximum);(item.GetItemComponent<MedKitComponent>()??throw new ArgumentException("物品没有医疗使用量。")).HpResource=medical;}
  if(FoodResource is float food){Check(food,0,StorageMaximum);(item.GetItemComponent<FoodDrinkComponent>()??throw new ArgumentException("物品没有食物使用量。")).HpPercent=food;}
  if(KeyRemaining is int remaining){if(remaining<0||remaining>int.MaxValue)throw new ArgumentException("钥匙剩余次数超出范围。");var key=item.GetItemComponent<KeyComponent>()??throw new ArgumentException("物品不是钥匙。");key.NumberOfUsages=checked(key.Template.MaximumNumberOfUsage-remaining);}
  if(PoisonUses is float poison){Check(poison,0,StorageMaximum);(item.GetItemComponent<SideEffectComponent>()??throw new ArgumentException("物品没有毒素余量。")).Value=poison;}
  if(Resource is float resource){Check(resource,0,StorageMaximum);(item.GetItemComponent<ResourceComponent>()??throw new ArgumentException("物品没有资源余量。")).Value=resource;}
  if(RepairKitResource is float repair){Check(repair,0,StorageMaximum);(item.GetItemComponent<RepairKitComponent>()??throw new ArgumentException("物品不是维修工具。")).Resource=repair;}
  if(ToggledOn is bool toggled){var component=item.GetItemComponent<TogglableComponent>()??item.GetItemComponent<NightVisionComponent>()?.Togglable??throw new ArgumentException("物品没有开关状态。");component.On=toggled;}
  bool? fir=FoundInRaid??inheritedFir;if(fir.HasValue)item.SpawnedInSession=fir.Value;
 }
 static void Check(float value,float min,float max){if(!float.IsFinite(value)||value<min||value>max)throw new ArgumentException("物品参数超出允许的数值范围。");}
}
internal sealed class ItemComponentEditor {
 readonly List<(string Key,string Name,float Default,bool Integer)> fields=new();
 readonly Dictionary<string,string> draft=new();
 readonly HashSet<string> enabled=new();
 bool canToggle;
 readonly bool showFir;
 int toggle,fir;
 internal ItemComponentEditor(Item sample,ItemComponentParameters settings,bool includeBasic=true){
  showFir=includeBasic;
  void Add(string key,string name,float value,bool integer=false){fields.Add((key,name,value,integer));draft[key]=value.ToString("0.##",CultureInfo.InvariantCulture);}
  var repair=sample.GetItemComponent<RepairableComponent>();if(includeBasic&&repair!=null){Add("durability","当前耐久",repair.Durability);Add("maxDurability","最大耐久",repair.MaxDurability);}
  var med=sample.GetItemComponent<MedKitComponent>();if(includeBasic&&med!=null&&med.MaxHpResource>0)Add("medicalResource","医疗剩余使用量",med.HpResource);
  var food=sample.GetItemComponent<FoodDrinkComponent>();if(includeBasic&&food!=null&&food.MaxResource>0)Add("foodResource","食物剩余使用量",food.HpPercent);
  var key=sample.GetItemComponent<KeyComponent>();if(includeBasic&&key?.Template.MaximumNumberOfUsage>0)Add("keyRemaining","钥匙剩余次数",(float)((long)key.Template.MaximumNumberOfUsage-key.NumberOfUsages),true);
  var poison=sample.GetItemComponent<SideEffectComponent>();if(poison!=null)Add("poisonUses","剩余毒素",poison.Value);
  var resource=sample.GetItemComponent<ResourceComponent>();if(resource!=null)Add("resource","剩余资源",resource.Value);
  var kit=sample.GetItemComponent<RepairKitComponent>();if(kit!=null)Add("repairKitResource","维修工具余量",kit.Resource);
  canToggle=sample.GetItemComponent<TogglableComponent>()!=null||sample.GetItemComponent<NightVisionComponent>()!=null;
  foreach(var pair in settings.Attributes())if(draft.ContainsKey(pair.Key)){enabled.Add(pair.Key);draft[pair.Key]=Convert.ToString(pair.Value,CultureInfo.InvariantCulture)??"0";}
  toggle=settings.ToggledOn is bool on?on?1:2:0;fir=settings.FoundInRaid is bool found?found?1:2:0;
 }
 internal bool HasOptions=>fields.Count>0||canToggle||showFir;
 internal float Height=>52+fields.Sum(option=>enabled.Contains(option.Key)?77:39)+(canToggle?43:0)+(showFir?43:0);
 internal void Draw(float width,float startY=0){
  float y=startY;InterfaceMenu.Paragraph(new Rect(0,y,width,42),"只修改勾选的参数；浮点数存储边界不代表游戏有效上限，实际写入后回读。",null,12);y+=48;
  foreach(var field in fields){bool on=enabled.Contains(field.Key);if(InterfaceMenu.Button(new Rect(0,y,width,32),field.Name+(on?" · 修改":" · 默认"),on)){if(!enabled.Remove(field.Key))enabled.Add(field.Key);}y+=39;
   if(on){draft[field.Key]=InterfaceMenu.Field(new Rect(0,y,width,31),draft[field.Key]);y+=38;}
  }
  if(canToggle){if(InterfaceMenu.Button(new Rect(0,y,width,32),toggle==0?"开关状态 · 默认":toggle==1?"开关状态 · 开启":"开关状态 · 关闭",toggle!=0))toggle=(toggle+1)%3;y+=43;}
  if(showFir&&InterfaceMenu.Button(new Rect(0,y,width,32),fir==0?"搜刮标记 · 默认":fir==1?"搜刮标记 · 有":"搜刮标记 · 无",fir!=0))fir=(fir+1)%3;
 }
 internal ItemComponentParameters Read(){
  var result=new ItemComponentParameters{ToggledOn=toggle==0?null:toggle==1,FoundInRaid=fir==0?null:fir==1};
  foreach(var field in fields)if(enabled.Contains(field.Key)){
   if(field.Integer){result.KeyRemaining=ItemSpawnRules.Integer(draft[field.Key],0,int.MaxValue,field.Name);continue;}
   float value=ItemSpawnRules.Number(draft[field.Key],field.Key=="maxDurability"?.01f:0,ItemComponentParameters.StorageMaximum,field.Name);
   switch(field.Key){case "durability":result.Durability=value;break;case "maxDurability":result.MaxDurability=value;break;case "medicalResource":result.MedicalResource=value;break;case "foodResource":result.FoodResource=value;break;case "keyRemaining":result.KeyRemaining=(int)value;break;case "poisonUses":result.PoisonUses=value;break;case "resource":result.Resource=value;break;case "repairKitResource":result.RepairKitResource=value;break;}
  }
  if(result.Durability is float current&&result.MaxDurability is float maximum&&current>maximum)throw new ArgumentException("当前耐久不能大于设定的最大耐久。");return result;
 }
}
