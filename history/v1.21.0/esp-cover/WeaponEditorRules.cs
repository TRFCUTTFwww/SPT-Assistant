using System;
using System.Collections.Generic;
using System.Globalization;
namespace LocalSpt5Esp;
internal sealed class WeaponTuningPreset {
 public string Id{get;set;}="";public string Name{get;set;}="";public string Template{get;set;}="";public bool IgnoreAttachments{get;set;}public Dictionary<string,double> Values{get;set;}=new();
}
internal static class WeaponEditorRules {
 internal static double Number(string text,bool integer,double minimum){if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out double value)||!double.IsFinite(value)||value<minimum||integer&&(value!=Math.Truncate(value)||value>int.MaxValue||value<int.MinValue)||!integer&&!float.IsFinite((float)value))throw new ArgumentException("请输入有效数字。");return value;}
 internal static string PresetName(string text){var name=text.Trim();if(name.Length is <1 or >80)throw new ArgumentException("请输入 1～80 字的参数方案名称。");return name;}
}
