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
gate.Begin(10);gate.Commit(300,100,10);gate.End();
foreach(double now in new[]{100,100.01,100.5,101,105,109.999})Check(gate.Blocks(10,301,now),"separate hits are blocked across the configured ten-second window: "+now);
Check(!gate.Blocks(10,301,110)&&!gate.Blocks(10,302,120),"protection expires exactly at ten seconds and does not become permanent");
Check(!gate.Blocks(11,301,105)&&!gate.Blocks(0,301,105),"other players/controllers and missing identities do not inherit the timed shield");
Check(!gate.Blocks(10,301,99)&&!gate.Blocks(10,301,double.NaN),"clock reversal and invalid time do not create a stale shield");
Check(gate.Busy(105)&&!gate.Busy(110),"hot reload waits for an active shield to finish");
Check(gate.Remaining(105)==5&&gate.Remaining(110)==0,"diagnostics show remaining protection and zero after expiry");
Check(gate.BlocksHealthChange(10,301,105,-2)&&!gate.BlocksHealthChange(10,301,105,2)&&!gate.BlocksHealthChange(10,301,105,0),"direct damage-over-time is blocked while healing and zero changes continue");
Check(!gate.BlocksHealthChange(11,301,105,-2)&&!gate.BlocksHealthChange(10,301,110,-2),"other actors and expired shields retain direct health changes");
int burstConsumptions=1;for(int hit=0;hit<600;hit++)if(!gate.Blocks(10,301+hit,100+hit/60d))burstConsumptions++;
Check(burstConsumptions==1,"ten seconds of automatic fire cannot consume more totems during one rescue");
gate.Begin(10);gate.Commit(900,200,.25);gate.End();Check(gate.Blocks(10,901,200.249)&&!gate.Blocks(10,902,200.25),"fractional user-configured duration is respected");
gate.Begin(10);gate.Commit(1000,300,0);gate.End();Check(gate.Blocks(10,1000,300)&&!gate.Blocks(10,1001,300.001),"zero duration keeps same-hit safety and disables the extra timed shield");
foreach(double value in new[]{-1,double.NaN,double.PositiveInfinity,double.NegativeInfinity})Check(TotemRescueGate.Duration(value)==0,"invalid/negative configuration cannot create infinite protection: "+value);
Check(TotemRescueGate.Duration(600)==600&&TotemRescueGate.Duration(.5)==.5,"valid durations have no arbitrary small maximum");
gate.Begin(12);gate.End();Check(!gate.Blocks(12,1001,305),"failed inventory preflight cannot carry over a previous timed shield");
gate.Begin(10);gate.Commit(1100,400,10);gate.End();gate.Clear();Check(!gate.Blocks(10,1100,401)&&!gate.Busy(401),"raid exit and shutdown immediately clear the timed shield");
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
Check(Calls("TotemHealthChange","BlocksHealthChange")&&Calls("TotemDestroyPart","Blocks"),"compiled effect damage and body-part destruction honor the same timed shield");
Check(Calls("get_CanReload","Busy")&&Calls("Cleanup","Clear"),"compiled module protects active-shield reload and clears the gate on shutdown");
Check(type.Methods.Single(m=>m.Name=="get_ExtraSettings").Body.Instructions.Any(i=>i.Operand is FieldReference f&&f.Name=="totemProtectionSeconds"),"the editable totem protection duration is exposed through the actual fun-feature settings API");
Check(Calls("TotemBeforeKill","Commit")&&Calls("TotemBeforeKill","FullRestoreBodyPart"),"compiled successful rescue restores native body state and commits protection");
Check(Calls("TotemBindable","Current")&&Calls("TotemBindable","Bindable"),"compiled binding checks current inventory ownership and placement");
Check(Calls("TotemSlotCommand","GetBoundItem"),"passive hotbar commands resolve the actual native binding");
var healing=type.Methods.Single(m=>m.Name=="HealingDamage");
using var enums=AssemblyDefinition.ReadAssembly(Path.Combine(root,"totem-tests/bin/Release/net10.0/SPTarkov.Server.Core.dll"));
var damageEnum=enums.MainModule.Types.Single(t=>t.Name=="DamageType");
int medicine=Convert.ToInt32(damageEnum.Fields.Single(f=>f.Name=="Medicine").Constant);
Check(medicine!=0&&!damageEnum.Fields.Any(f=>f.HasConstant&&Convert.ToInt32(f.Constant)==0),"zero is not a valid native damage type; Medicine has a defined nonzero value");
Check(healing.Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldc_I4&&Convert.ToInt32(i.Operand)==medicine)&&Calls("HealingDamage","set_DamageType"),"compiled healing explicitly supplies the native Medicine damage type");
Check(Calls("TotemBeforeKill","HealingDamage")&&Calls("TickTotem","HealingDamage")&&Calls("ApplyArtifact","HealingDamage"),"totem restoration, regeneration and artifact healing use valid history metadata");
bool rejected=false;
try{System.Text.Json.JsonSerializer.Deserialize<SPTarkov.Server.Core.Models.Eft.Common.Tables.BodyPartsDamageHistory>("{\"Chest\":[{\"Type\":0,\"Amount\":68}]}");}catch(System.Text.Json.JsonException){rejected=true;}
Check(rejected,"actual server settlement DTO rejects the old numeric damage type");
var result=System.Text.Json.JsonSerializer.Deserialize<SPTarkov.Server.Core.Models.Eft.Common.Tables.BodyPartsDamageHistory>("{\"Chest\":[{\"Type\":\"Medicine\",\"Amount\":68},{\"Type\":\"Bullet\",\"Amount\":12}]}");
Check(result.Chest.Count==2&&result.Chest[0].Type=="Medicine"&&result.Chest[1].Type=="Bullet","actual server settlement DTO accepts Medicine while retaining normal damage history");
if(args.Length>0){
 using var report=System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[0]));
 Check(report.RootElement.GetProperty("oldType").GetInt32()==0&&report.RootElement.GetProperty("healingType").GetInt32()==medicine,"live native DamageInfo confirms old zero and compiled healing Medicine values");
 bool nativeRejected=false;
 try{System.Text.Json.JsonSerializer.Deserialize<SPTarkov.Server.Core.Models.Eft.Common.Tables.DamageStats>(report.RootElement.GetProperty("bad").GetString());}catch(System.Text.Json.JsonException){nativeRejected=true;}
 Check(nativeRejected,"actual server DTO reproduces the error from native old metadata JSON");
 var native=System.Text.Json.JsonSerializer.Deserialize<SPTarkov.Server.Core.Models.Eft.Common.Tables.DamageStats>(report.RootElement.GetProperty("good").GetString());
 Check(native.Type=="Medicine"&&native.Amount==68,"native fixed metadata JSON round-trips through actual server DTO without losing health adjustment");
}
Console.WriteLine($"PASS {count} totem rescue, quick-slot and settlement checks");
