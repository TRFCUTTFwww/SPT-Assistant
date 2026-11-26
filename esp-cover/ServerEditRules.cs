using System;
using System.Globalization;
namespace LocalSpt5Esp;
internal static class ServerEditRules {
 internal static double Number(string raw,double min,double max,bool integer=false,bool minusOne=false){if(!double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out double value)||!double.IsFinite(value)||integer&&(value!=Math.Truncate(value)||value<int.MinValue||value>int.MaxValue))throw new ArgumentException("配置数值无效或超出允许范围。");return value;}
}
