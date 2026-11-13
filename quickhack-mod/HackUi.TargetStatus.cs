using UnityEngine;

namespace LocalQuickhacks;

internal static partial class HackUi {
    static bool HeadPosition(EFT.Player actor,out Vector2 point,out float size) {
        point=default; size=1;
        var camera=Runtime.Camera;
        if(camera==null||!Runtime.Visible(actor)) return false;
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
    static void HeadBadge(Vector2 center,float size,HackKind kind,float progress,float remaining,bool uploading,int slot=0,float age=0,float completeAge=-1) {
        // Original widget: background/frame 80x88, glyph 64x64 at 2160p.
        // Keep those proportions and use a small distance-aware HUD scale.
        var intro=UploadAnimation.Intro(age);
        var complete=completeAge>=0&&completeAge<UploadAnimation.CompleteDuration
            ?UploadAnimation.Complete(completeAge):(Y:0f,Alpha:1f);
        bool finishing=completeAge>=0&&completeAge<UploadAnimation.CompleteDuration;
        bool uploadStyle=uploading||finishing;
        Color c=uploadStyle?ScanRed:ScanCyan;
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
        var upload=Runtime.UploadTarget?.Resolve();
        foreach(var group in Effects.Markers.GroupBy(m=>m.Ref)) {
            var actor=group.Key.Resolve();
            if(actor==null||!HeadPosition(actor,out var point,out var size)) continue;
            var states=group.OrderByDescending(m=>m.Start).ToArray();
            int slot=upload?.Pointer==actor.Pointer?1:0;
            foreach(var state in states) {
                float activeAge=Now-state.Start;
                HeadBadge(point,size,state.Kind,activeAge<UploadAnimation.CompleteDuration?1:Math.Clamp((state.Until-Now)/HackRules.Duration(state.Kind),0,1),state.Until-Now,false,slot++,activeAge+HackRules.Upload(state.Kind),activeAge);
            }
        }
        if(upload!=null&&HeadPosition(upload,out var head,out var uploadSize)) HeadBadge(head,uploadSize,Runtime.UploadKind,Runtime.UploadProgress,0,true,age:Runtime.UploadElapsed);
    }
}
