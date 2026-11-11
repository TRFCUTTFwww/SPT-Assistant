using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 static bool ShortcutInput(GameWorld current){
  bool external=InterfaceMenu.Open||RaidChat.InputBlocked||FloatingStatus.Capturing||!Application.isFocused||DirectorStage.FreeActive;
  bool cursor=dialog=="context"||altCursor||!Cursor.visible&&!gate.Blocked;
  if(!EditorShortcutRules.Allowed(dialog,moving,dragging,external,cursor))return false;
  var action=EditorShortcutRules.Read(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl),Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift),Input.GetKeyDown(KeyCode.C),Input.GetKeyDown(KeyCode.V),Input.GetKeyDown(KeyCode.Delete));
  if(action==EditorShortcut.None)return false;
  shortcutFrame=Time.frameCount;
  if(action==EditorShortcut.Paste){
   if(clipboard.Length==0){FeatureToasts.Show("剪贴板为空。");return true;}
   if(dialog.Length==0)ReadPoint(current,PointerRay(AltDown));
   Paste();return true;
  }
  PruneSelection();
  if(!HasSelection){FeatureToasts.Show("请先选择对象。");return true;}
  if(selectedActors.Count>0&&selectedItems.Count>0&&(dialog!="context"||categoryMenu)){
   contextAnchor=altCursor?MousePosition+new Vector2(14,10):new(Screen.width/2f+14,Screen.height/2f+10);
   categoryMenu=true;contextTitle="选择对象类别";
   SetContext(new[]{new ContextAction("AI ×"+selectedActors.Count,true,()=>ApplyShortcut(action,SelectionKind.Ai)),new ContextAction(I18n.Text("物品")+" ×"+selectedItems.Count,true,()=>ApplyShortcut(action,SelectionKind.Items))});
  }else ApplyShortcut(action,selectedActors.Count==0?SelectionKind.Items:selectedItems.Count==0?SelectionKind.Ai:selectionKind);
  return true;
 }
 static void ApplyShortcut(EditorShortcut action,SelectionKind kind){
  selectionKind=kind;TargetAt(0);
  if(action==EditorShortcut.Copy){Copy();FeatureToasts.Show(notice);}
  else if(action==EditorShortcut.Delete)Delete();
 }
}
