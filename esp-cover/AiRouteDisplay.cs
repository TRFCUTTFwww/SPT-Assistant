using BepInEx.Configuration;
using EFT;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace LocalSpt5Esp;

internal static class AiRouteDisplay {
 const string Section="71. AI 路线";
 const float Near=.1f;
 static readonly Color routeColor=new(.2f,.88f,1f,.85f),destinationColor=new(1f,.78f,.25f);
 static readonly FocusSelection focus=new();
 static readonly List<FocusSelection.Candidate> candidates=new();
 static ConfigEntry<bool> route=null!,destination=null!,persistent=null!,ownerLabels=null!;
 sealed class Track(Player actor){
  internal readonly Player Actor=actor;
  internal readonly GroundRouteVisual Visual=new("Local.Spt5.AiNavigation."+actor.Pointer);
  internal readonly Color Color=UnityEngine.Color.HSVToRGB((uint)actor.Pointer.GetHashCode()%997/997f,.64f,1);
  internal readonly string Name=actor.Profile?.Nickname??actor.ProfileId;
  internal Vector3? Label,Endpoint;
  internal bool HasTarget;
 }
 static readonly Dictionary<IntPtr,Track> tracks=new();
 static readonly HashSet<IntPtr> active=new();
 static readonly List<NVector> remaining=new();
 static Player? selected;
 static IntPtr worldPointer;
 static float nextSelection,nextWarning;

 internal static void Initialize(){
  route=FeatureConfig.Bind(Section,"Show route","显示 AI 路线",false,"显示 AI 当前已规划的剩余路线。");
  persistent=FeatureConfig.Bind(Section,"Always show nearby routes","常显附近 AI 路线",true);
  ownerLabels=FeatureConfig.Bind(Section,"Show route owner","标注路线所属 AI",true);
  destination=FeatureConfig.Bind(Section,"Show destination","显示 AI 目的地",false,"标记当前移动目标；只有路线末端可用时，单独标为路线终点。");
 }

 internal static void Draw(GameWorld world,Player local,Camera camera){
  if(!RaidClock.Active||!route.Value&&!destination.Value){if(worldPointer!=IntPtr.Zero)Clear();return;}
  try{
   if(worldPointer!=world.Pointer){Clear();worldPointer=world.Pointer;}
   if(Time.unscaledTime>=nextSelection){
    nextSelection=Time.unscaledTime+.15f;
    active.Clear();
    if(persistent.Value){
     var all=world.AllAlivePlayersList;
     float radius=Plugin.Self.PeopleRange.Value;
     if(all!=null)for(int i=0;i<all.Count;i++){
      var actor=all[i];
      if(actor!=null&&!actor.IsYourPlayer&&actor.HealthController?.IsAlive==true&&(actor.Position-local.Position).sqrMagnitude<=radius*radius)Refresh(actor);
     }
    }else{
     Select(world,local,camera);
     if(selected!=null&&selected.HealthController?.IsAlive==true)Refresh(selected);
    }
    foreach(var key in tracks.Keys.Where(key=>!active.Contains(key)).ToArray()){tracks[key].Visual.DestroyVisual();tracks.Remove(key);}
   }
   foreach(var track in tracks.Values){
    if(track.Actor==null||track.Actor.HealthController?.IsAlive!=true){track.Visual.Hide();continue;}
    if(route.Value&&ownerLabels.Value&&track.Label is {} label){
     var p=OverlayProjection.ScreenPoint(camera,label+Vector3.up*.45f);
     if(Finite(p)&&p.z>Near)InterfaceMenu.Label(new Rect(p.x+6,Screen.height-p.y-14,220,24),track.Name,track.Color,13,outline:true);
    }
    if(destination.Value&&track.Endpoint is {} point)DrawDestination(camera,local.Position,point,track.HasTarget,ownerLabels.Value?track.Name:"");
   }
  }catch(Exception error){
   Clear();
   Warn(error);
  }
 }

 static void Warn(Exception error){
  if(Time.unscaledTime<nextWarning)return;
  nextWarning=Time.unscaledTime+20;Plugin.Self.Error(error);
 }
 static void Refresh(Player actor){
  try{RefreshRoute(actor);}catch(Exception error){active.Remove(actor.Pointer);Warn(error);}
 }
 static void RefreshRoute(Player actor){
  var mover=actor.AIData?.BotOwner?.Mover;
  if(mover==null)return;
  var path=mover.HasPathAndNoComplete?mover.CurPath:null;
  var corners=path?.Corners;
  int index=path?.CurIndex??-1,count=corners?.Length??0;
  bool hasRoute=index>=0&&index<count;
  if(!hasRoute&&(!destination.Value||!mover.IsMoving))return;
  var key=actor.Pointer;
  active.Add(key);
  if(!tracks.TryGetValue(key,out var track))tracks[key]=track=new(actor);
  track.Label=null;track.Endpoint=null;
  remaining.Clear();
  if(route.Value&&hasRoute&&Finite(actor.Position)){
   var origin=actor.Position;
   remaining.Add(new(origin.x,origin.y,origin.z));
   for(int i=index;i<count&&i-index<128;i++){
    var point=corners![i];if(!Finite(point))break;
    remaining.Add(new(point.x,point.y,point.z));
   }
  }
  if(remaining.Count>1){
   var start=new RouteGeometry.Projection(remaining[0],0,0,0);
   var points=RouteGeometry.Slice(remaining,start,GroundNavigation.Horizon);
   if(track.Visual.Render(points,GroundNavigation.LineWidth,GroundNavigation.ArrowSpacing,ownerLabels.Value?track.Color:routeColor))track.Label=actor.Position;
  }else track.Visual.Hide();
  if(destination.Value){
   var target=mover.TargetPoint;
   track.HasTarget=target.HasValue&&Finite(target.Value);
   if(track.HasTarget)track.Endpoint=target.Value;
   else if(hasRoute)track.Endpoint=corners![count-1];
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


 static void DrawDestination(Camera camera,Vector3 origin,Vector3 point,bool hasTarget,string owner){
  if(!Finite(point))return;
  var screen=OverlayProjection.ScreenPoint(camera,point+Vector3.up*.25f);
  if(!Finite(screen)||screen.z<Near||screen.x<0||screen.x>Screen.width||screen.y<0||screen.y>Screen.height)return;
  var center=new Vector2(screen.x,Screen.height-screen.y);
  var top=center+new Vector2(0,-7);var right=center+new Vector2(7,0);
  var bottom=center+new Vector2(0,7);var left=center+new Vector2(-7,0);
  RaidLoot.Line(top,right,destinationColor,2f);RaidLoot.Line(right,bottom,destinationColor,2f);
  RaidLoot.Line(bottom,left,destinationColor,2f);RaidLoot.Line(left,top,destinationColor,2f);
  string title=I18n.Text(hasTarget?"AI 目的地":"AI 路线终点");
  if(owner.Length>0)title=owner+" · "+title;
  float labelX=Math.Clamp(center.x+12,0,Math.Max(0,Screen.width-210));
  float labelY=Math.Clamp(center.y-12,0,Math.Max(0,Screen.height-24));
  InterfaceMenu.Label(new Rect(labelX,labelY,210,24),$"{title} · {Vector3.Distance(origin,point):F0} m",destinationColor,13,outline:true);
 }

 static bool Finite(Vector3 point)=>float.IsFinite(point.x)&&float.IsFinite(point.y)&&float.IsFinite(point.z);
 internal static void Clear(){foreach(var track in tracks.Values)track.Visual.DestroyVisual();tracks.Clear();active.Clear();worldPointer=IntPtr.Zero;selected=null;nextSelection=0;focus.Clear();candidates.Clear();}
}
