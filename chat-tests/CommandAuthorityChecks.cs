using System;
using System.Linq;
using LocalSpt5Esp;

internal static class CommandAuthorityChecks {
 internal static void Run() {
  void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
  ChatCommandScope Scope(string command,params string[] args)=>ChatCommandAuthority.Scope(command,args);
  foreach(string query in new[]{"list","position","inspect","tag","marker","navigate"})
   Check(Scope(query)==ChatCommandScope.Local,"Fika local/query command: "+query);
  foreach(string command in new[]{"tp","teleport","back","heal","effect","spreadplayers"})
   Check(Scope(command)==ChatCommandScope.OwnedActors,"Fika preflight owned target set: "+command);
  foreach(string command in new[]{"kill","damage","summon","attribute"})
   Check(Scope(command)==ChatCommandScope.Host,"Fika host command: "+command);
  Check(Scope("ai","pause")==ChatCommandScope.Host&&Scope("ai","remove")==ChatCommandScope.Unsupported,"Fika AI control does not enable local-only disposal");
  foreach(string command in new[]{"give","repair","reload","paste","edit","remove","rename","weather","unknown"})
   Check(Scope(command)==ChatCommandScope.Unsupported,"unsynchronized command stays explicit: "+command);
  Check(Scope("item","inspect")==ChatCommandScope.Local&&Scope("item","replace")==ChatCommandScope.Unsupported&&Scope("item","modify")==ChatCommandScope.Unsupported,"inventory reads do not enable writes");
  Check(Scope("clear","@a","item:x","0")==ChatCommandScope.Local&&Scope("clear","@a","item:x","1")==ChatCommandScope.Unsupported,"inventory count-only remains available");
  Check(Scope("data","get")==ChatCommandScope.Local&&Scope("data","modify","entity","ai:test","name")==ChatCommandScope.Unsupported,"data alias cannot bypass multiplayer rename restriction");
  Check(Scope("time","query")==ChatCommandScope.Local&&Scope("time","set")==ChatCommandScope.Unsupported,"daytime read/write split");
  Check(Scope("raid","time","query")==ChatCommandScope.Local&&Scope("raid","time","set")==ChatCommandScope.Unsupported&&Scope("raid","extract")==ChatCommandScope.Unsupported,"local countdown/extraction never used as coop authority");
  Check(Scope("gamerule","showRaidClock","true")==ChatCommandScope.Local&&Scope("gamerule","pauseRaidTime","true")==ChatCommandScope.Unsupported,"HUD toggle does not grant timer writes");
  Check(Scope("btr","status")==ChatCommandScope.Local&&Scope("btr","route")==ChatCommandScope.Host,"BTR status available to members, control belongs to host");
  foreach(bool host in new[]{false,true}){
   Check(ChatCommandAuthority.CanMutateActor(host,true,false,false),"own player is writable: host="+host);
   Check(!ChatCommandAuthority.CanMutateActor(host,false,false,false),"remote human is never writable: host="+host);
   Check(!ChatCommandAuthority.CanMutateActor(host,false,true,false),"observed AI is never writable: host="+host);
   Check(ChatCommandAuthority.CanMutateActor(host,false,true,true)==host,"controlled AI requires raid host: host="+host);
  }
  Check(!new[]{(true,false,false),(false,false,false)}.All(t=>ChatCommandAuthority.CanMutateActor(true,t.Item1,t.Item2,t.Item3)),"mixed self/remote selection fails whole preflight");
  foreach(var sample in new[]{(new[]{"player:friend"},-1),(new[]{"@s","player:friend"},0),(new[]{"~","~","~"},-1),(new[]{"@s","~","~","~"},0),(new[]{"player:friend","facing","@s"},-1),(new[]{"@s","player:friend","facing","~","~","~"},0)})
   Check(ChatCommandAuthority.TeleportSource(sample.Item1)==sample.Item2,"teleport source ignores destination/facing: "+string.Join(" ",sample.Item1));
 }
}
