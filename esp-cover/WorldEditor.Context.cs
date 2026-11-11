using System;
using System.Linq;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 sealed record ContextAction(string Name,bool Enabled,Action Execute,string Shortcut="");
 static ContextAction[] contextActions=Array.Empty<ContextAction>();
 static int contextChoice,contextFrame,contextTop;static Vector2 contextAnchor,contextPointer;static bool categoryMenu;static string contextTitle="场景编辑";
 static int ContextVisibleRows=>Math.Min(contextActions.Length,Math.Max(1,(Screen.height-74)/28));
 static float ContextKeyWidth=>Math.Min(180,Math.Max(66,contextActions.Select(action=>action.Shortcut.Length*7f).DefaultIfEmpty(66).Max()));
 static Rect ContextBounds{get{var b=EditorMenuRules.Popup(contextAnchor.x,contextAnchor.y,Math.Max(252,ContextKeyWidth+176),58+ContextVisibleRows*28,Screen.width,Screen.height);return new(b.X,b.Y,b.Width,b.Height);}}
 static Vector2 MousePosition=>new(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
 static void OpenContext(){
  PruneSelection();contextAnchor=altCursor?MousePosition+new Vector2(14,10):new(Screen.width/2f+14,Screen.height/2f+10);
  if(SelectionKinds>1){categoryMenu=true;contextTitle="选择对象类别";SetContext(CategoryActions(OpenEditContext));return;}
  OpenEditContext(FirstSelectionKind);
 }
 static int SelectionKinds=>(selectedActors.Count>0?1:0)+(selectedItems.Count>0?1:0)+(selectedCorpses.Count>0?1:0);
 static SelectionKind FirstSelectionKind=>selectedActors.Count>0?SelectionKind.Ai:selectedItems.Count>0?SelectionKind.Items:selectedCorpses.Count>0?SelectionKind.Corpses:SelectionKind.Items;
 static ContextAction[] CategoryActions(Action<SelectionKind> apply){var result=new System.Collections.Generic.List<ContextAction>();if(selectedActors.Count>0)result.Add(new("AI ×"+selectedActors.Count,true,()=>apply(SelectionKind.Ai)));if(selectedItems.Count>0)result.Add(new(I18n.Text("物品")+" ×"+selectedItems.Count,true,()=>apply(SelectionKind.Items)));if(selectedCorpses.Count>0)result.Add(new(I18n.Text("尸体")+" ×"+selectedCorpses.Count,true,()=>apply(SelectionKind.Corpses)));return result.ToArray();}
 static void SetContext(ContextAction[] actions){contextActions=actions;contextChoice=EditorMenuRules.Next(Array.ConvertAll(actions,a=>a.Enabled),-1,1);contextTop=0;contextFrame=Time.frameCount;contextPointer=MousePosition;Modal("context");}
 static void OpenEditContext(SelectionKind kind){
  selectionKind=kind;TargetAt(0);categoryMenu=false;contextTitle="场景编辑";bool selected=SelectionCount>0;
  var available=new System.Collections.Generic.Dictionary<string,ContextAction>();
  void Add(string id,bool enabled,Action execute,string shortcut="")=>available[id]=new(EditorContextRules.Options.First(o=>o.Id==id).Name,enabled,execute,shortcut);
  void Toggle(string id,string name,bool enabled,Action execute)=>available[id]=new(name,enabled,execute);
  Add("create",true,BeginCreate,ShortcutLabel(EditorShortcut.Create));Add("move",selected,BeginMove,ShortcutLabel(EditorShortcut.Move));Add("copy",selected,Copy,ShortcutLabel(EditorShortcut.Copy));Add("paste",clipboard.Length>0,Paste,ShortcutLabel(EditorShortcut.Paste));Add("duplicate",selected,()=>ApplyShortcut(EditorShortcut.Duplicate,selectionKind),ShortcutLabel(EditorShortcut.Duplicate));
  if(kind==SelectionKind.Ai&&selected){
   Add("rename",true,BeginRename,ShortcutLabel(EditorShortcut.Rename));Add("kill",true,Kill,ShortcutLabel(EditorShortcut.Kill));
   if(SelectedActors().Any(AiFreePlacement.Pinned))Add("release-air",true,ReleaseAir,ShortcutLabel(EditorShortcut.ReleaseAir));
   Add("remove-weapons",!AiEquipmentRemoval.Busy&&!AiSceneEquipment.Busy,ContextRemoveWeapons);
   Add("team-a",true,()=>ContextTeam(1));Add("team-b",true,()=>ContextTeam(2));Add("team-clear",true,()=>ContextTeam(0));
   var targets=SelectedActors();
   bool pause=EditorContextRules.EnableNext(targets.Select(ContextPaused).ToArray()),invincible=EditorContextRules.EnableNext(targets.Select(p=>AiActorControl.EditorState(p).Invincible).ToArray()),peace=EditorContextRules.EnableNext(targets.Select(AiActorControl.EditorPlayerCeasefire).ToArray());
   bool hostile=!AiActorControl.EditorSidesHostile();
   Toggle("pause-toggle",pause?"暂停活动":"恢复活动",true,ContextTogglePause);
   Toggle("ab-toggle",hostile?"A ↔ B 双方敌对":"A ↔ B 双方停战",AiActorControl.EditorSidesReady,ContextToggleSides);
   Toggle("player-toggle",peace?"对玩家停战":"对玩家敌对",true,ContextTogglePlayerRelation);Add("player-default",true,()=>ContextPlayerRelation(0));
   Toggle("invincible-toggle",invincible?"开启无敌":"关闭无敌",true,ContextToggleInvincible);Add("clear-effects",true,ContextClearEffects);
  }
  Add("delete",selected,Delete,ShortcutLabel(EditorShortcut.Delete));if(kind==SelectionKind.Corpses)Add("revive",selected,ReviveCorpses);else Add("properties",selected,OpenProperties,ShortcutLabel(EditorShortcut.Properties));
  var actions=EditorContextRules.Read(contextOrder.Value).Where(available.ContainsKey).Select(id=>available[id]).ToList();
  actions.Add(new("自定义右键菜单",true,OpenContextSettings));
  if(SelectionKinds>1)actions.Add(new("返回类别选择",true,OpenContext));SetContext(actions.ToArray());
 }
 static void ContextInput(){
  if(Time.frameCount<=contextFrame)return;
  if(Input.GetMouseButtonDown(1)){if(!categoryMenu&&SelectionKinds>1)OpenContext();else CloseDialog();return;}
  var available=Array.ConvertAll(contextActions,a=>a.Enabled);float wheel=Input.mouseScrollDelta.y;
  if(wheel!=0){contextChoice=EditorMenuRules.Next(available,contextChoice,wheel>0?-1:1);contextPointer=MousePosition;}
  else if(Input.GetKeyDown(KeyCode.DownArrow))contextChoice=EditorMenuRules.Next(available,contextChoice,1);
  else if(Input.GetKeyDown(KeyCode.UpArrow))contextChoice=EditorMenuRules.Next(available,contextChoice,-1);
  else if((MousePosition-contextPointer).sqrMagnitude>1){contextPointer=MousePosition;var bounds=ContextBounds;int row=(int)((contextPointer.y-bounds.y-28)/28);if(contextPointer.y>=bounds.y+28&&row>=0&&row<ContextVisibleRows&&contextPointer.x>=bounds.x&&contextPointer.x<=bounds.xMax&&contextActions[row+contextTop].Enabled)contextChoice=row+contextTop;}
  if(contextChoice>=0){if(contextChoice<contextTop)contextTop=contextChoice;else if(contextChoice>=contextTop+ContextVisibleRows)contextTop=contextChoice-ContextVisibleRows+1;}
  if((Input.GetMouseButtonDown(0)||Input.GetKeyDown(KeyCode.Return))&&contextChoice>=0&&contextChoice<contextActions.Length&&contextActions[contextChoice].Enabled)Run(contextActions[contextChoice].Execute);
 }
 static void DrawContext(){
  var rect=ContextBounds;InterfaceMenu.Surface(rect,InterfaceMenu.Background,3);InterfaceMenu.Surface(new Rect(rect.x,rect.y,rect.width,25),Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.07f),3);
  InterfaceMenu.Label(new Rect(rect.x+10,rect.y,rect.width-20,25),categoryMenu?I18n.Text(contextTitle):I18n.Text(contextTitle)+(SelectionCount>0?" · "+(selectionKind==SelectionKind.Ai?"AI":I18n.Text(selectionKind==SelectionKind.Items?"物品":"尸体"))+" ×"+SelectionCount:""),InterfaceMenu.Accent,12);
  float keyWidth=ContextKeyWidth;
  for(int i=contextTop;i<Math.Min(contextActions.Length,contextTop+ContextVisibleRows);i++){var action=contextActions[i];var row=new Rect(rect.x+4,rect.y+28+(i-contextTop)*28,rect.width-8,26);if(i==contextChoice)InterfaceMenu.Surface(row,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Accent,.27f),2);var color=action.Enabled?InterfaceMenu.Text:Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.3f);InterfaceMenu.Label(new Rect(row.x+10,row.y,Math.Max(1,row.width-(action.Shortcut.Length>0?keyWidth+38:30)),row.height),action.Name,color,13);if(action.Shortcut.Length>0)InterfaceMenu.Label(new Rect(row.xMax-keyWidth-22,row.y,keyWidth,row.height),action.Shortcut,color,11);if(i==contextChoice)InterfaceMenu.Label(new Rect(row.xMax-20,row.y,18,row.height),"›",InterfaceMenu.Accent,15);}
  InterfaceMenu.Label(new Rect(rect.x+10,rect.yMax-25,rect.width-20,22),"滚轮选择 · 左键进入",Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.65f),11);
 }
}
