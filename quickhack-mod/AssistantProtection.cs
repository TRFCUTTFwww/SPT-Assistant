using System.Reflection;
using EFT;
using EFT.HealthSystem;
using UnityEngine;

namespace LocalQuickhacks;
internal static class AssistantProtection {
    static FieldInfo? livePlugin;
    static Func<ActiveHealthController,bool>? blocked;
    static float nextLookup;
    internal static bool Protected(Player actor) {
        try {
            if(blocked!=null&&livePlugin?.GetValue(null)!=null)return blocked(actor.ActiveHealthController);
            blocked=null; livePlugin=null;
            if(Time.unscaledTime<nextLookup)return false;
            nextLookup=Time.unscaledTime+1;
            // Unloaded hot-reload generations remain in AppDomain. Only bind
            // the generation whose Plugin.Self is still alive.
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Reverse()) {
                var type=assembly.GetType("LocalSpt5Esp.Plugin"); if(type==null)continue;
                var field=type.GetField("Self",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
                if(field?.GetValue(null)==null)continue;
                var method=type.GetMethod("DamageBlocked",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
                if(method==null)continue;
                blocked=method.CreateDelegate<Func<ActiveHealthController,bool>>();livePlugin=field;
                return blocked(actor.ActiveHealthController);
            }
            return false;
        } catch(Exception e) { Runtime.Warn("Assistant protection",e); return true; }
    }
}
