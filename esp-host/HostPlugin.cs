using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
namespace LocalSpt5Esp.HotReload;
[BepInPlugin("local.spt5.esp","SPT5 中文辅助","1.5.0")]
public sealed class HostPlugin:BasePlugin {
 internal static HostPlugin? Instance;
 internal RuntimeReloader? Runtime;
 public override void Load(){
  Instance=this;
  Runtime=new RuntimeReloader(Config,Log,Path.GetDirectoryName(typeof(HostPlugin).Assembly.Location)!,RefreshMenu);
  AddComponent<RuntimeDriver>();Runtime.Start();
 }
 internal void Update(){
  if(Input.GetKeyDown(KeyCode.F1)&&(Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl)))Runtime?.RequestReload();
  Runtime?.Update();
 }
 void RefreshMenu(){
  var manager=IL2CPPChainloader.Instance.Plugins.Values.FirstOrDefault(p=>p.Metadata.GUID=="com.bepis.bepinex.configurationmanager")?.Instance;
  manager?.GetType().GetMethod("BuildSettingList",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.Invoke(manager,null);
 }
 public override bool Unload(){try{Runtime?.Dispose();}finally{Runtime=null;Instance=null;}return true;}
}
public sealed class RuntimeDriver:MonoBehaviour {
 public RuntimeDriver(IntPtr pointer):base(pointer){}
 public void Update()=>HostPlugin.Instance?.Update();
 public void LateUpdate()=>HostPlugin.Instance?.Runtime?.LateUpdate();
 public void OnGUI()=>HostPlugin.Instance?.Runtime?.Draw();
}
