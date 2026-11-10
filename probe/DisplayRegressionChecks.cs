using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Mono.Cecil;
using LocalSpt5Esp;

internal static class DisplayRegressionChecks {
 internal static void Run(JsonElement database,AssemblyDefinition core){
  void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
  string Value(ItemDetails.Detail[] rows,string label)=>rows.Single(r=>r.Label==label).Value;
  using(var sample=JsonDocument.Parse("""{"Damage":50,"PenetrationPower":3,"ArmorDamage":15,"InitialSpeed":400,"buckshotBullets":8,"ProjectileCount":1,"Weight":0.04}""")){
   var rows=ItemDetails.Read(sample.RootElement);Check(Value(rows,"单弹丸伤害")=="50"&&Value(rows,"弹丸数量")=="8"&&Value(rows,"全部弹丸理论伤害")=="400","shotgun pellets retain per-pellet and total damage separately");
  }
  using(var sample=JsonDocument.Parse("""{"Grids":[{"_props":{"cellsH":4,"cellsV":5}},{"_props":{"cellsH":2,"cellsV":3}}],"armorClass":"6","ArmorMaterial":"Ceramic","MaxDurability":80}""")){
   var rows=ItemDetails.Read(sample.RootElement);Check(Value(rows,"容纳格数")=="26"&&Value(rows,"独立分区")=="4 × 5 / 2 × 3","split inventory grids add slots without pretending to be one rectangle");
   Check(Value(rows,"防护等级")=="6"&&Value(rows,"防护材质")=="陶瓷"&&Value(rows,"模板最大耐久")=="80","armor accepts native string class and readable material");
  }
  using(var sample=JsonDocument.Parse("""{"Damage":"NaN","PenetrationPower":20,"Weight":null,"Grids":[],"ArmorMaterial":"ModdedMaterial"}""")){
   var rows=ItemDetails.Read(sample.RootElement);Check(!rows.Any(r=>r.Label is "单弹丸伤害" or "重量" or "容纳格数")&&Value(rows,"防护材质")=="ModdedMaterial","unknown materials survive while missing/nonfinite numbers do not become fabricated stats");
  }
  int ammunition=0,containers=0,armor=0;
  foreach(var item in database.EnumerateObject()){
   if(!item.Value.TryGetProperty("_type",out var type)||type.GetString()!="Item")continue;
   var props=item.Value.GetProperty("_props");var rows=ItemDetails.Read(props);
   if(props.TryGetProperty("Damage",out _)&&props.TryGetProperty("PenetrationPower",out _)){Check(rows.Any(r=>r.Label=="单弹丸伤害")&&rows.Any(r=>r.Label=="穿透力"),"actual ammunition details: "+item.Name);ammunition++;}
   if(props.TryGetProperty("Grids",out var grids)&&grids.ValueKind==JsonValueKind.Array&&grids.GetArrayLength()>0){double slots=grids.EnumerateArray().Sum(g=>g.GetProperty("_props").GetProperty("cellsH").GetDouble()*g.GetProperty("_props").GetProperty("cellsV").GetDouble());if(slots>0){Check(Value(rows,"容纳格数")==slots.ToString(System.Globalization.CultureInfo.InvariantCulture),"actual container capacity: "+item.Name);containers++;}}
   if(props.TryGetProperty("ArmorMaterial",out var material)&&material.ValueKind==JsonValueKind.String&&!string.IsNullOrEmpty(material.GetString())){Check(rows.Any(r=>r.Label=="防护材质"),"actual armor material: "+item.Name);armor++;}
  }
  Check(ammunition>100&&containers>30&&armor>30,"real catalog exercises ammunition, backpacks/rigs and armor");
  Vector3[] Corners(float z,float halfDepth=1)=>Enumerable.Range(0,8).Select(i=>new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,z+((i&4)==0?-halfDepth:halfDepth))).ToArray();
  Check(VehicleBoxRules.ViewBounds(Corners(-10),out var far)&&Math.Abs(far.X+1f/9)<.00001f&&Math.Abs(far.Width-2f/9)<.00001f,"perspective bounds use front corners at their own depth");
  Check(VehicleBoxRules.ViewBounds(Corners(-.5f),out var crossing)&&float.IsFinite(crossing.Width)&&crossing.Width>20,"near-plane crossing clips original edges before division");
  Check(!VehicleBoxRules.ViewBounds(Corners(10),out _)&&!VehicleBoxRules.ViewBounds(Array.Empty<Vector3>(),out _),"fully behind-camera and malformed boxes are rejected");
  var invalid=Corners(-10);invalid[0].X=float.NaN;Check(!VehicleBoxRules.ViewBounds(invalid,out _),"nonfinite bounds never enter a render mesh");
  var offset=Corners(-10).Select(p=>p+new Vector3(20,0,0)).ToArray();Check(VehicleBoxRules.ViewBounds(offset,out var outside)&&outside.X>1,"offscreen bounds retain original edges instead of snapping to an optic border");
  var menu=core.MainModule.Types.Single(t=>t.Name=="InterfaceMenu");
  Check(menu.Methods.Where(m=>m.HasBody).Any(m=>m.Body.Instructions.Any(i=>i.Operand is MethodReference call&&call.DeclaringType.Name=="NextRaidLoot"&&call.Name=="Draw")&&m.Body.Instructions.Any(i=>Equals(i.Operand,"刷新规则"))),"refresh rules page dispatches to the real advanced editor");
  var scope=core.MainModule.Types.Single(t=>t.Name=="ScopeBoxes");
  var buildCalls=scope.Methods.Single(m=>m.Name=="Build").Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray();
  Check(buildCalls.Any(m=>m.DeclaringType.Name=="BtrDisplay"&&m.Name=="Markers")&&buildCalls.Any(m=>m.DeclaringType.Name=="AirdropControl"&&m.Name=="Markers"),"optic render batch includes live BTR, crate and plane marker sources");
  Check(scope.Methods.Single(m=>m.Name=="Tick").Body.Instructions.Any(i=>i.Operand is MethodReference call&&call.DeclaringType.Name=="VehicleEsp"&&call.Name=="get_Enabled"),"vehicle optic markers work independently of the people ESP switch");
  Check(scope.Methods.Single(m=>m.Name=="Detach").Body.Instructions.Any(i=>i.Operand is FieldReference field&&field.DeclaringType.Name=="VehicleEsp"&&field.Name=="OpticActive"),"leaving optics restores ordinary vehicle markers");
  Console.WriteLine($"PASS item details ({ammunition} ammunition, {containers} containers, {armor} armor), vehicle near-plane/optic bounds and refresh rules dispatch");
 }
}
