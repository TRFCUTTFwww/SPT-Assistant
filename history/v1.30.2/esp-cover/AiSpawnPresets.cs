using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal sealed record AiBirthPreset(float Total,float Percent,bool Paused,bool Invincible,bool Clean,int PlayerRelation,int AiRelation,string[] Buffs,EBodyPart Part,float Duration,float Strength,ActorScenePreset? Scene=null,Dictionary<string,BodyHealthPreset>? BodyHealth=null,ItemAttachmentNode? Weapon=null,Dictionary<string,ItemAttachmentNode?>? Equipment=null,Dictionary<string,string>? Appearance=null,string Name="",SceneEffectPreset[]? Effects=null,SceneLockedPose? LockedPose=null,string SceneKey="");
internal static class AiSpawnPresets {
 static AiBirthPreset? inherited;
 static ConfigEntry<string> health=null!,percent=null!,buffs=null!,duration=null!,strength=null!;
 static ConfigEntry<bool> paused=null!,invincible=null!,clean=null!;static ConfigEntry<int> playerRelation=null!,aiRelation=null!,body=null!;
 static readonly EBodyPart[] parts={EBodyPart.Head,EBodyPart.Chest,EBodyPart.Stomach,EBodyPart.LeftArm,EBodyPart.RightArm,EBodyPart.LeftLeg,EBodyPart.RightLeg};
 static readonly string[] partNames={"头部","胸部","腹部","左臂","右臂","左腿","右腿"};
 internal const float Height=1120;
 internal static void Initialize(){
  AiBodyHealth.Initialize();
  const string section="34. AI 测试";
  health=FeatureConfig.Bind(section,"Spawn total health","出生总血量（0 原生）","0");percent=FeatureConfig.Bind(section,"Spawn health percent","出生当前血量百分比","100");
  paused=FeatureConfig.Bind(section,"Spawn paused","出生后暂停行动",false);invincible=FeatureConfig.Bind(section,"Spawn invincible","出生 AI 单独无敌",false);
  clean=FeatureConfig.Bind(section,"Spawn clear effects","清除出生原有异常状态",false,"保留基础生理进程，然后添加勾选的 Buff。");
  playerRelation=FeatureConfig.Bind(section,"Spawn player relation","出生对玩家关系（0默认／1停战／2敌对）",0,"",new AcceptableValueRange<int>(0,2));
  aiRelation=FeatureConfig.Bind(section,"Spawn AI relation","出生对其他 AI（0默认／1停战／2敌对）",0,"",new AcceptableValueRange<int>(0,2));
  buffs=FeatureConfig.Bind(section,"Spawn buffs","出生 Buff 类型列表","", "由出生参数面板勾选，多项用英文逗号分隔。");
  duration=FeatureConfig.Bind(section,"Spawn buff duration","出生 Buff 持续秒数","60");strength=FeatureConfig.Bind(section,"Spawn buff strength","出生 Buff 强度","1");
  body=FeatureConfig.Bind(section,"Spawn buff body part","出生 Buff 局部作用部位",5,"",new AcceptableValueRange<int>(0,6));
 }
 static HashSet<string> Selected()=>buffs.Value.Split(new[]{',',';','，','；',' '},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
 internal static AiBirthPreset ToolPayload(ToyAction action){if(action==ToyAction.SpawnAI)return Snapshot();var selected=action==ToyAction.AddBuff?Selected():new HashSet<string>();return new(0,100,false,false,false,0,0,selected.ToArray(),parts[body.Value],selected.Count>0?SpawnRules.Number(duration.Value,.01f,float.MaxValue,"Buff 秒数"):60,selected.Count>0?SpawnRules.Number(strength.Value,.01f,float.MaxValue,"Buff 强度"):1,action==ToyAction.SetAction?AiSceneDirector.Snapshot():null,action==ToyAction.SetBodyHealth?AiBodyHealth.ToolValues():null,action==ToyAction.GiveWeapon?AiSceneEquipment.ToolWeapon:null,Effects:action==ToyAction.AddBuff?inherited?.Effects:null);}
 internal static AiBirthPreset Snapshot(){
  float total=SpawnRules.Number(health.Value,0,float.MaxValue/8,"出生总血量"),fraction=SpawnRules.Number(percent.Value,.01f,100,"当前血量百分比");var selected=Selected();
  if(selected.Any(type=>!BuffRules.Common.Any(option=>option.Type==type)))throw new ArgumentException("存在不支持的出生 Buff 类型。");
  var part=parts[body.Value];if(selected.Contains("Fracture")&&part is not (EBodyPart.LeftArm or EBodyPart.RightArm or EBodyPart.LeftLeg or EBodyPart.RightLeg))throw new ArgumentException("骨折的作用部位请选择四肢。");
  return new(total,fraction,paused.Value,invincible.Value,clean.Value,playerRelation.Value,aiRelation.Value,selected.ToArray(),part,selected.Count==0?60:SpawnRules.Number(duration.Value,1,float.MaxValue,"Buff 持续秒数"),selected.Count==0?1:SpawnRules.Number(strength.Value,.01f,float.MaxValue,"Buff 强度"),AiSceneDirector.Snapshot(),AiBodyHealth.Snapshot(),AiSceneEquipment.BirthWeapon,inherited?.Equipment,inherited?.Appearance,inherited?.Name??"",inherited?.Effects,inherited?.LockedPose);
 }
 internal static void Prepare(Profile profile,AiBirthPreset preset){
  AiSceneEquipment.Prepare(profile,preset);
  var source=profile.Health??throw new ArgumentException("AI 资料没有生命信息。");var original=source.BodyParts??throw new ArgumentException("AI 资料没有身体部位。");
  var entries=new List<(EBodyPart Part,Profile.HealthInfo.BodyPartInfo Info)>();foreach(var pair in original)if(pair.Key!=EBodyPart.Common&&pair.Value?.Health!=null)entries.Add((pair.Key,pair.Value));
  var maximum=SpawnRules.Health(entries.Select(entry=>entry.Info.Health.Maximum).ToArray(),preset.Total,preset.Percent);
  var copy=source.MemberwiseClone().Cast<Profile.HealthInfo>();var bodyParts=new Il2CppSystem.Collections.Generic.Dictionary<EBodyPart,Profile.HealthInfo.BodyPartInfo>();
  foreach(var pair in original)bodyParts[pair.Key]=pair.Value;
  for(int i=0;i<entries.Count;i++){
   var entry=entries[i];var part=entry.Info.MemberwiseClone().Cast<Profile.HealthInfo.BodyPartInfo>();var value=entry.Info.Health.MemberwiseClone().Cast<Profile.HealthInfo.ValueInfo>();
   if(preset.BodyHealth?.TryGetValue(entry.Part.ToString(),out var bodyOverride)==true){AiBodyHealth.Validate(preset.BodyHealth);maximum[i]=bodyOverride.Maximum;value.Current=bodyOverride.Current;}else value.Current=SpawnRules.Current(maximum[i],preset.Percent);
   value.Maximum=maximum[i];part.Health=value;
   if(preset.Clean)part.Effects=new Il2CppSystem.Collections.Generic.Dictionary<string,Profile.HealthInfo.EffectInfo>();bodyParts[entry.Part]=part;
  }
  copy.Energy=CopyValue(source.Energy)!;copy.Hydration=CopyValue(source.Hydration)!;copy.Temperature=CopyValue(source.Temperature)!;copy.Poison=CopyValue(source.Poison)!;
  if(bodyParts.ContainsKey(EBodyPart.Common)&&bodyParts[EBodyPart.Common]?.Health is {} common){var commonCopy=bodyParts[EBodyPart.Common].MemberwiseClone().Cast<Profile.HealthInfo.BodyPartInfo>();var value=CopyValue(common)!;value.Maximum=(float)maximum.Sum(item=>(double)item);value.Current=(float)entries.Sum(entry=>(double)bodyParts[entry.Part].Health.Current);commonCopy.Health=value;bodyParts[EBodyPart.Common]=commonCopy;}
  copy.BodyParts=bodyParts;profile.Health=copy;
 }
 internal static void Load(AiBirthPreset preset){inherited=preset;health.Value=preset.Total.ToString("G9",System.Globalization.CultureInfo.InvariantCulture);percent.Value=preset.Percent.ToString("G9",System.Globalization.CultureInfo.InvariantCulture);paused.Value=preset.Paused;invincible.Value=preset.Invincible;clean.Value=preset.Clean;playerRelation.Value=preset.PlayerRelation;aiRelation.Value=preset.AiRelation;buffs.Value=string.Join(",",preset.Buffs);body.Value=Math.Max(0,Array.IndexOf(parts,preset.Part));duration.Value=preset.Duration.ToString("G9",System.Globalization.CultureInfo.InvariantCulture);strength.Value=preset.Strength.ToString("G9",System.Globalization.CultureInfo.InvariantCulture);AiBodyHealth.Load(preset.BodyHealth);AiSceneEquipment.LoadBirth(preset.Weapon);AiSceneDirector.Load(preset.Scene??new());}
 static Profile.HealthInfo.ValueInfo? CopyValue(Profile.HealthInfo.ValueInfo? value)=>value?.MemberwiseClone().Cast<Profile.HealthInfo.ValueInfo>();
 internal static string Apply(GameWorld current,Player actor,AiBirthPreset preset){
  var failures=new List<string>();
  if(preset.Total>0)try{var actual=actor.ActiveHealthController?.GetBodyPartHealth(EBodyPart.Common,false).Maximum??0;if(Math.Abs((double)actual-preset.Total)>Math.Max(.1,preset.Total*.0001))failures.Add("最大血量回读为 "+actual+"，请求 "+preset.Total);}catch(Exception error){failures.Add("血量回读："+error.Message);}
  try{AiActorControl.ApplyBirthState(current,actor,preset.Invincible,preset.PlayerRelation,preset.AiRelation);}catch(Exception error){failures.Add("敌对／无敌："+error.Message);}
  try{if(preset.Clean)CharacterBuffs.ClearBirthEffects(actor);}catch(Exception error){failures.Add("清除状态："+error.Message);}
  foreach(var type in preset.Buffs)try{CharacterBuffs.AddBirthBuff(current,actor,type,preset.Part,preset.Duration,preset.Strength);}catch(Exception error){failures.Add(BuffRules.Name(type)+"："+error.Message);}
  if(preset.Effects!=null)foreach(var effect in preset.Effects)try{CharacterBuffs.AddBirthBuff(current,actor,effect.Type,Enum.Parse<EBodyPart>(effect.Part),effect.Seconds,effect.Strength);}catch(Exception error){failures.Add("已存状态 "+effect.Type+"："+error.Message);}
  try{AiTestControl.SetPaused(new[]{actor},preset.Paused);}catch(Exception error){failures.Add("行动状态："+error.Message);}
  try{if(preset.Scene?.Enabled==true)AiSceneDirector.Apply(current,actor,preset.Scene);}catch(Exception error){failures.Add("导演动作："+error.Message);}
  try{if(preset.LockedPose!=null)ModelPoseLock.Import(actor,preset.LockedPose);}catch(Exception error){failures.Add("已存姿势："+error.Message);}
  return string.Join("；",failures);
 }
 static void Flag(Rect rect,ConfigEntry<bool> entry,string name){if(InterfaceMenu.Button(rect,name+" · "+(entry.Value?"开":"关"),entry.Value))entry.Value=!entry.Value;}
 static void Relation(Rect rect,ConfigEntry<int> entry,string name){string[] names={"默认","停战","敌对"};if(InterfaceMenu.Button(rect,name+" · "+names[entry.Value]))entry.Value=(entry.Value+1)%3;}
 internal static void Draw(Rect rect){
  float x=rect.x,y=rect.y,w=rect.width,half=(w-12)/2;InterfaceMenu.Label(new Rect(x,y,w,30),"出生血量与状态",InterfaceMenu.Accent,18);y+=42;
  InterfaceMenu.Label(new Rect(x,y,half,24),"总血量（0 = 原生，按部位比例分配）",null,12);InterfaceMenu.Label(new Rect(x+half+12,y,half,24),"当前血量（最大血量的百分比）",null,12);y+=30;
  health.Value=InterfaceMenu.Field(new Rect(x,y,half,34),health.Value);percent.Value=InterfaceMenu.Field(new Rect(x+half+12,y,half,34),percent.Value);y+=46;
  Flag(new Rect(x,y,half,34),paused,"暂停行动");Flag(new Rect(x+half+12,y,half,34),invincible,"单独无敌");y+=46;
  Relation(new Rect(x,y,half,34),playerRelation,"对玩家");Relation(new Rect(x+half+12,y,half,34),aiRelation,"对其他 AI");y+=46;
  Flag(new Rect(x,y,w,34),clean,"清除原有异常状态后再添加所选 Buff");y+=48;
  InterfaceMenu.Label(new Rect(x,y,w,24),"出生 Buff／异常状态（可多选）",InterfaceMenu.Accent,15);y+=34;
  var selected=Selected();int columns=w>=700?4:3;float cell=(w-8*(columns-1))/columns;
  for(int i=0;i<BuffRules.Common.Length;i++){var option=BuffRules.Common[i];bool on=selected.Contains(option.Type);if(InterfaceMenu.Button(new Rect(x+i%columns*(cell+8),y+i/columns*40,cell,32),option.Name,on)){if(!selected.Remove(option.Type))selected.Add(option.Type);buffs.Value=string.Join(",",selected);}}
  y+=(float)Math.Ceiling(BuffRules.Common.Length/(double)columns)*40+10;
  InterfaceMenu.Label(new Rect(x,y,half,24),"持续秒数",null,12);InterfaceMenu.Label(new Rect(x+half+12,y,half,24),"强度倍率",null,12);y+=28;duration.Value=InterfaceMenu.Field(new Rect(x,y,half,32),duration.Value);strength.Value=InterfaceMenu.Field(new Rect(x+half+12,y,half,32),strength.Value);y+=44;
  if(InterfaceMenu.Button(new Rect(x,y,w,32),"局部状态作用部位："+partNames[body.Value]))body.Value=(body.Value+1)%parts.Length;y+=44;
  InterfaceMenu.Paragraph(new Rect(x,y,w,52),"每次生成固定一份设置，后续修改不影响排队请求。姿势／动作／路线在“导演工具 → AI 动作”设置并开启出生应用；枪械在“预设枪械”中选择。",null,12);y+=64;
  AiBodyHealth.Draw(new Rect(x,y,w,480));
  if(inherited!=null&&InterfaceMenu.Button(new Rect(x,y+480,w,30),"清除模板继承的装备／外观／状态／锁定姿势"))inherited=null;
 }
}
