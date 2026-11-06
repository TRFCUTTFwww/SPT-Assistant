using System;
using System.Globalization;
namespace LocalSpt5Esp;
internal enum RaidTimeTone { Unknown,Green,Yellow,Red }
internal static class RaidRules {
 internal static double MaximumSeconds=>TimeSpan.MaxValue.TotalSeconds;
 internal static (int Width,int Height) HudSize(float width,float height){if(!float.IsFinite(width)||!float.IsFinite(height))throw new ArgumentException("窗口尺寸必须为有限数值。");return ((int)Math.Clamp(Math.Round(width),180,720),(int)Math.Clamp(Math.Round(height),44,160));}
 internal static double Duration(double elapsed,double remaining){if(!double.IsFinite(elapsed)||elapsed<0||!double.IsFinite(remaining)||remaining<0||remaining>MaximumSeconds-elapsed)throw new ArgumentException("剩余时间须为非负有限值，且总时长须能被原生 TimeSpan 表示。");return elapsed+remaining;}
 internal static double Minutes(string text){if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||!double.IsFinite(value)||value<0||value>MaximumSeconds/60)throw new ArgumentException("请输入可由原生 TimeSpan 表示的非负分钟数。");return value*60;}
 internal static double Hms(string hours,string minutes,string seconds){double Parse(string raw,bool whole,string label,double max){if(!double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)||!double.IsFinite(number)||number<0||number>max||whole&&number!=Math.Truncate(number))throw new ArgumentException(label+"输入无效。");return number;}double h=Parse(hours,true,"时",MaximumSeconds/3600),m=Parse(minutes,true,"分",59),s=Parse(seconds,false,"秒",Math.BitDecrement(60d));double total=h*3600+m*60+s;if(total>MaximumSeconds)throw new ArgumentException("时长超出原生计时范围。");return total;}
 internal static string Clock(double seconds){if(!double.IsFinite(seconds))return "未同步";long total=(long)Math.Ceiling(Math.Max(0,seconds));return $"{total/3600:00}:{total/60%60:00}:{total%60:00}";}
 internal static RaidTimeTone TimeTone(double? remaining,double? duration){if(remaining is not double s||!double.IsFinite(s))return RaidTimeTone.Unknown;if(s<600)return RaidTimeTone.Red;if(duration is not double total||!double.IsFinite(total)||total<=0)return RaidTimeTone.Unknown;return s>total/2?RaidTimeTone.Green:RaidTimeTone.Yellow;}
 internal static bool Match(string name,string id,string query)=>string.IsNullOrWhiteSpace(query)||ItemSpawnRules.NormalizeSearch(name+" "+id).Contains(ItemSpawnRules.NormalizeSearch(query),StringComparison.Ordinal);
}
