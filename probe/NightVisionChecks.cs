using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using LocalSpt5Esp;
using Mono.Cecil;
internal static class NightVisionChecks {
 internal static void Run(string sourceRoot,string? gameRoot){
  int checks=0;
  void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
  void Reject(Action action,string message){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,message);}
  var devices=NightVisionRules.Devices;
  Check(devices.Length==6&&devices.Select(d=>d.Style).Distinct().Count()==6&&devices.Select(d=>d.Template).Distinct().Count()==6,"six distinct native night vision devices");
  foreach(var d in devices){
   Check(NightVisionRules.Device(d.Style)==d.Profile&&NightVisionRules.Name(d.Style).Length>0,"each device resolves to its labeled native profile");
   var p=d.Profile;
   Check(NightVisionRules.Custom(p.Hex,p.Intensity,p.NoiseIntensity,p.NoiseScale,p.Ambient,p.Mask,p.MaskSize)==p,"device parameters survive custom editing without color loss");
  }
  var custom=NightVisionRules.Custom(" #1234AB ",-12,500,-50,-1,NightVisionMask.None,-4);
  Check(custom.R==0x12&&custom.G==0x34&&custom.B==0xab&&custom.A==255&&custom.Intensity==-12&&custom.NoiseIntensity==500&&custom.MaskSize==-4,"custom numbers retain finite developer values without artificial caps");
  foreach(var color in new[]{"#a","12345g","#123456789",""})Reject(()=>NightVisionRules.Custom(color,1,1,1,1,NightVisionMask.None,1),"invalid color rejected");
  foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
   Reject(()=>NightVisionRules.Custom("#ffffff",invalid,1,1,1,NightVisionMask.None,1),"nonfinite number rejected");
  Check(NightVisionRules.ScopeActive(true,true,true,true,true,false),"live local optic ADS enables optic night vision");
  for(int missing=0;missing<6;missing++){
   var gates=new[]{true,true,true,true,true,false};gates[missing]=!gates[missing];
   Check(!NightVisionRules.ScopeActive(gates[0],gates[1],gates[2],gates[3],gates[4],gates[5]),"scope rejects nonlocal/dead/hipfire/no-optic/no-camera/free-camera state");
  }
  var lights=new Dictionary<string,bool>{{"AI gun",true},{"player gun",true},{"map lamp",true},{"off lamp",false}};
  var scope=new CameraLightScope<string,bool>(()=>lights.Keys.ToArray(),k=>lights[k],(k,v)=>lights[k]=v,false);
  bool Original()=>lights["AI gun"]&&lights["player gun"]&&lights["map lamp"]&&!lights["off lamp"];
  bool Dark()=>lights.Values.All(v=>!v);
  // All four combinations, with the optic rendered as a nested camera.
  foreach(bool full in new[]{false,true})foreach(bool optic in new[]{false,true}){
   scope.Begin(1,full);Check(full?Dark():Original(),"global switch affects the main camera");
   scope.Begin(2,optic);Check(optic?Dark():Original(),"optic switch is independent during nested camera render");
   scope.End(2);Check(full?Dark():Original(),"ending optic render resumes the main camera state");
   scope.End(1);Check(Original()&&scope.Depth==0,"all preexisting enabled/disabled light states restored after render");
  }
  scope.Begin(1,true);scope.Begin(2,false);scope.Reset();
  Check(Original()&&scope.Depth==0,"death, raid exit, failure and module unload recover an interrupted nested pass");
  scope.Begin(1,true);scope.End(99);Check(Dark()&&scope.Depth==1,"unmatched camera completion cannot release a live parent pass");
  scope.End(1);lights["AI gun"]=false;scope.Begin(1,true);scope.End(1);
  Check(!lights["AI gun"]&&lights["map lamp"],"a device switched off between frames stays off after restoration");
  lights["new AI gun"]=true;scope.Begin(1,true);Check(!lights["new AI gun"],"newly discovered AI gun is suppressed");scope.Reset();Check(lights["new AI gun"],"newly discovered AI gun recovers after suppression");
  IntensityChecks(Check,sourceRoot);
  Nw11Checks(Check,sourceRoot);
  Check(CameraBufferOrder.Restore(new[]{"before","flashlight","after"},new[]{"flashlight"},new[]{"before","after"}).SequenceEqual(new[]{"before","flashlight","after"}),"lighting buffer returns to its original position among other effects");
  Check(CameraBufferOrder.Restore(new[]{"before","flashlight","after"},new[]{"flashlight"},new[]{"after","new effect"}).SequenceEqual(new[]{"flashlight","after","new effect"}),"restoration retains native buffer additions and removals");
  Check(CameraBufferOrder.Restore(new[]{"A","B","C","D"},new[]{"A","C"},new[]{"B","D","E"}).SequenceEqual(new[]{"A","B","C","D","E"}),"multiple removed lighting buffers retain their original order");
  Check(CameraBufferOrder.Restore(new[]{"A","B"},new[]{"B"},new[]{"A","B"}).SequenceEqual(new[]{"A","B"}),"native reattachment never creates a duplicate buffer");
  if(gameRoot!=null){
   using var nativeAssembly=AssemblyDefinition.ReadAssembly(Path.Combine(gameRoot,"BepInEx/interop/Assembly-CSharp.dll"));
   var lightSwitch=nativeAssembly.MainModule.Types.Single(t=>t.Name=="Player").NestedTypes.Single(t=>t.Name=="FirearmController").Methods.Single(m=>m.Name=="SetLightsState");
   Check(lightSwitch.ReturnType.FullName=="System.Boolean"&&lightSwitch.Parameters.Count==3&&lightSwitch.Parameters[1].Name=="force"&&lightSwitch.Parameters[2].Name=="animated"&&lightSwitch.Parameters[2].ParameterType.FullName=="System.Boolean","installed native light switch exposes the independent optional animation argument");
   string database=Path.Combine(gameRoot,"SPT_Runtime/SPT_Data/database/templates/items.json");
   if(File.Exists(database)){
    var items=JsonNode.Parse(File.ReadAllText(database))!;
    foreach(var device in devices){
     var native=items[device.Template]!["_props"]!;
     var p=device.Profile;var color=native["Color"]!;
     Check(color["r"]!.GetValue<byte>()==p.R&&color["g"]!.GetValue<byte>()==p.G&&color["b"]!.GetValue<byte>()==p.B&&color["a"]!.GetValue<byte>()==p.A,"device color matches installed SPT item data");
     foreach(var pair in new[]{("Intensity",p.Intensity),("NoiseIntensity",p.NoiseIntensity),("NoiseScale",p.NoiseScale),("DiffuseIntensity",p.Ambient),("MaskSize",p.MaskSize)})
      Check(Math.Abs(native[pair.Item1]!.GetValue<float>()-pair.Item2)<.00001,"device parameter "+pair.Item1+" matches installed item data");
     Check(native["Mask"]!.GetValue<string>()==p.Mask.ToString(),"device mask matches installed SPT item data");
    }
   }
  }
  Console.WriteLine("PASS "+checks+" night vision device, custom input, optic lifecycle, independent camera, AI gun restoration and buffer ordering checks");
 }
 static void Nw11Checks(Action<bool,string> check,string sourceRoot){
  // Serialized enum numbers belong to the existing configuration contract.
  check((int)NightVisionStyle.Custom==7&&(int)NightVisionStyle.Nw11Green==8,"existing saved style numbers remain stable");
  foreach(var style in new[]{NightVisionStyle.Nw11Green,NightVisionStyle.Nw11Blue,NightVisionStyle.Nw11Day}){
   var p=NightVisionRules.Device(style);
   check(p.Clarity&&p.NoiseIntensity==0&&p.Mask==NightVisionMask.None,"Nw11 presets have no simulated noise or device mask");
   check(NightVisionRules.Custom(p.Hex,p.Intensity,p.NoiseIntensity,p.NoiseScale,p.Ambient,p.Mask,p.MaskSize,p.Clarity,p.PreserveColor,p.Gain,p.Contrast)==p,"copy to custom retains the full Nw11 rendering profile");
   check(NightVisionRules.Tone(0,p.Gain,p.Contrast)==0&&NightVisionRules.Tone(1,p.Gain,p.Contrast)==1,"Nw11 retains absolute black and white");
   var ramp=Enumerable.Range(0,257).Select(i=>NightVisionRules.Tone(i/256f,p.Gain,p.Contrast)).ToArray();
   check(ramp.All(float.IsFinite)&&ramp.Zip(ramp.Skip(1)).All(v=>v.First<=v.Second),"tone mapping is monotonic across the full range without band reversals");
   check(NightVisionRules.Tone(.02f,p.Gain,p.Contrast)>.08f&&NightVisionRules.Tone(.8f,p.Gain,p.Contrast)<1,"dark detail is raised while highlights retain headroom");
  }
  check(NightVisionRules.Name(NightVisionStyle.Nw11Day)=="Nw11昼","day profile uses the requested exact display name");
  var day=NightVisionRules.Pixel(NightVisionRules.Nw11Day,.2f,.05f,.01f);
  check(day.R>day.G&&Math.Abs(day.R/day.G-4)<.0001&&Math.Abs(day.G/day.B-5)<.0001,"day enhancement preserves RGB ratios and colored highlights");
  var green=NightVisionRules.Pixel(NightVisionRules.Nw11Green,.05f,.05f,.05f);var blue=NightVisionRules.Pixel(NightVisionRules.Nw11Blue,.05f,.05f,.05f);
  check(green.G>green.B&&green.B>green.R&&blue.B>blue.G&&blue.G>blue.R,"green and blue profiles use distinct intended phosphor colors");
  var custom=NightVisionRules.Custom("#FFAB20",1,0,1,-2,NightVisionMask.None,1,true,false,500,-4);
  check(custom.Gain==500&&custom.Contrast==-4&&custom.Ambient==-2,"new developer inputs retain finite values without input caps");
  foreach(bool full in new[]{false,true})foreach(bool optic in new[]{false,true}){
   float current=.03f;var scope=new CameraRenderValue<float>(()=>current,v=>current=v);
   scope.Begin(1,full?v=>v+.08f:null);check(Math.Abs(current-(full?.11f:.03f))<.000001,"main camera has its own ambient fill");
   scope.Begin(2,optic?v=>v+.12f:null);check(Math.Abs(current-(optic?.15f:.03f))<.000001,"optic uses the baseline, independent of main-camera fill");
   scope.End(2);check(Math.Abs(current-(full?.11f:.03f))<.000001,"ending optic render restores main-camera fill");
   scope.End(1);check(current==.03f&&scope.Depth==0,"render completion restores original environmental lighting");
   current=.06f;scope.Begin(1,v=>v+.08f);scope.Begin(2,null);scope.Reset();check(current==.06f&&scope.Depth==0,"weather changes are recaptured and interrupted nested passes restore safely");
  }
  using var assembly=AssemblyDefinition.ReadAssembly(Path.Combine(sourceRoot,"esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll"));
  var renderer=assembly.MainModule.Types.Single(t=>t.Name=="Nw11Vision");
  var after=renderer.Methods.Single(m=>m.Name=="RenderGrade");
  check(!after.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Any(m=>m.Name is "ReadPixels" or "FindObjectsOfType" or "FindObjectsByType" or "Blit" or "GetTemporaryRT"),"active Nw11 grading reuses the native pass without frame readback, scene scans or extra blits");
 }
 static void IntensityChecks(Action<bool,string> check,string sourceRoot){
  var values=new Dictionary<string,float>{{"AI",7.25f},{"player",2.5f},{"dim lamp",.005f},{"dark",0},{"disabled",12}};
  var initial=new Dictionary<string,float>(values);int writes=0;
  var pass=new CameraLightScope<string,float>(()=>values.Keys.Where(k=>k!="disabled").ToArray(),k=>values[k],(k,v)=>{values[k]=v;writes++;},0);
  pass.Begin(1,true);
  check(values["AI"]==0&&values["player"]==0&&values["dim lamp"]==0&&values["disabled"]==12,"lighting masks intensity without touching disabled devices");
  check(writes==3,"already dark lights are not dirtied");
  pass.Begin(2,false);
  check(values.All(kv=>initial[kv.Key]==kv.Value),"an unmasked nested camera sees exact original light intensities");
  pass.End(2);check(values["AI"]==0&&values["disabled"]==12,"nested render returns to the parent intensity mask");
  pass.End(1);check(values.All(kv=>initial[kv.Key]==kv.Value),"fractional and bright light intensities restore exactly");
  values["player"]=0;pass.Begin(1,true);pass.Reset();
  check(values["player"]==0&&values["AI"]==7.25f,"interrupted passes preserve a player flashlight switched off between frames");
  using var core=AssemblyDefinition.ReadAssembly(Path.Combine(sourceRoot,"esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll"));
  var lighting=core.MainModule.Types.Single(t=>t.Name=="NightVisionLighting");
  IEnumerable<TypeDefinition> Flat(TypeDefinition t){yield return t;foreach(var n in t.NestedTypes)foreach(var x in Flat(n))yield return x;}
  MethodReference[] Calls(MethodDefinition m)=>m.HasBody?m.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().ToArray():Array.Empty<MethodReference>();
  var calls=Flat(lighting).SelectMany(t=>t.Methods).SelectMany(Calls).ToArray();
  check(!calls.Any(m=>m.Name=="set_enabled")&&calls.Any(m=>m.DeclaringType.FullName=="UnityEngine.Light"&&m.Name=="set_intensity"),"compiled suppression cannot enable/disable native lights or repeatedly run their lifecycle");
  check(!calls.Any(m=>m.Name=="FindObjectsOfType")&&Calls(lighting.Methods.Single(m=>m.Name=="Scan")).Any(m=>m.Name=="FindObjectsByType"),"light refresh uses the unsorted native enumeration without scanning other component types");
  foreach(string method in new[]{"Frame","End"})
   check(!Calls(lighting.Methods.Single(m=>m.Name==method)).Any(m=>m.Name=="RestoreBuffers"),"native lighting buffers remain masked across stable frames and camera completion");
 }
}
