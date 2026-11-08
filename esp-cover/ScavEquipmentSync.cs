using System;
using System.Collections.Generic;
using System.Text.Json;
using Comfort.Common;
using EFT;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ScavEquipmentSync {
 static Il2CppSystem.Threading.Tasks.Task<Il2CppReferenceArray<ProfileDescriptor>>? pending;
 static readonly Dictionary<string,string> expected=new(StringComparer.Ordinal);
 static ClientBackendSession? session;
 static string owner="",status="";
 static long pet;
 static float deadline;
 static bool checkOnly;
 internal static bool Busy=>pending!=null;
 internal static string Status=>status;
 static TarkovApplication? application;
 static TarkovApplication? App()=>application??=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>()??UnityEngine.Object.FindObjectOfType<TarkovApplication>();
 static ClientBackendSession? Session()=>App()?.GetClientBackEndSession()?.TryCast<ClientBackendSession>()??App()?.Session?.TryCast<ClientBackendSession>();
 internal static string Owner=>Session()?.Profile?.Id??"";
 internal static bool Matching=>App()?._localMatchmakerOperation!=null;
 internal static void Begin(JsonElement saved){
  Reset();
  try{
   foreach(var row in saved.GetProperty("scavItems").EnumerateArray())expected[row.GetProperty("slot").GetString()!]=row.GetProperty("template").GetString()!;
   Start();
   status="Scav 装备已保存，正在同步当前角色…";
  }catch(Exception error){Failed(error);}
 }
 static void Start(){
  session=Session();var current=session?.ProfileOfPet;
  if(current==null||session?.Profile==null||ItemSpawner.InRaid()||App()?._localMatchmakerOperation!=null)throw new InvalidOperationException("Scav preview is unavailable");
  owner=session.Profile.Id;pet=current.Pointer.ToInt64();
  pending=App()!.Session.LoadProfiles<Il2CppReferenceArray<ProfileDescriptor>>();deadline=Time.unscaledTime+20;
 }
 internal static object Check(){
  if(Busy)throw new InvalidOperationException("Scav synchronization is pending");
  Reset();checkOnly=true;
  try{foreach(var slot in Read(Session()?.ProfileOfPet??throw new InvalidOperationException("Scav is unavailable")))expected[slot.Key]=slot.Value;Start();status="Checking native Scav descriptors (read only)";}catch(Exception error){Failed(error);}
  return Diagnostics();
 }
 internal static void Tick(){
  if(pending==null)return;
  try{
   var currentSession=Session();var current=currentSession?.ProfileOfPet;
   if(!ScavEquipmentRules.CanSync(owner,currentSession?.Profile?.Id??"",pet,current?.Pointer.ToInt64()??0,ItemSpawner.InRaid(),App()?._localMatchmakerOperation!=null)||session?.Pointer!=currentSession?.Pointer)throw new InvalidOperationException("Scav session changed");
   if(!pending.IsCompleted){if(Time.unscaledTime>=deadline)throw new TimeoutException("Profile refresh timed out");return;}
   var descriptors=pending.GetAwaiter().GetResult();pending=null;
   ProfileDescriptor? descriptor=null;foreach(var candidate in descriptors)if(candidate!=null&&candidate.Id.ToString()==current!.Id){descriptor=candidate;break;}
   if(descriptor==null)throw new InvalidOperationException("Current Scav was not returned by the server");
   var refreshed=new Profile(descriptor);var slots=Read(refreshed);
   if(!ScavEquipmentRules.Matches(expected,slots))throw new InvalidOperationException("Server Scav equipment differs from the saved preset");
   // Retain the PMC and existing pet identity; only replace the pet inventory
   // after the native descriptor constructor and equipment verification succeed.
   if(checkOnly){status="Native Scav descriptor check passed; inventory unchanged";return;}
   current!.Inventory=refreshed.Inventory;
   status="Scav 装备已保存并同步；重新打开 Scav 出击预览查看。";
   Plugin.Self.Log.LogInfo("Scav equipment saved and verified against the native profile response: "+slots.Count+" slots");
  }catch(Exception error){Failed(error);}
 }
 static Dictionary<string,string> Read(Profile profile){
  var result=new Dictionary<string,string>(StringComparer.Ordinal);
  foreach(var slot in profile.Inventory.Equipment.Slots){string name=slot.Name;if(!ProfileAdminPanel.IsScavSlot(name))continue;var item=slot.ContainedItem;if(item!=null)result[name]=item.StringTemplateId;}
  return result;
 }
 static void Failed(Exception error){pending=null;status=checkOnly?"Native Scav descriptor check failed: "+error.Message:"Scav 装备已保存；当前预览未同步，请重新登录角色后生效。";Plugin.Self.Log.LogWarning("Scav equipment preview sync: "+error.Message);}
 internal static void Reset(){pending=null;session=null;owner=status="";pet=0;checkOnly=false;expected.Clear();}
 internal static object Diagnostics()=>new{busy=Busy,status,slots=expected.Count,checkOnly,application=App()!=null,session=Session()!=null,pmc=Session()?.Profile!=null,scav=Session()?.ProfileOfPet!=null,matching=Matching};
}
