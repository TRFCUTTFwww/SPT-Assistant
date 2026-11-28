using System.Diagnostics;
using System.Runtime.CompilerServices;
using HarmonyLib;
using LocalSpt5Esp;

public sealed class HookSampleTarget {
 [MethodImpl(MethodImplOptions.NoInlining)]
 public string Compute(int input,bool throwError=false){
  if(throwError)throw new InvalidOperationException("boom");
  return "orig:"+input;
 }
}

internal static class PersistentHarmonyRouterChecks {
 struct PrivateStructState { public int Original; public string Tag; }
 sealed record PrivateClassState(int Value);
 static int orderLog;

 // Gen 1 handlers
 static bool Gen1Prefix(HookSampleTarget __instance,ref int __0,out PrivateStructState __state){
  __state=new(){Original=__0,Tag="g1"};
  __0+=10;
  return true;
 }
 static void Gen1Postfix(HookSampleTarget __instance,ref string __result,PrivateStructState __state,bool __runOriginal){
  if(__runOriginal)__result+=$"|{__state.Tag}:{__state.Original}";
 }
 static Exception? Gen1Finalizer(Exception? __exception)=>__exception is InvalidOperationException?null:__exception;

 // Gen 2 handlers (same signatures, completely new logic)
 static bool Gen2Prefix(HookSampleTarget __instance,ref int __0,out PrivateStructState __state){
  __state=new(){Original=__0,Tag="g2"};
  __0*=3;
  return true;
 }
 static void Gen2Postfix(HookSampleTarget __instance,ref string __result,PrivateStructState __state,bool __runOriginal){
  if(__runOriginal)__result=$"gen2({__result},{__state.Tag},{__state.Original})";
 }
 static Exception? Gen2Finalizer(Exception? __exception)=>__exception;

 // Nullable struct & private class __state + multi-patch ordinal checks
 static void NullablePre(ref int __0,out float? __state){__state=__0==0?null:12.5f;}
 static void NullablePost(ref string __result,float? __state){if(__state is float f)__result+=$"|nf:{f:0.#}";}
 static void ClassPre(out PrivateClassState? __state){__state=new(77);orderLog=orderLog*10+1;}
 static void ClassPost(PrivateClassState? __state){if(__state!=null)orderLog=orderLog*10+__state.Value;}
 static void ExtraSameSigPre(out PrivateClassState? __state){__state=new(9);orderLog=orderLog*10+2;}
 static void ExtraSameSigPost(PrivateClassState? __state){if(__state!=null)orderLog=orderLog*10+__state.Value;}

 internal static void Run(){
  void Check(bool ok,string msg){if(!ok)throw new Exception("HookRouter: "+msg);}
  var method=AccessTools.Method(typeof(HookSampleTarget),nameof(HookSampleTarget.Compute))!;
  var target=new HookSampleTarget();

  // Generation 1: initial bridge creation
  var gen1=new PersistentHarmonyPatcher("test.router.gen1");
  gen1.Patch(method,
   prefix:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(Gen1Prefix)),
   postfix:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(Gen1Postfix)),
   finalizer:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(Gen1Finalizer)));
  gen1.Patch(method,
   prefix:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(NullablePre)),
   postfix:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(NullablePost)));
  gen1.Patch(method,
   prefix:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(ClassPre)),
   postfix:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(ClassPost)));
  gen1.Patch(method,
   prefix:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(ExtraSameSigPre)),
   postfix:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(ExtraSameSigPost)));

  Check(gen1.CreatedCount==4&&gen1.ReusedCount==0,"first generation creates 4 persistent bridge slots");
  orderLog=0;
  string r1=target.Compute(5);
  Check(r1=="orig:15|g1:5|nf:12.5","prefix ref arg, struct __state, nullable __state and postfix __result work in Gen 1: "+r1);
  Check(orderLog>0,"multiple patches with identical signatures execute via distinct ordinals");
  Check(target.Compute(-10)=="orig:0|g1:-10","null Nullable<float> __state round-trips cleanly");
  Check(target.Compute(1,throwError:true)==null,"finalizer suppresses InvalidOperationException in Gen 1");

  // Unload Gen 1: O(N) pointer clear, zero Harmony.Unpatch
  var sw=Stopwatch.StartNew();
  gen1.DeactivateAll();
  double unloadMs=sw.Elapsed.TotalMilliseconds;
  Check(target.Compute(5)=="orig:5","deactivated bridges pass straight through to original method with zero mutation");
  bool threw=false;
  try{target.Compute(5,throwError:true);}catch(InvalidOperationException){threw=true;}
  Check(threw,"deactivated finalizer preserves original exception");

  // Generation 2: 100% bridge reuse, zero Harmony.Patch / DobbyDetour
  sw.Restart();
  var gen2=new PersistentHarmonyPatcher("test.router.gen2");
  gen2.Patch(method,
   prefix:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(Gen2Prefix)),
   postfix:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(Gen2Postfix)),
   finalizer:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(Gen2Finalizer)));
  gen2.Patch(method,
   prefix:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(NullablePre)),
   postfix:new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(NullablePost)));
  double rebindMs=sw.Elapsed.TotalMilliseconds;

  Check(gen2.CreatedCount==0&&gen2.ReusedCount==2,"second generation reuses 100% of existing bridge slots with zero new detours");
  string r2=target.Compute(5);
  Check(r2=="gen2(orig:15,g2,5)|nf:12.5","rebound Gen 2 delegates execute updated logic immediately: "+r2);
  gen2.DeactivateAll();
  Check(target.Compute(5)=="orig:5","Gen 2 unload restores pure original behavior");

  // Swap-safe singleton guard test (zero unguarded gap across generations)
  var guardGen1=new PersistentHarmonyPatcher("test.router.guard1");
  guardGen1.PatchSwapSafe("sample-guard",method,new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(Gen1Prefix)));
  Check(target.Compute(5)=="orig:15","swap-safe guard Gen 1 active");
  var guardGen2=new PersistentHarmonyPatcher("test.router.guard2");
  guardGen2.PatchSwapSafe("sample-guard",method,new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(Gen2Prefix)));
  guardGen1.DeactivateAll();
  Check(guardGen2.CreatedCount==0&&guardGen2.ReusedCount==1,"swap-safe guard reuses singleton bridge slot");
  Check(target.Compute(5)=="orig:15","swap-safe guard stays continuously active across Gen 1 DeactivateAll(): "+target.Compute(5));
  // Clean up swap-safe guard slot so HookSampleTarget is clean
  var (_,_,guardFields)=PersistentHarmonyRouter.BindOrCreateSlot(
   $"swap-safe:sample-guard:{PersistentHarmonyRouter.MethodKey(method)}|{PersistentHarmonyRouter.SignatureKey(new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(Gen2Prefix)),null,null)}",
   method,new HarmonyMethod(typeof(PersistentHarmonyRouterChecks),nameof(Gen2Prefix)),null,null);
  foreach(var f in guardFields)f.SetValue(null,null);
  Check(target.Compute(5)=="orig:5","swap-safe slot cleared cleanly");

  Console.WriteLine($"PASS persistent Harmony router: 0 detours on reload, swap-safe guard, struct/nullable/private-class __state, ref args, finalizers (unload={unloadMs:F3}ms, rebind={rebindMs:F3}ms)");
 }
}
