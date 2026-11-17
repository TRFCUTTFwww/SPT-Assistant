using System;
using System.IO;
using System.Linq;
using LocalSpt5Esp;
using Mono.Cecil;
internal static class SeasonSelectionChecks {
 internal static void Run(string sourceRoot){
  for(int bits=0;bits<32;bits++){
   bool enabled=(bits&1)!=0,raid=(bits&2)!=0,locked=(bits&4)!=0,exists=(bits&8)!=0,selectable=(bits&16)!=0;
   bool result=SeasonSelectionRules.CanAlias(enabled,raid,locked,exists,selectable,"pmc");
   if(result!=(bits==29))throw new Exception("Season alias escaped disabled/raid/locked/existing/selectable guards");
  }
  if(SeasonSelectionRules.CanAlias(true,false,true,true,true,"")||SeasonSelectionRules.CanSubmit(false,"a","a")||SeasonSelectionRules.CanSubmit(true,"a","b")||!SeasonSelectionRules.CanSubmit(true,"a","a"))throw new Exception("Season selection allowed empty, stale or another character");
  using var core=AssemblyDefinition.ReadAssembly(Path.Combine(sourceRoot,"esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll"));
  var type=core.MainModule.Types.Single(t=>t.Name=="SeasonSelectionControl");
  var calls=type.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
  if(calls.Any(m=>m.Name is "set_Status" or "set_ProfileId" or "CreateProfile" or "ChangeGameMode" or "set_CurrentGameMode")||!calls.Any(m=>m.Name=="CanSubmit"))throw new Exception("Season alias mutates native profile/mode state or omits identity validation");
  var hooks=type.Methods.Single(m=>m.Name=="Initialize").Body.Instructions.Where(i=>i.Operand is string).Select(i=>(string)i.Operand).ToArray();
  if(!hooks.Contains("ShowSlot")||!hooks.Contains("Submit")||!hooks.Contains("SubmitAsync"))throw new Exception("Season unlock misses the actual character slot or a native submission path");
  Console.WriteLine("PASS actual seasonal slot/native submit hooks, 32 eligibility combinations, stale identity rejection and profile/mode write isolation");
 }
}
