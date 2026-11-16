namespace ProjectE;
public static class PageRules {
 public static int Count(int count,int size){if(size<=0)throw new ArgumentOutOfRangeException(nameof(size));return Math.Max(1,(int)((Math.Max(0,(long)count)+size-1)/size));}
 public static int Clamp(int page,int count,int size)=>Math.Clamp(page,0,Count(count,size)-1);
 public static int Step(int page,int direction,int count,int size)=>Clamp((int)Math.Clamp((long)Clamp(page,count,size)+Math.Sign(direction),int.MinValue,int.MaxValue),count,size);
}
