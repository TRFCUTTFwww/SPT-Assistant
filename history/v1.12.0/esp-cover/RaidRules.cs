using System;
using System.Globalization;
namespace LocalSpt5Esp;
internal static class RaidRules {
 internal const double MaximumSeconds=86400;
 internal static (int Width,int Height) HudSize(float width,float height){if(!float.IsFinite(width)||!float.IsFinite(height))throw new ArgumentException("窗口尺寸必须为有限数值。");return ((int)Math.Clamp(Math.Round(width),180,720),(int)Math.Clamp(Math.Round(height),44,160));}
 internal static double Duration(double elapsed,double remaining){if(!double.IsFinite(elapsed)||elapsed<0||!double.IsFinite(remaining)||remaining<0||remaining>MaximumSeconds)throw new ArgumentException("剩余时间必须为 0～1440 分钟。");return elapsed+remaining;}
 internal static double Minutes(string text){if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||!double.IsFinite(value)||value<0||value>1440)throw new ArgumentException("请输入 0～1440 的分钟数。");return value*60;}
 internal static string Clock(double seconds){if(!double.IsFinite(seconds))return "未同步";long total=(long)Math.Ceiling(Math.Max(0,seconds));return $"{total/3600:00}:{total/60%60:00}:{total%60:00}";}
 internal static bool Match(string name,string id,string query)=>string.IsNullOrWhiteSpace(query)||ItemSpawnRules.NormalizeSearch(name+" "+id).Contains(ItemSpawnRules.NormalizeSearch(query),StringComparison.Ordinal);
}
