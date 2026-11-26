using System;
using BepInEx.Configuration;
namespace LocalSpt5Esp;
// Slider travel bounds are hints; typed values use only the numeric storage type.
internal static class ConfigLimits {
 internal sealed record SliderBounds(double Minimum,double Maximum);
 internal static ConfigDescription Describe<T>(string section,string key,T value,ConfigDescription original){
  if(typeof(T)!=typeof(int)&&typeof(T)!=typeof(float)&&typeof(T)!=typeof(double))return original;
  var tags=original.Tags;var range=original.AcceptableValues;
  var min=range?.GetType().GetProperty("MinValue")?.GetValue(range);
  var max=range?.GetType().GetProperty("MaxValue")?.GetValue(range);
  if(min!=null&&max!=null)tags=[..tags,new SliderBounds(Convert.ToDouble(min),Convert.ToDouble(max))];
  return new ConfigDescription(original.Description,new NumericValue(typeof(T)),tags);
 }
 sealed class NumericValue(Type type):AcceptableValueBase(type) {
  public override object Clamp(object value)=>IsValid(value)?value:ValueType==typeof(float)?(object)0f:ValueType==typeof(double)?(object)0d:0;
  public override bool IsValid(object value)=>value.GetType()==ValueType&&(value is int||value is float f&&float.IsFinite(f)||value is double d&&double.IsFinite(d));
  public override string ToDescriptionString()=>"# Any finite value representable by "+ValueType.Name;
 }
}
