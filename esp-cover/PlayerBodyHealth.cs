using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.UI;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class PlayerBodyHealth {
 static readonly string[] vitals={"Energy","Hydration"};
 static readonly string[] labels={"头部","胸部","腹部","左臂","右臂","左腿","右腿"};
 static readonly Dictionary<string,(string Maximum,string Current)> drafts=new();
 static string owner="",requestOwner="",notice="";static Task<string>? request;static Vector2 scroll;static bool saving;
 static Player? Live=>Singleton<GameWorld>.Instance?.MainPlayer is {} p&&p.IsYourPlayer&&p.HealthController?.IsAlive==true?p:null;
 static Profile? Profile=>Live?.Profile??ItemUiContext.Instance?._profile;
 static void ReadLive(Player player){drafts.Clear();foreach(var entry in AiBodyHealth.Capture(player))drafts[entry.Key]=(entry.Value.Maximum.ToString("G9",CultureInfo.InvariantCulture),entry.Value.Current.ToString("G9",CultureInfo.InvariantCulture));foreach(string key in vitals){var v=key=="Energy"?player.ActiveHealthController.Energy:player.ActiveHealthController.Hydration;drafts[key]=(v.Maximum.ToString("G9",CultureInfo.InvariantCulture),v.Current.ToString("G9",CultureInfo.InvariantCulture));}owner=player.ProfileId;notice="已读取当前各部位生命值。";}
 static void Begin(bool save){if(request!=null)return;saving=save;var payload=new Dictionary<string,object?>{{"action",save?"bodyHealth":"healthStatus"}};
  if(save){var values=Values();var all=values.ToDictionary(x=>x.Key,x=>(object)new{maximum=x.Value.Maximum,current=x.Value.Current});foreach(var pair in VitalValues())all[pair.Key]=new{maximum=pair.Value.Maximum,current=pair.Value.Current};payload["settings"]=all;}
  requestOwner=owner;request=CharacterDataPanel.Call("/local/gear/data","characterData",payload);notice=save?"正在保存…":"正在读取…";
 }
 static Dictionary<string,BodyHealthPreset> Values(){var result=new Dictionary<string,BodyHealthPreset>();foreach(var part in AiBodyHealth.Parts){string key=part.ToString();var value=drafts[key];result[key]=new(ItemSpawnRules.Number(value.Maximum,.01f,100000000,"最大血量"),ItemSpawnRules.Number(value.Current,0,100000000,"当前血量"));}AiBodyHealth.Validate(result);return result;}
 static Dictionary<string,BodyHealthPreset> VitalValues(){var result=new Dictionary<string,BodyHealthPreset>();foreach(string key in vitals){var d=drafts[key];float maximum=ItemSpawnRules.Number(d.Maximum,.01f,100000000,"最大值"),current=ItemSpawnRules.Number(d.Current,0,100000000,"当前值");if(current>maximum)throw new ArgumentException("能量和水分的当前值不能超过最大值。");result[key]=new(maximum,current);}return result;}
 static void Complete(){if(request?.IsCompleted!=true)return;var task=request;request=null;if(Profile?.Id!=requestOwner){notice="角色已切换，请重新读取。";return;}try{
  using var doc=JsonDocument.Parse(task.GetAwaiter().GetResult());var root=doc.RootElement;if(root.TryGetProperty("err",out var code)&&code.GetInt32()!=0)throw new ArgumentException(root.TryGetProperty("errmsg",out var message)?message.GetString():"保存失败。");
  var data=CharacterDataPanel.Data(root);var health=data.GetProperty("health");var parts=health.GetProperty("BodyParts");drafts.Clear();foreach(var part in AiBodyHealth.Parts){var h=parts.GetProperty(part.ToString()).GetProperty("Health");drafts[part.ToString()]=(h.GetProperty("Maximum").GetDouble().ToString("G9",CultureInfo.InvariantCulture),h.GetProperty("Current").GetDouble().ToString("G9",CultureInfo.InvariantCulture));}
  foreach(string key in vitals){var v=health.GetProperty(key);drafts[key]=(v.GetProperty("Maximum").GetDouble().ToString("G9",CultureInfo.InvariantCulture),v.GetProperty("Current").GetDouble().ToString("G9",CultureInfo.InvariantCulture));}
  if(saving&&Live==null&&Profile?.Health is {} native)Newtonsoft.Json.JsonConvert.PopulateObject(health.GetRawText(),new Il2CppSystem.Object(native.Pointer));
  notice=saving?"各部位生命值已保存。":"已读取当前 PMC 生命值。";
 }catch(Exception e){notice=e.Message;}}
 static void ApplyLive(Player player){
  var values=Values();var vitality=VitalValues();var health=player.ActiveHealthController??throw new ArgumentException("当前生命控制器不可用");
  foreach(var pair in values){var part=Enum.Parse<EBodyPart>(pair.Key);var state=health.BodyState[part].Health;if(state.Current<=0&&pair.Value.Current>0)health.FullRestoreBodyPart(part);state=health.BodyState[part].Health;state.Init(Math.Min(state.Current,pair.Value.Maximum),0,pair.Value.Maximum);health.ChangeHealth(part,pair.Value.Current-state.Current,new EFT.Ballistics.DamageInfo());}
  var energy=vitality["Energy"];health._energy.Init(Math.Min(health.Energy.Current,energy.Maximum),0,energy.Maximum);health.ChangeEnergy(energy.Current-health.Energy.Current);
  var hydration=vitality["Hydration"];health._hydration.Init(Math.Min(health.Hydration.Current,hydration.Maximum),0,hydration.Maximum);health.ChangeHydration(hydration.Current-health.Hydration.Current);
  foreach(string key in vitals){var actual=key=="Energy"?health.Energy:health.Hydration;var expected=vitality[key];if(Math.Abs(actual.Current-expected.Current)>.01f||Math.Abs(actual.Maximum-expected.Maximum)>.01f)throw new ArgumentException("能量或水分回读不一致，请重新读取。");}
  foreach(var pair in values){var actual=health.GetBodyPartHealth(Enum.Parse<EBodyPart>(pair.Key),false);if(Math.Abs(actual.Current-pair.Value.Current)>.01f||Math.Abs(actual.Maximum-pair.Value.Maximum)>.01f)throw new ArgumentException("部分血量未能应用，请重新读取后重试");}
 }
 internal static void Draw(Rect rect){
  Complete();var profile=Profile;if(profile==null){InterfaceMenu.Paragraph(rect,"请先登录 PMC 角色，或进入存活的战局。",InterfaceMenu.Accent);return;}
  if(owner!=profile.Id){owner=profile.Id;drafts.Clear();if(Live is {} local)ReadLive(local);else Begin(false);}
  InterfaceMenu.Label(new(rect.x,rect.y,rect.width-110,30),"部位血量、能量与水分",InterfaceMenu.Accent,18);
  if(InterfaceMenu.Button(new(rect.xMax-100,rect.y,100,30),"重新读取")){if(Live is {} local)ReadLive(local);else Begin(false);}
  var view=new Rect(rect.x,rect.y+44,rect.width,Math.Max(50,rect.height-100));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,rect.width-22,490));
  bool enabled=GUI.enabled;GUI.enabled=enabled&&request==null;
  try{float width=rect.width-28,field=(width-120)/2,y=0;InterfaceMenu.Label(new(106,y,field,24),"最大值",null,13);InterfaceMenu.Label(new(114+field,y,field,24),"当前值",null,13);y+=30;
   for(int i=0;i<AiBodyHealth.Parts.Length;i++){string key=AiBodyHealth.Parts[i].ToString();var d=drafts.GetValueOrDefault(key,("",""));InterfaceMenu.Label(new(0,y,100,30),labels[i]);drafts[key]=(InterfaceMenu.Field(new(106,y,field,30),d.Item1),InterfaceMenu.Field(new(114+field,y,field,30),d.Item2));y+=40;}
   for(int i=0;i<vitals.Length;i++){string key=vitals[i];var d=drafts.GetValueOrDefault(key,("",""));InterfaceMenu.Label(new(0,y,100,30),i==0?"能量":"水分");drafts[key]=(InterfaceMenu.Field(new(106,y,field,30),d.Item1),InterfaceMenu.Field(new(114+field,y,field,30),d.Item2));y+=40;}
   if(InterfaceMenu.Button(new(0,y,(width-10)/2,34),"填满生命、能量与水分"))foreach(string key in drafts.Keys.ToArray())drafts[key]=(drafts[key].Maximum,drafts[key].Maximum);
   if(InterfaceMenu.Button(new((width+10)/2,y,(width-10)/2,34),Live!=null?"应用到当前角色":"保存到 PMC 存档"))try{if(Live is {} local){ApplyLive(local);ReadLive(local);notice="已应用并读回当前角色各部位血量。";}else Begin(true);}catch(Exception e){notice=e.Message;}
  }finally{GUI.enabled=enabled;GUI.EndScrollView();}
  InterfaceMenu.Paragraph(new(rect.x,rect.yMax-48,rect.width,48),notice,null,13);
 }
}
