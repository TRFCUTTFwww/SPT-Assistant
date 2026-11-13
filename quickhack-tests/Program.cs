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

var gesture=new ScannerGesture();
Check(!gesture.Step(0,true,false,.25)&&!gesture.Step(.1,true,false,.25)&&gesture.Step(.26,true,false,.25),"Ctrl requires a deliberate standalone hold");
Check(!gesture.Step(.27,true,true,.25)&&!gesture.Step(2,true,false,.25),"Ctrl chord cancels scanning until release, even after other key is released");
gesture.Step(3,false,false,.25);
Check(!gesture.Step(4,true,false,.25)&&gesture.Step(4.3,true,false,.25),"Releasing Ctrl rearms the scanner");
gesture.Cancel();
Check(!gesture.Step(5,true,false,.25),"ESC or a conflicting UI cannot reopen scanning while Ctrl stays held");
gesture.Step(6,false,false,.25);
Check(gesture.Step(7,true,false,0),"Other configurable scanner keys retain immediate activation");
Check(HackRules.All.Select(HackRules.Name).SequenceEqual(new[]{"重启义眼","武器故障","阻碍移动","记忆清除","赛博精神病","黑墙网关"}),"Six retained quickhacks appear in order after removing backup");
Check(HackRules.Cycle(HackKind.RebootOptics,-1)==HackKind.BlackwallGateway&&HackRules.Cycle(HackKind.BlackwallGateway,1)==HackKind.RebootOptics,"Scrolling wraps over all six skills");
Check(HackRules.All.Select(HackRules.Icon).Distinct().Count()==6,"Each quickhack uses its own original icon");
var introStart=UploadAnimation.Intro(-1); var introEnd=UploadAnimation.Intro(2);
Check(introStart.Scale==1.5f&&introStart.Y==-50&&introStart.Alpha==0,"Upload starts above the badge at the original 1.5 scale");
Check(Math.Abs(introEnd.Scale-1)<.0001f&&Math.Abs(introEnd.Y)<.0001f&&introEnd.Alpha==1,"Intro settles independently of gameplay upload duration");
Check(UploadAnimation.Intro(.125f).Y < -50,"Back easing preserves the initial overshoot instead of smoothstep");
Check(Math.Abs(UploadAnimation.Complete(.269f).Y-12.5f)<.001f,"Completion reaches half of the 2160p 25px offset");
Check(UploadAnimation.Complete(0).Alpha==1&&UploadAnimation.Complete(UploadAnimation.CompleteDuration+.001f).Alpha==0,"Completion fades only at its delayed final stage");
for(int i=0;i<=500;i++) {
    var pose=UploadAnimation.Complete(i/1000f);
    Check(float.IsFinite(pose.Y)&&pose.Y>=-.001f&&pose.Y<=12.501f&&pose.Alpha>=0&&pose.Alpha<=1,"Completion is bounded through translation and fade transitions");
}

string root=args.FirstOrDefault()??"D:/SPT-5.0.0-48558";
using var game=AssemblyDefinition.ReadAssembly(Path.Combine(root,"BepInEx/interop/Assembly-CSharp.dll"));
var helper=game.MainModule.Types.Single(t=>t.FullName=="EFT.InteractionContextHelper");
foreach(string type in new[]{"CardReader","KeycardDoor"}) Check(helper.Methods.Count(m=>m.Name=="GetAvailableActions"&&m.Parameters.Count==3&&m.Parameters[1].ParameterType.Name==type&&m.Parameters[2].Name=="isProxy")==1,"Exact native "+type+" menu overload");
var action=game.MainModule.Types.Single(t=>t.FullName=="EFT.UI.InteractionAction");
Check(new[]{"Name","Action","Disabled"}.All(n=>action.Properties.Any(p=>p.Name==n)),"Native menu supports a real action independent of card items");
Check(game.MainModule.Types.Single(t=>t.FullName=="EFT.GameTriggers.TriggersEmitter").Methods.Any(m=>m.Name=="Emit"&&m.Parameters.Count==2&&m.Parameters[0].ParameterType.FullName=="System.String"),"Native success trigger emission contract");
var bot=game.MainModule.Types.Single(t=>t.FullName=="EFT.BotOwner");
Check(bot.Methods.Any(m=>m.Name=="UpdateManual"&&m.Parameters.Count==0),"Madness evaluation uses the real EFT owner update entry point");
var mover=game.MainModule.Types.Single(t=>t.Name=="BotMover");
Check(new[]{"Pause","_pauseEndTime"}.All(n=>mover.Properties.Any(p=>p.Name==n)),"Movement lock has reversible native pause state");
Check(new[]{"ManualUpdate","ManualFixedUpdate"}.All(n=>mover.Methods.Count(m=>m.Name==n)==1),"Movement patches have unique native entry points");
Check(game.MainModule.Types.Single(t=>t.FullName=="EFT.BotMemory").Methods.Any(m=>m.Name=="DeleteInfoAboutEnemy"&&m.Parameters.Count==1),"Memory wipe uses native personal enemy-memory removal");
var memoryType=game.MainModule.Types.Single(t=>t.FullName=="EFT.BotMemory");
Check(memoryType.Properties.Single(p=>p.Name=="GoalEnemy").SetMethod.Parameters.Single().ParameterType.Name=="EnemyInfo","Cyberpsychosis can preserve a commanded goal through the native setter");
Check(memoryType.Methods.Any(m=>m.Name=="AddEnemyInfo"&&m.Parameters.Count==3&&m.Parameters[1].ParameterType.Name=="BotGroupEnemyInfo"),"Cyberpsychosis registers native memory and its shared enemy position");
Check(game.MainModule.Types.Single(t=>t.Name=="BotEnemiesController").Methods.Any(m=>m.Name=="UpdateFor"&&m.Parameters.Count==2&&m.Parameters[1].ParameterType.FullName=="System.Boolean"),"Commanded enemy participates in native observation updates");
var firearm=game.MainModule.Types.Single(t=>t.FullName=="EFT.Player").NestedTypes.Single(t=>t.Name=="FirearmController");
Check(firearm.Methods.Any(m=>m.Name=="SetTriggerPressed"&&m.Parameters.Count==1&&m.Parameters[0].ParameterType.FullName=="System.Boolean"),"Weapon glitch uses the dedicated trigger-press entry point");
if(args.Length>1) {
    foreach(string key in new[]{"font_body","font_code","font_latin"}) {
        using var fontDoc=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(args[1],key+".json")));
        var font=fontDoc.RootElement; int width=font.GetProperty("width").GetInt32(),height=font.GetProperty("height").GetInt32();
        var png=File.ReadAllBytes(Path.Combine(args[1],key+".png"));
        int PngInt(int start)=>System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(start,4));
        Check(PngInt(16)==width&&PngInt(20)==height,"Font metrics match the packaged texture dimensions: "+key);
        Check(font.GetProperty("sourceSha256").GetString()!.Length==64&&font.GetProperty("pixels").GetInt32()==64,"Font atlas records its source hash and raster size: "+key);
        foreach(var property in font.GetProperty("glyphs").EnumerateObject()) {
            var g=property.Value; int x=g.GetProperty("x").GetInt32(),y=g.GetProperty("y").GetInt32(),w=g.GetProperty("width").GetInt32(),h=g.GetProperty("height").GetInt32();
            Check(x>=1&&y>=1&&x+w<width&&y+h<height&&g.GetProperty("advance").GetSingle()>0,"Font glyph fits its atlas with a sampling gutter: "+key);
        }
        if(key=="font_body") foreach(char c in string.Concat(HackRules.All.Select(HackRules.Name)))
            Check(font.GetProperty("glyphs").TryGetProperty(((int)c).ToString(),out _),"Original Chinese atlas covers every retained quickhack name");
    }
    var sounds=Directory.GetFiles(args[1],"*.wav");
    Check(sounds.Length==14,"UI, five quickhacks and real Blackwall death audio; no borrowed upload sound");
    foreach(var kind in HackRules.All) {
        Check(File.Exists(Path.Combine(args[1],HackRules.Icon(kind)+".png")),"Original quickhack icon packaged: "+kind);
        Check(File.Exists(Path.Combine(args[1],HackRules.Icon(kind)+".wav"))==(kind!=HackKind.BlackwallGateway),"Only confirmed quickhack sounds packaged: "+kind);
    }
    foreach(var path in sounds) {
        var wave=WavePcm.Read(path);
        Check(wave.Samples.Length>0&&wave.Samples.Length%wave.Channels==0&&wave.Samples.All(float.IsFinite),"Packaged WAV decodes for Unity: "+Path.GetFileName(path));
    }
    string payload=Directory.GetParent(args[1])!.FullName;
    string corePath=Path.Combine(payload,"runtime/Local.Cp2077.Quickhacks.Core.module");
    if(File.Exists(corePath)) {
        using var core=AssemblyDefinition.ReadAssembly(corePath);
        var aiHacks=core.MainModule.Types.Single(t=>t.FullName=="LocalQuickhacks.AiHacks");
        var patchStrings=aiHacks.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Where(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Ldstr).Select(i=>i.Operand as string).ToArray();
        Check(!patchStrings.Contains("CanPressTrigger")&&!aiHacks.Methods.Any(m=>m.Name=="CanFire"),"Regression: shared IL2CPP CanPressTrigger body must never be detoured");
        Check(!aiHacks.Methods.Any(m=>m.Name.Contains("Backup")),"Removed backup does not create native requests");
        Check(patchStrings.Contains("SetTriggerPressed"),"Dedicated trigger hook remains present in the built module");
        using var host=AssemblyDefinition.ReadAssembly(Path.Combine(payload,"Local.Cp2077.Quickhacks.dll"));
        Check(core.MainModule.Types.Single(t=>t.FullName=="LocalQuickhacks.RuntimeModule").Interfaces.Any(i=>i.InterfaceType.FullName=="LocalQuickhacks.Hosting.IQuickhackModule"),"Core implements the standalone host contract");
        Check(!core.MainModule.Types.Any(t=>t.CustomAttributes.Any(a=>a.AttributeType.Name=="BepInPlugin"))&&!core.MainModule.Types.Any(t=>t.BaseType?.FullName=="UnityEngine.MonoBehaviour"),"Core registers neither a second plugin nor a per-generation IL2CPP driver");
        Check(host.MainModule.Types.Count(t=>t.CustomAttributes.Any(a=>a.AttributeType.Name=="BepInPlugin"))==1,"Exactly one standalone host plugin");
        Check(!core.MainModule.AssemblyReferences.Concat(host.MainModule.AssemblyReferences).Any(a=>a.Name.StartsWith("Local.Spt5.ESP")),"Quickhacks remains independent of SPT Assistant");
    }
}
Console.WriteLine($"PASS {checks} puzzle, authorization, packaged audio and installed EFT 48558 API checks");
