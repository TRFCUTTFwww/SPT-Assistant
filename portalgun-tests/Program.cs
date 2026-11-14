using Mono.Cecil;
using LocalPortalGun;
using LocalBlackwallPulse;
using System.Numerics;

if(args.Length>0&&args[0]=="inspect") {
    using var asm=AssemblyDefinition.ReadAssembly(args[1]);
    foreach(var t in asm.MainModule.Types.Where(t=>args.Skip(2).Any(n=>t.Name.Contains(n)))) {
        Console.WriteLine("TYPE "+t.FullName);
        foreach(var p in t.Properties)Console.WriteLine(" PROP "+p.PropertyType+" "+p.Name);
        foreach(var f in t.Fields)Console.WriteLine(" FIELD "+f.Name+" = "+f.Constant);
        foreach(var m in t.Methods.Where(m=>!m.IsGetter&&!m.IsSetter&&m.IsPublic))Console.WriteLine(" METHOD "+m);
    }
    return;
}
void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
Check(PortalRules.Enters(new(0,0,.7f),new(0,0,.4f)),"forward wall entry");
Check(!PortalRules.Enters(new(0,0,.4f),new(0,0,.7f)),"walking away cannot teleport");
Check(!PortalRules.Enters(new(0,0,.4f),new(.01f,0,.4f)),"standing / parallel motion cannot teleport");
Check(!PortalRules.Enters(new(1,0,.7f),new(1,0,.4f)),"outside aperture cannot teleport");
Check(!PortalRules.Enters(new(0,0,-.1f),new(0,0,-.2f)),"back face cannot teleport");
Check(!PortalRules.Enters(new(0,0,8),new(0,0,.2f)),"external teleport does not chain into portal");
Check(!PortalRules.Enters(new(float.NaN,0,1),new(0,0,0)),"invalid coordinates rejected");
var offset=new Vector3(.2f,.6f,.4f);
Check(PortalRules.Through(PortalRules.Through(offset))==offset&&PortalRules.Through(offset)==new Vector3(-.2f,.6f,-.4f),"portal transform is reversible with correct handedness");
Check(PortalRules.EntersFloor(new(.8f,0,.12f),new(.2f,0,.12f)),"walk onto floor aperture");
Check(PortalRules.EntersFloor(new(0,0,.7f),new(0,0,.2f)),"fall onto floor aperture");
Check(!PortalRules.EntersFloor(new(0,0,.12f),new(0,0,.12f)),"arrival on floor does not bounce back after cooldown");
Check(!PortalRules.EntersFloor(new(.8f,0,.7f),new(.2f,0,.7f)),"walking above floor does not trigger");
Check(!PortalRules.EntersFloor(new(.8f,0,-.3f),new(.2f,0,-.3f)),"underside of floor cannot trigger");
if(args.Length>0)foreach(var file in Directory.EnumerateFiles(args[0],"*.wav")) {
    var pcm=WavePcm.Read(file);Check(pcm.Samples.Length>0&&pcm.Samples.All(float.IsFinite),"decode "+Path.GetFileName(file));
}
if(args.Length>1) {
    var all=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(args[1]))!.AsObject();
    var original=all[PortalIdentity.BaseWeapon]!.AsObject();string before=original.ToJsonString();
    var custom=PortalItemRules.Create(original);var props=custom["_props"]!.AsObject();
    Check(original.ToJsonString()==before,"native base weapon is unchanged");
    Check(custom["_id"]!.ToString()==PortalIdentity.Template&&custom["_parent"]!.ToString()==PortalIdentity.PistolClass,"independent template fits pistol category");
    Check(props["Prefab"]!.ToJsonString()==original["_props"]!["Prefab"]!.ToJsonString(),"native animator prefab remains available");
    Check(props["Chambers"]![0]!["_props"]!["filters"]![0]!["Filter"]!.AsArray().Count==0,"portal gun cannot be loaded with live ammunition");
    Check(props["IsFlareGun"]!.ToString()=="false"&&props["Slots"]!.AsArray().Count==0,"complete standalone device needs no required attachments or flare logic");
    Check(!all.ContainsKey(PortalIdentity.Template),"custom ID does not collide with stock database");
}
if(args.Length>2) {
    using var module=AssemblyDefinition.ReadAssembly(args[2]);
    Check(!module.MainModule.AssemblyReferences.Any(a=>a.Name.Contains("Quickhack")||a.Name.Contains("Spt5.ESP")),"universal module has no Assistant or Quickhack dependency");
    var moduleType=module.MainModule.Types.Single(t=>t.Name=="PortalModule");
    Check(moduleType.Interfaces.Any(i=>i.InterfaceType.FullName=="Local.ModHost.IHotModule"),"module uses universal lifecycle");
    var command=moduleType.Methods.Single(m=>m.Name=="Command");
    Check(command.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="IsGun"),"input hook checks actual held item identity");
    Check(!module.MainModule.Types.Any(t=>t.BaseType?.Name=="MonoBehaviour"),"hot module registers no unload-unsafe native behaviour types");
}
