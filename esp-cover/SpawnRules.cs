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
 internal static float Number(string value,float minimum,float maximum,string label){if(!float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float result)||!float.IsFinite(result))throw new ArgumentException(label+"请输入有限数值。");return result;}
 internal static float[] Health(float[] original,float total,float percent){
  if(original.Length==0||!float.IsFinite(total)||!float.IsFinite(percent))throw new ArgumentException("出生血量设置无效。");
  double sum=0;foreach(var value in original){if(!float.IsFinite(value)||value<=0)throw new ArgumentException("原生部位血量无效。");sum+=value;}
  var result=new float[original.Length];for(int i=0;i<result.Length;i++)result[i]=total==0?original[i]:(float)(original[i]/sum*total);
  return result;
 }
 internal static float Current(float maximum,float percent){if(!float.IsFinite(maximum)||!float.IsFinite(percent))throw new ArgumentException("当前血量参数无效。");float value=(float)((double)maximum*percent/100);if(!float.IsFinite(value))throw new ArgumentException("当前血量超出 Float32 存储范围。");return value;}
}
