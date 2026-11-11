using System;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 static ConfigEntry<string> contextOrder=null!;static ConfigEntry<bool> splitMixedContext=null!;static Vector2 contextSettingsScroll;
 static void InitializeContextSettings(){
  contextOrder=FeatureConfig.Bind("63. 场景编辑","Context menu actions","右键菜单项目与顺序",EditorContextRules.Default,"在编辑模式设置中勾选、隐藏和排序；按对象类型显示适用项目。删除所有武器包含近战；A/B 仅作队伍标记。");
  splitMixedContext=FeatureConfig.Bind("63. 场景编辑","Show both actions for mixed states","混合状态显示两个按钮",true,"开启：状态不一致时同时显示两个明确操作；关闭：只显示一个按钮，先统一暂停、开启无敌或停战（A/B 则统一敌对）。状态一致时始终只显示反向操作。");
 }
 static void OpenContextSettings(){contextSettingsScroll=Vector2.zero;Modal("context-settings");}
 static void DrawContextSettings(Rect rect){
  var order=EditorContextRules.Read(contextOrder.Value);
  InterfaceMenu.Label(new(rect.x,rect.y,rect.width-130,28),"自定义右键菜单",InterfaceMenu.Accent,16);
  if(InterfaceMenu.Button(new(rect.xMax-124,rect.y,124,28),"恢复默认菜单"))contextOrder.Value=EditorContextRules.Default;
  InterfaceMenu.Paragraph(new(rect.x,rect.y+36,rect.width,56),"勾选显示项目，↑↓ 调整顺序；单选和多选共用设置，只显示当前对象适用的操作。删除所有武器包含近战；A/B 标记供关系工具使用。",null,12);
  if(InterfaceMenu.Button(new(rect.x,rect.y+94,rect.width,30),I18n.Text("混合状态显示两个按钮")+(splitMixedContext.Value?" ✓":" ○"),splitMixedContext.Value))splitMixedContext.Value=!splitMixedContext.Value;
  var rows=order.Concat(EditorContextRules.Options.Select(o=>o.Id).Except(order)).ToArray();
  var view=new Rect(rect.x,rect.y+134,rect.width,Math.Max(30,rect.height-134));contextSettingsScroll=GUI.BeginScrollView(view,contextSettingsScroll,new Rect(0,0,rect.width-22,rows.Length*38));
  try{for(int i=0;i<rows.Length;i++){
   string id=rows[i],name=EditorContextRules.Options.First(o=>o.Id==id).Name;int index=Array.IndexOf(order,id);bool on=index>=0;float y=i*38,w=rect.width-28;
   if(InterfaceMenu.Button(new(0,y,Math.Max(60,w-90),32),(on?"✓  ":"○  ")+name,on))contextOrder.Value=EditorContextRules.Toggle(contextOrder.Value,id);
   bool was=GUI.enabled;
   try{GUI.enabled=was&&index>0;if(InterfaceMenu.Button(new(w-82,y,36,32),"↑"))contextOrder.Value=EditorContextRules.Move(contextOrder.Value,id,-1);GUI.enabled=was&&on&&index<order.Length-1;if(InterfaceMenu.Button(new(w-40,y,36,32),"↓"))contextOrder.Value=EditorContextRules.Move(contextOrder.Value,id,1);}finally{GUI.enabled=was;}
  }}finally{GUI.EndScrollView();}
 }
 static void ContextRemoveWeapons(){var targets=SelectedActors();AiEquipmentRemoval.RemoveWeapons(Current(),targets);notice=AiEquipmentRemoval.Status;CloseDialog();}
 static bool ContextPaused(EFT.Player target)=>AiTestControl.IsPaused(target)||AiFreePlacement.Pinned(target)||ModelPoseLock.Locked(target)||SceneTemplates.IsStaging(target);
 static void ContextTeam(int side){var current=Current();var targets=SelectedActors();foreach(var target in targets)AiActorControl.MarkEditorSide(current,target,side);FeatureToasts.Show(side==0?"已清除队伍标记。":side==1?"已标记为 A 方。":"已标记为 B 方。");CloseDialog();}
 static void ContextPause(bool value){var targets=SelectedActors();if(value){AiTestControl.SetPaused(targets,true);FeatureToasts.Show("所选 AI 已暂停");}else AiTestControl.ResumeActors(targets);CloseDialog();}
 static void ContextSides(bool hostile){AiActorControl.CommandEditorSides(hostile);CloseDialog();}
 static void ContextPlayerRelation(int mode){var current=Current();foreach(var target in SelectedActors())AiActorControl.CommandRelations(current,target,mode,null);FeatureToasts.Show(mode==0?"已恢复所选 AI 对玩家的默认关系。":mode==1?"所选 AI 已对玩家停战。":"所选 AI 已对玩家敌对。");CloseDialog();}
 static void ContextInvincible(bool value){var current=Current();foreach(var target in SelectedActors()){var state=AiActorControl.EditorState(target);AiActorControl.ApplyEditorState(current,target,value,state.Damage);}FeatureToasts.Show(value?"所选 AI 已开启无敌。":"所选 AI 已关闭无敌。");CloseDialog();}
 static void ContextClearEffects(){foreach(var target in SelectedActors())CharacterBuffs.ClearBirthEffects(target);FeatureToasts.Show("已清除所选 AI 可移除的异常状态。");CloseDialog();}
}
