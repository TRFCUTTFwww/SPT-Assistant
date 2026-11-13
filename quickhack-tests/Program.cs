using LocalQuickhacks;
using Mono.Cecil;
using LocalBlackwallPulse;

int checks=0;
void Check(bool value,string message) { if(!value) throw new Exception(message); checks++; }
for(int seed=0;seed<1000;seed++) for(int difficulty=1;difficulty<=3;difficulty++) {
    var puzzle=new BreachPuzzle(seed,difficulty);
    Check(puzzle.Started==null&&puzzle.Remaining(1000)==30,"Timer starts on first valid cell");
    Check(!puzzle.Pick(1,0,10)&&puzzle.Buffer.Count==0&&puzzle.Started==null,"Reject starting outside first row");
    foreach(var cell in puzzle.Solution) {
        if(puzzle.Won) break; // An overlapping symbol path can match early.
        Check(puzzle.CanPick(cell.Row,cell.Col),"Generated solution obeys row/column and uniqueness");
        Check(puzzle.Pick(cell.Row,cell.Col,10),"Generated solution selects");
        Check(!puzzle.CanPick(cell.Row,cell.Col),"Used cells cannot be reused");
    }
    Check(puzzle.Won&&!puzzle.Lost&&puzzle.Buffer.Count<=puzzle.Capacity,"Every generated board is solvable within buffer");
    Check(!puzzle.Pick(0,0,11),"No further input after authorization");
}
var timer=new BreachPuzzle(3);
Check(timer.Pick(0,0,7),"Start timeout board"); timer.Tick(38);
Check(timer.Lost&&!timer.Won&&!timer.Pick(1,0,38),"Timeout cannot authorize or accept input");
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
}
Console.WriteLine($"PASS {checks} puzzle, authorization, packaged audio and installed EFT 48558 API checks");
