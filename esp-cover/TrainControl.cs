using System;
using System.Globalization;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.MovingPlatforms;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class TrainControl {
 static string arrival="60",departure="300",notice="";static Vector2 scroll;
 static Locomotive Current(){if(!RaidClock.Active)throw new ArgumentException("进入有装甲列车的战局后使用。");return UnityEngine.Object.FindObjectOfType<Locomotive>()??throw new ArgumentException("当前地图没有装甲列车。");}
 static double Elapsed(Locomotive train)=>(EFT.DateTimeExtensions.UtcNow.Ticks-train._depart.Ticks)/(double)TimeSpan.TicksPerSecond;
 static float Seconds(string raw){if(!float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out float n)||!float.IsFinite(n)||n<0||n>604800)throw new ArgumentException("请输入 0 至 604800 秒。");return n;}
 static float Timing(Locomotive train,Locomotive.ETravelState state){var times=train._travelStateTiming;int index=(int)state;if(times==null||index>=times.Length||!float.IsFinite(times[index]))throw new ArgumentException("列车时刻尚未同步。");return times[index];}
 static void Arrival(float seconds){
  var train=Current();double elapsed=Elapsed(train);float arrived=Timing(train,Locomotive.ETravelState.Arrived),leaving=Timing(train,Locomotive.ETravelState.Departing);
  if(elapsed>=arrived)throw new ArgumentException("列车已经到站；请使用发车时间控制。");
  var previous=train._depart;var next=EFT.DateTimeExtensions.UtcNow.AddSeconds(seconds-arrived);
  try{train._depart=next;if(train.Carriage!=null)foreach(var car in train.Carriage)if(car!=null)car._depart=next;train.Move(true);train.UpdateTravelState();if(train.Carriage!=null)foreach(var car in train.Carriage)car?.Move(true);notice=seconds==0?"已请求列车立即到站。":"列车将在 "+seconds+" 秒后到站；原停站时长保持 "+Math.Round(leaving-arrived)+" 秒。";}
  catch{train._depart=previous;if(train.Carriage!=null)foreach(var car in train.Carriage)if(car!=null)car._depart=previous;throw;}
 }
 sealed record CurveState(MovingPlatform Platform,AnimationCurve Curve,float Duration);
 static List<CurveState> Capture(Locomotive train){var result=new List<CurveState>();void Add(MovingPlatform platform){var source=platform.MovementCurve;var curve=new AnimationCurve(source.keys){preWrapMode=source.preWrapMode,postWrapMode=source.postWrapMode};result.Add(new(platform,curve,platform._routeDuration));}Add(train);if(train.Carriage!=null)foreach(var car in train.Carriage)if(car!=null)Add(car);return result;}
 static void Departure(float seconds){
  var train=Current();double elapsed=Elapsed(train);float arrived=Timing(train,Locomotive.ETravelState.Arrived),leaving=Timing(train,Locomotive.ETravelState.Departing);
  if(elapsed>=leaving)throw new ArgumentException("列车已经发车，不能再次发车或倒回。");
  if(elapsed+seconds<arrived)throw new ArgumentException("发车时间必须晚于到站；立即发车请先让列车到站。");
  var curves=Capture(train);var timing=train._travelStateTiming;var previous=new float[timing.Length];for(int i=0;i<previous.Length;i++)previous[i]=timing[i];
  try{
  float dwell=(float)Math.Max(.1,elapsed+seconds-arrived);train.SetBoardingTime(dwell);
  float actual=Timing(train,Locomotive.ETravelState.Departing);if(Math.Abs(actual-(elapsed+seconds))>2)throw new InvalidOperationException("原生列车未接受发车时间，请重新读取后重试。");
  train.UpdateTravelState();notice=seconds==0?"已请求列车发车。":"列车将在 "+seconds+" 秒后发车。";
  }catch{for(int i=0;i<previous.Length;i++)timing[i]=previous[i];train._travelStateTiming=timing;foreach(var state in curves){state.Platform.MovementCurve=state.Curve;state.Platform._routeDuration=state.Duration;}throw;}

 }
 static void Run(Action action){try{action();}catch(Exception e){notice=e.Message;}}
 internal static void Draw(Rect rect){
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-22,540));try{float w=rect.width-30,y=0;
   InterfaceMenu.Label(new(0,y,w,30),"装甲列车",InterfaceMenu.Accent,20);y+=46;
   string status="当前地图列车尚未就绪。";try{var train=Current();double now=Elapsed(train),a=Timing(train,Locomotive.ETravelState.Arrived)-now,d=Timing(train,Locomotive.ETravelState.Departing)-now;status=d<=0?"列车已发车":a<=0?"列车已到站 · 发车倒计时 "+RaidRules.Clock(d):"到站倒计时 "+RaidRules.Clock(a)+" · 发车倒计时 "+RaidRules.Clock(d);}catch(Exception e){status=e.Message;}
   InterfaceMenu.Paragraph(new(0,y,w,60),status,InterfaceMenu.Accent,14);y+=76;
   if(InterfaceMenu.Button(new(0,y,(w-12)/2,38),"立即到站"))Run(()=>Arrival(0));if(InterfaceMenu.Button(new((w+12)/2,y,(w-12)/2,38),"立即发车"))Run(()=>Departure(0));y+=60;
   InterfaceMenu.Label(new(0,y,w,28),"距离到站还有多少秒",null,14);y+=36;arrival=InterfaceMenu.Field(new(0,y,140,34),arrival);if(InterfaceMenu.Button(new(154,y,w-154,34),"修改到站时间"))Run(()=>Arrival(Seconds(arrival)));y+=60;
   InterfaceMenu.Label(new(0,y,w,28),"距离发车还有多少秒",null,14);y+=36;departure=InterfaceMenu.Field(new(0,y,140,34),departure);if(InterfaceMenu.Button(new(154,y,w-154,34),"修改发车时间"))Run(()=>Departure(Seconds(departure)));y+=60;
   InterfaceMenu.Paragraph(new(0,y,w,100),notice+"\n使用本局原生列车和撤离流程；修改到站时间会一并平移发车时间，修改发车时间调整原生停站时长。",null,13);
  }finally{GUI.EndScrollView();}
 }
}
