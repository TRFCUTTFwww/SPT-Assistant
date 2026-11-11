using System;
using System.Linq;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 sealed record ContextAction(string Name,bool Enabled,Action Execute);
 static ContextAction[] contextActions=Array.Empty<ContextAction>();
 static int contextChoice,contextFrame;static Vector2 contextAnchor,contextPointer;static bool categoryMenu;static string contextTitle="场景编辑";
 static Rect ContextBounds{get{var b=EditorMenuRules.Popup(contextAnchor.x,contextAnchor.y,252,58+contextActions.Length*28,Screen.width,Screen.height);return new(b.X,b.Y,b.Width,b.Height);}}
 static Vector2 MousePosition=>new(Input.mousePosition.x,Screen.height-Input.mousePosition.y);
 static void OpenContext(){
  PruneSelection();contextAnchor=altCursor?MousePosition+new Vector2(14,10):new(Screen.width/2f+14,Screen.height/2f+10);
  if(selectedActors.Count>0&&selectedItems.Count>0){
   categoryMenu=true;contextTitle="选择对象类别";
   SetContext(new[]{new ContextAction("AI ×"+selectedActors.Count,true,()=>OpenEditContext(SelectionKind.Ai)),new ContextAction(I18n.Text("物品")+" ×"+selectedItems.Count,true,()=>OpenEditContext(SelectionKind.Items))});return;
  }
  OpenEditContext(selectedActors.Count>0?SelectionKind.Ai:SelectionKind.Items);
 }
 static void SetContext(ContextAction[] actions){contextActions=actions;contextChoice=0;contextFrame=Time.frameCount;contextPointer=MousePosition;Modal("context");}
 static void OpenEditContext(SelectionKind kind){
  selectionKind=kind;TargetAt(0);categoryMenu=false;contextTitle="场景编辑";bool selected=SelectionCount>0;
  var actions=new System.Collections.Generic.List<ContextAction>{
   new("创建",true,()=>{query=lastQuery="";items=Array.Empty<(string,string)>();Modal("create");}),
   new("移动",selected,BeginMove),new("复制",selected,Copy),new("粘贴",clipboard.Length>0,Paste)
  };
  if(actor!=null){actions.Add(new("重命名",true,()=>{rename=Actor().Profile.Nickname;Modal("rename");}));actions.Add(new("杀死",true,Kill));}
  if(actor!=null&&SelectedActors().Any(AiFreePlacement.Pinned))actions.Add(new("解除空中固定",true,ReleaseAir));
  actions.Add(new("删除",selected,Delete));actions.Add(new("属性",selected,OpenProperties));
  if(selectedActors.Count>0&&selectedItems.Count>0)actions.Add(new("返回类别选择",true,OpenContext));SetContext(actions.ToArray());
 }
 static void ContextInput(){
  if(Time.frameCount<=contextFrame)return;
  if(Input.GetMouseButtonDown(1)){if(!categoryMenu&&selectedActors.Count>0&&selectedItems.Count>0)OpenContext();else CloseDialog();return;}
  var available=Array.ConvertAll(contextActions,a=>a.Enabled);float wheel=Input.mouseScrollDelta.y;
  if(wheel!=0){contextChoice=EditorMenuRules.Next(available,contextChoice,wheel>0?-1:1);contextPointer=MousePosition;}
  else if(Input.GetKeyDown(KeyCode.DownArrow))contextChoice=EditorMenuRules.Next(available,contextChoice,1);
  else if(Input.GetKeyDown(KeyCode.UpArrow))contextChoice=EditorMenuRules.Next(available,contextChoice,-1);
  else if((MousePosition-contextPointer).sqrMagnitude>1){contextPointer=MousePosition;var bounds=ContextBounds;int row=(int)((contextPointer.y-bounds.y-28)/28);if(contextPointer.y>=bounds.y+28&&row>=0&&row<contextActions.Length&&contextPointer.x>=bounds.x&&contextPointer.x<=bounds.xMax&&contextActions[row].Enabled)contextChoice=row;}
  if((Input.GetMouseButtonDown(0)||Input.GetKeyDown(KeyCode.Return))&&contextChoice>=0&&contextChoice<contextActions.Length&&contextActions[contextChoice].Enabled)Run(contextActions[contextChoice].Execute);
 }
 static void DrawContext(){
  var rect=ContextBounds;InterfaceMenu.Surface(rect,InterfaceMenu.Background,3);InterfaceMenu.Surface(new Rect(rect.x,rect.y,rect.width,25),Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.07f),3);
  InterfaceMenu.Label(new Rect(rect.x+10,rect.y,rect.width-20,25),categoryMenu?I18n.Text(contextTitle):I18n.Text(contextTitle)+(SelectionCount>0?" · "+(selectionKind==SelectionKind.Ai?"AI":I18n.Text("物品"))+" ×"+SelectionCount:""),InterfaceMenu.Accent,12);
  for(int i=0;i<contextActions.Length;i++){var action=contextActions[i];var row=new Rect(rect.x+4,rect.y+28+i*28,rect.width-8,26);if(i==contextChoice)InterfaceMenu.Surface(row,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Accent,.27f),2);InterfaceMenu.Label(new Rect(row.x+10,row.y,row.width-30,row.height),action.Name,action.Enabled?InterfaceMenu.Text:Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.3f),13);if(i==contextChoice)InterfaceMenu.Label(new Rect(row.xMax-20,row.y,18,row.height),"›",InterfaceMenu.Accent,15);}
  InterfaceMenu.Label(new Rect(rect.x+10,rect.yMax-25,rect.width-20,22),"滚轮选择 · 左键进入",Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.65f),11);
 }
}
