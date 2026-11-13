using UnityEngine;

namespace LocalQuickhacks;

internal static partial class HackUi {
    static bool HeadPosition(EFT.Player actor,out Vector2 point,out float size) {
        point=default; size=1;
        var camera=Runtime.Camera;
        // Status badges follow infected actors even after they leave scanner range.
        if(camera==null) return false;
        var position=actor.PlayerBones?.Head?.Original?.position??actor.Position+Vector3.up*1.6f;
        int mask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider");
        var delta=position-camera.transform.position;
        if(mask==0||Physics.Raycast(camera.transform.position,delta.normalized,Math.Max(0,delta.magnitude-.15f),mask,QueryTriggerInteraction.Ignore)) return false;
        var screen=camera.WorldToScreenPoint(position+Vector3.up*.55f);
        if(screen.z<=0) return false;
        point=new((screen.x-ox)/scale,(Screen.height-screen.y-oy)/scale);
        size=Math.Clamp(30/Math.Max(1,delta.magnitude),.85f,1.12f);
        return point.x>32&&point.x<1888&&point.y>55&&point.y<1010;
    }
    static void HeadBadge(Vector2 center,float size,HackKind kind,float progress,float remaining,bool uploading,int slot=0,float age=0,float completeAge=-1,bool settled=false) {
        // Original widget: background/frame 80x88, glyph 64x64 at 2160p.
        // Keep those proportions and use a small distance-aware HUD scale.
        var intro=settled?(Scale:1f,Y:0f,Alpha:1f):UploadAnimation.Intro(age);
        var complete=completeAge>=0&&completeAge<UploadAnimation.CompleteDuration
            ?UploadAnimation.Complete(completeAge):(Y:0f,Alpha:1f);
        bool finishing=completeAge>=0&&completeAge<UploadAnimation.CompleteDuration;
        bool uploadStyle=uploading||finishing;
        Color c=uploadStyle||kind==HackKind.BlackwallGateway?ScanRed:ScanCyan;
        float alpha=intro.Alpha*complete.Alpha;
        float w=40*size,h=44*size,x=center.x+slot*46*size-w/2,y=center.y-36*size+complete.Y*size;
        var plate=new Rect(x,y,w,h);
        float glyphSize=32*size*intro.Scale;
        var glyph=new Rect(x+w/2-glyphSize/2,y+h/2-glyphSize/2-3.5f*size+intro.Y*size,glyphSize,glyphSize);
        Image(plate,"qh_frame_qh_bg",Alpha(c,alpha*.1f),true);
        Image(glyph,HackRules.Icon(kind),Alpha(c,alpha*.6f));
        Image(plate,"qh_frame_qh_fg",Alpha(c,alpha*.7f),true);
        // m_bar=mask_fill, anchor/pivot at the top: UPLOAD (2,progress),
        // DURATION (2,1-progress). Clip all active layers, including glyph.
        float fill=Math.Clamp(progress,0,1);
        if(fill>0) {
            var clip=new Rect(x,y,w,h*fill);
            GUI.BeginGroup(clip);
            try {
                Image(new Rect(0,0,w,h),"qh_frame_qh_bg",Alpha(c,alpha*(uploadStyle?.6f:.4f)),true);
                Image(new Rect(glyph.x-x,glyph.y-y,glyph.width,glyph.height),HackRules.Icon(kind),new Color(0,0,0,alpha));
                Image(new Rect(0,0,w,h),"qh_frame_qh_fg",Alpha(c,alpha),true);
            } finally { GUI.EndGroup(); }
        }
    }
    static void TargetStatuses() {
        if(Runtime.Mode is UiMode.Preview or UiMode.Breach) return;
        Effects.PruneMarkers();
        var infected=AiHacks.GatewayUploads.ToArray();
        var references=Runtime.Uploads.Targets.Concat(infected.Select(c=>c.Ref)).Concat(Effects.Markers.Select(m=>m.Ref)).Distinct().ToArray();
        foreach(var reference in references) {
            var actor=reference.Resolve();
            if(actor==null||!HeadPosition(actor,out var point,out var size)) continue;
            var queue=Runtime.Uploads.For(reference);
            var chain=infected.FirstOrDefault(c=>c.Ref==reference);
            bool chainVisible=chain.Ref!=null&&(chain.Completed<0||Now-chain.Completed<UploadAnimation.CompleteDuration);
            int slot=0;
            Vector2 Position(int index)=>index==0?point:point+new Vector2(35*size+(index-1)%8*28*size,(index-1)/8*32*size);
            float Size(int index)=>index==0?size:size*.6f;
            if(queue.Length>0) {
                var current=queue[0];
                bool promoted=float.IsFinite(current.PromotedAt);
                float move=promoted?UploadAnimation.QueueMove(Now-current.PromotedAt):1;
                var mainPosition=point+new Vector2((1-move)*35*size,0);
                HeadBadge(mainPosition,size*(.6f+.4f*move),current.Kind,current.Progress(Now),0,true,age:current.Elapsed(Now),completeAge:current.Completed<0?-1:Now-current.Completed,settled:promoted); slot++;
                float wait=current.Completed<0?Math.Max(0,current.Started-Now)+Math.Max(0,HackRules.Upload(current.Kind)-current.Elapsed(Now))+UploadQueue<ActorRef>.HandoffDuration:Math.Max(0,UploadAnimation.CompleteDuration-(Now-current.Completed))+UploadAnimation.QueueMoveDuration;
                foreach(var pending in queue.Skip(1)) {
                    var position=Position(slot++);
                    if(promoted) position.x+=(1-move)*28*size;
                    QueuedBadge(position,size*.6f,pending.Kind,wait);
                    wait+=HackRules.Upload(pending.Kind)+UploadQueue<ActorRef>.HandoffDuration;
                }
            }
            if(chainVisible) {
                float completeAge=chain.Completed<0?-1:Now-chain.Completed;
                float progress=completeAge<0?Math.Clamp((Now-chain.Started)/Math.Max(.01f,chain.At-chain.Started),0,1):1;
                HeadBadge(Position(slot),Size(slot),HackKind.BlackwallGateway,progress,0,true,age:Now-chain.Started,completeAge:completeAge); slot++;
            }
            var states=Effects.Markers.Where(m=>m.Ref==reference&&!(chainVisible&&m.Kind==HackKind.BlackwallGateway)&&!queue.Any(e=>e.Kind==m.Kind&&e.Completed>=0)).OrderByDescending(m=>m.Start);
            int activeSlot=0;
            foreach(var state in states) {
                float activeAge=Now-state.Start;
                // Active effects have their own cyan row below the upload queue.
                // Their completion animation belongs solely to the main upload badge.
                var activePosition=point+new Vector2(activeSlot++*27*size,38*size);
                HeadBadge(activePosition,size*.6f,state.Kind,Math.Clamp((state.Until-Now)/HackRules.Duration(state.Kind),0,1),state.Until-Now,false,settled:true);
            }
        }
    }
    static void QueuedBadge(Vector2 center,float size,HackKind kind,float wait) {
        // Static small frames stay to the right; promotion uses the main intro track.
        var plate=new Rect(center.x-20*size,center.y-36*size,40*size,44*size);
        Image(plate,"qh_frame_qh_bg",Alpha(ScanRed,.15f),true);
        Image(new Rect(plate.x+4*size,plate.y+3*size,32*size,32*size),HackRules.Icon(kind),Alpha(ScanRed,.65f));
        Image(plate,"qh_frame_qh_fg",Alpha(ScanRed,.65f),true);
        Text(new Rect(plate.x-5,plate.yMax+1,plate.width+10,14),MathF.Ceiling(wait).ToString("0"),Alpha(ScanRed,.8f),10,2,TextAnchor.UpperCenter);
    }
}
