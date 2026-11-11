using System.Collections.Generic;
using BepInEx.Configuration;
using EFT;
using UnityEngine;
using Shortcut=BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 static readonly Dictionary<EditorShortcut,ConfigEntry<Shortcut>> shortcuts=new();
 static ConfigEntry<Shortcut>? toggleEditor;
 static void InitializeShortcuts(){
  shortcuts.Clear();
  void Bind(EditorShortcut action,string key,string name,KeyCode main,params KeyCode[] modifiers)=>shortcuts[action]=FeatureConfig.Bind("63. 场景编辑",key,name,new Shortcut(main,modifiers),"仅在编辑模式下生效；支持组合键，输入窗口中不触发。可在热键页清除绑定。");
  toggleEditor=FeatureConfig.Bind("63. 场景编辑","Editor toggle shortcut","切换编辑模式热键",new Shortcut(KeyCode.F6),"F6 切换编辑模式，可在全部热键中修改；聊天、属性输入和其他界面占用输入时不触发。");
  Bind(EditorShortcut.Create,"Editor create shortcut","编辑：创建",KeyCode.Insert);
  Bind(EditorShortcut.Move,"Editor move shortcut","编辑：移动",KeyCode.G);
  Bind(EditorShortcut.Copy,"Editor copy shortcut","编辑：复制",KeyCode.C,KeyCode.LeftControl);
  Bind(EditorShortcut.Paste,"Editor paste shortcut","编辑：粘贴",KeyCode.V,KeyCode.LeftControl);
  Bind(EditorShortcut.Duplicate,"Editor duplicate shortcut","编辑：复制并粘贴",KeyCode.D,KeyCode.LeftControl);
  Bind(EditorShortcut.Delete,"Editor delete shortcut","编辑：删除",KeyCode.Delete);
  Bind(EditorShortcut.Rename,"Editor rename shortcut","编辑：重命名",KeyCode.F2);
  Bind(EditorShortcut.Properties,"Editor properties shortcut","编辑：属性",KeyCode.F4);
  Bind(EditorShortcut.Kill,"Editor kill shortcut","编辑：杀死 AI",KeyCode.None);
  Bind(EditorShortcut.ReleaseAir,"Editor release air shortcut","编辑：解除空中固定",KeyCode.None);
 }
 static string ShortcutLabel(EditorShortcut action)=>shortcuts.TryGetValue(action,out var key)?ShortcutBindings.Label(key.Value):"None";
 static string ShortcutHint=>I18n.Text("移动")+" "+ShortcutLabel(EditorShortcut.Move)+" · "+I18n.Text("复制")+" "+ShortcutLabel(EditorShortcut.Copy)+" · "+I18n.Text("粘贴")+" "+ShortcutLabel(EditorShortcut.Paste)+" · "+I18n.Text("删除")+" "+ShortcutLabel(EditorShortcut.Delete);
 static void ToggleShortcut(){
  if(toggleEditor==null||!RaidClock.Active||InterfaceMenu.Open||RaidChat.InputBlocked||FloatingStatus.Capturing||!Application.isFocused||DirectorStage.FreeActive||dialog is not ("" or "context")||Cursor.visible&&!cursorOwned)return;
  if(ShortcutBindings.Take(toggleEditor)){enabled!.Value=!enabled.Value;shortcutFrame=Time.frameCount;}
 }
 static void BeginCreate(){query=lastQuery="";items=System.Array.Empty<(string,string)>();createItemSample=null;createItemEditor=null;Modal("create");}
 static void BeginRename(){rename=Actor().Profile.Nickname;Modal("rename");}
 static bool ShortcutInput(GameWorld current){
  bool external=InterfaceMenu.Open||RaidChat.InputBlocked||FloatingStatus.Capturing||!Application.isFocused||DirectorStage.FreeActive;
  bool cursor=dialog=="context"||altCursor||!Cursor.visible&&!gate.Blocked;
  if(!EditorShortcutRules.Allowed(dialog,moving,dragging,external,cursor))return false;
  var action=EditorShortcut.None;
  foreach(var pair in shortcuts)if(ShortcutBindings.Take(pair.Value,true)){action=pair.Key;break;}
  if(action==EditorShortcut.None)return false;
  shortcutFrame=Time.frameCount;
  if(action==EditorShortcut.Create){if(dialog.Length==0)ReadPoint(current,PointerRay(AltDown));BeginCreate();return true;}
  if(action==EditorShortcut.Paste){
   if(clipboard.Length==0){FeatureToasts.Show("剪贴板为空。");return true;}
   if(dialog.Length==0)ReadPoint(current,PointerRay(AltDown));
   Paste();return true;
  }
  PruneSelection();
  if(!HasSelection){FeatureToasts.Show("请先选择对象。");return true;}
  if(action==EditorShortcut.Duplicate&&dialog.Length==0)ReadPoint(current,PointerRay(AltDown));
  if(SelectionKinds>1&&(dialog!="context"||categoryMenu)){
   contextAnchor=altCursor?MousePosition+new Vector2(14,10):new(Screen.width/2f+14,Screen.height/2f+10);
   categoryMenu=true;contextTitle="选择对象类别";
   SetContext(CategoryActions(kind=>ApplyShortcut(action,kind)));
  }else ApplyShortcut(action,SelectionKinds==1?FirstSelectionKind:selectionKind);
  return true;
 }
 static void ApplyShortcut(EditorShortcut action,SelectionKind kind){
  selectionKind=kind;TargetAt(0);
  switch(action){
   case EditorShortcut.Copy:Copy();FeatureToasts.Show(notice);break;
   case EditorShortcut.Duplicate:Copy();Paste();break;
   case EditorShortcut.Delete:Delete();break;
   case EditorShortcut.Move:BeginMove();break;
   case EditorShortcut.Properties:if(kind!=SelectionKind.Corpses)OpenProperties();break;
   case EditorShortcut.Rename:if(kind==SelectionKind.Ai)BeginRename();else FeatureToasts.Show("仅支持 AI。");break;
   case EditorShortcut.Kill:if(kind==SelectionKind.Ai)Kill();else FeatureToasts.Show("仅支持 AI。");break;
   case EditorShortcut.ReleaseAir:if(kind==SelectionKind.Ai)ReleaseAir();else FeatureToasts.Show("仅支持 AI。");break;
  }
 }
}
