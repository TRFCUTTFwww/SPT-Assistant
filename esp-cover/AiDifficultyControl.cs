using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Comfort.Common;
using EFT;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AiDifficultyControl {
 sealed record Applied(BotOwner Owner,string Profile,BotSettings Original,BotSettings Copy);
 static readonly Dictionary<IntPtr,Applied> applied=new();
 static readonly Dictionary<IntPtr,string> failed=new();
 static readonly JsonSerializerOptions json=new(){WriteIndented=true};
 static AiDifficultySettings active=new();
 static IntPtr world;
 static float nextTick;
 static string error="";
 internal static string FilePath=>Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","ai-difficulty.json");
 internal static int AppliedCount=>applied.Count;
 internal static int FailedCount=>failed.Count;
 internal static string Error=>error;
 internal static AiDifficultySettings Read(){var value=File.Exists(FilePath)?JsonSerializer.Deserialize<AiDifficultySettings>(File.ReadAllText(FilePath))??throw new ArgumentException("AI 难度设置无法读取。"):new();value.Validate();return value;}
 internal static void Initialize(){try{active=Read();}catch(Exception e){error=e.Message;Plugin.Self.Log.LogWarning("AI difficulty load: "+e);}}
 internal static void Save(AiDifficultySettings value){
  value.Validate();string content=JsonSerializer.Serialize(value,json);var snapshot=JsonSerializer.Deserialize<AiDifficultySettings>(content)!;
  Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);File.WriteAllText(FilePath+".tmp",content);if(File.Exists(FilePath))File.Copy(FilePath,FilePath+".bak",true);File.Move(FilePath+".tmp",FilePath,true);
  Restore();active=snapshot;failed.Clear();error="";nextTick=0;
 }
 static void Restore(Applied item){if(item.Owner.Settings?.Pointer==item.Copy.Pointer&&item.Owner.GetPlayer?.ProfileId==item.Profile)item.Owner.Settings=item.Original;}
 static void Restore(){foreach(var item in applied.Values)try{Restore(item);}catch(Exception e){Plugin.Self.Log.LogWarning("AI difficulty restore: "+e.Message);}applied.Clear();}
 internal static void Shutdown(){Restore();failed.Clear();world=IntPtr.Zero;nextTick=0;}
 static readonly Dictionary<string,PropertyInfo> properties=new();
 static PropertyInfo Property(Type type,string name){string key=type.FullName+"."+name;if(!properties.TryGetValue(key,out var p)){p=type.GetProperty(name,BindingFlags.Public|BindingFlags.Instance)??throw new MissingMemberException(type.FullName,name);properties.Add(key,p);}return p;}
 static (object Owner,PropertyInfo Property) Resolve(BotSettingsComponents settings,string path){var parts=path.Split('.');object owner=Property(settings.GetType(),parts[0]).GetValue(settings)??throw new InvalidOperationException(path);return(owner,Property(owner.GetType(),parts[1]));}
 static void Set(BotSettingsComponents settings,string path,object value){var field=Resolve(settings,path);field.Property.SetValue(field.Owner,value);}
 static void EnsurePrivate(BotSettings original,BotSettings copy){
  if(copy.Pointer==original.Pointer||copy.FileSettings==null||copy.FileSettings.Pointer==original.FileSettings.Pointer)throw new InvalidOperationException("AI settings copy is shared");
  foreach(string name in new[]{"Core","Look","Hearing","Aiming","Scattering","Shoot","Mind","Grenade"}){
   var property=Property(typeof(BotSettingsComponents),name);var a=property.GetValue(original.FileSettings) as Il2CppObjectBase;var b=property.GetValue(copy.FileSettings) as Il2CppObjectBase;
   if(a==null||b==null||a.Pointer==b.Pointer)throw new InvalidOperationException("AI settings component copy is shared: "+name);
  }
 }
 static BotSettings Prepare(BotSettings original,AiDifficultyProfile profile){
  var copy=original.MemberwiseClone().Cast<BotSettings>();
  var settings=original.FileSettings.MemberwiseClone().Cast<BotSettingsComponents>();copy.FileSettings=settings;
  // Native Copy shares most components. Clone every scalar component we edit.
  settings.Core=original.FileSettings.Core.MemberwiseClone().Cast<BotCoreSettings>();
  settings.Look=original.FileSettings.Look.MemberwiseClone().Cast<BotGlobalLookData>();
  settings.Hearing=original.FileSettings.Hearing.MemberwiseClone().Cast<BotGlobasHearingSettings>();
  settings.Aiming=original.FileSettings.Aiming.MemberwiseClone().Cast<BotGlobalAimingSettings>();
  settings.Scattering=original.FileSettings.Scattering.MemberwiseClone().Cast<BotGlobalsScatteringSettings>();
  settings.Shoot=original.FileSettings.Shoot.MemberwiseClone().Cast<BotGlobalShootData>();
  settings.Mind=original.FileSettings.Mind.MemberwiseClone().Cast<BotGlobalsMindSettings>();
  settings.Grenade=original.FileSettings.Grenade.MemberwiseClone().Cast<BotGlobalsGrenadeSettings>();
  EnsurePrivate(original,copy);
  foreach(var field in AiDifficultyRules.Fields)foreach(string path in field.Paths){var source=Resolve(original.FileSettings,path);float value=(float)source.Property.GetValue(source.Owner)!;Set(settings,path,AiDifficultyRules.Scale(value,profile.Value(field.Key),path));}
  if(!profile.AllowGrenades)Set(settings,"Core.CanGrenade",false);
  if(!profile.AllowHeadshots){Set(settings,"Shoot.MISS_TO_HEAD",true);Set(settings,"Look.CHECK_HEAD_ANY_DIST",false);Set(settings,"Look.MIDDLE_DIST_CAN_SHOOT_HEAD",false);}
  // Preserve live injury/location coefficients while redirecting all file reads.
  copy.Current=original.Current.MemberwiseClone().Cast<BotCurrentSettings>();
  copy.Current._FileSettings_k__BackingField=settings;
  return copy;
 }
 internal static object Diagnostics(){
  var current=Singleton<GameWorld>.Instance;var results=new List<object>();
  if(current?.AllAlivePlayersList is {} actors)for(int i=0;i<actors.Count;i++){
   var actor=actors[i];var original=actor?.AIData?.BotOwner?.Settings;
   if(actor==null||actor.IsYourPlayer||actor.HealthController?.IsAlive!=true||original?.FileSettings==null||original.Current==null)continue;
   var snapshot=AiDifficultyRules.Fields.SelectMany(f=>f.Paths).Distinct().ToDictionary(path=>path,path=>{var r=Resolve(original.FileSettings,path);return (float)r.Property.GetValue(r.Owner)!;});
   bool valid=true,unchanged=true,derived=true;int checkedFields=0;
   foreach(int level in new[]{0,2,4}){
    var profile=AiDifficultyRules.Preset(level);var copy=Prepare(original,profile);
    foreach(var field in AiDifficultyRules.Fields)foreach(string path in field.Paths){var r=Resolve(copy.FileSettings,path);float actual=(float)r.Property.GetValue(r.Owner)!;valid&=actual==AiDifficultyRules.Scale(snapshot[path],profile.Value(field.Key),path);checkedFields++;}
    foreach(var pair in snapshot){var r=Resolve(original.FileSettings,pair.Key);unchanged&=(float)r.Property.GetValue(r.Owner)! ==pair.Value;}
    derived&=copy.Current.Pointer!=original.Current.Pointer&&copy.Current.FileSettings.Pointer==copy.FileSettings.Pointer;
    derived&=Math.Abs(copy.Current.CurrentVisibleDistance-AiDifficultyRules.Scale(original.Current.CurrentVisibleDistance,profile.Value("vision"),"Core.VisibleDistance"))<.01f;
   }
   results.Add(new{role=actor.Profile?.Info?.Settings?.Role.ToString(),valid,unchanged,derived,checkedFields});if(results.Count>=3)break;
  }
  return new{enabled=active.Enabled,applied=applied.Count,failed=failed.Count,error,raid=RaidClock.Active,checks=results};
 }
 internal static void Tick(){
  if(Time.unscaledTime<nextTick)return;nextTick=Time.unscaledTime+.5f;
  var current=Singleton<GameWorld>.Instance;
  if(current==null||!RaidClock.Active){if(world!=IntPtr.Zero)Shutdown();return;}
  if(world!=current.Pointer){Shutdown();world=current.Pointer;}
  if(!active.Enabled)return;
  var seen=new HashSet<IntPtr>();var all=current.AllAlivePlayersList;if(all==null)return;
  for(int i=0;i<all.Count;i++){
   var actor=all[i];if(actor==null||actor.IsYourPlayer||actor.HealthController?.IsAlive!=true||actor.AIData?.BotOwner is not {} bot||bot.Settings?.FileSettings==null)continue;
   seen.Add(bot.Pointer);
   if(bot.Settings.Current==null)continue;
   if(applied.TryGetValue(bot.Pointer,out var existing)){
    if(existing.Profile==actor.ProfileId&&bot.Settings.Pointer==existing.Copy.Pointer)continue;
    applied.Remove(bot.Pointer);failed.Remove(bot.Pointer);
   }
   if(failed.GetValueOrDefault(bot.Pointer)==actor.ProfileId)continue;
   try{var original=bot.Settings;var copy=Prepare(original,active.ForRole(actor.Profile?.Info?.Settings?.Role.ToString()??""));bot.Settings=copy;applied[bot.Pointer]=new(bot,actor.ProfileId,original,copy);}
   catch(Exception e){failed[bot.Pointer]=actor.ProfileId;error="部分 AI 暂未应用难度设置。";Plugin.Self.Log.LogWarning("AI difficulty apply: "+e);}
  }
  foreach(var pointer in applied.Keys.Where(p=>!seen.Contains(p)).ToArray()){try{Restore(applied[pointer]);}catch{}applied.Remove(pointer);}
  foreach(var pointer in failed.Keys.Where(p=>!seen.Contains(p)).ToArray())failed.Remove(pointer);
 }
}
