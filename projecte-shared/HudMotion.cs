namespace ProjectE;
public static class HudMotion {
 public static int Offset(float ratio,int icon,int kind,float seconds,bool recentlyHurt=false){
  if(!float.IsFinite(ratio)||!float.IsFinite(seconds)||ratio<0||ratio>1)return 0;
  int tick=(int)Math.Max(0,Math.Floor(seconds*20));
  bool shake=kind==0?(ratio<=.2f||recentlyHurt):ratio<=.3f&&tick%(Math.Max(1,(int)Math.Ceiling(ratio*20))*3+1)==0;
  if(!shake)return 0;
  uint value=unchecked((uint)(tick*374761393+icon*668265263+kind*1274126177));value=unchecked((value^(value>>13))*1274126177);value^=value>>16;
  if(ratio==0)return (int)(value%5)-2;
  return kind==0?(int)(value%2):(int)(value%3)-1;
 }
}
