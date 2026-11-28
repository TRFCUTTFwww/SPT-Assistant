using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace LocalSpt5Esp;

/// <summary>
/// Keeps native IL2CPP / MonoMod Harmony trampolines alive in a process-wide dynamic assembly
/// and swaps managed target delegates in &lt;1 ms across hot-reload generations without calling
/// DobbyDetour or Harmony.Unpatch / Harmony.Patch again for unchanged hook signatures.
/// </summary>
internal static class PersistentHarmonyRouter {
 // Stored in AppDomain.CurrentDomain using only BCL / 0Harmony types so every reloaded
 // Local.Spt5.ESP.Core generation shares the exact same bridge table across LoadContexts.
 // Layout of root object[]:
 //   [0] = Harmony persistentHarmony ("local.spt5.esp.bridge")
 //   [1] = ModuleBuilder dynamicModule ("Local.Spt5.HookBridge")
 //   [2] = ConcurrentDictionary<string, object[]> slots
 //         slot object[] layout:
 //           [0] = Type? prefixDelegateType
 //           [1] = FieldInfo? prefixActiveField
 //           [2] = Type? postfixDelegateType
 //           [3] = FieldInfo? postfixActiveField
 //           [4] = Type? finalizerDelegateType
 //           [5] = FieldInfo? finalizerActiveField
 //           [6] = MethodInfo? representativePatchedMethod
 //   [3] = int[] counter (length 1, locked on root)
 const string DomainKey="LocalSpt5Esp.PersistentHarmonyRouter.v1";

 static object[] SharedState(){
  if(AppDomain.CurrentDomain.GetData(DomainKey) is object[] existing&&existing.Length==4)return existing;
  lock(typeof(AppDomain)){
   if(AppDomain.CurrentDomain.GetData(DomainKey) is object[] raced&&raced.Length==4)return raced;
   var asm=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Local.Spt5.HookBridge"),AssemblyBuilderAccess.Run);
   var mod=asm.DefineDynamicModule("Main");
   var harmony=new Harmony("local.spt5.esp.bridge");
   var slots=new ConcurrentDictionary<string,object[]>(StringComparer.Ordinal);
   var created=new object[]{harmony,mod,slots,new[]{0}};
   AppDomain.CurrentDomain.SetData(DomainKey,created);
   return created;
  }
 }

 internal static bool CanBridge(MethodBase original,HarmonyMethod? prefix,HarmonyMethod? postfix,HarmonyMethod? transpiler,HarmonyMethod? finalizer){
  if(transpiler?.method!=null)return false;
  if(prefix?.method==null&&postfix?.method==null&&finalizer?.method==null)return false;
  var ownAsm=typeof(PersistentHarmonyRouter).Assembly;
  bool Compatible(HarmonyMethod? hm){
   if(hm?.method==null)return true;
   if(!hm.method.IsStatic||hm.method.ContainsGenericParameters)return false;
   if(IsTransientType(hm.method.ReturnType,ownAsm))return false;
   foreach(var p in hm.method.GetParameters()){
    if(p.Name=="__state")continue;
    if(IsTransientType(p.ParameterType,ownAsm))return false;
   }
   return true;
  }
  return !original.ContainsGenericParameters&&Compatible(prefix)&&Compatible(postfix)&&Compatible(finalizer);
 }

 static bool IsTransientType(Type type,Assembly ownAsm){
  if(type.IsByRef||type.IsPointer||type.IsArray)return IsTransientType(type.GetElementType()!,ownAsm);
  var asm=type.Assembly;
  if(asm.IsCollectible)return true;
  var alc=System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(asm);
  if(alc!=null&&alc!=System.Runtime.Loader.AssemblyLoadContext.Default)return true;
  if(asm==ownAsm&&string.Equals(asm.GetName().Name,"Local.Spt5.ESP.Core",StringComparison.Ordinal))return true;
  if(type.IsGenericType)foreach(var arg in type.GetGenericArguments())if(IsTransientType(arg,ownAsm))return true;
  return false;
 }

 internal static string MethodKey(MethodBase m){
  var decl=m.DeclaringType;
  string typeName=decl?.AssemblyQualifiedName??decl?.FullName??"?";
  string pars=string.Join(",",m.GetParameters().Select(p=>p.ParameterType.FullName??p.ParameterType.Name));
  return $"{typeName}::{m.Name}({pars})";
 }

 internal static string SignatureKey(HarmonyMethod? prefix,HarmonyMethod? postfix,HarmonyMethod? finalizer)=>
  $"P:{PartSig(prefix)}|S:{PartSig(postfix)}|F:{PartSig(finalizer)}";

 static string PartSig(HarmonyMethod? hm){
  if(hm?.method==null)return "-";
  var m=hm.method;
  string pars=string.Join(";",m.GetParameters().Select(p=>{
   string typeId=p.Name=="__state"?(p.ParameterType.IsByRef?"__state&":"__state"):(p.ParameterType.AssemblyQualifiedName??p.ParameterType.FullName??p.ParameterType.Name);
   return $"{p.Name}:{(int)p.Attributes}:{typeId}";
  }));
  string before=hm.before is {Length:>0}?string.Join(",",hm.before):"";
  string after=hm.after is {Length:>0}?string.Join(",",hm.after):"";
  return $"{m.ReturnType.FullName}({pars})@{hm.priority}[{before}/{after}]";
 }

 internal static (MethodInfo Representative,bool Reused,List<FieldInfo> BoundFields) BindOrCreateSlot(
  string slotKey,
  MethodBase? original,
  HarmonyMethod? prefix,
  HarmonyMethod? postfix,
  HarmonyMethod? finalizer)
 {
  var state=SharedState();
  var harmony=(Harmony)state[0];
  var moduleBuilder=(ModuleBuilder)state[1];
  var slots=(ConcurrentDictionary<string,object[]>)state[2];
  var counter=(int[])state[3];

  bool reused=true;
  if(!slots.TryGetValue(slotKey,out var slot)){
   lock(state){
    if(!slots.TryGetValue(slotKey,out slot)){
     reused=false;
     int id=++counter[0];
     slot=CreateBridgeSlot(harmony,moduleBuilder,id,original,prefix,postfix,finalizer);
     slots[slotKey]=slot;
    }
   }
  }

  var boundFields=new List<FieldInfo>(3);
  if(prefix?.method!=null&&slot[0] is Type preDelType&&slot[1] is FieldInfo preField){
   var del=CreateAdapterDelegate(preDelType,prefix.method);
   preField.SetValue(null,del);
   boundFields.Add(preField);
  }
  if(postfix?.method!=null&&slot[2] is Type postDelType&&slot[3] is FieldInfo postField){
   var del=CreateAdapterDelegate(postDelType,postfix.method);
   postField.SetValue(null,del);
   boundFields.Add(postField);
  }
  if(finalizer?.method!=null&&slot[4] is Type finDelType&&slot[5] is FieldInfo finField){
   var del=CreateAdapterDelegate(finDelType,finalizer.method);
   finField.SetValue(null,del);
   boundFields.Add(finField);
  }
  return ((MethodInfo)slot[6]!,reused,boundFields);
 }

 static object[] CreateBridgeSlot(
  Harmony harmony,
  ModuleBuilder moduleBuilder,
  int id,
  MethodBase? original,
  HarmonyMethod? prefix,
  HarmonyMethod? postfix,
  HarmonyMethod? finalizer)
 {
  var hostType=moduleBuilder.DefineType($"Local.Spt5.HookBridge.Slot_{id}",
   TypeAttributes.Public|TypeAttributes.Abstract|TypeAttributes.Sealed|TypeAttributes.BeforeFieldInit);

  Type? preDel=null,postDel=null,finDel=null;
  FieldBuilder? preField=null,postField=null,finField=null;

  if(prefix?.method!=null){
   preDel=DefineDelegateType(moduleBuilder,$"Local.Spt5.HookBridge.DelPre_{id}",prefix.method.ReturnType,BridgeParameterTypes(prefix.method));
   preField=hostType.DefineField("ActivePre",preDel,FieldAttributes.Public|FieldAttributes.Static);
   EmitBridgeMethod(hostType,"Pre",prefix.method,preDel,preField);
  }
  if(postfix?.method!=null){
   postDel=DefineDelegateType(moduleBuilder,$"Local.Spt5.HookBridge.DelPost_{id}",postfix.method.ReturnType,BridgeParameterTypes(postfix.method));
   postField=hostType.DefineField("ActivePost",postDel,FieldAttributes.Public|FieldAttributes.Static);
   EmitBridgeMethod(hostType,"Post",postfix.method,postDel,postField);
  }
  if(finalizer?.method!=null){
   finDel=DefineDelegateType(moduleBuilder,$"Local.Spt5.HookBridge.DelFin_{id}",finalizer.method.ReturnType,BridgeParameterTypes(finalizer.method));
   finField=hostType.DefineField("ActiveFin",finDel,FieldAttributes.Public|FieldAttributes.Static);
   EmitBridgeMethod(hostType,"Fin",finalizer.method,finDel,finField);
  }

  var baked=hostType.CreateType()!;
  HarmonyMethod? Wrap(string methodName,HarmonyMethod? src){
   if(src?.method==null)return null;
   var bridgeMethod=baked.GetMethod(methodName,BindingFlags.Public|BindingFlags.Static)!;
   return new HarmonyMethod(bridgeMethod){
    priority=src.priority,
    before=src.before,
    after=src.after,
    debug=src.debug
   };
  }

  var patched=harmony.Patch(
   original,
   prefix:Wrap("Bridge_Pre",prefix),
   postfix:Wrap("Bridge_Post",postfix),
   finalizer:Wrap("Bridge_Fin",finalizer));

  return [
   preDel!,
   preDel!=null?baked.GetField("ActivePre",BindingFlags.Public|BindingFlags.Static)!:null!,
   postDel!,
   postDel!=null?baked.GetField("ActivePost",BindingFlags.Public|BindingFlags.Static)!:null!,
   finDel!,
   finDel!=null?baked.GetField("ActiveFin",BindingFlags.Public|BindingFlags.Static)!:null!,
   patched
  ];
 }

 static Type[] BridgeParameterTypes(MethodInfo target){
  var pars=target.GetParameters();
  var result=new Type[pars.Length];
  for(int i=0;i<pars.Length;i++){
   var p=pars[i];
   if(p.Name=="__state")result[i]=p.ParameterType.IsByRef?typeof(object).MakeByRefType():typeof(object);
   else result[i]=p.ParameterType;
  }
  return result;
 }

 static Type DefineDelegateType(ModuleBuilder mb,string fullName,Type returnType,Type[] paramTypes){
  var dt=mb.DefineType(fullName,
   TypeAttributes.Class|TypeAttributes.Public|TypeAttributes.Sealed|TypeAttributes.AnsiClass|TypeAttributes.AutoClass,
   typeof(MulticastDelegate));
  dt.DefineConstructor(MethodAttributes.RTSpecialName|MethodAttributes.HideBySig|MethodAttributes.Public,
   CallingConventions.Standard,[typeof(object),typeof(IntPtr)])
   .SetImplementationFlags(MethodImplAttributes.Runtime|MethodImplAttributes.Managed);
  dt.DefineMethod("Invoke",MethodAttributes.Public|MethodAttributes.HideBySig|MethodAttributes.NewSlot|MethodAttributes.Virtual,
   returnType,paramTypes)
   .SetImplementationFlags(MethodImplAttributes.Runtime|MethodImplAttributes.Managed);
  return dt.CreateType()!;
 }

 static void EmitBridgeMethod(TypeBuilder hostType,string kind,MethodInfo sampleTarget,Type delType,FieldBuilder activeField){
  var targetParams=sampleTarget.GetParameters();
  var bridgeParams=BridgeParameterTypes(sampleTarget);
  var returnType=sampleTarget.ReturnType;
  var mb=hostType.DefineMethod("Bridge_"+kind,MethodAttributes.Public|MethodAttributes.Static|MethodAttributes.HideBySig,returnType,bridgeParams);
  for(int i=0;i<targetParams.Length;i++)
   mb.DefineParameter(i+1,targetParams[i].Attributes,targetParams[i].Name);

  var il=mb.GetILGenerator();
  var locDel=il.DeclareLocal(delType);
  var lblInactive=il.DefineLabel();

  il.Emit(OpCodes.Volatile);
  il.Emit(OpCodes.Ldsfld,activeField);
  il.Emit(OpCodes.Stloc,locDel);
  il.Emit(OpCodes.Ldloc,locDel);
  il.Emit(OpCodes.Brfalse,lblInactive);

  il.Emit(OpCodes.Ldloc,locDel);
  for(int i=0;i<bridgeParams.Length;i++)EmitLdarg(il,i);
  il.Emit(OpCodes.Callvirt,delType.GetMethod("Invoke")!);
  il.Emit(OpCodes.Ret);

  il.MarkLabel(lblInactive);
  for(int i=0;i<targetParams.Length;i++){
   if(targetParams[i].IsOut&&bridgeParams[i].IsByRef){
    var elem=bridgeParams[i].GetElementType()!;
    EmitLdarg(il,i);
    if(elem.IsValueType)il.Emit(OpCodes.Initobj,elem);
    else{il.Emit(OpCodes.Ldnull);il.Emit(OpCodes.Stind_Ref);}
   }
  }

  if(returnType==typeof(void)){
   il.Emit(OpCodes.Ret);
   return;
  }
  if(kind=="Pre"&&returnType==typeof(bool)){
   il.Emit(OpCodes.Ldc_I4_1);
   il.Emit(OpCodes.Ret);
   return;
  }
  if(kind=="Fin"&&returnType==typeof(Exception)){
   int excIdx=Array.FindIndex(targetParams,p=>p.Name=="__exception"&&!p.ParameterType.IsByRef&&typeof(Exception).IsAssignableFrom(p.ParameterType));
   if(excIdx>=0)EmitLdarg(il,excIdx);
   else il.Emit(OpCodes.Ldnull);
   il.Emit(OpCodes.Ret);
   return;
  }
  if(!returnType.IsValueType){
   il.Emit(OpCodes.Ldnull);
   il.Emit(OpCodes.Ret);
   return;
  }
  var defLocal=il.DeclareLocal(returnType);
  il.Emit(OpCodes.Ldloca,defLocal);
  il.Emit(OpCodes.Initobj,returnType);
  il.Emit(OpCodes.Ldloc,defLocal);
  il.Emit(OpCodes.Ret);
 }

 internal static Delegate CreateAdapterDelegate(Type delType,MethodInfo targetMethod){
  var targetParams=targetMethod.GetParameters();
  var bridgeParams=BridgeParameterTypes(targetMethod);
  bool hasTypedState=false;
  for(int i=0;i<targetParams.Length;i++){
   if(targetParams[i].Name=="__state"){
    var t=targetParams[i].ParameterType;
    if((t.IsByRef?t.GetElementType()!:t)!=typeof(object)){hasTypedState=true;break;}
   }
  }
  if(!hasTypedState&&targetMethod.IsPublic&&(targetMethod.DeclaringType?.IsPublic==true))
   return Delegate.CreateDelegate(delType,targetMethod);

  var returnType=targetMethod.ReturnType;
  var dm=new DynamicMethod("Adapt_"+targetMethod.DeclaringType?.Name+"_"+targetMethod.Name,
   returnType,bridgeParams,typeof(PersistentHarmonyRouter).Module,skipVisibility:true);
  var il=dm.GetILGenerator();

  int stateIdx=Array.FindIndex(targetParams,p=>p.Name=="__state");
  LocalBuilder? stateLocal=null;
  Type? stateElemType=null;
  bool stateByRef=false;

  if(stateIdx>=0){
   var rawStateType=targetParams[stateIdx].ParameterType;
   stateByRef=rawStateType.IsByRef;
   stateElemType=stateByRef?rawStateType.GetElementType()!:rawStateType;
   stateLocal=il.DeclareLocal(stateElemType);

   if(!stateByRef){
    EmitUnboxOrCastState(il,() => EmitLdarg(il,stateIdx),stateElemType,stateLocal);
   }else if(!targetParams[stateIdx].IsOut){
    EmitUnboxOrCastState(il,()=>{EmitLdarg(il,stateIdx);il.Emit(OpCodes.Ldind_Ref);},stateElemType,stateLocal);
   }
  }

  for(int i=0;i<targetParams.Length;i++){
   if(i==stateIdx){
    if(stateByRef)il.Emit(OpCodes.Ldloca,stateLocal!);
    else il.Emit(OpCodes.Ldloc,stateLocal!);
   }else{
    EmitLdarg(il,i);
   }
  }

  il.Emit(OpCodes.Call,targetMethod);

  if(stateIdx>=0&&stateByRef){
   LocalBuilder? retLocal=null;
   if(returnType!=typeof(void)){
    retLocal=il.DeclareLocal(returnType);
    il.Emit(OpCodes.Stloc,retLocal);
   }
   EmitLdarg(il,stateIdx);
   il.Emit(OpCodes.Ldloc,stateLocal!);
   if(stateElemType!.IsValueType)il.Emit(OpCodes.Box,stateElemType);
   il.Emit(OpCodes.Stind_Ref);
   if(retLocal!=null)il.Emit(OpCodes.Ldloc,retLocal);
  }

  il.Emit(OpCodes.Ret);
  return dm.CreateDelegate(delType);
 }

 static void EmitUnboxOrCastState(ILGenerator il,Action loadObj,Type targetType,LocalBuilder dest){
  if(targetType==typeof(object)){
   loadObj();
   il.Emit(OpCodes.Stloc,dest);
   return;
  }
  if(!targetType.IsValueType){
   loadObj();
   il.Emit(OpCodes.Isinst,targetType);
   il.Emit(OpCodes.Stloc,dest);
   return;
  }
  var checkType=Nullable.GetUnderlyingType(targetType)??targetType;
  var lblValid=il.DefineLabel();
  var lblDone=il.DefineLabel();
  loadObj();
  il.Emit(OpCodes.Isinst,checkType);
  il.Emit(OpCodes.Brtrue,lblValid);
  il.Emit(OpCodes.Ldloca,dest);
  il.Emit(OpCodes.Initobj,targetType);
  il.Emit(OpCodes.Br,lblDone);
  il.MarkLabel(lblValid);
  loadObj();
  il.Emit(OpCodes.Unbox_Any,targetType);
  il.Emit(OpCodes.Stloc,dest);
  il.MarkLabel(lblDone);
 }

 static void EmitLdarg(ILGenerator il,int index){
  switch(index){
   case 0:il.Emit(OpCodes.Ldarg_0);break;
   case 1:il.Emit(OpCodes.Ldarg_1);break;
   case 2:il.Emit(OpCodes.Ldarg_2);break;
   case 3:il.Emit(OpCodes.Ldarg_3);break;
   default:
    if(index<=255)il.Emit(OpCodes.Ldarg_S,(byte)index);
    else il.Emit(OpCodes.Ldarg,index);
    break;
  }
 }
}

internal sealed class PersistentHarmonyPatcher {
 public string Id {get;}
 readonly Harmony fallback;
 readonly List<FieldInfo> activeBridgeFields=new(256);
 readonly Dictionary<(MethodBase Original,MethodInfo Target),List<FieldInfo>> fieldsByTarget=new();
 readonly Dictionary<string,int> ordinals=new(StringComparer.Ordinal);
 readonly HashSet<MethodBase> fallbackMethods=new();
 internal int ReusedCount {get;private set;}
 internal int CreatedCount {get;private set;}

 internal PersistentHarmonyPatcher(string id){
  Id=id;
  fallback=new Harmony(id);
 }

 public MethodInfo Patch(
  MethodBase? original,
  HarmonyMethod? prefix=null,
  HarmonyMethod? postfix=null,
  HarmonyMethod? transpiler=null,
  HarmonyMethod? finalizer=null)
 {
  if(original==null)throw new ArgumentNullException(nameof(original));
  if(!PersistentHarmonyRouter.CanBridge(original,prefix,postfix,transpiler,finalizer)){
   var fallbackResult=transpiler!=null
    ?fallback.Patch(original,prefix:prefix,postfix:postfix,transpiler:transpiler)
    :fallback.Patch(original,prefix:prefix,postfix:postfix,finalizer:finalizer);
   fallbackMethods.Add(original);
   return fallbackResult;
  }

  string baseKey=PersistentHarmonyRouter.MethodKey(original)+"|"+PersistentHarmonyRouter.SignatureKey(prefix,postfix,finalizer);
  int ord=ordinals.TryGetValue(baseKey,out int cur)?cur:0;
  ordinals[baseKey]=ord+1;
  string slotKey=$"{baseKey}#{ord}";

  var (rep,reused,fields)=PersistentHarmonyRouter.BindOrCreateSlot(slotKey,original,prefix,postfix,finalizer);
  if(reused)ReusedCount++;else CreatedCount++;
  activeBridgeFields.AddRange(fields);

  foreach(var hm in new[]{prefix,postfix,finalizer}){
   if(hm?.method!=null){
    var key=(original,hm.method);
    if(!fieldsByTarget.TryGetValue(key,out var list))fieldsByTarget[key]=list=new(2);
    list.AddRange(fields);
   }
  }
  return rep;
 }

 /// <summary>
 /// Atomically swaps a named singleton guard slot without ever leaving the native method unguarded.
 /// </summary>
 internal void PatchSwapSafe(string fixedSlotName,MethodBase original,HarmonyMethod prefix){
  if(original==null)throw new ArgumentNullException(nameof(original));
  string slotKey=$"swap-safe:{fixedSlotName}:{PersistentHarmonyRouter.MethodKey(original)}|{PersistentHarmonyRouter.SignatureKey(prefix,null,null)}";
  var (_,reused,_)=PersistentHarmonyRouter.BindOrCreateSlot(slotKey,original,prefix,null,null);
  if(reused)ReusedCount++;else CreatedCount++;
 }

 public void Unpatch(MethodBase original,MethodInfo patch){
  if(original==null||patch==null)return;
  if(fieldsByTarget.TryGetValue((original,patch),out var list)){
   foreach(var f in list)f.SetValue(null,null);
   fieldsByTarget.Remove((original,patch));
  }else{
   fallback.Unpatch(original,patch);
  }
 }

 public void Unpatch(MethodBase original,HarmonyPatchType type,string harmonyID){
  if(original==null)return;
  if(fallbackMethods.Contains(original))
   fallback.Unpatch(original,type,harmonyID);
 }

 internal void DeactivateAll(){
  for(int i=activeBridgeFields.Count-1;i>=0;i--)
   activeBridgeFields[i].SetValue(null,null);
  activeBridgeFields.Clear();
  fieldsByTarget.Clear();
  ordinals.Clear();
  foreach(var orig in fallbackMethods){
   try{fallback.Unpatch(orig,HarmonyPatchType.All,Id);}
   catch(Exception ex){Plugin.Self?.Log.LogWarning("Fallback unpatch skipped for "+orig.Name+": "+ex.Message);}
  }
  fallbackMethods.Clear();
 }
}
