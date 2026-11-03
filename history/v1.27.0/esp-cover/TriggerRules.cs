using System;
namespace LocalSpt5Esp;
internal sealed class TriggerGate {
 string target="";double since,next;
 internal void Reset(){target="";since=next=0;}
 internal bool Ready(string id,double now,double delay,double interval){
  if(string.IsNullOrEmpty(id)||!double.IsFinite(now)||!double.IsFinite(delay)||!double.IsFinite(interval)||delay<0||interval<=0){target="";return false;}
  if(now<since){target="";next=0;}
  if(id!=target){target=id;since=now;}
  if(now-since+1e-6<delay||now+1e-6<next)return false;
  next=now+interval;return true;
 }
}
