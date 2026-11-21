using System;
using LocalSpt5Esp;
internal static class CultistCircleChecks {
 internal static void Run(){
  bool Check(string owner="p",bool raid=false,long item=1,long process=2,double duration=100,bool ready=true,bool running=false)=>CultistCircleSyncRules.CanApply("p",owner,raid,1,item,2,process,duration,1234,100,ready,running);
  if(!Check()||!Check(duration:101)||Check(owner:"other")||Check(raid:true)||Check(item:3)||Check(process:4)||Check(duration:0)||Check(duration:double.NaN)||Check(ready:false)||Check(running:true))throw new Exception("Cultist sync rejected a valid duration override or accepted an invalid completion");
  Console.WriteLine("PASS cultist duration overrides and profile/item/process guards");
 }
}
