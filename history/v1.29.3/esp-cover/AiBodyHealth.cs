using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using BepInEx.Configuration;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AiBodyHealth {
 static ConfigEntry<bool> enabled=null!;static ConfigEntry<string> values=null!;static string notice="";
 static readonly Dictionary<string,(string Maximum,string Current)> drafts=new();
 internal static readonly EBodyPart[] Parts={EBodyPart.Head,EBodyPart.Chest,EBodyPart.Stomach,EBodyPart.LeftArm,EBodyPart.RightArm,EBodyPart.LeftLeg,EBodyPart.RightLeg};
 static readonly string[] names={"头部","胸部","腹部","左臂","右臂","左腿","右腿"};static readonly float[] defaults={35,85,70,60,60,65,65};
 internal static void Initialize(){enabled=FeatureConfig.Bind("34. AI 测试","Per body health","逐部位设置出生血量",false,"开启时覆盖对应身体部位的最大／当前血量，优先于总血量与百分比。");values=FeatureConfig.Bind("34. AI 测试","Per body health values","各部位血量数据","{}");}
 static Dictionary<string,BodyHealthPreset> Read(){var result=JsonSerializer.Deserialize<Dictionary<string,BodyHealthPreset>>(values.Value)??new();return result;}
 static Dictionary<string,BodyHealthPreset> Edited(){var result=Read();foreach(var pair in drafts){if(!float.TryParse(pair.Value.Maximum,NumberStyles.Float,CultureInfo.InvariantCulture,out float max)||!float.TryParse(pair.Value.Current,NumberStyles.Float,CultureInfo.InvariantCulture,out float cur))throw new ArgumentException("部位血量尚未输入完整："+pair.Key);result[pair.Key]=new(max,cur);}return result;}
 internal static Dictionary<string,BodyHealthPreset>? Snapshot(){if(!enabled.Value)return null;var result=Edited();for(int i=0;i<Parts.Length;i++)if(!result.ContainsKey(Parts[i].ToString()))result[Parts[i].ToString()]=new(defaults[i],defaults[i]);Validate(result);return result;}
 internal static Dictionary<string,BodyHealthPreset> ToolValues(){var result=Edited();for(int i=0;i<Parts.Length;i++)if(!result.ContainsKey(Parts[i].ToString()))result[Parts[i].ToString()]=new(defaults[i],defaults[i]);Validate(result);return result;}
 internal static void Validate(Dictionary<string,BodyHealthPreset>? result)=>DirectorRules.ValidateHealth(result);
 internal static Dictionary<string,BodyHealthPreset> Capture(Player player){var result=new Dictionary<string,BodyHealthPreset>();foreach(var part in Parts){var health=player.ActiveHealthController!.GetBodyPartHealth(part,false);result[part.ToString()]=new(health.Maximum,health.Current);}return result;}
 internal static void Load(Dictionary<string,BodyHealthPreset>? parts){Validate(parts);enabled.Value=parts!=null;values.Value=JsonSerializer.Serialize(parts??new());drafts.Clear();}
 internal static void Apply(Player player,Dictionary<string,BodyHealthPreset> parts){Validate(parts);var health=player.ActiveHealthController??throw new ArgumentException("AI 生命状态未就绪。");foreach(var pair in parts){var part=Enum.Parse<EBodyPart>(pair.Key);var state=health.BodyState[part].Health;state.Init(Math.Min(state.Current,pair.Value.Maximum),0,pair.Value.Maximum);health.ChangeHealth(part,pair.Value.Current-state.Current,new EFT.Ballistics.DamageInfo());}notice="已应用各部位血量。";}
 internal static void Draw(Rect rect){float x=rect.x,y=rect.y,w=rect.width,third=(w-20)/3;InterfaceMenu.Label(new Rect(x,y,w,28),"各部位血量",InterfaceMenu.Accent,17);y+=38;if(InterfaceMenu.Button(new Rect(x,y,w,32),enabled.Value?"出生逐部位覆盖 · 开":"出生逐部位覆盖 · 关",enabled.Value))enabled.Value=!enabled.Value;y+=42;
  InterfaceMenu.Label(new Rect(x+third+10,y,third,24),"最大血量",null,12);InterfaceMenu.Label(new Rect(x+2*(third+10),y,third,24),"当前血量",null,12);y+=28;Dictionary<string,BodyHealthPreset> parts;try{parts=Read();}catch{parts=new();}
  for(int i=0;i<Parts.Length;i++){string key=Parts[i].ToString();var value=parts.GetValueOrDefault(key)??new(defaults[i],defaults[i]);var draft=drafts.GetValueOrDefault(key,(value.Maximum.ToString("G7",CultureInfo.InvariantCulture),value.Current.ToString("G7",CultureInfo.InvariantCulture)));InterfaceMenu.Label(new Rect(x,y,third,30),names[i],null,13);string max=InterfaceMenu.Field(new Rect(x+third+10,y,third,30),draft.Item1),current=InterfaceMenu.Field(new Rect(x+2*(third+10),y,third,30),draft.Item2);drafts[key]=(max,current);if(float.TryParse(max,NumberStyles.Float,CultureInfo.InvariantCulture,out float a)&&float.TryParse(current,NumberStyles.Float,CultureInfo.InvariantCulture,out float b)&&float.IsFinite(a)&&float.IsFinite(b)&&a>0&&a<=float.MaxValue/8&&b>=0&&b<=a){parts[key]=new(a,b);values.Value=JsonSerializer.Serialize(parts);}y+=38;}
  if(InterfaceMenu.Button(new Rect(x,y,(w-12)/2,32),"读取首个所选 AI 血量"))try{var target=AiTestControl.PoseSelection.FirstOrDefault()??throw new ArgumentException("请先选择 AI。");Load(Capture(target));notice="已读取 "+target.Profile?.Nickname;}catch(Exception e){notice=e.Message;}
  if(InterfaceMenu.Button(new Rect(x+(w+12)/2,y,(w-12)/2,32),"应用到所选 AI"))try{var edited=Edited();Validate(edited);foreach(var actor in AiTestControl.PoseSelection)Apply(actor,edited);}catch(Exception e){notice=e.Message;}y+=42;InterfaceMenu.Paragraph(new Rect(x,y,w,48),notice.Length>0?notice:"最大血量大于 0；当前血量不能超过最大血量。关键部位为 0 可能导致死亡。",InterfaceMenu.Accent,12);
 }
}
