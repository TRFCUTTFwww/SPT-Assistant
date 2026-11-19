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
Check(HackRules.All.Select(HackRules.Name).SequenceEqual(new[]{"重启义眼","武器故障","阻碍移动","记忆清除","义体过热","突触熔融","赛博精神病","黑墙网关"}),"Eight quickhacks appear in order with the two new combat programs");
Check(HackRules.Cycle(HackKind.RebootOptics,-1)==HackKind.BlackwallGateway&&HackRules.Cycle(HackKind.BlackwallGateway,1)==HackKind.RebootOptics,"Scrolling wraps over all six skills");
Check(HackRules.All.Select(HackRules.Icon).Distinct().Count()==8,"Each quickhack uses its own original icon");
var uploads=new UploadQueue<string>();
Check(uploads.TryAdd("a",HackKind.Overheat,5,10)&&uploads.TryAdd("a",HackKind.WeaponGlitch,3,10)&&uploads.TryAdd("b",HackKind.RebootOptics,2,10),"Different targets upload independently while a target queues multiple programs");
Check(uploads.Current("a")!.Progress(11)==.5f&&uploads.Current("b")!.Progress(11)>0,"Both targets advance concurrently");
Check(uploads.For("a")[1].Elapsed(100)==0,"Waiting entries never upload early");
Check(uploads.Complete("a",11)==null&&uploads.Complete("a",12)!.Kind==HackKind.Overheat,"A program cannot execute before its actual upload deadline");
Check(uploads.Current("a")!.Kind==HackKind.Overheat&&uploads.Current("a")!.Completed==12&&uploads.For("a")[1].Elapsed(12.3f)==0,"Completed program keeps the main icon until the entire original completion track finishes");
Check(uploads.Complete("a",12.3f)==null&&uploads.Current("a")!.Kind==HackKind.Overheat,"Next upload cannot overlap the previous completion animation");
Check(uploads.Complete("a",12.5f)==null&&uploads.Current("a")!.Kind==HackKind.WeaponGlitch&&uploads.Current("a")!.Progress(12.6f)==0,"Queue promotion has its own 0.25 second move before the next upload starts");
Check(uploads.Current("a")!.Progress(13) > 0&&uploads.Complete("a",13)==null&&uploads.Cancel("a")==3&&uploads.Cancel("a")==0,"Only pending paid RAM is refunded once after animated promotion");
Check(uploads.Current("b")!=null&&uploads.Clear()==2&&uploads.Count==0,"Cancellation is isolated by target and global cleanup refunds the rest");
foreach(var kind in HackRules.All.Take(UploadQueue<string>.PerTargetLimit)) Check(uploads.TryAdd("c",kind,1,20),"Four per-target slots are accepted");
Check(!uploads.TryAdd("c",HackKind.BlackwallGateway,12,20)&&uploads.Count==4,"Full target queues reject before payment");
uploads.Clear();
Check(uploads.TryAdd("free",HackKind.SynapseBurnout,0,30)&&uploads.Clear()==0,"Infinite RAM requests never manufacture refunds");
Check(uploads.TryAdd("outro",HackKind.RebootOptics,2,40)&&uploads.Complete("outro",42)!=null&&uploads.Cancel("outro")==0,"Cancelling a completed but visible icon does not refund an applied effect");
foreach(float tick in new[]{.016f,.1f,.7f,5f}) {
    var sequence=new UploadQueue<string>();
    sequence.TryAdd("npc",HackKind.RebootOptics,2,0); sequence.TryAdd("npc",HackKind.WeaponGlitch,3,0);
    Check(sequence.TryAdd("npc",HackKind.RebootOptics,2,.5f)&&sequence.TryAdd("npc",HackKind.RebootOptics,2,.7f),"Repeated programs can be added while the same program is uploading or waiting");
    Check(!sequence.TryAdd("npc",HackKind.RebootOptics,2,.8f)&&sequence.Count==4,"Repeated programs retain the per-target slot limit");
    var executed=new List<(HackKind Kind,float At)>();
    for(float now=0;now<40;now+=tick) if(sequence.Complete("npc",now) is {} entry) executed.Add((entry.Kind,now));
    Check(executed.Select(e=>e.Kind).SequenceEqual(new[]{HackKind.RebootOptics,HackKind.WeaponGlitch,HackKind.RebootOptics,HackKind.RebootOptics}),"All frame rates execute repeated programs exactly once per entry and in order");
    Check(executed[1].At-executed[0].At>=UploadQueue<string>.HandoffDuration+HackRules.Upload(HackKind.WeaponGlitch)-.001f,"Frame stalls cannot skip completion/move animations or upload the next program early");
}
var repeated=new UploadQueue<string>();
for(int i=0;i<4;i++) Check(repeated.TryAdd("same",HackKind.Overheat,5,i*.1f),"Each repeated upload reserves its own slot and payment");
Check(repeated.Complete("same",2)!=null&&repeated.Complete("same",2)==null,"Repeated queue completes a single entry only once");
Check(repeated.Cancel("same")==15&&repeated.Cancel("same")==0,"Only the three unapplied repeated entries are refunded, once");
for(int i=0;i<UploadQueue<string>.TotalLimit;i++) Check(repeated.TryAdd("target"+i,HackKind.Overheat,5,0),"Independent targets fill the global upload capacity");
Check(!repeated.TryAdd("overflow",HackKind.Overheat,5,0)&&repeated.Count==32&&repeated.Clear()==160,"Global limit and refund accounting still hold with repeatable uploads");

var factions=new Dictionary<string,string[]> {
    ["黑色军团"]=["blackDivision","bossWedge","bossWedgeLab","followerWedgeLab","bossBullyBlackDiv","followerBullyBlackDiv","pmcBotBlackDiv","pmcBotBlackDivSeason","blackfox"],
    ["BOSS"]=["bossKilla","bossScourgeEventLab","infectedTagilla","ravangeZryachiyEvent","peacefullZryachiyEvent"],
    ["保镖"]=["followerBully","bossBoarSniper","tagillaHelperAgro","followerScourgeEventLab","followerBigPipe","followerBirdEye"],
    ["邪教徒"]=["sectantWarrior","sectantPriest","sectantOni","sectantPredvestnik","sectantPrizrak","sectactPriestEvent"],
    ["感染者"]=["infectedAssault","infectedPmc","infectedCivil","infectedLaborant","infectedScreamer"],
    ["掠夺者"]=["pmcBot"], ["游荡者"]=["exUsec","exUsecFree"],
    ["USEC"]=["pmcUSEC"], ["BEAR"]=["pmcBEAR"],
    ["Scav"]=["assault","assaultTutorial","assaultGroup","marksman","cursedAssault","crazyAssaultEvent"],
    ["平民"]=["civilian"], ["BTR 射手"]=["shooterBTR"], ["圣诞老人"]=["gifter"],
    ["冬季精灵"]=["spiritWinter"], ["春季精灵"]=["spiritSpring"],
    ["俄军"]=["vsRF","vsRFSniper","vsRFFight"], ["竞技场战士"]=["arenaFighter","arenaFighterEvent"],
    ["Peacemaker"]=["peacemaker"], ["Skier"]=["skier"], ["哨兵"]=["sentry"]
};
foreach(var pair in factions) foreach(string role in pair.Value) foreach(string side in new[]{"Savage","Usec","Bear",""})
    Check(TargetFaction.Name(role,side)==pair.Key&&TargetFaction.Name(role.ToUpperInvariant(),side)==pair.Key,"Specific role overrides broad or stale native side: "+role);
Check(TargetFaction.Name(null,"Usec")=="USEC"&&TargetFaction.Name("","Bear")=="BEAR"&&TargetFaction.Name(null,null)=="未知","Missing role uses only known PMC sides");
Check(TargetFaction.Name("futureSpecialRole","Savage")=="其他/未知"&&TargetFaction.Name("futureSpecialRole","Usec")=="其他/未知"&&TargetFaction.Name("","Savage")=="其他/未知","Unknown special AI is never presented as a confirmed Scav");
Check(UploadAnimation.QueueMove(0)==0&&UploadAnimation.QueueMove(UploadAnimation.QueueMoveDuration)==1&&UploadAnimation.QueueMove(.1f)>.5f,"Original queue promotion uses its own quartic easing and settles at the main slot");
foreach(float duration in new[]{.5f,1.2f,2f,5f}) {
    Check(!GatewayTiming.CanExecute(-1,duration,100)&&!GatewayTiming.CanExecute(10,duration,10+duration-.01f)&&GatewayTiming.CanExecute(10,duration,10+duration),"Blackwall damage coincides exactly with the configured animation end; no extra upload or delay");
}
var timeLease=new ScalarLease();
Check(timeLease.Update(true,1,.15f)==.15f&&timeLease.Update(true,.15f,.2f)==.2f&&timeLease.Update(false,.2f,1)==1,"Scanner slowdown updates settings and restores its original time scale on exit");
Check(timeLease.Update(true,.5f,.2f)==.1f&&timeLease.Update(false,.1f,1)==.5f,"Scanner preserves an existing slow time scale");
Check(timeLease.Update(true,0,.15f)==0&&timeLease.Update(false,0,1)==0,"Scanner cannot unpause an already paused game");
timeLease.Update(true,1,.15f);
Check(timeLease.Update(true,0,.15f)==0&&timeLease.Update(false,0,1)==0,"External pause takes ownership and survives scanner close");
timeLease.Update(true,1,.15f);
Check(timeLease.Update(true,.7f,.15f)==.7f&&timeLease.Update(false,.7f,1)==.7f,"External speed changes are preserved during scanner activity and shutdown");
foreach(float step in new[]{.016f,.25f,.7f,3f}) {
    float sum=0,previous=0;
    for(float elapsed=step;previous<10;elapsed+=step) { sum+=CombatHackRules.HeatDamage(elapsed,previous); previous=elapsed; }
    Check(Math.Abs(sum-160)<.01f,"Heat integrates the same capped damage at different frame and tick rates");
}
Check(CombatHackRules.HeatDamage(-1,0)==0&&CombatHackRules.HeatDamage(10,10)==0,"Heat cannot damage before start or after expiry");
Check(CombatHackRules.BurnoutDamage(0)==80&&CombatHackRules.BurnoutDamage(12)==200&&CombatHackRules.BurnoutDamage(24)==320&&CombatHackRules.BurnoutDamage(100)==320,"Burnout scales with missing RAM within the documented damage cap");
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
string botTypes=Path.Combine(root,"SPT_Runtime/SPT_Data/database/bots/types");
if(Directory.Exists(botTypes)) foreach(string path in Directory.GetFiles(botTypes,"*.json")) {
    string role=Path.GetFileNameWithoutExtension(path);
    Check(TargetFaction.Name(role,"Savage")!="其他/未知","Installed server role has an explicit scanner faction: "+role);
}
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
var hostilityService=game.MainModule.Types.Single(t=>t.Name=="BotHostilityService");
Check(hostilityService.Methods.Count(m=>m.Name=="EvaluateHostility"&&m.Parameters.Count==2&&m.Parameters[0].ParameterType.Name=="BotGroupInnerHostilityData"&&m.Parameters[1].ParameterType.Name=="BotHostilityData"&&m.ReturnType.Name=="HostilityEvaluationResult")==1,"Cyberpsychosis hooks the exact native hostility evaluation overload");
var hostility=game.MainModule.Types.Single(t=>t.Name=="BotGroupHostility");
Check(new[]{"Enemies","Allies","Warns","Neutrals","LastHostilityStatus"}.All(n=>hostility.Properties.Any(p=>p.Name==n)),"Temporary native hostility can save and restore all relationship registries");
Check(hostility.Methods.Any(m=>m.Name=="UpdatePlayerHostilityCollection"&&m.Parameters.Count==2),"Temporary hostility uses the same native transition as Assistant's friend-to-enemy action");
Check(memoryType.Properties.Single(p=>p.Name=="GoalEnemy").SetMethod.Parameters.Single().ParameterType.Name=="EnemyInfo","Cyberpsychosis can preserve a commanded goal through the native setter");
Check(memoryType.Methods.Any(m=>m.Name=="AddEnemyInfo"&&m.Parameters.Count==3&&m.Parameters[1].ParameterType.Name=="BotGroupEnemyInfo"),"Cyberpsychosis registers native memory and its shared enemy position");
Check(game.MainModule.Types.Single(t=>t.Name=="BotEnemiesController").Methods.Any(m=>m.Name=="SetInfo"&&m.Parameters.Count==2&&m.Parameters[0].ParameterType.Name=="IPlayer"&&m.Parameters[1].ParameterType.Name=="EnemyInfo"),"Factory-created squad enemies use the native dictionary/observation registration entry point");
Check(game.MainModule.Types.Single(t=>t.Name=="BotEnemiesMissController").Methods.Count(m=>m.Name=="ShouldApplyDamage"&&m.ReturnType.FullName=="System.Boolean"&&m.Parameters.Select(p=>p.ParameterType.Name).SequenceEqual(new[]{"Player","DamageInfo","EBodyPart"}))==1,"Forced AI combat bypasses only the dedicated pre-armor AI miss gate");
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
        if(key=="font_body") foreach(char c in string.Concat(HackRules.All.Select(HackRules.Name).Concat(factions.Keys).Append("其他/未知上传槽位已满每目标全局正在上传等待上传x")))
            Check(font.GetProperty("glyphs").TryGetProperty(((int)c).ToString(),out _),"Original Chinese atlas covers quickhack names, all faction labels and repeated-upload status");
    }
    var sounds=Directory.GetFiles(args[1],"*.wav");
    Check(sounds.Length==17,"UI, eight quickhacks and Blackwall death audio");
    foreach(var kind in HackRules.All) {
        Check(File.Exists(Path.Combine(args[1],HackRules.Icon(kind)+".png")),"Original quickhack icon packaged: "+kind);
        Check(File.Exists(Path.Combine(args[1],HackRules.Icon(kind)+".wav")) ,"Quickhack sound packaged: "+kind);
    }
    Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(args[1],"BlackwallBreach.wav")))).Equals("EF1A6CEF913B9FABA4DAC792C26189DE5D270EA350D6047E55439B6E6BA4102B"),"Blackwall cast uses the verified Blackwall recording");
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
