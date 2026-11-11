namespace LocalSpt5Esp;
internal enum EditorShortcut { None,Copy,Paste,Delete }
internal static class EditorShortcutRules {
 internal static bool Allowed(string dialog,bool moving,bool dragging,bool externalInput,bool cursorAvailable)=>
  (dialog is "" or "context")&&!moving&&!dragging&&!externalInput&&cursorAvailable;
 internal static EditorShortcut Read(bool control,bool shift,bool copy,bool paste,bool delete){
  if(shift)return EditorShortcut.None;
  if(control)return copy?EditorShortcut.Copy:paste?EditorShortcut.Paste:EditorShortcut.None;
  return delete?EditorShortcut.Delete:EditorShortcut.None;
 }
}
