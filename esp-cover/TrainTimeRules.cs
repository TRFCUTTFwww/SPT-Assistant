using System;
using System.Globalization;
namespace LocalSpt5Esp;
internal static class TrainTimeRules {
 internal static float Parse(string hours,string minutes,string seconds){
  int Read(string raw,int maximum){if(!int.TryParse(raw,NumberStyles.None,CultureInfo.InvariantCulture,out int n)||n<0||n>maximum)throw new ArgumentException("小时须为 0～168，分钟和秒须为 0～59 的整数。");return n;}
  int total=Read(hours,168)*3600+Read(minutes,59)*60+Read(seconds,59);if(total>604800)throw new ArgumentException("列车倒计时最多为 168:00:00。");return total;
 }
 internal static string Clock(double value){if(!double.IsFinite(value))return "--:--:--";long seconds=(long)Math.Ceiling(Math.Clamp(value,0,604800));return $"{seconds/3600:00}:{seconds/60%60:00}:{seconds%60:00}";}
}
