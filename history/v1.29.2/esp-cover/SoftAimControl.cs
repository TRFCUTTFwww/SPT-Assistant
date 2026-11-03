using System;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class SoftAimControl {
 static ConfigEntry<float> speed=null!,angle=null!,seconds=null!;static string speedDraft="",angleDraft="",secondsDraft="";static Vector2 scroll;
 internal static float Seconds=>seconds.Value;internal static float Speed=>speed.Value;internal static float Angle=>angle.Value;
 internal static void Initialize(){if(BepInEx.Configuration.TomlTypeConverter.ConvertToValue("3",typeof(AimMode)) is not AimMode parsed||parsed!=AimModes.Soft)throw new InvalidOperationException("Micro aim mode serialization unavailable");speed=FeatureConfig.Bind("8. Aim assist","Soft rotation speed","微自瞄转动速度（度／秒）",30f,"0 不拉动；数值越大越快。",new AcceptableValueRange<float>(0,180));seconds=FeatureConfig.Bind("8. Aim assist","Soft correction seconds","每枪微调持续秒数",.3f,"每次实际开火后最多修正多久。",new AcceptableValueRange<float>(0,5));angle=FeatureConfig.Bind("8. Aim assist","Soft acquisition angle","微自瞄范围（度）",6f,"只选择准星附近的 AI。",new AcceptableValueRange<float>(0,180));}
 internal static void Draw(Rect rect){scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,660));float x=8,y=8,w=rect.width-38;InterfaceMenu.Label(new Rect(x,y,w,30),"微自瞄",InterfaceMenu.Accent,21);y+=46;bool enabled=CombatAssist.Mode.Value==AimModes.Soft;if(InterfaceMenu.Button(new Rect(x,y,w,40),enabled?"微自瞄 · 已开启":"启用微自瞄",enabled))CombatAssist.Mode.Value=enabled?AimMode.Off:AimModes.Soft;y+=56;
  InterfaceMenu.Paragraph(new Rect(x,y,w,46),"玩家每开一枪，才触发一次短暂的平滑准星修正；不开枪时不拉动准星。",null,14);y+=62;
  InterfaceMenu.Label(new Rect(x,y,w*.55f,36),"触发：每次实际开枪后",InterfaceMenu.Accent,14);if(InterfaceMenu.Button(new Rect(x+w*.57f,y,w*.43f,36),"部位："+Part(CombatAssist.TargetPart.Value)))CombatAssist.TargetPart.Value=(AimTargetPart)(((int)CombatAssist.TargetPart.Value+1)%7);y+=56;
  void Number(string name,ConfigEntry<float> entry,ref string draft){InterfaceMenu.Label(new Rect(x,y,w,26),name+" · 当前 "+entry.Value.ToString("G6",CultureInfo.InvariantCulture),null,14);y+=34;if(draft.Length==0)draft=entry.Value.ToString("G6",CultureInfo.InvariantCulture);draft=InterfaceMenu.Field(new Rect(x,y,160,34),draft);if(InterfaceMenu.Button(new Rect(x+174,y,100,34),"应用")){if(float.TryParse(draft,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&float.IsFinite(value)&&entry.Description.AcceptableValues!.IsValid(value)){entry.Value=value;FeatureToasts.Show(name+"已设置");}else FeatureToasts.Show("请输入有效范围内的数字。");}y+=54;}
  Number("转动速度（度／秒，0 不拉动）",speed,ref speedDraft);Number("准星周围选择范围（0–180 度）",angle,ref angleDraft);Number("每枪修正最长秒数（0 不修正）",seconds,ref secondsDraft);InterfaceMenu.Paragraph(new Rect(x,y,w,70),"部位、可见性、距离和目标排除与其他模式共用；按住激活／常开开关用于暴力和静默模式，微自瞄由开枪触发。",null,12);GUI.EndScrollView();
 }
 static string Part(AimTargetPart part)=>new[]{"头部","胸部","腹部","左臂","右臂","左腿","右腿"}[(int)part];
}
