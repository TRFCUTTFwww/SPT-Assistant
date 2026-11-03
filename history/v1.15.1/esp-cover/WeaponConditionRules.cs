using System;
using System.Globalization;
namespace LocalSpt5Esp;
internal enum WeaponCondition { New,Original,Custom }
internal static class WeaponConditionRules {
 internal static (float Current,float Maximum) Parse(string current,string maximum){float Number(string raw){if(!float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out float value)||!float.IsFinite(value))throw new ArgumentException("耐久必须为有效数字。");return value;}float max=Number(maximum),now=Number(current);if(max<=0||now<0||now>max)throw new ArgumentException("最大耐久需大于 0，当前耐久需介于 0 和最大耐久之间。");return(now,max);}
}
