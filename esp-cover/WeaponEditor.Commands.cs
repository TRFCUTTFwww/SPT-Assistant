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

 sealed class StashOwned { internal Weapon Gun=null!; internal WeaponTemplate Original=null!,Clone=null!; internal bool Ignore; }
 static readonly Dictionary<IntPtr,StashOwned> stashOwned=new();
 static readonly Dictionary<IntPtr,ModCopy> stashMods=new();
 static readonly Dictionary<IntPtr,Shared> stashShared=new();

 internal sealed record TuningField(string Path,string Name,bool Integer,double Minimum);
 internal static List<TuningField> InspectFields(WeaponTemplate template){
  var list=new List<TuningField>();
  foreach(string key in basic){
   var property=template.GetType().GetProperty(key);
   if(property?.CanRead!=true||property.CanWrite!=true||property.PropertyType!=typeof(float)&&property.PropertyType!=typeof(int))continue;
   double min=key=="Velocity"?-100:key is "bFirerate" or "SingleFireRate"?1:0;
   list.Add(new(key,labels.GetValueOrDefault(key,key),property.PropertyType==typeof(int),min));
  }
  foreach(string section in new[]{"WeaponRecoilSettings","WeaponAimSettings"})
   if(Property(template,section).GetValue(template) is object nested)
    foreach(var property in nested.GetType().GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance).Where(p=>p.CanRead&&p.CanWrite&&(p.PropertyType==typeof(float)||p.PropertyType==typeof(int))))
     list.Add(new(section+"."+property.Name,(section=="WeaponRecoilSettings"?"后座 / ":"瞄准 / ")+property.Name,property.PropertyType==typeof(int),double.NegativeInfinity));
  return list;
 }
 internal static double ReadFieldValue(WeaponTemplate template,TuningField f)=>Get(template,new Field(f.Path,f.Name,f.Integer,f.Minimum));
 internal static string FormatFinal(Weapon gun)=>Final(gun);
 internal static IReadOnlyList<WeaponTuningPreset> SavedPresets(){Load();return presets;}
 internal static string SaveTuningPreset(string rawName,string existingId,bool saveAs,string templateId,bool ignoreMods,Dictionary<string,double> values){
  Load();if(corrupt)throw new ArgumentException("请先修复参数方案文件。");
  string name=WeaponEditorRules.PresetName(rawName);
  if(values.Count==0)throw new ArgumentException("请至少勾选一个要保存的参数。");
  var preset=new WeaponTuningPreset{Id=saveAs||existingId.Length==0?Guid.NewGuid().ToString("N"):existingId,Name=name,Template=templateId,IgnoreAttachments=ignoreMods,Values=values};
  var next=presets.Where(p=>p.Id!=preset.Id).Append(preset).ToList();
  PresetStorage.Save(FilePath,next);presets.Clear();presets.AddRange(next);
  return preset.Id;
 }
 static void RestoreStashMods(IntPtr owner){
  foreach(var key in stashMods.Where(p=>p.Value.Owner==owner).Select(p=>p.Key).ToArray()){
   var copy=stashMods[key];try{if(copy.Mod?.Template?.Pointer==copy.Copy.Pointer)copy.Mod._Template_k__BackingField=copy.Original;}finally{stashMods.Remove(key);}
  }
 }
 static void ApplyStashIgnoreMods(StashOwned state){
  var seen=new HashSet<IntPtr>();
  foreach(var mod in state.Gun.Mods){
   if(mod==null)continue;seen.Add(mod.Pointer);if(stashMods.ContainsKey(mod.Pointer))continue;
   var original=mod.Template;var clone=(ModTemplate)CloneNative(original);
   clone.Recoil=0;clone.Ergonomics=0;clone.Accuracy=0;clone.Velocity=0;
   mod._Template_k__BackingField=clone;stashMods[mod.Pointer]=new(mod,original,clone,state.Gun.Pointer);
  }
  foreach(var key in stashMods.Where(p=>p.Value.Owner==state.Gun.Pointer&&!seen.Contains(p.Key)).Select(p=>p.Key).ToArray()){
   var copy=stashMods[key];if(copy.Mod?.Template?.Pointer==copy.Copy.Pointer)copy.Mod._Template_k__BackingField=copy.Original;stashMods.Remove(key);
  }
  state.Gun._lastModsCalculateFrame=-1;
 }
 internal static bool IsContextIgnoringMods(Weapon gun,bool inRaid)=>inRaid?owned.TryGetValue(gun.Pointer,out var s)&&s.Ignore:stashOwned.TryGetValue(gun.Pointer,out var st)&&st.Ignore;
 internal static void ApplyContextWeapon(Weapon gun,bool inRaid,bool asTemplate,bool ignoreMods,Dictionary<string,double> values){
  var defs=InspectFields(gun.Template).ToDictionary(f=>f.Path,f=>new Field(f.Path,f.Name,f.Integer,f.Minimum));
  if(inRaid){
   var player=Singleton<GameWorld>.Instance?.MainPlayer??throw new ArgumentException("本地玩家尚未就绪。");
   if(asTemplate){
    if(owned.Remove(gun.Pointer,out var prev)){RestoreMods(gun.Pointer);if(gun.Template.Pointer==prev.Clone.Pointer)gun._Template_k__BackingField=prev.Original;}
    var tpl=gun.Template;if(!shared.ContainsKey(tpl.Pointer))shared[tpl.Pointer]=new(tpl,Clone(tpl));
    var backup=Clone(tpl);
    try{
     foreach(var pair in values)Set(tpl,defs[pair.Key],pair.Value);
     foreach(var pair in values){double wanted=defs[pair.Key].Integer?pair.Value:(float)pair.Value;if(Get(tpl,defs[pair.Key])!=wanted)throw new InvalidOperationException("参数未能生效："+defs[pair.Key].Name);}
     gun._lastModsCalculateFrame=-1;gun.RaiseRefreshEvent(true,true);
     if(player.HandsController?.TryCast<Player.FirearmController>()?.Item?.Pointer==gun.Pointer)player.ProceduralWeaponAnimation?.UpdateWeaponVariables();
     player.Physical?.MarkWeightAsDirty();
    }catch{foreach(var f in defs.Values)Set(tpl,f,Get(backup,f));throw;}
   }else{
    CommandTune(player,gun,values,ignoreMods);
   }
   return;
  }
  if(asTemplate){
   if(stashOwned.Remove(gun.Pointer,out var prev)){RestoreStashMods(gun.Pointer);if(gun.Template.Pointer==prev.Clone.Pointer)gun._Template_k__BackingField=prev.Original;}
   var tpl=gun.Template;if(!stashShared.ContainsKey(tpl.Pointer))stashShared[tpl.Pointer]=new(tpl,Clone(tpl));
   var backup=Clone(tpl);
   try{
    foreach(var pair in values)Set(tpl,defs[pair.Key],pair.Value);
    foreach(var pair in values){double wanted=defs[pair.Key].Integer?pair.Value:(float)pair.Value;if(Get(tpl,defs[pair.Key])!=wanted)throw new InvalidOperationException("参数未能生效："+defs[pair.Key].Name);}
    gun._lastModsCalculateFrame=-1;gun.RaiseRefreshEvent(true,true);
   }catch{foreach(var f in defs.Values)Set(tpl,f,Get(backup,f));throw;}
  }else{
   if(!stashOwned.TryGetValue(gun.Pointer,out var state)){
    state=new StashOwned{Gun=gun,Original=gun.Template,Clone=Clone(gun.Template)};
    stashOwned[gun.Pointer]=state;gun._Template_k__BackingField=state.Clone;
   }
   var backup=Clone(state.Clone);
   try{
    foreach(var pair in values)Set(state.Clone,defs[pair.Key],pair.Value);
    foreach(var pair in values){double wanted=defs[pair.Key].Integer?pair.Value:(float)pair.Value;if(Get(state.Clone,defs[pair.Key])!=wanted)throw new InvalidOperationException("参数未能生效："+defs[pair.Key].Name);}
    state.Ignore=ignoreMods;if(ignoreMods)ApplyStashIgnoreMods(state);else RestoreStashMods(gun.Pointer);
    gun._lastModsCalculateFrame=-1;gun.RaiseRefreshEvent(true,true);
   }catch{foreach(var f in defs.Values)Set(state.Clone,f,Get(backup,f));throw;}
  }
 }
 internal static void RestoreContextWeapon(Weapon gun,bool inRaid,bool asTemplate){
  if(inRaid){
   if(owned.Remove(gun.Pointer,out var state)){RestoreMods(gun.Pointer);if(gun.Template.Pointer==state.Clone.Pointer)gun._Template_k__BackingField=state.Original;}
   if(asTemplate&&shared.Remove(gun.Template.Pointer,out var common)){var defs=InspectFields(common.Template);foreach(var f in defs){var fld=new Field(f.Path,f.Name,f.Integer,f.Minimum);Set(common.Template,fld,Get(common.Original,fld));}}
  }else{
   if(stashOwned.Remove(gun.Pointer,out var state)){RestoreStashMods(gun.Pointer);if(gun.Template.Pointer==state.Clone.Pointer)gun._Template_k__BackingField=state.Original;}
   if(asTemplate&&stashShared.Remove(gun.Template.Pointer,out var common)){var defs=InspectFields(common.Template);foreach(var f in defs){var fld=new Field(f.Path,f.Name,f.Integer,f.Minimum);Set(common.Template,fld,Get(common.Original,fld));}}
  }
  gun._lastModsCalculateFrame=-1;gun.RaiseRefreshEvent(true,true);
  var player=Singleton<GameWorld>.Instance?.MainPlayer;
  if(player?.HandsController?.TryCast<Player.FirearmController>()?.Item?.Pointer==gun.Pointer)player.ProceduralWeaponAnimation?.UpdateWeaponVariables();
 }
 internal static void ShutdownStash(){
  foreach(var state in stashOwned.Values.ToArray())try{RestoreStashMods(state.Gun.Pointer);if(state.Gun.Template?.Pointer==state.Clone.Pointer)state.Gun._Template_k__BackingField=state.Original;}catch{}
  stashOwned.Clear();stashMods.Clear();
  foreach(var common in stashShared.Values.ToArray())try{var defs=InspectFields(common.Template);foreach(var f in defs){var fld=new Field(f.Path,f.Name,f.Integer,f.Minimum);Set(common.Template,fld,Get(common.Original,fld));}}catch{}
  stashShared.Clear();
 }
}
