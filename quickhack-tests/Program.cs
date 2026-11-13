using LocalQuickhacks;
using Mono.Cecil;
using LocalBlackwallPulse;

int checks=0;
void Check(bool value,string message) { if(!value) throw new Exception(message); checks++; }
for(int seed=0;seed<1000;seed++) for(int difficulty=1;difficulty<=3;difficulty++) {
    var puzzle=new BreachPuzzle(seed,difficulty);
    Check(puzzle.Size==7&&puzzle.Capacity==10,"Board uses seven rows/columns and ten buffer slots");
    Check(puzzle.Started==null&&puzzle.Remaining(1000)==60,"Timer starts on first valid cell");
    Check(puzzle.Programs.Length==3&&puzzle.Programs.All(p=>p.Length==3),"Three independent programs need three codes each");
    Check(puzzle.Solution.Length<=puzzle.Capacity-3,"Full upload route leaves three spare buffer slots");
    Check(Enumerable.Range(0,3).All(a=>Enumerable.Range(0,3).All(b=>a==b||!BreachPuzzle.Contains(puzzle.Programs[a],puzzle.Programs[b]))),"Programs are distinct, not nested versions of one sequence");
    Check(!puzzle.Pick(1,0,10)&&puzzle.Buffer.Count==0&&puzzle.Started==null,"Reject starting outside first row");
    foreach(var cell in puzzle.Solution) {
        if(puzzle.Won) break; // An overlapping symbol path can match early.
        Check(puzzle.CanPick(cell.Row,cell.Col),"Generated solution obeys row/column and uniqueness");
        Check(puzzle.Pick(cell.Row,cell.Col,10),"Generated solution selects");
        Check(!puzzle.CanPick(cell.Row,cell.Col),"Used cells cannot be reused");
    }
    Check(puzzle.Won&&!puzzle.Lost&&puzzle.Buffer.Count<=puzzle.Capacity,"Every generated board is solvable within buffer");
    Check(puzzle.AllUploaded,"One legal route can upload all three independent programs with overlap");
    Check(puzzle.Remaining(1000)==60,"Result retains the time at completion");
    Check(!puzzle.Pick(0,0,11),"No further input after authorization");
}
var timer=new BreachPuzzle(3);
Check(timer.Pick(0,0,7),"Start timeout board"); timer.Tick(68);
Check(timer.Lost&&!timer.Won&&timer.Remaining(100)==0&&!timer.Pick(1,0,68),"Timeout cannot authorize or accept input");
var full=new BreachPuzzle(0);
while(!full.Ended) {
    var cell=Enumerable.Range(0,full.Size*full.Size).Select(i=>(Row:i/full.Size,Col:i%full.Size)).First(p=>full.CanPick(p.Row,p.Col));
    full.Pick(cell.Row,cell.Col,1);
}
Check(full.Buffer.Count==10&&full.Lost&&!full.Won,"Unmatched full buffer fails without authorizing");
BreachPuzzle FindPartial(bool authorization) {
    for(int seed=0;seed<1000;seed++) {
        var p=new BreachPuzzle(seed,2);
        foreach(var cell in p.Solution.Take(4)) {
            p.Pick(cell.Row,cell.Col,10);
            if(!p.Ended&&p.Uploaded(2)==authorization&&Enumerable.Range(0,3).Any(p.Uploaded)) return p;
        }
    }
    throw new Exception("Cannot find partial program fixture");
}
var authorized=FindPartial(true);
Check(!authorized.Ended&&!authorized.AllUploaded,"Matching authorization alone leaves room for other programs");
double partialTime=authorized.Remaining(20);
authorized.Tick(70);
Check(authorized.Won&&!authorized.Lost&&authorized.Uploaded(2),"Timeout keeps previously uploaded authorization and finishes successfully");
Check(partialTime<60&&authorized.Remaining(100)==0,"Timer advances until settlement and then freezes");
var optional=FindPartial(false);
Check(Enumerable.Range(0,2).Any(optional.Uploaded)&&!optional.Uploaded(2),"Optional programs can match independently of authorization");
optional.Tick(70);
Check(optional.Lost&&!optional.Won&&Enumerable.Range(0,2).Any(optional.Uploaded),"Optional uploads remain installed but cannot open a door");
Check(BreachPuzzle.Contains(new[]{"FF","1C","55","BD"},new[]{"1C","55","BD"}),"Sequence can start inside buffer");
Check(!BreachPuzzle.Contains(new[]{"1C","FF","55"},new[]{"1C","55"}),"Sequence must be contiguous");
var latch=new AuthorizationLatch();
Check(!latch.Take(false,true)&&!latch.Take(true,false)&&latch.Take(true,true)&&!latch.Take(true,true),"Failure and invalid target do not authorize; success authorizes exactly once");

string root=args.FirstOrDefault()??"D:/SPT-5.0.0-48558";
using var game=AssemblyDefinition.ReadAssembly(Path.Combine(root,"BepInEx/interop/Assembly-CSharp.dll"));
var helper=game.MainModule.Types.Single(t=>t.FullName=="EFT.InteractionContextHelper");
foreach(string type in new[]{"CardReader","KeycardDoor"}) Check(helper.Methods.Count(m=>m.Name=="GetAvailableActions"&&m.Parameters.Count==3&&m.Parameters[1].ParameterType.Name==type&&m.Parameters[2].Name=="isProxy")==1,"Exact native "+type+" menu overload");
var action=game.MainModule.Types.Single(t=>t.FullName=="EFT.UI.InteractionAction");
Check(new[]{"Name","Action","Disabled"}.All(n=>action.Properties.Any(p=>p.Name==n)),"Native menu supports a real action independent of card items");
Check(game.MainModule.Types.Single(t=>t.FullName=="EFT.GameTriggers.TriggersEmitter").Methods.Any(m=>m.Name=="Emit"&&m.Parameters.Count==2&&m.Parameters[0].ParameterType.FullName=="System.String"),"Native success trigger emission contract");
if(args.Length>1) {
    var sounds=Directory.GetFiles(args[1],"*.wav");
    Check(sounds.Length==12,"Complete packaged UI, quickhack and Blackwall audio set");
    foreach(var path in sounds) {
        var wave=WavePcm.Read(path);
        Check(wave.Samples.Length>0&&wave.Samples.Length%wave.Channels==0&&wave.Samples.All(float.IsFinite),"Packaged WAV decodes for Unity: "+Path.GetFileName(path));
    }
    string payload=Directory.GetParent(args[1])!.FullName;
    string corePath=Path.Combine(payload,"runtime/Local.Cp2077.Quickhacks.Core.module");
    if(File.Exists(corePath)) {
        using var core=AssemblyDefinition.ReadAssembly(corePath);
        using var host=AssemblyDefinition.ReadAssembly(Path.Combine(payload,"Local.Cp2077.Quickhacks.dll"));
        Check(core.MainModule.Types.Single(t=>t.FullName=="LocalQuickhacks.RuntimeModule").Interfaces.Any(i=>i.InterfaceType.FullName=="LocalQuickhacks.Hosting.IQuickhackModule"),"Core implements the standalone host contract");
        Check(!core.MainModule.Types.Any(t=>t.CustomAttributes.Any(a=>a.AttributeType.Name=="BepInPlugin"))&&!core.MainModule.Types.Any(t=>t.BaseType?.FullName=="UnityEngine.MonoBehaviour"),"Core registers neither a second plugin nor a per-generation IL2CPP driver");
        Check(host.MainModule.Types.Count(t=>t.CustomAttributes.Any(a=>a.AttributeType.Name=="BepInPlugin"))==1,"Exactly one standalone host plugin");
        Check(!core.MainModule.AssemblyReferences.Concat(host.MainModule.AssemblyReferences).Any(a=>a.Name.StartsWith("Local.Spt5.ESP")),"Quickhacks remains independent of SPT Assistant");
    }
}
Console.WriteLine($"PASS {checks} puzzle, authorization, packaged audio and installed EFT 48558 API checks");
