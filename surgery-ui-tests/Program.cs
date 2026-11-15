using ProjectE.Client;
using Mono.Cecil;
int count=0;
void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
for(int mask=0;mask<512;mask++)Check(InventoryScreenRules.Stale((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0,(mask&16)!=0,(mask&32)!=0,(mask&64)!=0,(mask&128)!=0,(mask&256)!=0)==(mask==39),"stale inventory boundary "+mask);
Console.WriteLine("PASS 512 combinations: local/alive/open, inventory view, battle view, screen identity, cursor, modal UI and native operation");
foreach(float seconds in new[]{-1,0,.5f,1.49f})Check(!InventoryScreenRules.Stable(1,1,2,2,seconds),"native transient view transition "+seconds);
Check(InventoryScreenRules.Stable(1,1,2,2,1.5f),"stable orphan can recover");
Check(!InventoryScreenRules.Stable(0,0,2,2,3)&&!InventoryScreenRules.Stable(1,3,2,2,3)&&!InventoryScreenRules.Stable(1,1,0,0,3)&&!InventoryScreenRules.Stable(1,1,2,4,3),"missing or replaced player/world cannot reuse stale timer");
using var assembly=AssemblyDefinition.ReadAssembly(args[0]);var types=assembly.MainModule.Types;
var terminal=types.Single(t=>t.Name=="TerminalInventory");var lifecycle=types.Single(t=>t.Name=="InventoryScreenLifecycle");var module=types.Single(t=>t.Name=="ProjectModule");
MethodReference[] Calls(TypeDefinition type,string name)=>type.Methods.Single(m=>m.Name==name).Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
Check(!terminal.Methods.SelectMany(m=>m.HasBody?m.Body.Instructions:[]).Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.DeclaringType.Name=="InventoryScreen"&&m.Name=="Close"),"terminal never directly closes only the inventory view");
Check(Calls(terminal,"Tick").Any(m=>m.Name=="CloseOwnedScreen")&&Calls(terminal,"Shutdown").Any(m=>m.Name=="CloseOwnedScreen")&&Calls(terminal,"CloseOwnedScreen").Any(m=>m.DeclaringType.Name=="InventoryScreenLifecycle"),"both held-item change and module cleanup use owned native navigation");
Check(Calls(lifecycle,"Close").Any(m=>m.Name=="CloseInventoryIfOpen")&&!Calls(lifecycle,"Close").Any(m=>m.Name is "SetActive" or "SetInventoryOpened" or "set__isInventoryOpened"),"restoration delegates view/screen/input/player flags to the native owner");
Check(Calls(lifecycle,"Tick").Any(m=>m.Name=="Stale")&&Calls(lifecycle,"Tick").Any(m=>m.Name=="Stable")&&Calls(lifecycle,"Tick").Any(m=>m.Name=="get_ProcessStatus"),"guard waits for a stable orphan and excludes native hand transitions");
Check(Calls(module,"TickExtra").Any(m=>m.DeclaringType.Name=="InventoryScreenLifecycle"&&m.Name=="Tick"),"normal module lifecycle drives UI repair with modal state");
Check(!lifecycle.Methods.SelectMany(m=>m.HasBody?m.Body.Instructions:[]).Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name is "ApplyDamage" or "RemoveItem" or "Proceed" or "FastForwardCurrentOperations"),"UI restoration never consumes items, changes health or skips surgery");
Console.WriteLine($"{count} surgery/terminal UI regression checks passed.");
