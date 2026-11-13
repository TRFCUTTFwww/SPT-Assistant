using UnityEngine;

namespace LocalQuickhacks;

internal static partial class HackUi {
    static void Ring(Vector2 center,float radius,float progress,Color color,float phase=0) {
        const int steps=36;
        for(int i=0;i<steps;i++) {
            float a=(i/(float)steps*360-90+phase)*Mathf.Deg2Rad;
            float b=((i+.65f)/steps*360-90+phase)*Mathf.Deg2Rad;
            Line(center.x+Mathf.Cos(a)*radius,center.y+Mathf.Sin(a)*radius,
                center.x+Mathf.Cos(b)*radius,center.y+Mathf.Sin(b)*radius,
                Alpha(color,i<steps*progress?1:.16f),2);
        }
    }
    static bool HeadPosition(EFT.Player actor,out Vector2 point,out float size) {
        point=default; size=1;
        var camera=Runtime.Camera;
        if(camera==null||!Runtime.Visible(actor)) return false;
        var position=actor.PlayerBones?.Head?.Original?.position??actor.Position+Vector3.up*1.6f;
        int mask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider");
        var delta=position-camera.transform.position;
        if(mask==0||Physics.Raycast(camera.transform.position,delta.normalized,Math.Max(0,delta.magnitude-.15f),mask,QueryTriggerInteraction.Ignore)) return false;
        var screen=camera.WorldToScreenPoint(position+Vector3.up*.37f);
        if(screen.z<=0) return false;
        point=new((screen.x-ox)/scale,(Screen.height-screen.y-oy)/scale);
        size=Math.Clamp(14/Math.Max(1,delta.magnitude),.65f,1.15f);
        return point.x>32&&point.x<1888&&point.y>55&&point.y<1010;
    }
    static void HeadBadge(Vector2 center,float size,HackKind kind,float progress,float remaining,bool uploading,int slot=0) {
        float radius=24*size;
        center.x+=slot*57*size;
        var plate=new Rect(center.x-radius-4,center.y-radius-4,radius*2+8,radius*2+8);
        Color c=kind is HackKind.BlackwallGateway or HackKind.Cyberpsychosis?Red:Yellow;
        Fill(plate,new Color(.025f,.035f,.035f,.7f));
        Ring(center,radius,progress,c,uploading?Now*75:0);
        float blink=uploading?.55f+.45f*Mathf.Abs(Mathf.Sin(Now*8)):1;
        Image(new Rect(center.x-15*size,center.y-15*size,30*size,30*size),HackRules.Icon(kind),Alpha(c,blink));
        if(uploading) {
            float left=center.x-62*size,y=center.y+radius+8;
            Fill(new Rect(left,y,124*size,3),Alpha(c,.18f)); Fill(new Rect(left,y,124*size*progress,3),c);
            Text(new Rect(center.x-130,y+8,260,25),$"{HackRules.Name(kind)}  {(int)(progress*100):00}%",c,15,0,TextAnchor.MiddleCenter);
            // Packets step toward the icon while the upload is pending.
            for(int i=0;i<3;i++) {
                float t=(Now*1.4f+i/3f)%1;
                Fill(new Rect(center.x-2,center.y+radius+38-t*32,4,4),Alpha(c,1-t));
            }
        } else {
            Text(new Rect(center.x-29,center.y+radius+3,58,21),$"{remaining:0.0}",c,14,1,TextAnchor.MiddleCenter);
            if(kind==HackKind.Cyberpsychosis) Ring(center,radius+6,.28f,Alpha(Red,.75f),Now*140);
            if(kind==HackKind.RebootOptics) {
                float y=center.y-12*size+(Now*23%24)*size;
                Fill(new Rect(center.x-14*size,y,28*size,2),Alpha(Cyan,.8f));
            }
            if(kind==HackKind.BlackwallGateway) for(int i=0;i<4;i++) {
                float jitter=Mathf.Sin(Mathf.Floor(Now*14+i)*9)*radius;
                Fill(new Rect(center.x+jitter,center.y-radius+i*radius*.6f,12*size,2),Red);
            }
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
            foreach(var state in states) HeadBadge(point,size,state.Kind,Math.Clamp((state.Until-Now)/HackRules.Duration(state.Kind),0,1),state.Until-Now,false,slot++);
        }
        if(upload!=null&&HeadPosition(upload,out var head,out var uploadSize)) HeadBadge(head,uploadSize,Runtime.UploadKind,Runtime.UploadProgress,0,true);
    }
}
