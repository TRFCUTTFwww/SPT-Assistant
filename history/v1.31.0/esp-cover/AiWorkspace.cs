using System;
using System.Linq;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AiWorkspace {
 static string actionPage="导演动作";static string equipment="管理与关系",equipmentSelection="",teleportSelection="";
 internal static void CloseEditors(){equipmentSelection="";AiActorControl.Close();AiEquipmentEditor.Close();AiBackpackEditor.Close();CharacterBuffs.CloseAi();}
 static void Run(Action action){try{action();}catch(Exception e){FeatureToasts.Show(e.Message);}}
 internal static void Draw(Rect rect,string page){
  var selected=AiTestControl.CheckedSelection;string fingerprint=string.Join("|",selected.Select(p=>p.ProfileId));float x=rect.x,y=rect.y,w=rect.width;
  InterfaceMenu.Surface(new Rect(x,y,w,64),Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Accent,.06f),8);
  InterfaceMenu.Label(new Rect(x+10,y+3,w-20,24),selected.Length==0?"先在 AI 列表勾选角色；切换下面的工具会保留选择。":"已选 "+selected.Length+" 名 · "+string.Join("、",selected.Take(3).Select(p=>p.Profile?.Nickname??"AI"))+(selected.Length>3?" 等":""),InterfaceMenu.Accent,12);
  float button=(w-40)/5;bool old=GUI.enabled;if(InterfaceMenu.Button(new Rect(x+6,y+30,button,28),"列表选人"))InterfaceMenu.Navigate("AI 工作台","AI 列表");GUI.enabled=old&&selected.Length>0;
  if(InterfaceMenu.Button(new Rect(x+14+button,y+30,button,28),"暂停所选"))AiTestControl.SetPaused(selected,true);
  if(InterfaceMenu.Button(new Rect(x+22+button*2,y+30,button,28),"恢复所选"))AiTestControl.ResumeActors(selected);
  if(InterfaceMenu.Button(new Rect(x+30+button*3,y+30,button,28),"换武器"))InterfaceMenu.Navigate("AI 工作台","换武器");GUI.enabled=old&&selected.Length>0&&!AiEquipmentRemoval.Busy;
  if(InterfaceMenu.Button(new Rect(x+38+button*4,y+30,button-6,28),"删除武器"))Run(()=>AiEquipmentRemoval.RemoveWeapons(Singleton<GameWorld>.Instance!,selected));GUI.enabled=old;
  rect=new Rect(x,y+76,w,Math.Max(70,rect.height-76));
  switch(page){
   case "AI 列表":AiTestControl.DrawWorkspace(rect,0);break;
   case "生成 AI":AiTestControl.DrawWorkspace(rect,1);break;case "换武器":AiSceneEquipment.Draw(rect);break;
   case "全局控制":AiTestControl.DrawWorkspace(rect,2);break;
   case "动作与路线":actionPage=InterfaceMenu.Tabs(rect,new[]{"导演动作","模型动作锁定"},actionPage,out float actionHeight);var actionsRect=new Rect(rect.x,rect.y+actionHeight,rect.width,Math.Max(70,rect.height-actionHeight));if(actionPage=="导演动作")AiSceneDirector.Draw(actionsRect);else ModelPoseLock.DrawAi(actionsRect);break;
   case "装备与 Buff":DrawEquipment(rect,selected,fingerprint);break;
   case "位置传送":if(fingerprint!=teleportSelection){teleportSelection=fingerprint;if(Singleton<GameWorld>.Instance is {} current)TeleportControl.SetAiSources(current,selected);}TeleportControl.Draw(rect);break;
   case "AI 模板":SceneTemplates.Draw(rect);break;
   case "地图场景":SceneTemplates.Draw(rect,true);break;
   case "尸体清理":CorpseCleanup.Draw(rect);break;
   case "刷新规则":NextRaidLoot.Draw(rect);break;
  }
 }
 static void DrawEquipment(Rect rect,Player[] selected,string fingerprint){
  var pages=new[]{"管理与关系","装备参数","背包与口袋","Buff 状态","预设枪械"};equipment=InterfaceMenu.Tabs(rect,pages,equipment,out float height);rect=new Rect(rect.x,rect.y+height,rect.width,Math.Max(60,rect.height-height));
  if(equipment=="预设枪械"){AiSceneEquipment.Draw(rect);return;}
  if(selected.Length==0){InterfaceMenu.Paragraph(new Rect(rect.x+8,rect.y+8,rect.width-16,64),"当前没有勾选存活 AI。点击上方“列表选人”，勾选后再回到装备工具。",null,14);return;}
  string key=equipment+":"+fingerprint;
  if(equipmentSelection!=key){CloseEditors();equipmentSelection=key;Run(()=>{var current=Singleton<GameWorld>.Instance!;if(equipment=="管理与关系")AiActorControl.Select(current,selected);else if(equipment=="Buff 状态")CharacterBuffs.SelectAi(current,selected);else if(selected.Length==1){if(equipment=="装备参数")AiEquipmentEditor.Select(current,selected[0]);else AiBackpackEditor.Select(current,selected[0]);}});}
  if(equipment=="管理与关系"){if(AiActorControl.Open)AiActorControl.Draw(rect);else{equipmentSelection="";InterfaceMenu.Navigate("AI 工作台","AI 列表");}}
  else if(equipment=="Buff 状态"){if(CharacterBuffs.AiOpen)CharacterBuffs.DrawAi(rect);else{equipmentSelection="";InterfaceMenu.Navigate("AI 工作台","AI 列表");}}
  else if(selected.Length!=1)InterfaceMenu.Paragraph(rect,"逐件装备与储物格编辑需要单选一名 AI；多选可批量修改行为、Buff、预设枪械或删除武器。",null,13);
  else if(equipment=="装备参数"){if(AiEquipmentEditor.Open)AiEquipmentEditor.Draw(rect);else{equipmentSelection="";InterfaceMenu.Navigate("AI 工作台","AI 列表");}}
  else {if(AiBackpackEditor.Open)AiBackpackEditor.Draw(rect);else{equipmentSelection="";InterfaceMenu.Navigate("AI 工作台","AI 列表");}}
 }
}
