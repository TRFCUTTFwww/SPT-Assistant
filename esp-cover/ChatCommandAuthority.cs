using System;
namespace LocalSpt5Esp;

// This class has no game dependency so authority decisions can be regression tested.
internal enum ChatCommandScope { Local, OwnedActors, Host, Unsupported }
internal static class ChatCommandAuthority {
 internal static ChatCommandScope Scope(string command,string[] args) {
  string A(int n)=>n<args.Length?args[n]:"";
  return command switch {
   "list" or "position" or "inspect" or "tag" or "marker" or "navigate" or "experience" or "xp"=>ChatCommandScope.Local,
   "tp" or "teleport" or "back" or "spreadplayers" or "heal" or "effect"=>ChatCommandScope.OwnedActors,
   "kill" or "damage" or "summon"=>ChatCommandScope.Host,
   "ai"=>A(0)=="remove"?ChatCommandScope.Unsupported:ChatCommandScope.Host,
   "attribute"=>ChatCommandScope.Host,
   "data"=>A(0)=="get"?ChatCommandScope.Local:A(3)=="name"?ChatCommandScope.Unsupported:ChatCommandScope.Host,
   "item"=>A(0)=="inspect"?ChatCommandScope.Local:ChatCommandScope.Unsupported,
   "clear"=>A(2)=="0"?ChatCommandScope.Local:ChatCommandScope.Unsupported,
   "time"=>A(0)=="query"?ChatCommandScope.Local:ChatCommandScope.Unsupported,
   "gamerule"=>args.Length<2||A(0)=="showRaidClock"?ChatCommandScope.Local:ChatCommandScope.Unsupported,
   "raid"=>A(0)=="time"&&A(1)=="query"?ChatCommandScope.Local:ChatCommandScope.Unsupported,
   "btr"=>A(0)=="status"?ChatCommandScope.Local:ChatCommandScope.Host,
   _=>ChatCommandScope.Unsupported
  };
 }
 internal static bool CanMutateActor(bool host,bool self,bool ai,bool controlledAi)=>self||host&&ai&&controlledAi;
 internal static int TeleportSource(string[] args) {
  int facing=Array.IndexOf(args,"facing");int count=facing<0?args.Length:facing;
  return count is 2 or 4?0:-1;
 }
}
