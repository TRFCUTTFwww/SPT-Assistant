using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using Il2CppInterop.Runtime.InteropTypes;
namespace LocalSpt5Esp;

// Optional integration: the single-player client still loads without Fika.
internal static class FikaTeams {
 static readonly Dictionary<IntPtr,bool> teammates=new();
 static readonly Dictionary<IntPtr,bool?> observed=new();
 static Type? fikaType;static MethodInfo? cast;static FieldInfo? observedAi;
 static bool resolved,available;
 static void Resolve() {
  if(resolved)return;resolved=true;
  fikaType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Fika.Core.Main.Players.FikaPlayer",false)).FirstOrDefault(t=>t!=null);
  available=fikaType!=null;
  if(fikaType==null)return;
  observedAi=fikaType.GetField("IsObservedAI",BindingFlags.Public|BindingFlags.Instance);
  cast=typeof(Il2CppObjectBase).GetMethods(BindingFlags.Public|BindingFlags.Instance).FirstOrDefault(m=>m.Name=="TryCast"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==0)?.MakeGenericMethod(fikaType);
 }
 static bool? ObservedAi(Player actor) {
  if(observed.TryGetValue(actor.Pointer,out var cached))return cached;
  bool? value=null;
  try { if(cast!=null&&observedAi!=null&&cast.Invoke(actor,null) is {} fika)value=observedAi.GetValue(fika) as bool?; }catch { /* Group fallback remains available for human teammates. */ }
  return observed[actor.Pointer]=value;
 }
 internal static void BeginScan() { teammates.Clear();observed.Clear(); }
 internal static void Reset() { BeginScan(); }
 internal static bool IsTeammate(Player actor) {
  if(teammates.TryGetValue(actor.Pointer,out var found))return found;
  Resolve();var local=Singleton<GameWorld>.Instance?.MainPlayer;
  bool result=local!=null&&FikaTeamRules.Teammate(actor.IsYourPlayer||actor.Pointer==local.Pointer,!string.IsNullOrEmpty(actor.ProfileId)&&actor.ProfileId==local.ProfileId,actor.IsAI||actor.AIData?.BotOwner!=null,available?ObservedAi(actor):null,available,actor.Profile?.Info?.GroupId??"",local.Profile?.Info?.GroupId??"");
  return teammates[actor.Pointer]=result;
 }
 internal static bool IsAi(Player actor) { Resolve();return !IsTeammate(actor)&&(actor.IsAI||actor.AIData?.BotOwner!=null||available&&ObservedAi(actor)==true); }
}
