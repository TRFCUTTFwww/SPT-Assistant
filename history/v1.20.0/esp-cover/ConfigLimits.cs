using System;
using BepInEx.Configuration;
namespace LocalSpt5Esp;
// A slider's convenient travel range is independent from validation of typed values.
internal static class ConfigLimits {
 internal sealed record SliderBounds(double Minimum,double Maximum);
 internal static ConfigDescription Describe<T>(string section,string key,T value,ConfigDescription original){
  bool open=key.Contains("distance",StringComparison.OrdinalIgnoreCase)||key.Contains("range",StringComparison.OrdinalIgnoreCase)||key is "High value threshold" or "Minimum value" or "Minimum cell value" or "Speed multiplier" or "Time multiplier" or "Draw distance" or "Outgoing damage multiplier" or "Soft rotation speed" or "Trail lifetime" or "Soft correction seconds";
  if(!open||original.AcceptableValues==null)return original;
  var old=original.AcceptableValues;var min=old.GetType().GetProperty("MinValue")?.GetValue(old);var max=old.GetType().GetProperty("MaxValue")?.GetValue(old);if(min==null||max==null)return original;
  var tags=new object[original.Tags.Length+1];Array.Copy(original.Tags,tags,original.Tags.Length);tags[^1]=new SliderBounds(Convert.ToDouble(min),Convert.ToDouble(max));
  AcceptableValueBase limit=typeof(T)==typeof(float)?new FiniteFloat(Convert.ToSingle(min)):new AcceptableValueRange<int>(Convert.ToInt32(min),int.MaxValue);
  return new ConfigDescription(original.Description,limit,tags);
 }
 sealed class FiniteFloat:AcceptableValueBase {
  readonly float minimum;internal FiniteFloat(float min):base(typeof(float)){minimum=min;}
  public override object Clamp(object value)=>value is float number&&float.IsFinite(number)&&number>=minimum?number:minimum;
  public override bool IsValid(object value)=>value is float number&&float.IsFinite(number)&&number>=minimum;
  public override string ToDescriptionString()=>"# finite float >= "+minimum+"; effective native maximum unverified";
 }
}
