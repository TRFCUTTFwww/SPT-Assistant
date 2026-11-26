using System;
using System.Globalization;
namespace LocalSpt5Esp;

internal enum CornerScriptTimeUnit { Hours, Minutes, Seconds, Milliseconds }
internal static class CornerScriptInterval {
 internal static double Normalize(double seconds)=>double.IsFinite(seconds)?Math.Clamp(seconds,.001,3600):30;
 static decimal Factor(CornerScriptTimeUnit unit)=>unit switch{CornerScriptTimeUnit.Hours=>3600m,CornerScriptTimeUnit.Minutes=>60m,CornerScriptTimeUnit.Milliseconds=>.001m,_=>1m};
 internal static string Format(double seconds,CornerScriptTimeUnit unit)=>((decimal)Normalize(seconds)/Factor(unit)).ToString(CultureInfo.InvariantCulture);
 internal static bool TrySeconds(string input,CornerScriptTimeUnit unit,out double seconds){
  seconds=0;if(!decimal.TryParse(input,NumberStyles.Float,CultureInfo.InvariantCulture,out var value))return false;
  decimal total;try{total=decimal.Round(value*Factor(unit),12);}catch(OverflowException){return false;}
  if(total is <.001m or >3600m)return false;seconds=(double)total;return true;
 }
}
