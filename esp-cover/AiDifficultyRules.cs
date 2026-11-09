using System;
using System.Collections.Generic;
using System.Linq;
using LocalRaidShared;
namespace LocalSpt5Esp;
internal sealed record AiDifficultyField(string Key,string Name,string Help,float Min,float Max,string[] Paths);
internal sealed class AiDifficultyProfile {
 public Dictionary<string,float> Values {get;set;}=new();
 public bool AllowGrenades {get;set;}=true;
 public bool AllowHeadshots {get;set;}=true;
 public float Value(string key)=>Values.GetValueOrDefault(key,1);
 public AiDifficultyProfile Clone()=>new(){Values=new(Values),AllowGrenades=AllowGrenades,AllowHeadshots=AllowHeadshots};
}
internal sealed class AiDifficultySettings {
 public int Schema {get;set;}=1;
 public bool Enabled {get;set;}
 public AiDifficultyProfile Overall {get;set;}=new();
 public Dictionary<string,AiDifficultyProfile> Groups {get;set;}=new();
 public AiDifficultyProfile ForRole(string role)=>Groups.GetValueOrDefault(AiDifficultyRules.Group(role),Overall);
 public void Validate(){
  if(Schema!=1||Overall==null||Groups==null)throw new ArgumentException("AI 难度设置无法读取。");
  void Check(AiDifficultyProfile p){if(p==null||p.Values==null)throw new ArgumentException("AI 难度设置不完整。");foreach(var pair in p.Values){var f=AiDifficultyRules.Fields.FirstOrDefault(f=>f.Key==pair.Key);if(f==null||!float.IsFinite(pair.Value)||pair.Value<f.Min||pair.Value>f.Max)throw new ArgumentException("AI 难度数值超出范围。");}}
  Check(Overall);foreach(var pair in Groups){if(!AiDifficultyRules.Groups.Contains(pair.Key))throw new ArgumentException("AI 类别无法识别。");Check(pair.Value);}
 }
}
internal static class AiDifficultyRules {
 internal static readonly string[] Groups={"scav","sniper","usec","bear","boss","follower","cultist","blackDivision","raider","rogue","infected","other"};
 internal static string Group(string role)=>role.Equals("marksman",StringComparison.OrdinalIgnoreCase)?"sniper":role.StartsWith("infected",StringComparison.OrdinalIgnoreCase)?"infected":RaidSpawnRules.Faction(role);
 internal static string Name(string group)=>group switch{"scav"=>"普通 Scav","sniper"=>"狙击 Scav","infected"=>"感染者",_=>RaidSpawnRules.Name(group)};
 internal static readonly AiDifficultyField[] Fields={
  new("vision","视距","越大，看得越远。",.25f,3,new[]{"Core.VisibleDistance"}),
  new("angle","视野宽度","越大，左右两侧越容易被发现。",.5f,2,new[]{"Core.VisibleAngle"}),
  new("detection","发现目标速度","越大，确认眼前目标越快。",.25f,3,new[]{"Look.VISIBILITY_CHANGE_SPEED"}),
  new("hearing","听觉范围","越大，越容易听到远处动静。",.25f,3,new[]{"Core.HearingSense","Hearing.CLOSE_DIST","Hearing.FAR_DIST"}),
  new("reaction","初次交火延迟","越大，初次发现目标后开火越慢。",.25f,4,new[]{"Aiming.FIRST_CONTACT_ADD_SEC"}),
  new("soundDelay","听声反应时间","越大，听到声音后反应越慢。",.25f,4,new[]{"Hearing.HEAR_DELAY_WHEN_PEACE","Hearing.HEAR_DELAY_WHEN_HAVE_SMT"}),
  new("aimSpeed","瞄准收敛速度","越大，准星收稳越快。",.25f,3,new[]{"Core.AccuratySpeed","Scattering.SpeedUpAim"}),
  new("scatter","射击散布","越大，子弹越分散。",.25f,4,new[]{"Core.ScatteringPerMeter","Core.ScatteringClosePerMeter","Scattering.MinScatter","Scattering.WorkingScatter","Scattering.MaxScatter"}),
  new("recoil","后坐力","越大，连续射击时枪口越不稳定。",.25f,4,new[]{"Shoot.RECOIL_PER_METER","Shoot.MAX_RECOIL_PER_METER","Shoot.HORIZONT_RECOIL_COEF"}),
  new("shotPause","单发射击间隔","越大，两次单发射击之间停顿越久。",.25f,4,new[]{"Shoot.WAIT_NEXT_SINGLE_SHOT","Shoot.WAIT_NEXT_SINGLE_SHOT_LONG_MIN","Shoot.WAIT_NEXT_SINGLE_SHOT_LONG_MAX","Shoot.NEXT_SINGLE_SHOT_PAUSE"}),
  new("burst","连发持续时间","越大，一轮自动射击持续越久。",.25f,3,new[]{"Shoot.BASE_AUTOMATIC_TIME"}),
  new("memory","敌人记忆时间","越大，失去视线后仍会记住敌人更久。",.25f,4,new[]{"Mind.TIME_TO_FORGOR_ABOUT_ENEMY_SEC"}),
  new("cover","掩体停留时间","越大，两轮射击之间在掩体中停留越久。",.25f,4,new[]{"Core.WaitInCoverBetweenShotsSec"}),
  new("grenadePause","手雷投掷间隔","越大，尝试投掷手雷的间隔越长。",.25f,4,new[]{"Grenade.DELTA_NEXT_ATTEMPT","Grenade.DELTA_NEXT_ATTEMPT_FROM_COVER"}),
  new("damage","造成伤害","越大，命中造成的伤害越高。",.1f,3,new[]{"Core.DamageCoeff"})
 };
 internal static AiDifficultyProfile Preset(int level){
  if(level is <0 or >4)throw new ArgumentOutOfRangeException(nameof(level));
  var p=new AiDifficultyProfile();if(level==2)return p;
  float power=new[]{.55f,.8f,1,1.2f,1.5f}[level];
  foreach(var key in new[]{"vision","detection","hearing","aimSpeed","memory"})p.Values[key]=power;
  foreach(var key in new[]{"reaction","soundDelay","scatter","recoil","shotPause","grenadePause"})p.Values[key]=1/power;
  p.Values["burst"]=power;p.AllowGrenades=level!=0;p.AllowHeadshots=level>0;return p;
 }
 internal static float Scale(float baseline,float factor,string path){
  if(!float.IsFinite(baseline)||!float.IsFinite(factor)||factor<=0)throw new ArgumentException("AI 难度数值无效。");
  if(baseline<=0)return baseline;
  double result=(double)baseline*factor;
  if(path=="Core.VisibleAngle")result=Math.Clamp(result,1,360);
  if(result>float.MaxValue||result< -float.MaxValue)throw new ArgumentException("AI 难度数值超出范围。");
  return (float)result;
 }
}
