using System;
using System.Collections.Generic;
using System.Globalization;
namespace LocalSpt5Esp;
internal sealed class SpawnLedger {
 internal readonly int Wanted;internal readonly HashSet<string> Created=new(StringComparer.Ordinal);
 internal SpawnLedger(int wanted){if(wanted<1)throw new ArgumentException("生成数量须大于零。");Wanted=wanted;}
 internal int Missing=>Math.Max(0,Wanted-Created.Count);
 internal bool Confirm(string id)=>!string.IsNullOrWhiteSpace(id)&&Created.Add(id);
}
internal static class SpawnRules {
 internal static float Number(string value,float minimum,float maximum,string label){if(!float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float result)||!float.IsFinite(result)||result<minimum||result>maximum)throw new ArgumentException(label+"必须为 "+minimum+"～"+maximum+" 之间的有限数字。");return result;}
 internal static float[] Health(float[] original,float total,float percent){
  if(original.Length==0||!float.IsFinite(total)||total<0||total>float.MaxValue/8||!float.IsFinite(percent)||percent<=0||percent>100)throw new ArgumentException("出生血量设置无效。");
  double sum=0;foreach(var value in original){if(!float.IsFinite(value)||value<=0)throw new ArgumentException("原生部位血量无效。");sum+=value;}
  if(total>0&&total<original.Length)throw new ArgumentException("指定总血量不能小于身体部位数量。");
  var result=new float[original.Length];for(int i=0;i<result.Length;i++)result[i]=total==0?original[i]:(float)(original[i]/sum*total);
  return result;
 }
 internal static float Current(float maximum,float percent){if(!float.IsFinite(maximum)||maximum<=0||!float.IsFinite(percent)||percent<=0||percent>100)throw new ArgumentException("当前血量参数无效。");return Math.Max(float.Epsilon,(float)((double)maximum*percent/100));}
}
