namespace LocalSpt5Esp;
internal enum EditorShortcut { None,Create,Move,Copy,Paste,Duplicate,Delete,Rename,Properties,Kill,ReleaseAir }
internal static class EditorShortcutRules {
 internal static bool Allowed(string dialog,bool moving,bool dragging,bool externalInput,bool cursorAvailable)=>
  (dialog is "" or "context")&&!moving&&!dragging&&!externalInput&&cursorAvailable;
}
