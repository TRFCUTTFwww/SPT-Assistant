using System;
using System.Globalization;
namespace LocalSpt5Esp;
internal static class ServerEditRules {
 internal static double Number(string raw,double min,double max,bool integer=false,bool minusOne=false){if(!double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out double value)||!double.IsFinite(value)||value<min||value>max||integer&&value!=Math.Truncate(value)||minusOne&&value<0&&value!=-1)throw new ArgumentException("配置数值无效或超出允许范围。");return value;}
}
