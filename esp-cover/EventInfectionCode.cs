using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text.Json;
using EFT;
namespace LocalSpt5Esp;
internal static class EventInfectionCode {
 internal static void Dump(string home){
  var methods=new Dictionary<long,string>();var sections=new List<(string Name,long Address,byte[] Bytes)>();var offsets=new Dictionary<string,int>();foreach(var type in new[]{typeof(BotMover),typeof(BotOwner),typeof(BotMemory),typeof(EnemyInfo)})foreach(var field in type.GetFields(BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public))if(field.Name.StartsWith("NativeFieldInfoPtr_")){var ptr=(IntPtr)field.GetValue(null)!;if(ptr!=IntPtr.Zero)offsets[type.Name+"."+field.Name]=unchecked((int)Il2CppInterop.Runtime.IL2CPP.il2cpp_field_get_offset(ptr));}File.WriteAllText(Path.Combine(home,"infection-field-offsets.json"),JsonSerializer.Serialize(offsets));
  foreach(var type in new[]{typeof(BotMover),typeof(BotMoverStateMachine),typeof(BotPathController),typeof(BotHalloweenWithZombies),typeof(BotHalloweenCrowdSpawn),typeof(BotHalloweenDistributions),typeof(BotSpawner),typeof(BotsController),typeof(BotsEventsController),typeof(BotsList)})
   foreach(var field in type.GetFields(BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public))if(field.Name.StartsWith("NativeMethodInfoPtr_")){
    var info=(IntPtr)field.GetValue(null)!;if(info==IntPtr.Zero)continue;var address=Marshal.ReadIntPtr(info);if(address==IntPtr.Zero)continue;
    string name=type.Name+"."+field.Name;methods[address.ToInt64()]=name;
    if(type==typeof(BotMover)&&field.Name.Contains("_Teleport_")||type==typeof(BotsController)&&field.Name.StartsWith("NativeMethodInfoPtr_Init_")||type==typeof(BotHalloweenWithZombies)&&(field.Name.Contains("_Activate_")||field.Name.Contains("_SpawnAction_")||field.Name.Contains("_AddBackupProfiles_")||field.Name.Contains("_ManualUpdate_")||field.Name.Contains("_GetProfilesOnStart_"))){var bytes=new byte[8192];Marshal.Copy(address,bytes,0,bytes.Length);sections.Add((name,address.ToInt64(),bytes));}
   }
  foreach(var section in sections)File.WriteAllText(Path.Combine(home,section.Name+".json"),JsonSerializer.Serialize(new{methods,code=new[]{new{method=section.Name,address=section.Address,bytes=Convert.ToBase64String(section.Bytes)}}}));
 }
}
