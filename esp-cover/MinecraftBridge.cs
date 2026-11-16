using System;
using System.IO;
using AllTheModium.Client;
using ProjectE.Client;
using Local.ModHost;
namespace LocalSpt5Esp;
internal static class MinecraftBridge {
 static SwordModule? swords;static ProjectModule? project;
 static ModuleContext? swordContext,projectContext;static float nextInitialize;
 internal static bool CanReload=>(swords?.CanReload??true)&&(project?.CanReload??true);
 internal static void Initialize(){
  if(!FunItemClient.Known||!FunItemClient.Enabled)return;
  ModuleContext Context(string id,string folder){string root=Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","Minecraft",folder),data=Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","Minecraft","state",id);Directory.CreateDirectory(data);return new(id,root,data,Plugin.Self.Patches.Id,(level,message)=>{if(level==LogLevel.Error)Plugin.Self.Log.LogError(message);else if(level==LogLevel.Warning)Plugin.Self.Log.LogWarning(message);else Plugin.Self.Log.LogInfo(message);});}
  try{swordContext=Context("atm-swords","AllTheModium");swords=new();swords.Initialize(swordContext);projectContext=Context("projecte","ProjectE");project=new();project.Initialize(projectContext);}catch{Reset();throw;}
 }
 internal static void Tick(){FunItemClient.Tick();if(!FunItemClient.Enabled){if((swords!=null||project!=null)&&CanReload)Reset();return;}if(swords==null&&project==null){if(UnityEngine.Time.unscaledTime<nextInitialize)return;try{Initialize();}catch(Exception error){nextInitialize=UnityEngine.Time.unscaledTime+10;Plugin.Self.Log.LogWarning("Minecraft initialization: "+error.Message);return;}}swords?.Tick();project?.Tick();}
 internal static void Late(){swords?.LateTick();project?.LateTick();}
 internal static void Draw(){swords?.Draw();project?.Draw();}
 internal static void Reset(){try{try{project?.Shutdown();}finally{projectContext?.Release();}}finally{try{try{swords?.Shutdown();}finally{swordContext?.Release();}}finally{project=null;swords=null;projectContext=null;swordContext=null;}}}
}
