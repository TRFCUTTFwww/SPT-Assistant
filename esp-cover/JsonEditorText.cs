using LocalSpt.Shared;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class JsonEditorText {
 internal static string Accept(string next){
  string normalized=JsonEditPath.EditorText(next);
  if(normalized==next)return next;
  var editor=GUIUtility.GetStateObject(Il2CppInterop.Runtime.Il2CppType.Of<TextEditor>(),GUIUtility.keyboardControl)?.TryCast<TextEditor>();
  if(editor!=null){
   int cursor=JsonEditPath.EditorPosition(next,editor.cursorIndex),selection=JsonEditPath.EditorPosition(next,editor.selectIndex);
   editor.text=normalized;editor.cursorIndex=cursor;editor.selectIndex=selection;
  }
  return normalized;
 }
}
