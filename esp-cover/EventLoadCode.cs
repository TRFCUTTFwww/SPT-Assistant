using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using EFT;
using Il2CppInterop.Runtime;
namespace LocalSpt5Esp;
internal static class EventLoadCode {
 internal static object RestoreStateMachine(){
  var method=HarmonyLib.AccessTools.Method(typeof(TarkovApplication._LoadMapAndData_d__237),"MoveNext");
  var patches=HarmonyLib.Harmony.GetPatchInfo(method);if(patches!=null&&patches.Owners.Count>0)throw new InvalidOperationException("MoveNext still has Harmony owners");
  var patcher=HarmonyLib.Public.Patching.PatchManager.GetMethodPatcher(method);
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=patcher.GetType();
  var field=type.GetField("nativeDetour",flags)??throw new InvalidOperationException("Native patcher missing");var detour=(IDisposable?)field.GetValue(patcher);
  var original=(Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo.INativeMethodInfoStruct)type.GetField("originalNativeMethodInfo",flags)!.GetValue(patcher)!;
  var modified=(Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo.INativeMethodInfoStruct)type.GetField("modifiedNativeMethodInfo",flags)!.GetValue(patcher)!;
  modified.MethodPointer=original.MethodPointer;detour?.Dispose();field.SetValue(patcher,null);
  var bytes=new byte[12];Marshal.Copy(original.MethodPointer,bytes,0,bytes.Length);return new{restored=detour!=null,address=original.MethodPointer.ToInt64(),bytes=Convert.ToHexString(bytes)};
 }
 internal static void Resolve(string home){System.Threading.Tasks.Task.Run(()=>{
  var thread=IL2CPP.il2cpp_thread_attach(IL2CPP.il2cpp_domain_get());
  try{
   var targets=new HashSet<long>(JsonSerializer.Deserialize<long[]>(File.ReadAllText(Path.Combine(home,"native-load-call-targets.json")))!);var result=new Dictionary<long,List<string>>();int scanned=0;
   var images=(IntPtr[])typeof(IL2CPP).GetMethod("GetIl2CppImages",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static)!.Invoke(null,null)!;
   foreach(var image in images){
    string imageName=IL2CPP.il2cpp_image_get_name_(image);if(imageName!="Assembly-CSharp.dll"&&imageName!="CommonExtensions.dll"&&imageName!="Comfort.Unity.dll"&&imageName!="UnityEngine.CoreModule.dll")continue;
    uint count=IL2CPP.il2cpp_image_get_class_count(image);for(uint index=0;index<count;index++){
     var type=IL2CPP.il2cpp_image_get_class(image,index);IntPtr iterator=IntPtr.Zero;IntPtr method;
     while((method=IL2CPP.il2cpp_class_get_methods(type,ref iterator))!=IntPtr.Zero){scanned++;long address=Marshal.ReadIntPtr(method).ToInt64();if(!targets.Contains(address))continue;
      if(!result.TryGetValue(address,out var names))result[address]=names=new List<string>();if(names.Count>=8)continue;names.Add(IL2CPP.il2cpp_class_get_namespace_(type)+"."+IL2CPP.il2cpp_class_get_name_(type)+"."+IL2CPP.il2cpp_method_get_name_(method));
     }
    }
   }
   File.WriteAllText(Path.Combine(home,"native-load-symbols.json"),JsonSerializer.Serialize(new{scanned,targets=targets.Count,methods=result}));
  }catch(Exception error){File.WriteAllText(Path.Combine(home,"native-load-symbols.json"),JsonSerializer.Serialize(new{error=error.ToString()}));}
  finally{IL2CPP.il2cpp_thread_detach(thread);}
 });}
 internal static void Dump(string home){
  new HarmonyLib.Harmony("local.halloween2026.map-wait-probe").UnpatchSelf();
  var methods=new Dictionary<long,string>();var code=new List<object>();
  foreach(var type in new[]{typeof(TarkovApplication),typeof(TarkovApplication._LoadMapAndData_d__237),typeof(GameWorld),typeof(LoadScenesFromPresetOperation),typeof(LoadScenesFromPresetOperation._LoadPresetAsync_d__17),typeof(AssetsManagerExtension),typeof(TasksExtensions),typeof(TasksExtensions.OperationAwaiter),typeof(Koenigz.PerfectCulling.EFT.PerfectCullingCrossSceneSampler),typeof(UnityEngine.SceneManagement.SceneManager),typeof(UnityEngine.Resources),typeof(UnityEngine.AsyncOperation),typeof(UnityEngine.Object),typeof(Il2CppSystem.Threading.Tasks.Task),typeof(Il2CppSystem.Runtime.CompilerServices.YieldAwaitable.YieldAwaiter)}){
   foreach(var field in type.GetFields(BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public))if(field.Name.StartsWith("NativeMethodInfoPtr_")){var info=(IntPtr)field.GetValue(null)!;if(info==IntPtr.Zero)continue;var address=Marshal.ReadIntPtr(info);if(address==IntPtr.Zero)continue;methods[address.ToInt64()]=type.FullName+"."+field.Name;
    if((type==typeof(TarkovApplication._LoadMapAndData_d__237)||type==typeof(LoadScenesFromPresetOperation._LoadPresetAsync_d__17))&&field.Name.Contains("MoveNext")||type==typeof(LoadScenesFromPresetOperation)&&field.Name.Contains("Execute")||type==typeof(AssetsManagerExtension)&&field.Name.Contains("LoadScenesFromPreset")){var bytes=new byte[12288];Marshal.Copy(address,bytes,0,bytes.Length);code.Add(new{method=type.FullName+"."+field.Name,address=address.ToInt64(),bytes=Convert.ToBase64String(bytes)});}
   }
  }
  File.WriteAllText(Path.Combine(home,"native-load-code.json"),JsonSerializer.Serialize(new{methods,code}));
 }
}
