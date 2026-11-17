using System;
using System.IO;
using System.Linq;
using LocalSpt5Esp;
using Mono.Cecil;
internal static class SeasonSelectionChecks {
 internal static void Run(string sourceRoot){
  for(int bits=0;bits<32;bits++){
   bool enabled=(bits&1)!=0,raid=(bits&2)!=0,current=(bits&4)!=0,unlocked=(bits&8)!=0,same=(bits&16)!=0;
   if(SeasonSelectionRules.CanSubmit(enabled,raid,current,unlocked,same)!=(bits==29))throw new Exception("Season submit escaped disabled/raid/controller/lock/descriptor guards");
  }
  using var core=AssemblyDefinition.ReadAssembly(Path.Combine(sourceRoot,"esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll"));
  var type=core.MainModule.Types.Single(t=>t.Name=="SeasonSelectionControl");
  var calls=type.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
  if(calls.Any(m=>m.Name is "set_Status" or "set_ProfileId" or "CreateProfile" or "ChangeGameMode" or "set_CurrentGameMode")||!calls.Any(m=>m.Name=="CanSubmit"))throw new Exception("Season selection mutates native identity or omits submission guards");
  foreach(var method in type.Methods.Where(m=>m.Name is "Submit" or "SubmitAsync" or "Route"))if(method.Parameters.Any(p=>p.ParameterType.IsByReference&&p.ParameterType.FullName.Contains("EGameMode")))throw new Exception("Season selection still rewrites requested mode");
  var hooks=type.Methods.Single(m=>m.Name=="Initialize").Body.Instructions.Where(i=>i.Operand is string).Select(i=>(string)i.Operand).ToArray();
  if(!hooks.Contains("ShowSlot")||!hooks.Contains("Submit")||!hooks.Contains("SubmitAsync"))throw new Exception("Missing actual slot or native submission hook");
  Console.WriteLine("PASS independent seasonal slot, native seasonal mode preserved, 32 submission guards and no client profile writes");
 }
}
