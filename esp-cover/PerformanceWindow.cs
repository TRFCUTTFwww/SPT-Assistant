using System;
namespace LocalSpt5Esp;

internal sealed class PerformanceWindow {
 internal readonly record struct Sample(double Frame,double Update,double Late,double Gui,double Render);
 internal readonly record struct Snapshot(int Count,double Seconds,double Fps,double Frame,double P99,double Peak,int Slow,double Update,double Late,double Gui,double Render){
  internal double Mod=>Update+Late+Gui+Render;
 }
 readonly Sample[] samples;readonly double[] sorted;int start,count;double elapsed;
 internal PerformanceWindow(int capacity=4096){if(capacity<1)throw new ArgumentOutOfRangeException(nameof(capacity));samples=new Sample[capacity];sorted=new double[capacity];}
 internal void Clear(){start=count=0;elapsed=0;}
 internal void Add(Sample sample){
  if(!double.IsFinite(sample.Frame)||sample.Frame<=0||!Valid(sample.Update)||!Valid(sample.Late)||!Valid(sample.Gui)||!Valid(sample.Render))return;
  if(count==samples.Length)Remove();samples[(start+count)%samples.Length]=sample;count++;elapsed+=sample.Frame;
  while(count>1&&elapsed-samples[start].Frame>=5000)Remove();
 }
 void Remove(){elapsed-=samples[start].Frame;start=(start+1)%samples.Length;count--;}
 static bool Valid(double value)=>double.IsFinite(value)&&value>=0;
 internal Snapshot Read(){
  if(count==0)return default;
  double update=0,late=0,gui=0,render=0,peak=0;int slow=0;
  for(int i=0;i<count;i++){var s=samples[(start+i)%samples.Length];sorted[i]=s.Frame;peak=Math.Max(peak,s.Frame);if(s.Frame>50)slow++;update+=s.Update;late+=s.Late;gui+=s.Gui;render+=s.Render;}
  Array.Sort(sorted,0,count);
  return new(count,elapsed/1000,count*1000/elapsed,elapsed/count,sorted[Math.Clamp((int)Math.Ceiling(count*.99)-1,0,count-1)],peak,slow,update/count,late/count,gui/count,render/count);
 }
}
