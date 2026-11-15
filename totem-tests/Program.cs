using ProjectE.Client;
using Mono.Cecil;
int count=0;
void Check(bool value,string name){count++;if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
var gate=new TotemRescueGate();
Check(!gate.Blocks(1,100),"untriggered totem does not block damage");
gate.Begin(1);
Check(gate.Blocks(1,100),"restoration blocks reentrant death before commit");
Check(!gate.Blocks(2,100),"other actors cannot inherit protection");
gate.Commit(100);gate.End();
Check(gate.Blocks(1,100),"remaining overdamage from the triggering hit is blocked");
Check(!gate.Blocks(1,101),"the next frame is not permanent invulnerability");
Check(!gate.Blocks(0,100),"missing controller identity is never protected");
gate.Begin(2);gate.End();
Check(!gate.Blocks(2,100)&&!gate.Blocks(2,-1),"a failed preflight does not commit a free rescue");
gate.Begin(2);gate.Commit(102);gate.End();gate.Clear();
Check(!gate.Blocks(2,102),"raid exit or failure releases all protection");
// Model the observed native sequence: critical-part death -> Kill -> excess
// damage spread -> another Kill. All callbacks use the real gate under test.
int totems=1,rescues=0,deaths=0;
bool Kill(int frame){if(gate.Blocks(10,frame))return false;if(totems==0){deaths++;return true;}gate.Begin(10);totems--;rescues++;Check(!Kill(frame),"health restoration callbacks cannot reenter native death");gate.Commit(frame);gate.End();return false;}
Check(!Kill(200),"first lethal callback consumes one totem and cancels death");
foreach(int part in Enumerable.Range(0,7))if(!gate.Blocks(10,200))Kill(200);
Check(totems==0&&rescues==1&&deaths==0,"seven overdamage callbacks cannot consume twice or kill after animation");
Check(Kill(201)&&deaths==1,"a later lethal hit without another totem retains native death");
Check(TotemRescueGate.Bindable(true,true,true),"local pocket or vest totem can bind");
Check(!TotemRescueGate.Bindable(false,true,true),"other inventories retain native binding policy");
Check(!TotemRescueGate.Bindable(true,false,true),"ordinary barter items are not enabled globally");
Check(!TotemRescueGate.Bindable(true,true,false),"stash, backpack and nested-bag placement is not a quick slot");
var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../.."));
using var assembly=AssemblyDefinition.ReadAssembly(Path.Combine(root,"projecte-client/bin/Release/net10.0/Local.ProjectE.HotModule.dll"));
var type=assembly.MainModule.Types.Single(t=>t.Name=="ProjectModule");
bool Calls(string method,string callee)=>type.Methods.Single(m=>m.Name==method).Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name==callee);
Check(Calls("TotemDamage","Blocks")&&Calls("TotemSpread","Blocks")&&Calls("TotemBeforeKill","Blocks"),"compiled damage, spread and death hooks all share the rescue gate");
Check(Calls("TotemBeforeKill","Commit")&&Calls("TotemBeforeKill","FullRestoreBodyPart"),"compiled successful rescue restores native body state and commits protection");
Check(Calls("TotemBindable","Current")&&Calls("TotemBindable","Bindable"),"compiled binding checks current inventory ownership and placement");
Check(Calls("TotemSlotCommand","GetBoundItem"),"passive hotbar commands resolve the actual native binding");
Console.WriteLine($"PASS {count} totem rescue and quick-slot checks");
