using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 static readonly string[] propertyPages={"基本","生命","行为","战斗","装备","Buff"};
 static int propertyPage;static bool ownsEquipment,ownsBuffs,invincible;
 static string pitch="0",damageFactor="1",followRange="3",followSpeed="1",behaviorYaw="0",behaviorPitch="0";
 static ActorScenePreset behavior=new();
 internal static bool EditingBuffs=>dialog=="properties"&&actor!=null&&ownsBuffs;
 static void ReleasePropertyEditors(){if(ownsEquipment){AiBackpackEditor.Close();AiEquipmentEditor.Close();ownsEquipment=false;}if(ownsBuffs){CharacterBuffs.CloseAi();ownsBuffs=false;}}
 static void ReadAiProperties(){
  ReleasePropertyEditors();propertyPage=0;notice="";var target=Actor();rename=target.Profile.Nickname;yaw=target.Rotation.x.ToString("G7",CultureInfo.InvariantCulture);pitch=target.Rotation.y.ToString("G7",CultureInfo.InvariantCulture);paused=AiTestControl.IsPaused(target);
  foreach(var pair in AiBodyHealth.Capture(target))health[pair.Key]=(pair.Value.Maximum.ToString("G7",CultureInfo.InvariantCulture),pair.Value.Current.ToString("G7",CultureInfo.InvariantCulture));
  ReadBehavior(target);
  var combat=AiActorControl.EditorState(target);invincible=combat.Invincible;damageFactor=combat.Damage.ToString("G7",CultureInfo.InvariantCulture);
 }
 static void ChangePropertyPage(int next){Actor();propertyPage=next;scroll=Vector2.zero;GUI.FocusControl("");}
 static void ReadBehavior(Player target){behavior=AiSceneDirector.For(target);followRange=behavior.FollowDistance.ToString("G7",CultureInfo.InvariantCulture);followSpeed=behavior.Speed.ToString("G7",CultureInfo.InvariantCulture);behaviorYaw=behavior.Yaw.ToString("G7",CultureInfo.InvariantCulture);behaviorPitch=behavior.Pitch.ToString("G7",CultureInfo.InvariantCulture);}
 static void DrawAiProperties(Rect rect){
  var target=Actor();InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width,24),target.Profile.Nickname+" · "+target.Profile.Info.Settings.Role,InterfaceMenu.Accent,14);
  float cell=(rect.width-5*6)/6;for(int i=0;i<propertyPages.Length;i++)if(InterfaceMenu.Button(new Rect(rect.x+i*(cell+6),rect.y+32,cell,32),propertyPages[i],propertyPage==i))ChangePropertyPage(i);
  var content=new Rect(rect.x,rect.y+76,rect.width,Math.Max(40,rect.height-120));
  if(propertyPage==4){
   if(!ownsEquipment){AiEquipmentEditor.Select(Current(),target);ownsEquipment=true;}
   if(InterfaceMenu.Button(new Rect(content.x,content.y,140,30),"编辑储物内容"))AiBackpackEditor.Select(Current(),target);
   content.y+=40;content.height=Math.Max(40,content.height-40);
   if(AiBackpackEditor.Open)AiBackpackEditor.Draw(content);else if(AiEquipmentEditor.Open)AiEquipmentEditor.Draw(content,true);else if(InterfaceMenu.Button(new Rect(content.x,content.y,content.width,34),"打开装备编辑器"))AiEquipmentEditor.Select(Current(),target);
  }else if(propertyPage==5){if(!ownsBuffs||!CharacterBuffs.IsSelectedAi(target)){CharacterBuffs.SelectAi(Current(),target);ownsBuffs=true;}CharacterBuffs.DrawAi(content,true);}
  else{
   scroll=GUI.BeginScrollView(content,scroll,new Rect(0,0,content.width-22,propertyPage==2?750:propertyPage==1?385:370));
   try{float y=0,w=content.width-30;switch(propertyPage){case 0:DrawAiBasic(target,w,ref y);break;case 1:DrawAiHealth(w,ref y);break;case 2:DrawAiBehavior(w,ref y);break;case 3:DrawAiCombat(target,w,ref y);break;}}
   finally{GUI.EndScrollView();}
  }
  InterfaceMenu.Paragraph(new Rect(rect.x,rect.yMax-36,rect.width,36),notice.Length>0?notice:"各页独立应用；翻页保留草稿，关闭窗口放弃未应用修改。",InterfaceMenu.Accent,12);
 }
 static string PropertyField(float w,ref float y,string label,string value){InterfaceMenu.Label(new Rect(0,y,w,24),label,null,13);y+=26;string result=InterfaceMenu.Field(new Rect(0,y,w,30),value);y+=42;return result;}
 static bool PropertyToggle(float w,ref float y,string label,bool value){if(InterfaceMenu.Button(new Rect(0,y,w,32),I18n.Text(label)+" · "+I18n.Text(value?"开启":"关闭"),value))value=!value;y+=42;return value;}
 static void Applied(){notice="已应用当前页。";}
 static void DrawAiBasic(Player target,float w,ref float y){
  InterfaceMenu.Paragraph(new Rect(0,y,w,50),target.ProfileId+"\n"+target.Position,null,12);y+=58;
  rename=PropertyField(w,ref y,"名称",rename);yaw=PropertyField(w,ref y,"朝向角度",yaw);pitch=PropertyField(w,ref y,"抬头角度（负数抬头）",pitch);paused=PropertyToggle(w,ref y,"暂停 AI",paused);
  if(InterfaceMenu.Button(new Rect(0,y,w,34),"应用基本属性"))Run(()=>{string name=rename.Trim();if(name.Length is <1 or >64)throw new ArgumentException("名称长度须为 1～64 个字符。");float heading=ItemSpawnRules.Number(yaw,-36000,36000,"朝向角度"),vertical=ItemSpawnRules.Number(pitch,-89,89,"抬头角度（负数抬头）");var current=Actor();current.Profile.Info.Nickname=name;var scene=AiSceneDirector.For(current);if(scene.SetFacing)AiSceneDirector.Apply(Current(),current,scene with{Yaw=heading,Pitch=vertical});current.Rotation=new Vector2(heading,vertical);AiTestControl.SetPaused(new[]{current},paused);Applied();});
 }
 static void DrawAiHealth(float w,ref float y){
  InterfaceMenu.Label(new Rect(0,y,w,24),"身体部位 · 最大血量 / 当前血量",null,13);y+=32;
  string[] labels={"头部","胸部","腹部","左臂","右臂","左腿","右腿"};
  for(int i=0;i<AiBodyHealth.Parts.Length;i++){string key=AiBodyHealth.Parts[i].ToString();var value=health[key];float field=(w-120)/2;InterfaceMenu.Label(new Rect(0,y,100,30),labels[i],null,13);string max=InterfaceMenu.Field(new Rect(106,y,field,30),value.Max),cur=InterfaceMenu.Field(new Rect(114+field,y,field,30),value.Current);health[key]=(max,cur);y+=38;}
  if(InterfaceMenu.Button(new Rect(0,y,w,32),"填入满血（尚未应用）"))foreach(string key in health.Keys.ToArray())health[key]=(health[key].Max,health[key].Max);y+=42;
  if(InterfaceMenu.Button(new Rect(0,y,w,34),"应用生命属性"))Run(()=>{var values=new Dictionary<string,BodyHealthPreset>();foreach(var pair in health)values[pair.Key]=new(ItemSpawnRules.Number(pair.Value.Max,.01f,100000000,"最大血量"),ItemSpawnRules.Number(pair.Value.Current,0,100000000,"当前血量"));AiBodyHealth.Validate(values);AiBodyHealth.Apply(Actor(),values);Applied();});
 }
 static void DrawAiBehavior(float w,ref float y){
  string stance=behavior.Pose switch{ScenePose.Standing=>"站立",ScenePose.Crouched=>"蹲下",ScenePose.Prone=>"卧倒",_=>"原始姿势"};
  if(InterfaceMenu.Button(new Rect(0,y,w,32),I18n.Text("AI 姿势")+" · "+I18n.Text(stance)))behavior=behavior with{Pose=(ScenePose)(((int)behavior.Pose+1)%4)};y+=42;
  behavior=behavior with{SetFacing=PropertyToggle(w,ref y,"固定朝向与抬头角度",behavior.SetFacing)};
  if(behavior.SetFacing){behaviorYaw=PropertyField(w,ref y,"朝向角度",behaviorYaw);behaviorPitch=PropertyField(w,ref y,"抬头角度（负数抬头）",behaviorPitch);}
  behavior=behavior with{NoDoors=PropertyToggle(w,ref y,"禁止 AI 开门",behavior.NoDoors)};
  behavior=behavior with{NoHealing=PropertyToggle(w,ref y,"禁止 AI 治疗／打药",behavior.NoHealing)};
  behavior=behavior with{NoMovement=PropertyToggle(w,ref y,"禁止 AI 移动",behavior.NoMovement)};
  bool look=PropertyToggle(w,ref y,"持续看向玩家",behavior.Actions.HasFlag(SceneActions.LookAtPlayer));bool follow=PropertyToggle(w,ref y,"跟随玩家",behavior.Actions.HasFlag(SceneActions.FollowPlayer));behavior=behavior with{Actions=(look?SceneActions.LookAtPlayer:0)|(follow?SceneActions.FollowPlayer:0)};
  followRange=PropertyField(w,ref y,"跟随保持距离（米）",followRange);followSpeed=PropertyField(w,ref y,"跟随移动输入倍率",followSpeed);
  if(InterfaceMenu.Button(new Rect(0,y,w,34),"应用行为设置"))Run(()=>{var value=behavior with{Enabled=true,InitializeFacing=false,FollowDistance=ItemSpawnRules.Number(followRange,1,30,"跟随保持距离（米）"),Speed=ItemSpawnRules.Number(followSpeed,.1f,5,"跟随移动输入倍率"),Yaw=ItemSpawnRules.Number(behaviorYaw,-36000,36000,"朝向角度"),Pitch=ItemSpawnRules.Number(behaviorPitch,-89,89,"抬头角度（负数抬头）")};var current=Actor();AiSceneDirector.Apply(Current(),current,DirectorRules.Validate(value));behavior=value;paused=AiTestControl.IsPaused(current);Applied();});y+=44;
  if(InterfaceMenu.Button(new Rect(0,y,w,34),"撤销当前 AI 导演设置"))Run(()=>{var current=Actor();AiSceneDirector.Remove(current);ReadBehavior(current);paused=AiTestControl.IsPaused(current);Applied();});
 }
 static void DrawAiCombat(Player target,float w,ref float y){
  invincible=PropertyToggle(w,ref y,"AI 无敌",invincible);damageFactor=PropertyField(w,ref y,"承受玩家伤害倍率",damageFactor);
  InterfaceMenu.Paragraph(new Rect(0,y,w,52),"倍率仅影响本地玩家对该 AI 的伤害；0 免疫，1 正常。全局 AI 无敌仍然生效。",null,12);y+=62;
  if(InterfaceMenu.Button(new Rect(0,y,w,34),"应用战斗属性"))Run(()=>{AiActorControl.ApplyEditorState(Current(),Actor(),invincible,ItemSpawnRules.Number(damageFactor,0,float.MaxValue,"承受玩家伤害倍率"));Applied();});y+=46;
  InterfaceMenu.Label(new Rect(0,y,w,26),AiActorControl.Team(target.AIData.BotOwner),null,12);
 }
}
