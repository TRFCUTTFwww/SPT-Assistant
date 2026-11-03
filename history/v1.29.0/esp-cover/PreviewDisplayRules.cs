using System;
namespace LocalSpt5Esp;
internal static class PreviewDisplayRules {
 internal static (string Text,string Detail) Compose(string prediction,string detail,string actual,bool showActual){
  if(!showActual||actual.Length==0)return(prediction,detail);
  return prediction.Length==0?(actual,""):(prediction,actual+(detail.Length==0?"":" · "+detail));
 }
 internal static bool KeepGap(double now,double seen,float aimDelta,bool alive,bool blocked)=>alive&&!blocked&&double.IsFinite(now)&&double.IsFinite(seen)&&now>=seen&&now-seen<=.18&&float.IsFinite(aimDelta)&&aimDelta<=1.5f;
}
