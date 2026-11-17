using System;
namespace LocalSpt5Esp;
// Keep the player's deadline separate from the native timeout watchdog.
internal sealed class RaidDeadlineState {
 long owner;double visible,written;bool extended;
 internal double Duration(long timer,double native){
  if(owner!=timer||extended&&Math.Abs(native-written)>.001)Reset();
  return extended?visible:native;
 }
 internal double Update(long timer,bool protect,double elapsed,double duration){
  double buffer=duration;
  if(protect&&duration-elapsed<=10)buffer=owner==timer&&extended&&written-elapsed>30?written:RaidRules.Duration(elapsed,60);
  owner=timer;visible=duration;
  written=protect&&duration-elapsed<=10?Math.Max(duration,buffer):duration;
  extended=written!=visible;return written;
 }
 internal void Reset(){owner=0;visible=written=0;extended=false;}
}
