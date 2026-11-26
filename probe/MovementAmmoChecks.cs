using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using LocalSpt5Esp;

internal static class MovementAmmoChecks {
 internal static void Run(string sourceRoot,string gameRoot){
  void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
  using var native=AssemblyDefinition.ReadAssembly(Path.Combine(gameRoot,"BepInEx/interop/Assembly-CSharp.dll"));
  var context=native.MainModule.Types.Single(t=>t.FullName=="EFT.MovementContext");
  Check(context.Methods.Single(m=>m.Name=="RefreshObstacleRestrictions").Parameters.Count==0&&context.Properties.Any(p=>p.Name=="_enteredObstacles")&&context.Properties.Single(p=>p.Name=="_bushZoneCount").PropertyType.FullName=="System.Int32","native obstacle refresh, overlap list and bush zone counter contracts match");
  var obstacle=native.MainModule.Types.Single(t=>t.FullName=="EFT.Interactive.ObstacleCollider");
  Check(obstacle.Properties.Single(p=>p.Name=="speedMlp").PropertyType.FullName=="System.Single"&&obstacle.Properties.Single(p=>p.Name=="_disablesSprint").PropertyType.FullName=="System.Boolean"&&obstacle.Properties.Single(p=>p.Name=="_hasSwampSpeedLimit").PropertyType.FullName=="System.Boolean","native obstacle slowdown source, speed and sprint fields match");
  var cylinder=native.MainModule.Types.Single(t=>t.FullName=="EFT.InventoryLogic.CylinderMagazine");
  Check(cylinder.Methods.Single(m=>m.Name=="GetActiveCamoraSlot").Parameters.Single().ParameterType.FullName=="System.Boolean"&&native.MainModule.Types.Single(t=>t.FullName=="EFT.InventoryLogic.Ammo").Properties.Single(p=>p.Name=="IsUsed").PropertyType.FullName=="System.Boolean","active cylinder chamber and live cartridge contracts match");
  Check(MenuSections.Group("20. 体力与移动")=="人物与移动"&&MenuSections.OnPage("人物与移动","移动与穿墙","20. 体力与移动","No bush slowdown")&&MenuSections.OnPage("人物与移动","移动与穿墙","20. 体力与移动","No swamp slowdown"),"independent bush and swamp switches use the existing character movement page");
  using var core=AssemblyDefinition.ReadAssembly(Path.Combine(sourceRoot,"esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll"));
  var movement=core.MainModule.Types.Single(t=>t.Name=="MovementAssist");
  bool Calls(MethodDefinition method,string type,string name)=>method.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.DeclaringType.Name==type&&m.Name==name);
  Check(Calls(movement.Methods.Single(m=>m.Name=="Initialize"),"BushMovement","Initialize")&&Calls(movement.Methods.Single(m=>m.Name=="Tick"),"BushMovement","Tick")&&Calls(movement.Methods.Single(m=>m.Name=="Reset"),"BushMovement","Reset"),"bush movement is connected to initialization, frame updates and unload restoration");
  var magazine=core.MainModule.Types.Single(t=>t.Name=="MagazineDisplay");
  Check(Calls(magazine.Methods.Single(m=>m.Name=="ChamberAmmo"),"CylinderMagazine","GetActiveCamoraSlot")&&Calls(magazine.Methods.Single(m=>m.Name=="ChamberAmmo"),"Ammo","get_IsUsed"),"chamber readout resolves the active cylinder chamber and excludes spent rounds");
  Console.WriteLine("PASS installed native bush/chamber APIs, movement menu and core lifecycle integration");
 }
}
