namespace ProjectE;
public static class ArtifactRules {
 public static long Cost(string id,int effects,int charge=0)=>id switch {
  Gadgets.Body when effects==1=>64,
  Gadgets.Soul when effects==2=>64,
  Gadgets.Life when effects is >=1 and <=3=>((effects&1)!=0?64:0)+((effects&2)!=0?64:0),
  Gadgets.Watch when effects==0&&charge is >=0 and <=2=>20+charge*10,
  _=>throw new ArgumentException("无效的宝石效果请求。")
 };
 public static float Slowdown(int charge)=>charge switch {0=>.25f,1=>.16f,2=>.12f,_=>1};
 public static float Repair(float current,float maximum)=>float.IsFinite(current)&&float.IsFinite(maximum)&&current>=0&&maximum>0?Math.Min(current+1,maximum):current;
}
