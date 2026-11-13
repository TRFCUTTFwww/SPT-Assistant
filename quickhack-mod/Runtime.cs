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
    internal static UiMode Mode;
    internal static bool BlockInput=>Mode!=UiMode.None||Time.frameCount<=releaseFrame;
    internal static bool PreviewBreach;
    internal static BreachPuzzle? Puzzle;
    internal static DoorLease? Door;
    internal static ActorRef? Target;
    internal static HackKind Selected;
    internal static float Ram=24, PreviewStarted=-100, PreviewProgress;
    internal static string Status="按 F10 预览界面，按住 F9 扫描 AI。";
    internal static Camera? Camera=>CameraManager.Instance?.Camera??UnityEngine.Camera.main;
    internal static bool InRaid { get { var g=Singleton<AbstractGame>.Instance; return g!=null&&g.InRaid&&g.TryCast<HideoutGame>()==null; } }
    static bool cursorOwned,oldCursor;
    static CursorLockMode oldLock;
    static int releaseFrame=-1,openedFrame;
    static float nextScan,nextWarning,lastTime;
    static bool puzzleHandled;
    static float? finishedAt;
    static ActorRef? uploading;
    static HackKind uploadKind;
    static float uploadStart;
    static IntPtr localWorld;
    internal static bool Uploading=>uploading!=null;
    internal static float UploadProgress=>uploading==null?0:Math.Clamp((Time.unscaledTime-uploadStart)/HackRules.Upload(uploadKind),0,1);
    internal static string UploadName=>HackRules.Name(uploadKind);
    internal static int Frames { get; private set; }

    internal static void Start() { Assets.Initialize(); lastTime=Time.unscaledTime; }
    internal static void SetMode(UiMode mode) {
        if(mode==UiMode.None) {
            if(Mode==UiMode.None&&!cursorOwned) return;
            Mode=mode; releaseFrame=Time.frameCount+2;
            if(cursorOwned) { Cursor.lockState=oldLock; Cursor.visible=oldCursor; cursorOwned=false; }
            return;
        }
        if(!cursorOwned) { oldLock=Cursor.lockState; oldCursor=Cursor.visible; cursorOwned=true; }
        Mode=mode; openedFrame=Time.frameCount;
        Singleton<GameWorld>.Instance?.MainPlayer?.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);
        KeepCursor();
    }
    internal static void KeepCursor() { if(cursorOwned) { Cursor.lockState=CursorLockMode.None; Cursor.visible=true; } }
    internal static void Tick() {
        try {
            Frames++;
            float now=Time.unscaledTime,delta=Math.Clamp(now-lastTime,0,.1f); lastTime=now;
            if(!Plugin.Enabled.Value||!Application.isFocused) { Close(); uploading=null; AiHacks.Reset(); return; }
            var current=Singleton<GameWorld>.Instance;
            if(!InRaid||current?.MainPlayer?.HealthController?.IsAlive!=true||current.MainPlayer.IsYourPlayer!=true) {
                uploading=null; Target=null;
                if(Mode==UiMode.Scanner||Mode==UiMode.Breach&&!PreviewBreach) Close();
                AiHacks.Reset(); localWorld=IntPtr.Zero;
            } else {
                if(localWorld!=IntPtr.Zero&&localWorld!=current.Pointer) { Close(); uploading=null; Target=null; AiHacks.Reset(); DoorHacking.Reset(); }
                localWorld=current.Pointer;
                AiHacks.Tick();
            }
            Ram=Math.Min(24,Ram+delta);
            if(Input.GetKeyDown(Plugin.PreviewKey.Value)) { if(Mode==UiMode.None) { Assets.Play("open"); SetMode(UiMode.Preview); } else Close(); }
            if(Mode!=UiMode.None&&Time.frameCount>openedFrame&&Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if(Mode==UiMode.None&&InRaid&&!Cursor.visible&&Input.GetKeyDown(Plugin.ScanKey.Value)) { Assets.Play("scan"); SetMode(UiMode.Scanner); nextScan=0; }
            if(Mode==UiMode.Scanner) {
                if(!Input.GetKey(Plugin.ScanKey.Value)) { SetMode(UiMode.None); }
                else {
                    float wheel=Input.mouseScrollDelta.y;
                    if(Math.Abs(wheel)>.001f) { Selected=(HackKind)(((int)Selected+(wheel>0?2:1))%3); Assets.Play("hover"); }
                    if(now>=nextScan) { nextScan=now+.12f; Target=Scan(); }
                    if(Time.frameCount>openedFrame+1&&Input.GetKeyDown(KeyCode.F)) Upload();
                }
            }
            if(uploading!=null) {
                var target=uploading.Resolve();
                if(target==null||!Visible(target)) { Status="上传已中断：目标离开范围或视线。"; uploading=null; Assets.Play("denied"); }
                else if(UploadProgress>=1) { var actor=uploading; uploading=null; AiHacks.Apply(actor,uploadKind); Status=HackRules.Name(uploadKind)+" · 已上传"; Assets.Play("granted"); }
            }
            if(Mode==UiMode.Breach&&Puzzle!=null) {
                if(!PreviewBreach&&Door?.Valid()!=true) { Status="门禁已失效，破解取消。"; Close(); return; }
                Puzzle.Tick(now);
                if(Puzzle.Ended&&!puzzleHandled) {
                    puzzleHandled=true;
                    if(Puzzle.Won) { if(!PreviewBreach) Door!.Authorize(true); Status=PreviewBreach?"预览：门禁授权魔偶上传完成。":"门禁授权已提交。"; Assets.Play("granted"); }
                    else { Status="破解失败：缓冲区已满或时间耗尽。"; Assets.Play("denied"); }
                    finishedAt=now;
                }
                if(finishedAt is {} end&&now-end>2.5f) { if(PreviewBreach) { Puzzle=null; SetMode(UiMode.Preview); } else Close(); }
            }
            PreviewProgress=Math.Clamp((now-PreviewStarted)/HackRules.Upload(Selected),0,1);
            KeepCursor();
        } catch(Exception e) { Status=e.Message; Warn("Runtime",e); Close(); uploading=null; }
    }
    internal static void Upload() {
        if(uploading!=null) { Status="已有快速破解正在上传。"; return; }
        var target=Target?.Resolve();
        if(target==null||!Visible(target)) { Status="请锁定视线内的存活 AI。"; return; }
        if(Ram<HackRules.Cost(Selected)) { Status="RAM 不足。"; Assets.Play("denied"); return; }
        Ram-=HackRules.Cost(Selected); uploading=Target; uploadKind=Selected; uploadStart=Time.unscaledTime; Assets.Play("press");
        Status=HackRules.Name(Selected)+" · 上传中";
    }
    internal static bool Visible(Player actor) {
        var view=Camera; if(view==null) return false;
        var point=actor.PlayerBones?.Ribcage?.Original?.position??actor.Position+Vector3.up;
        var delta=point-view.transform.position; float distance=delta.magnitude;
        int mask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider");
        return mask!=0&&distance<=Plugin.Range.Value&&!Physics.Raycast(view.transform.position,delta.normalized,Math.Max(0,distance-.2f),mask,QueryTriggerInteraction.Ignore);
    }
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
        if(Mode!=UiMode.None||!Plugin.Enabled.Value||!door.Valid()) return;
        Door=door; PreviewBreach=false; BeginPuzzle(2); Assets.Play("open");
    }
    internal static void OpenPreviewBreach() { Door=null; PreviewBreach=true; BeginPuzzle(1); Assets.Play("open"); }
    static void BeginPuzzle(int difficulty) { Puzzle=new BreachPuzzle(System.Random.Shared.Next(),difficulty); puzzleHandled=false; finishedAt=null; SetMode(UiMode.Breach); }
    internal static void Pick(int r,int c) { if(Puzzle?.Pick(r,c,Time.unscaledTime)==true) Assets.Play("press"); else Assets.Play("fail"); }
    internal static void PreviewCast() { PreviewStarted=Time.unscaledTime; Assets.Play(HackRules.Icon(Selected)); Effects.Preview(Selected); Status="无伤预览 · "+HackRules.Name(Selected); }
    internal static void Close() { if(Mode!=UiMode.None) Assets.Play("close"); SetMode(UiMode.None); Door=null; Puzzle=null; finishedAt=null; PreviewBreach=false; }
    internal static void Warn(string context,Exception e) { if(Time.unscaledTime<nextWarning) return; nextWarning=Time.unscaledTime+5; Plugin.Logger.LogWarning(context+": "+e); }
    internal static void Stop() { Close(); uploading=null; AiHacks.Reset(); Effects.Clear(); DoorHacking.Reset(); Assets.Clear(); }
}
