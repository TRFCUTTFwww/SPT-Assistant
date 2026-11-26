using Comfort.Common;
using EFT;
namespace LocalSpt5Esp;
// Optional modules register callbacks; the assistant knows no feature assembly names.
public static class OptionalCombatIntegration {
 sealed record Hooks(Func<BotOwner,IPlayer,bool>? Hostile,Action<bool>? Input);
 static readonly Dictionary<string,Hooks> hooks=new();
 public static bool Active {get;private set;}
 internal static void Initialize()=>Active=true;
 public static Func<long,bool> Register(string id,Func<BotOwner,IPlayer,bool>? hostile,Action<bool>? input){hooks[id]=new(hostile,input);return Protected;}
 public static void Unregister(string id)=>hooks.Remove(id);
 static bool Protected(long pointer){var all=Singleton<GameWorld>.Instance?.AllAlivePlayersList;if(all==null)return true;for(int i=0;i<all.Count;i++){var actor=all[i];if(actor!=null&&actor.Pointer.ToInt64()==pointer)return actor.ActiveHealthController==null||Plugin.DamageBlocked(actor.ActiveHealthController);}return true;}
 internal static bool IsHostile(BotOwner owner,IPlayer target){foreach(var hook in hooks.Values)try{if(hook.Hostile?.Invoke(owner,target)==true)return true;}catch{}return false;}
 internal static void Tick(){bool blocked=InterfaceMenu.InputBlocked||WorldEditor.Active||DirectorStage.FreeActive||!RaidClock.Active;foreach(var hook in hooks.Values)try{hook.Input?.Invoke(blocked);}catch{}}
 internal static void Reset(){Active=false;hooks.Clear();}
}
