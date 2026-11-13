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
    static void HeadBadge(Vector2 center,float size,HackKind kind,float progress,float remaining,bool uploading,int slot=0,float age=0) {
        // User recording: 7.52-7.93s and 20.70-20.90s. The red glyph
        // starts above the cut-corner plate, then settles into it in ~0.4s.
        // The lower red tracking banner is a separate CP2077 mechanic.
        Color c=ScanRed;
        float appear=Math.Clamp(age/.12f,0,1),settle=Math.Clamp((age-.06f)/.34f,0,1);
        settle=settle*settle*(3-2*settle);
        float edge=48*size,x=center.x+slot*53*size-edge/2,y=center.y-36*size;
        var plate=new Rect(x,y,edge,edge);
        Image(plate,"qh_frame_qh_bg",Alpha(c,appear*(uploading?.12f+.25f*Math.Clamp(progress,0,1):.28f)),true);
        Image(plate,"qh_frame_qh_fg",Alpha(c,appear),true);
        Image(new Rect(x+4*size,y+4*size-(1-settle)*55*size,40*size,40*size),HackRules.Icon(kind),Alpha(c,appear));
        Image(new Rect(x-2*size,y-2*size,15*size,12*size),"qh_selection_fluff",Alpha(c,appear*.7f));
        if(!uploading) {
            float flash=Math.Clamp(1-age/.18f,0,1);
            Image(plate,"qh_frame_qh_bg",Alpha(c,flash*.45f),true);
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
            foreach(var state in states) HeadBadge(point,size,state.Kind,Math.Clamp((state.Until-Now)/HackRules.Duration(state.Kind),0,1),state.Until-Now,false,slot++,Now-state.Start+HackRules.Upload(state.Kind));
        }
        if(upload!=null&&HeadPosition(upload,out var head,out var uploadSize)) HeadBadge(head,uploadSize,Runtime.UploadKind,Runtime.UploadProgress,0,true,age:Runtime.UploadElapsed);
    }
}
