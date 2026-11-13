using Comfort.Common;
using EFT;
using EFT.CameraControl;
using UnityEngine;

namespace LocalQuickhacks;

internal enum UiMode { None, Scanner, Preview, Breach }
internal sealed record ActorRef(IntPtr World, IntPtr Pointer, string Profile) {
    internal Player? Resolve() {
        var world=Singleton<GameWorld>.Instance;
        if(!Runtime.InRaid||world?.Pointer!=World) return null;
        var list=world.AllAlivePlayersList;
        if(list!=null) for(int i=0;i<list.Count;i++) { var p=list[i]; if(p?.Pointer==Pointer&&p.ProfileId==Profile&&p.IsAI&&!p.IsYourPlayer&&p.HealthController?.IsAlive==true) return p; }
        return null;
    }
    internal static ActorRef Of(Player p)=>new(Singleton<GameWorld>.Instance.Pointer,p.Pointer,p.ProfileId);
}

internal static class Runtime {
    internal static bool Active { get; private set; }
    internal static UiMode Mode;
    internal static bool BlockInput=>Active&&(Mode!=UiMode.None||Time.frameCount<=releaseFrame);
    internal static bool PreviewBreach;
    internal static BreachPuzzle? Puzzle;
    internal static DoorLease? Door;
    internal static ActorRef? Target;
    internal static HackKind Selected;
    internal static HackKind PreviewKind;
    internal static float ModeStarted { get; private set; }
    internal static float Ram=24, PreviewStarted=-100, PreviewProgress;
    static string status="";
    static float statusStarted=-100;
    internal static string Status { get=>status; set { status=value; statusStarted=Time.unscaledTime; } }
    internal static float StatusAlpha=>1-Math.Clamp((Time.unscaledTime-statusStarted-2.5f)/.5f,0,1);
    internal static Camera? Camera=>CameraManager.Instance?.Camera??UnityEngine.Camera.main;
    internal static bool InRaid { get { var g=Singleton<AbstractGame>.Instance; return g!=null&&g.InRaid&&g.TryCast<HideoutGame>()==null; } }
    static bool cursorOwned,oldCursor;
    static CursorLockMode oldLock;
    static int releaseFrame=-1,openedFrame;
    static float nextScan,nextWarning,lastTime;
    static bool puzzleHandled;
    internal static readonly UploadQueue<ActorRef> Uploads=new();
    internal static ActorRef? UploadTarget=>Uploads.Current(Target)?.Target;
    internal static HackKind UploadKind=>Uploads.Current(Target)?.Kind??Selected;
    static readonly ScannerGesture scanGesture=new();
    static readonly KeyCode[] chordKeys=Enum.GetValues<KeyCode>().Where(k=>(int)k>=8&&(int)k<=329).Distinct().ToArray();
    static bool ControlScanner=>Plugin.ScanKey.Value is KeyCode.LeftControl or KeyCode.RightControl;
    static bool ScanConflict(bool scanning) {
        foreach(var key in chordKeys) {
            if(key==Plugin.ScanKey.Value||scanning&&(key==KeyCode.F||key==KeyCode.Escape||key==KeyCode.Mouse0)) continue;
            if(Input.GetKey(key)) return true;
        }
        return !scanning&&Math.Abs(Input.mouseScrollDelta.y)>.001f;
    }
    internal static void CheckScanChord() {
        if(!Active||!ControlScanner||Mode!=UiMode.Scanner||!ScanConflict(true)) return;
        scanGesture.Cancel(); SetMode(UiMode.None); releaseFrame=-1;
    }
    static IntPtr localWorld;
    internal static bool Uploading=>Uploads.Count>0;
    internal static float UploadElapsed=>Uploads.Current(Target)?.Elapsed(Time.unscaledTime)??0;
    internal static float UploadProgress=>Uploads.Current(Target)?.Progress(Time.unscaledTime)??0;
    internal static string UploadName=>HackRules.Name(UploadKind);
    internal static int QueuePosition(HackKind kind)=>Array.FindIndex(Uploads.For(Target),e=>e.Kind==kind);
    static void Refund(float paid)=>Ram=Math.Min(24,Ram+paid);
    static void CancelUploads()=>Refund(Uploads.Clear());
    internal static int Frames { get; private set; }

    internal static void Start() { Assets.Initialize(); OriginalFonts.Initialize(); lastTime=Time.unscaledTime; Active=true; }
    internal static void SetMode(UiMode mode) {
        if(mode==UiMode.None) {
            if(Mode==UiMode.None&&!cursorOwned) return;
            Mode=mode; releaseFrame=Time.frameCount+2;
            if(cursorOwned) { Cursor.lockState=oldLock; Cursor.visible=oldCursor; cursorOwned=false; }
            return;
        }
        if(!cursorOwned) { oldLock=Cursor.lockState; oldCursor=Cursor.visible; cursorOwned=true; }
        Mode=mode; openedFrame=Time.frameCount; ModeStarted=Time.unscaledTime;
        Singleton<GameWorld>.Instance?.MainPlayer?.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);
        KeepCursor();
    }
    internal static void KeepCursor() { if(cursorOwned) { Cursor.lockState=CursorLockMode.None; Cursor.visible=true; } }
    internal static void Tick() {
        if(!Active) return;
        try {
            Frames++;
            float now=Time.unscaledTime,delta=Math.Clamp(now-lastTime,0,.1f); lastTime=now;
            if(!Plugin.Enabled.Value||!Application.isFocused) { Close(); CancelUploads(); scanGesture.Cancel(); AiHacks.Reset(); return; }
            var current=Singleton<GameWorld>.Instance;
            if(!InRaid||current?.MainPlayer?.HealthController?.IsAlive!=true||current.MainPlayer.IsYourPlayer!=true) {
                CancelUploads(); Target=null;
                if(Mode==UiMode.Scanner||Mode==UiMode.Breach&&!PreviewBreach) Close();
                AiHacks.Reset(); localWorld=IntPtr.Zero;
            } else {
                if(localWorld!=IntPtr.Zero&&localWorld!=current.Pointer) { Close(); CancelUploads(); Target=null; AiHacks.Reset(); DoorHacking.Reset(); }
                localWorld=current.Pointer;
                AiHacks.Tick();
            }
            Ram=Plugin.InfiniteRam.Value?24:Math.Min(24,Ram+delta);
            if(Input.GetKeyDown(Plugin.PreviewKey.Value)) { if(Mode==UiMode.None) { Assets.Play("open"); SetMode(UiMode.Preview); } else Close(); }
            if(Mode!=UiMode.None&&Time.frameCount>openedFrame&&Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            CheckScanChord();
            bool scanReady=scanGesture.Step(now,Input.GetKey(Plugin.ScanKey.Value),!InRaid||Mode is UiMode.Preview or UiMode.Breach||Mode==UiMode.None&&Cursor.visible||ControlScanner&&ScanConflict(Mode==UiMode.Scanner),ControlScanner?.25:0);
            if(Mode==UiMode.None&&InRaid&&!Cursor.visible&&scanReady) { Assets.Play("scan"); SetMode(UiMode.Scanner); nextScan=0; }
            if(Mode==UiMode.Scanner) {
                if(!Input.GetKey(Plugin.ScanKey.Value)) { SetMode(UiMode.None); }
                else {
                    float wheel=Input.mouseScrollDelta.y;
                    if(Math.Abs(wheel)>.001f) { Selected=HackRules.Cycle(Selected,wheel>0?-1:1); Assets.Play("hover"); }
                    if(now>=nextScan) { nextScan=now+.12f; Target=Scan(); }
                    if(Time.frameCount>openedFrame+1&&Input.GetKeyDown(KeyCode.F)) Upload();
                }
            }
            foreach(var reference in Uploads.Targets) {
                var target=reference.Resolve();
                if(target==null||!Visible(target)) {
                    Refund(Uploads.Cancel(reference)); Status="目标上传已中断，未生效的 RAM 已返还。"; Assets.Play("denied"); continue;
                }
                var entry=Uploads.Complete(reference,now);
                if(entry==null) continue;
                try { Status=AiHacks.Apply(reference,entry.Kind,24-Ram); Assets.Play("granted"); }
                catch(Exception e) { Refund(entry.Paid); Status=e.Message+"（RAM 已返还）"; Assets.Play("denied"); Warn("Apply quickhack",e); }
                // Lethal programs cancel only this target's remaining queue.
                if(reference.Resolve()==null) Refund(Uploads.Cancel(reference));
            }
            if(Mode==UiMode.Breach&&Puzzle!=null) {
                if(!puzzleHandled&&!PreviewBreach&&Door?.Valid()!=true) { Status="门禁已失效，破解取消。"; Close(); return; }
                Puzzle.Tick(now);
                if(Puzzle.Ended&&!puzzleHandled) {
                    puzzleHandled=true;
                    if(Puzzle.Won) { if(!PreviewBreach) Door!.Authorize(true); Status=PreviewBreach?"预览：门禁授权魔偶上传完成。":"门禁授权已提交。"; }
                    else { Status="破解失败：缓冲区已满或时间耗尽。"; }
                }
            }
            PreviewProgress=Math.Clamp((now-PreviewStarted)/HackRules.Upload(PreviewKind),0,1);
            KeepCursor();
        } catch(Exception e) { Status=e.Message; Warn("Runtime",e); Close(); CancelUploads(); }
    }
    internal static void Upload() {
        var target=Target?.Resolve();
        if(target==null||!Visible(target)) { Status="请锁定视线内的存活 AI。"; return; }
        if(!CanAfford(Selected)) { Status="RAM 不足。"; Assets.Play("denied"); return; }
        float paid=Plugin.InfiniteRam.Value?0:HackRules.Cost(Selected);
        if(!Uploads.TryAdd(Target!,Selected,paid,Time.unscaledTime)) { Status="该破解已在队列中，或队列已满（每目标最多 4 个）。"; Assets.Play("fail"); return; }
        Ram-=paid; Assets.Play("press");
        Status=HackRules.Name(Selected)+(Uploads.For(Target).Length>1?" · 等待上传":" · 上传中");
    }
    internal static bool Visible(Player actor) {
        var view=Camera; if(view==null) return false;
        var point=actor.PlayerBones?.Ribcage?.Original?.position??actor.Position+Vector3.up;
        var delta=point-view.transform.position; float distance=delta.magnitude;
        int mask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider");
        return mask!=0&&distance<=Plugin.Range.Value&&!Physics.Raycast(view.transform.position,delta.normalized,Math.Max(0,distance-.2f),mask,QueryTriggerInteraction.Ignore);
    }
    internal static bool CanAfford(HackKind kind)=>Plugin.InfiniteRam.Value||Ram>=HackRules.Cost(kind);
    static ActorRef? Scan() {
        var view=Camera; var list=Singleton<GameWorld>.Instance?.AllAlivePlayersList;
        if(view==null||list==null) return null;
        Player? best=null; float score=.97f;
        for(int i=0;i<list.Count;i++) {
            var actor=list[i];
            if(actor==null||actor.IsYourPlayer||!actor.IsAI||actor.AIData?.BotOwner==null||actor.HealthController?.IsAlive!=true) continue;
            var delta=(actor.PlayerBones?.Ribcage?.Original?.position??actor.Position+Vector3.up)-view.transform.position;
            float dot=Vector3.Dot(delta.normalized,view.transform.forward);
            if(dot>score&&Visible(actor)) { score=dot; best=actor; }
        }
        return best==null?null:ActorRef.Of(best);
    }
    internal static void OpenDoor(DoorLease door) {
        if(!Active||Mode!=UiMode.None||!Plugin.Enabled.Value||!door.Valid()) return;
        Door=door; PreviewBreach=false; BeginPuzzle(2); Assets.Play("open");
    }
    internal static void OpenPreviewBreach() { Door=null; PreviewBreach=true; BeginPuzzle(1); Assets.Play("open"); }
    static void BeginPuzzle(int difficulty) { Puzzle=new BreachPuzzle(System.Random.Shared.Next(),difficulty); puzzleHandled=false; SetMode(UiMode.Breach); }
    internal static void ExitBreach() {
        if(PreviewBreach) { Assets.Play("close"); Puzzle=null; PreviewBreach=false; SetMode(UiMode.Preview); }
        else Close();
    }
    internal static void Pick(int r,int c) { if(Puzzle?.Pick(r,c,Time.unscaledTime)==true) Assets.Play("press"); else Assets.Play("fail"); }
    internal static void PreviewCast() { PreviewKind=Selected; PreviewStarted=Time.unscaledTime; Assets.Play(HackRules.Icon(Selected)); Effects.Preview(Selected); Status="无伤预览 · "+HackRules.Name(Selected); }
    internal static void Close() { scanGesture.Cancel(); if(Mode!=UiMode.None) Assets.Play("close"); SetMode(UiMode.None); Door=null; Puzzle=null; PreviewBreach=false; }
    internal static void Warn(string context,Exception e) { if(Time.unscaledTime<nextWarning) return; nextWarning=Time.unscaledTime+5; Plugin.Logger.LogWarning(context+": "+e); }
    internal static void Stop() {
        // Old native action lists can briefly retain callback delegates. Disable
        // this generation first so those callbacks cannot reopen its UI.
        Active=false; CancelUploads(); scanGesture.Reset();
        foreach(Action step in new Action[]{Close,AiHacks.Reset,Effects.Clear,DoorHacking.Reset,Assets.Clear})
            try {step();} catch(Exception e) {Plugin.Logger?.LogWarning("Quickhack cleanup: "+e);}
    }
}
