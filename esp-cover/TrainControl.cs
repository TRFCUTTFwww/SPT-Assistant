using System;
using System.Globalization;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.MovingPlatforms;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class TrainControl {
 static string arrivalH="0",arrivalM="1",arrivalS="0",departureH="0",departureM="5",departureS="0",notice="",cachedStatus="";static Vector2 scroll;
 static Locomotive? cachedTrain;static IntPtr cachedWorld;static float nextSearch,nextStatus;
 const string HelpText="使用本局原生列车和撤离流程；修改到站时间会一并平移发车时间，修改发车时间调整原生停站时长。";
 static Locomotive? FindTrain(bool searchNow){
  if(!RaidClock.Active||Singleton<GameWorld>.Instance is not {} world){cachedTrain=null;cachedWorld=IntPtr.Zero;nextSearch=0;return null;}
  if(world.Pointer!=cachedWorld){cachedTrain=null;cachedWorld=world.Pointer;nextSearch=0;}
  if(cachedTrain!=null)return cachedTrain;
  if(!searchNow&&Time.unscaledTime<nextSearch)return null;
  nextSearch=Time.unscaledTime+2f;
  return cachedTrain=UnityEngine.Object.FindObjectOfType<Locomotive>();
 }
 static Locomotive Current(){if(!RaidClock.Active)throw new ArgumentException("进入有装甲列车的战局后使用。");return FindTrain(true)??throw new ArgumentException("当前地图没有装甲列车。");}
 static double Elapsed(Locomotive train)=>(EFT.DateTimeExtensions.UtcNow.Ticks-train._depart.Ticks)/(double)TimeSpan.TicksPerSecond;
 static float Seconds(string raw){if(raw.Contains(':')){var parts=raw.Split(':');if(parts.Length!=3)throw new ArgumentException("时间格式为 HH:MM:SS。");return TrainTimeRules.Parse(parts[0],parts[1],parts[2]);}double n=ChatCommandSyntax.Duration(raw);if(!double.IsFinite(n)||n<0||n>604800)throw new ArgumentException("倒计时范围为 00:00:00 至 168:00:00。");return (float)n;}
 static float Timing(Locomotive train,Locomotive.ETravelState state){var times=train._travelStateTiming;int index=(int)state;if(times==null||index>=times.Length||!float.IsFinite(times[index]))throw new ArgumentException("列车时刻尚未同步。");return times[index];}
 static void Arrival(float seconds){
  var train=Current();double elapsed=Elapsed(train);float arrived=Timing(train,Locomotive.ETravelState.Arrived),leaving=Timing(train,Locomotive.ETravelState.Departing);
  if(elapsed>=arrived)throw new ArgumentException("列车已经到站；请使用发车时间控制。");
  var previous=train._depart;var next=EFT.DateTimeExtensions.UtcNow.AddSeconds(seconds-arrived);
  try{train._depart=next;if(train.Carriage!=null)foreach(var car in train.Carriage)if(car!=null)car._depart=next;train.Move(true);train.UpdateTravelState();if(train.Carriage!=null)foreach(var car in train.Carriage)car?.Move(true);notice=seconds==0?"已请求列车立即到站。":"到站倒计时已设为 "+TrainTimeRules.Clock(seconds)+"；原停站时长保持 "+TrainTimeRules.Clock(leaving-arrived)+"。";}
  catch{train._depart=previous;if(train.Carriage!=null)foreach(var car in train.Carriage)if(car!=null)car._depart=previous;throw;}
 }
 sealed record CurveState(MovingPlatform Platform,AnimationCurve Curve,float Duration);
 static List<CurveState> Capture(Locomotive train){var result=new List<CurveState>();void Add(MovingPlatform platform){var source=platform.MovementCurve;var curve=new AnimationCurve(source.keys){preWrapMode=source.preWrapMode,postWrapMode=source.postWrapMode};result.Add(new(platform,curve,platform._routeDuration));}Add(train);if(train.Carriage!=null)foreach(var car in train.Carriage)if(car!=null)Add(car);return result;}
 static void Departure(float seconds){
  var train=Current();double elapsed=Elapsed(train);float arrived=Timing(train,Locomotive.ETravelState.Arrived),leaving=Timing(train,Locomotive.ETravelState.Departing);
  if(elapsed>=leaving)throw new ArgumentException("列车已经发车，不能再次发车或倒回。");
  if(elapsed+seconds<arrived)throw new ArgumentException("发车时间必须晚于到站；立即发车请先让列车到站。");
  var oldEpoch=train._depart;var curves=Capture(train);var timing=train._travelStateTiming;var previous=new float[timing.Length];for(int i=0;i<previous.Length;i++)previous[i]=timing[i];
  try{
  float dwell=(float)Math.Max(.1,elapsed+seconds-arrived);train.SetBoardingTime(dwell);
  float actual=Timing(train,Locomotive.ETravelState.Departing);if(Math.Abs(actual-(elapsed+seconds))>2)throw new InvalidOperationException("原生列车未接受发车时间，请重新读取后重试。");
  // The movement clock and carriage interpolators cache their departure epoch.
  // Rebuilding the dwell curve alone leaves an already parked train stationary.
  if(seconds==0){var epoch=EFT.DateTimeExtensions.UtcNow.AddSeconds(-actual-.2);train._depart=epoch;if(train.Carriage!=null)foreach(var car in train.Carriage)if(car!=null)car._depart=epoch;}
  train.Move(true);if(train.Carriage!=null)foreach(var car in train.Carriage)car?.Move(true);train.UpdateTravelState();notice=seconds==0?"已请求列车发车。":"发车倒计时已设为 "+TrainTimeRules.Clock(seconds)+"。";
  }catch{train._depart=oldEpoch;if(train.Carriage!=null)foreach(var car in train.Carriage)if(car!=null)car._depart=oldEpoch;for(int i=0;i<previous.Length;i++)timing[i]=previous[i];train._travelStateTiming=timing;foreach(var state in curves){state.Platform.MovementCurve=state.Curve;state.Platform._routeDuration=state.Duration;}throw;}

 }
 static void Run(Action action){try{action();}catch(Exception e){notice=e.Message;}nextStatus=0;}
 static string FormatStatus(Locomotive train,float arrived,float departing){double now=Elapsed(train),a=arrived-now,d=departing-now;return d<=0?"列车已发车":a<=0?"列车已到站 · 发车倒计时 "+TrainTimeRules.Clock(d):"到站倒计时 "+TrainTimeRules.Clock(a)+" · 发车倒计时 "+TrainTimeRules.Clock(d);}
 internal static CornerGameData.Train? ReadSnapshot(){
  var train=FindTrain(false);
  if(train==null)return null;
  var times=train._travelStateTiming;
  int a=(int)Locomotive.ETravelState.Arrived,d=(int)Locomotive.ETravelState.Departing;
  if(times==null||a>=times.Length||d>=times.Length||!float.IsFinite(times[a])||!float.IsFinite(times[d]))
   return new("pending",null,null);
  double now=Elapsed(train),arrival=times[a]-now,departure=times[d]-now;
  return new(departure<=0?"departed":arrival<=0?"boarding":"approaching",
   Math.Round(Math.Max(0,arrival),2),Math.Round(Math.Max(0,departure),2));
 }
 static string StatusText(){
  if(!RaidClock.Active)return "进入有装甲列车的战局后使用。";
  var train=FindTrain(false);if(train==null)return "当前地图没有装甲列车。";
  var times=train._travelStateTiming;int a=(int)Locomotive.ETravelState.Arrived,d=(int)Locomotive.ETravelState.Departing;
  if(times==null||a>=times.Length||d>=times.Length||!float.IsFinite(times[a])||!float.IsFinite(times[d]))return "列车时刻尚未同步。";
  return FormatStatus(train,times[a],times[d]);
 }
 internal static string Status(){var train=Current();return FormatStatus(train,Timing(train,Locomotive.ETravelState.Arrived),Timing(train,Locomotive.ETravelState.Departing));}
 internal static void Command(string[] args){if(args.Length==1&&args[0]=="status"){RaidChat.Output(Status());return;}if(args.Length!=2||args[0] is not ("arrival" or "departure"))throw new ArgumentException("/train status | arrival|departure <now|HH:MM:SS>");float seconds=args[1]=="now"?0:Seconds(args[1]);if(args[0]=="arrival")Arrival(seconds);else Departure(seconds);RaidChat.Output(notice);}
 static void ReadCurrent(){var train=Current();double now=Elapsed(train);var a=TrainTimeRules.Clock(Timing(train,Locomotive.ETravelState.Arrived)-now).Split(':');var d=TrainTimeRules.Clock(Timing(train,Locomotive.ETravelState.Departing)-now).Split(':');arrivalH=a[0];arrivalM=a[1];arrivalS=a[2];departureH=d[0];departureM=d[1];departureS=d[2];notice="已读取当前倒计时，修改后点击对应按钮应用。";}
 static void TimeFields(float w,ref float y,ref string h,ref string m,ref string s){float col=(w-24)/3,top=y;string Field(int index,string value,string label){float x=index*(col+12);InterfaceMenu.Label(new(x,top,col,22),label,null,12);return InterfaceMenu.Field(new(x,top+27,col,36),value);}h=Field(0,h,"时");m=Field(1,m,"分");s=Field(2,s,"秒");y+=78;}
 internal static void Draw(Rect rect){
  if(cachedStatus.Length==0||(Event.current.type==EventType.Layout&&Time.unscaledTime>=nextStatus)){cachedStatus=StatusText();nextStatus=Time.unscaledTime+.25f;}
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-22,700));try{float w=rect.width-30,y=0;
   InterfaceMenu.Label(new(0,y,w,30),"装甲列车",InterfaceMenu.Accent,20);y+=46;
   InterfaceMenu.Paragraph(new(0,y,w,60),cachedStatus,InterfaceMenu.Accent,14);y+=76;
   if(InterfaceMenu.Button(new(0,y,(w-12)/2,38),"立即到站"))Run(()=>Arrival(0));if(InterfaceMenu.Button(new((w+12)/2,y,(w-12)/2,38),"立即发车"))Run(()=>Departure(0));y+=60;
   if(InterfaceMenu.Button(new(0,y,w,34),"读取当前倒计时"))Run(ReadCurrent);y+=54;
   InterfaceMenu.Label(new(0,y,w,28),"距离到站",null,14);y+=36;TimeFields(w,ref y,ref arrivalH,ref arrivalM,ref arrivalS);if(InterfaceMenu.Button(new(0,y,w,36),"修改到站时间"))Run(()=>Arrival(TrainTimeRules.Parse(arrivalH,arrivalM,arrivalS)));y+=60;
   InterfaceMenu.Label(new(0,y,w,28),"距离发车",null,14);y+=36;TimeFields(w,ref y,ref departureH,ref departureM,ref departureS);if(InterfaceMenu.Button(new(0,y,w,36),"修改发车时间"))Run(()=>Departure(TrainTimeRules.Parse(departureH,departureM,departureS)));y+=60;
   string footer=Event.current.type==EventType.Repaint?(notice.Length>0?notice+"\n"+HelpText:HelpText):"";
   InterfaceMenu.Paragraph(new(0,y,w,100),footer,null,13);
  }finally{GUI.EndScrollView();}
 }
}
