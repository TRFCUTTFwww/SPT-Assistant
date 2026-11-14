using System.Numerics;

namespace LocalPortalGun;

internal static class PortalRules {
    internal const float HalfWidth=.58f, HalfHeight=1.12f, Contact=.48f;
    // Cross the player's collision shell, not the wall itself. Walking parallel or
    // away from a portal must not trigger, including after a menu closes.
    internal static bool Enters(Vector3 previous,Vector3 current) {
        if(!Finite(previous)||!Finite(current)||(current-previous).LengthSquared()>9) return false;
        if(previous.Z<Contact || current.Z>Contact || current.Z<-.3f || current.Z>=previous.Z-.0001f) return false;
        float t=(previous.Z-Contact)/(previous.Z-current.Z);
        var point=Vector3.Lerp(previous,current,t);
        return point.X*point.X/(.36f*.36f)+point.Y*point.Y/(.87f*.87f)<=1;
    }
    internal static Vector3 Through(Vector3 local)=>new(-local.X,local.Y,-local.Z);
    internal static bool EntersFloor(Vector3 previous,Vector3 current) {
        if(!Finite(previous)||!Finite(current)||(current-previous).LengthSquared()>9)return false;
        static float Oval(Vector3 p)=>p.X*p.X/(.36f*.36f)+p.Y*p.Y/(.87f*.87f);
        return current.Z>=-.15f&&current.Z<=Contact&&Oval(current)<=1&&(previous.Z>Contact||Oval(previous)>1);
    }
    internal static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
