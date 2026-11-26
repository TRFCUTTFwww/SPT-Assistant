using BepInEx.Configuration;
using UnityEngine;
namespace Local.HotModules;
internal static class ModuleSettingsGui {
 internal static void CloseMenu(){
  foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Reverse()){
   var plugin=assembly.GetType("LocalSpt5Esp.Plugin");if(plugin?.GetField("Self",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)?.GetValue(null)==null)continue;
   assembly.GetType("LocalSpt5Esp.InterfaceMenu")?.GetMethod("CloseForExternalPanel",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)?.Invoke(null,null);break;
  }
 }
 internal static void Draw(Rect rect,ref Vector2 scroll,ConfigEntryBase[] entries,Action<ConfigEntryBase,Rect,string> draw,Func<ConfigEntryBase,string>? name=null){
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,Math.Max(40,rect.width-22),entries.Length*102));
  try{for(int i=0;i<entries.Length;i++)draw(entries[i],new Rect(0,i*102,rect.width-28,92),name?.Invoke(entries[i])??entries[i].Definition.Key);}finally{GUI.EndScrollView();}
 }
}
