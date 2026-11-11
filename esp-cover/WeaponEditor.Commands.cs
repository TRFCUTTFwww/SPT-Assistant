using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
namespace LocalSpt5Esp;
internal static partial class WeaponEditor {
 internal static string[] CommandFields(WeaponTemplate template){var result=basic.ToList();foreach(string section in new[]{"WeaponRecoilSettings","WeaponAimSettings"})if(Property(template,section).GetValue(template) is object nested)result.AddRange(nested.GetType().GetProperties().Where(p=>p.CanRead&&p.CanWrite&&(p.PropertyType==typeof(float)||p.PropertyType==typeof(int))).Select(p=>section+"."+p.Name));return result.ToArray();}
 internal static void CommandTune(Player owner,Weapon gun,Dictionary<string,double> values,bool ignore,bool validateOnly=false){
  var allowed=CommandFields(gun.Template);var plans=values.Select(p=>{
   if(!allowed.Contains(p.Key))throw new ArgumentException("未知枪械参数："+p.Key+"；/item inspect 可查询可写字段。");
   var property=Resolve(gun.Template,p.Key).Property;double min=p.Key=="Velocity"?-100:p.Key is "bFirerate" or "SingleFireRate"?1:p.Key.Contains('.')?double.NegativeInfinity:0;
   double n=WeaponEditorRules.Number(p.Value.ToString("R",System.Globalization.CultureInfo.InvariantCulture),property.PropertyType==typeof(int),min);
   return(Field:new Field(p.Key,p.Key,property.PropertyType==typeof(int),min),Value:n);
  }).ToArray();
  var copy=Clone(gun.Template);foreach(var plan in plans)Set(copy,plan.Field,plan.Value);
  foreach(var plan in plans)if(Get(copy,plan.Field)!=(plan.Field.Integer?plan.Value:(float)plan.Value))throw new ArgumentException("枪械参数回读失败："+plan.Field.Path);
  if(validateOnly)return;if(owned.Count>=512&&!owned.ContainsKey(gun.Pointer))throw new ArgumentException("本局枪械参数实例已达512件上限。");worldPointer=Singleton<GameWorld>.Instance.Pointer;
  owned.TryGetValue(gun.Pointer,out var state);state??=new Owned{Gun=gun,Owner=new LiveActorReference(Singleton<GameWorld>.Instance,owner),Original=gun.Template,Command=true};
  var previous=gun.Template;var previousClone=state.Clone;bool previousIgnore=state.Ignore,previousCommand=state.Command,existed=owned.ContainsKey(gun.Pointer);
  try{state.Command=true;state.Clone=copy;state.Ignore=ignore;owned[gun.Pointer]=state;gun._Template_k__BackingField=copy;
   if(ignore)IgnoreMods(state);else RestoreMods(gun.Pointer);gun._lastModsCalculateFrame=-1;gun.RaiseRefreshEvent(true,true);owner.Physical?.MarkWeightAsDirty();
   if(owner.HandsController?.TryCast<Player.FirearmController>()?.Item?.Pointer==gun.Pointer)owner.ProceduralWeaponAnimation?.UpdateWeaponVariables();
  }catch{RestoreMods(gun.Pointer);gun._Template_k__BackingField=previous;state.Clone=previousClone;state.Ignore=previousIgnore;state.Command=previousCommand;if(!existed)owned.Remove(gun.Pointer);else if(previousIgnore)IgnoreMods(state);throw;}
 }
 internal static object CommandInspect(Weapon gun)=>new{scope="raid-instance-only",values=CommandFields(gun.Template).ToDictionary(k=>k,k=>Resolve(gun.Template,k).Property.GetValue(Resolve(gun.Template,k).Owner)),final=new{gun.ErgonomicsTotal,gun.RecoilTotal,gun.TotalVelocity,gun.FireRate}};
}
