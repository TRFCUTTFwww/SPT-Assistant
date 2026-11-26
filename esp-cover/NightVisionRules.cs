using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace LocalSpt5Esp;
internal enum NightVisionStyle { Current,PNV57E,PNV10T,N15,PVS14,GPNVG18,PVS31A,Custom,Nw11Green,Nw11Blue,Nw11Day }
internal enum NightVisionMask { None=-1,Anvis=1,Binocular=2,OldMonocular=4 }
internal sealed record NightVisionProfile(byte R,byte G,byte B,byte A,float Intensity,float NoiseIntensity,float NoiseScale,float Ambient,NightVisionMask Mask,float MaskSize,bool Clarity=false,bool PreserveColor=false,float Gain=1,float Contrast=1){
 internal string Hex=>$"#{R:X2}{G:X2}{B:X2}{A:X2}";
}
internal static class NightVisionRules {
 internal static readonly (NightVisionStyle Style,string Template,NightVisionProfile Profile)[] Devices=[
  (NightVisionStyle.PNV57E,"67506ca81f18589016006aa6",new(37,255,62,254,1.3f,.06f,1,.018f,NightVisionMask.Binocular,.95f)),
  (NightVisionStyle.PNV10T,"5c0696830db834001d23f5da",new(0,255,32,254,1.7f,.06f,1.1f,.02f,NightVisionMask.OldMonocular,1.38f)),
  (NightVisionStyle.N15,"5c066e3a0db834001b7353f0",new(18,255,255,254,1.85f,.04f,2,.023f,NightVisionMask.Binocular,1.05f)),
  (NightVisionStyle.PVS14,"57235b6f24597759bf5a30f1",new(133,255,100,254,2.3f,.02f,5,.027f,NightVisionMask.OldMonocular,1.5f)),
  (NightVisionStyle.GPNVG18,"5c0558060db834001b735271",new(99,255,86,254,2.4f,.02f,5,.03f,NightVisionMask.Anvis,.99f)),
  (NightVisionStyle.PVS31A,"689b889473ebd6871805edd6",new(95,255,236,254,2.4f,.02f,5,.03f,NightVisionMask.Binocular,1.25f))
 ];
 internal static readonly NightVisionProfile Nw11Green=new(110,255,170,255,1,0,1,.085f,NightVisionMask.None,1,true,false,12,1.24f);
 internal static readonly NightVisionProfile Nw11Blue=new(125,210,255,255,1,0,1,.085f,NightVisionMask.None,1,true,false,12,1.24f);
 internal static readonly NightVisionProfile Nw11Day=new(255,255,255,255,1,0,1,.12f,NightVisionMask.None,1,true,true,10,1.08f);
 internal static NightVisionProfile Device(NightVisionStyle style)=>style switch{NightVisionStyle.Nw11Green=>Nw11Green,NightVisionStyle.Nw11Blue=>Nw11Blue,NightVisionStyle.Nw11Day=>Nw11Day,_=>Devices.First(d=>d.Style==style).Profile};
 internal static string Name(NightVisionStyle style)=>style switch{NightVisionStyle.Nw11Green=>"Nw11 绿",NightVisionStyle.Nw11Blue=>"Nw11 蓝",NightVisionStyle.Nw11Day=>"Nw11昼",NightVisionStyle.Current=>"当前设备效果",NightVisionStyle.Custom=>"自定义",NightVisionStyle.PNV57E=>"PNV-57E",NightVisionStyle.PNV10T=>"PNV-10T",NightVisionStyle.N15=>"Armasight N-15",NightVisionStyle.PVS14=>"AN/PVS-14",NightVisionStyle.GPNVG18=>"GPNVG-18",NightVisionStyle.PVS31A=>"PVS-31A",_=>"当前设备效果"};
 internal static string Name(NightVisionMask mask)=>mask switch{NightVisionMask.None=>"不使用夜视遮罩",NightVisionMask.Anvis=>"全景遮罩",NightVisionMask.Binocular=>"双目遮罩",_=>"单目遮罩"};
 internal static bool ScopeActive(bool local,bool alive,bool aiming,bool sight,bool camera,bool free)=>local&&alive&&aiming&&sight&&camera&&!free;
 internal static NightVisionProfile Custom(string hex,float intensity,float noise,float scale,float ambient,NightVisionMask mask,float size,bool clarity=false,bool preserveColor=false,float gain=1,float contrast=1){
  string raw=hex.Trim().TrimStart('#');
  if(raw.Length is not (6 or 8)||!uint.TryParse(raw,NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out uint color))throw new ArgumentException("夜视颜色请输入 #RRGGBB 或 #RRGGBBAA。");
  if(raw.Length==6)color=(color<<8)|255;
  if(new[]{intensity,noise,scale,ambient,size,gain,contrast}.Any(v=>!float.IsFinite(v))||!Enum.IsDefined(mask))throw new ArgumentException("夜视参数请输入有限数值。");
  return new((byte)(color>>24),(byte)(color>>16),(byte)(color>>8),(byte)color,intensity,noise,scale,ambient,mask,size,clarity,preserveColor,gain,contrast);
 }
 // Work in display RGB. A bounded exposure curve lifts shadows while keeping
 // black black and white white. Map by max RGB to preserve colored highlights.
 internal static float Tone(float value,float gain,float contrast){
  if(value<=0)return 0;if(value>=1)return 1;
  double exposure=Math.Max(.000001,gain),power=Math.Max(.000001,contrast);
  double lifted=value*exposure/(1+(exposure-1)*value);
  return (float)(1/(1+Math.Pow((1-lifted)/lifted,power)));
 }
 internal static (float R,float G,float B) Pixel(NightVisionProfile profile,float r,float g,float b){
  if(profile.PreserveColor){float peak=Math.Max(r,Math.Max(g,b));float ratio=peak>0?Tone(peak,profile.Gain,profile.Contrast)/peak:0;return(r*ratio,g*ratio,b*ratio);}
  float level=Tone(.2126f*r+.7152f*g+.0722f*b,profile.Gain,profile.Contrast);
  return(level*profile.R/255,level*profile.G/255,level*profile.B/255);
 }
}
// Camera passes may nest. A pass without suppression restores the scene's light
// states even when its parent pass suppresses lights, then resumes the parent.
internal sealed class CameraLightScope<T,TValue>(Func<IEnumerable<T>> lights,Func<T,TValue> read,Action<T,TValue> write,TValue suppressed) where T:notnull {
 readonly Dictionary<T,TValue> original=new();
 readonly List<(long Camera,bool Suppress)> stack=new();
 internal int Depth=>stack.Count;
 void Apply(bool suppress){foreach(var pair in original){var wanted=suppress?suppressed:pair.Value;if(!EqualityComparer<TValue>.Default.Equals(read(pair.Key),wanted))write(pair.Key,wanted);}}
 internal void Begin(long camera,bool suppress){
  stack.Add((camera,suppress));
  if(suppress&&original.Count==0)foreach(var light in lights())original.TryAdd(light,read(light));
  if(original.Count>0)Apply(suppress);
 }
 internal void End(long camera){
  int at=stack.FindLastIndex(p=>p.Camera==camera);if(at<0)return;
  stack.RemoveRange(at,stack.Count-at);Apply(stack.Count>0&&stack[^1].Suppress);
  if(stack.Count==0)original.Clear();
 }
 internal void Reset(){Apply(false);original.Clear();stack.Clear();}
}

internal static class CameraBufferOrder {
 // Insert only buffers removed by this feature. Keep new native buffers and
 // respect removals made by the game while the camera pass was in progress.
 internal static T[] Restore<T>(IEnumerable<T> previous,IEnumerable<T> removed,IEnumerable<T> current) where T:notnull {
  var prior=previous.ToArray();var restore=removed.ToHashSet();var result=current.ToList();
  for(int i=0;i<prior.Length;i++){
   var item=prior[i];if(!restore.Contains(item)||result.Contains(item))continue;
   int at=-1;
   for(int j=i+1;j<prior.Length;j++){at=result.IndexOf(prior[j]);if(at>=0)break;}
   if(at<0){for(int j=i-1;j>=0;j--){at=result.IndexOf(prior[j]);if(at>=0){at++;break;}}}
   result.Insert(at<0?result.Count:at,item);
  }
  return result.ToArray();
 }
}

// A global render value is scoped to nested cameras using the same baseline,
// so an ordinary camera never inherits another camera's Nw11 ambient fill.
internal sealed class CameraRenderValue<T>(Func<T> read,Action<T> write){
 readonly List<(long Camera,T Value)> stack=new();T original=default!;
 internal int Depth=>stack.Count;
 void Set(T value){if(!EqualityComparer<T>.Default.Equals(read(),value))write(value);}
 internal void Begin(long camera,Func<T,T>? transform){if(stack.Count==0)original=read();var value=transform==null?original:transform(original);stack.Add((camera,value));Set(value);}
 internal void End(long camera){int at=stack.FindLastIndex(x=>x.Camera==camera);if(at<0)return;stack.RemoveRange(at,stack.Count-at);Set(stack.Count==0?original:stack[^1].Value);}
 internal void Reset(){if(stack.Count==0)return;Set(original);stack.Clear();}
}
