using BepInEx.Configuration;
using EFT;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class AiRouteDisplay {
 const string Section="71. AI 路线";
 const float Near=.1f;
 static readonly Color routeColor=new(.2f,.88f,1f,.85f),destinationColor=new(1f,.78f,.25f);
 static readonly FocusSelection focus=new();
 static readonly List<FocusSelection.Candidate> candidates=new();
 static ConfigEntry<bool> route=null!,destination=null!;
 static Player? selected;
 static IntPtr worldPointer;
 static float nextSelection,nextWarning;

 internal static void Initialize(){
  route=FeatureConfig.Bind(Section,"Show route","显示 AI 路线",false,"准星靠近 AI 时，显示它当前已规划的剩余路线。");
  destination=FeatureConfig.Bind(Section,"Show destination","显示 AI 目的地",false,"标记当前移动目标；只有路线末端可用时，单独标为路线终点。");
 }

 internal static void Draw(GameWorld world,Player local,Camera camera){
  if(!RaidClock.Active||!route.Value&&!destination.Value){if(worldPointer!=IntPtr.Zero)Clear();return;}
  try{
   if(worldPointer!=world.Pointer){Clear();worldPointer=world.Pointer;}
   if(Time.unscaledTime>=nextSelection){nextSelection=Time.unscaledTime+.12f;Select(world,local,camera);}
   if(selected==null||selected.HealthController?.IsAlive!=true)return;
   var mover=selected.AIData?.BotOwner?.Mover;
   if(mover==null)return;
   var path=mover.HasPathAndNoComplete?mover.CurPath:null;
   var corners=path?.Corners;
   int index=path?.CurIndex??-1,count=corners?.Length??0;
   bool hasRoute=index>=0&&index<count;
   if(route.Value&&hasRoute){
    var previous=selected.Position+Vector3.up*.16f;
    var previousScreen=OverlayProjection.ScreenPoint(camera,previous);
    for(int corner=index;corner<count&&corner-index<128;corner++){
     var point=corners![corner]+Vector3.up*.16f;
     var screen=OverlayProjection.ScreenPoint(camera,point);
     DrawSegment(camera,previous,point,previousScreen,screen);
     previous=point;previousScreen=screen;
    }
   }
   if(destination.Value&&(path!=null||mover.IsMoving)){
    var target=mover.TargetPoint;
    if(target.HasValue&&Finite(target.Value))DrawDestination(camera,local.Position,target.Value,true);
    else if(hasRoute)DrawDestination(camera,local.Position,corners![count-1],false);
   }
  }catch(Exception error){
   if(Time.unscaledTime<nextWarning)return;
   nextWarning=Time.unscaledTime+20;Plugin.Self.Error(error);
  }
 }

 static void Select(GameWorld world,Player local,Camera camera){
  var all=world.AllAlivePlayersList;
  if(all==null){selected=null;focus.Clear();return;}
  var origin=local.Position;
  float range=Plugin.Self.PeopleRange.Value;
  candidates.Clear();
  for(int index=0;index<all.Count;index++){
   var actor=all[index];
   if(actor==null||actor.IsYourPlayer||actor.HealthController?.IsAlive!=true)continue;
   var position=actor.Position;
   if((position-origin).sqrMagnitude>range*range)continue;
   var point=OverlayProjection.ScreenPoint(camera,position+Vector3.up);
   if(!Finite(point)||point.z<Near||point.x<0||point.x>Screen.width||point.y<0||point.y>Screen.height)continue;
   if(actor.AIData?.BotOwner?.Mover!=null)candidates.Add(new(actor.Pointer.ToInt64(),point.x,point.y));
  }
  long key=focus.Choose(candidates,Screen.width/2f,Screen.height/2f,Time.unscaledTime);
  if(key==0){selected=null;return;}
  if(selected?.Pointer.ToInt64()==key)return;
  selected=null;
  for(int index=0;index<all.Count;index++){
   var actor=all[index];
   if(actor!=null&&actor.Pointer.ToInt64()==key){selected=actor;return;}
  }
 }

 static void DrawSegment(Camera camera,Vector3 from,Vector3 to,Vector3 a,Vector3 b){
  if(!Finite(a)||!Finite(b)||a.z<Near&&b.z<Near)return;
  if(a.z<Near)a=OverlayProjection.ScreenPoint(camera,Vector3.Lerp(from,to,(Near-a.z)/(b.z-a.z)));
  else if(b.z<Near)b=OverlayProjection.ScreenPoint(camera,Vector3.Lerp(to,from,(Near-b.z)/(a.z-b.z)));
  if(!Finite(a)||!Finite(b))return;
  var start=new Vector2(a.x,Screen.height-a.y);
  var delta=new Vector2(b.x-a.x,a.y-b.y);
  float first=0,last=1;
  if(Clip(start.x,delta.x,Screen.width,ref first,ref last)&&Clip(start.y,delta.y,Screen.height,ref first,ref last))
   RaidLoot.Line(start+delta*first,start+delta*last,routeColor,2f);
 }

 static bool Clip(float start,float delta,float limit,ref float first,ref float last){
  if(delta==0)return start>=0&&start<=limit;
  float lower=-start/delta,upper=(limit-start)/delta;
  first=Math.Max(first,Math.Min(lower,upper));last=Math.Min(last,Math.Max(lower,upper));
  return first<last;
 }

 static void DrawDestination(Camera camera,Vector3 origin,Vector3 point,bool hasTarget){
  if(!Finite(point))return;
  var screen=OverlayProjection.ScreenPoint(camera,point+Vector3.up*.25f);
  if(!Finite(screen)||screen.z<Near||screen.x<0||screen.x>Screen.width||screen.y<0||screen.y>Screen.height)return;
  var center=new Vector2(screen.x,Screen.height-screen.y);
  var top=center+new Vector2(0,-7);var right=center+new Vector2(7,0);
  var bottom=center+new Vector2(0,7);var left=center+new Vector2(-7,0);
  RaidLoot.Line(top,right,destinationColor,2f);RaidLoot.Line(right,bottom,destinationColor,2f);
  RaidLoot.Line(bottom,left,destinationColor,2f);RaidLoot.Line(left,top,destinationColor,2f);
  string title=I18n.Text(hasTarget?"AI 目的地":"AI 路线终点");
  float labelX=Math.Clamp(center.x+12,0,Math.Max(0,Screen.width-210));
  float labelY=Math.Clamp(center.y-12,0,Math.Max(0,Screen.height-24));
  InterfaceMenu.Label(new Rect(labelX,labelY,210,24),$"{title} · {Vector3.Distance(origin,point):F0} m",destinationColor,13,outline:true);
 }

 static bool Finite(Vector3 point)=>float.IsFinite(point.x)&&float.IsFinite(point.y)&&float.IsFinite(point.z);
 internal static void Clear(){worldPointer=IntPtr.Zero;selected=null;nextSelection=0;focus.Clear();candidates.Clear();}
}
