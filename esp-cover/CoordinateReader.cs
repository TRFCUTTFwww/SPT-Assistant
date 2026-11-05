using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class CoordinateReader {
 internal sealed record Point(float X,float Y,float Z){internal static Point Of(Vector3 v)=>new(v.x,v.y,v.z);internal string Line=>$"X {X.ToString("F5",CultureInfo.InvariantCulture)}   Y {Y.ToString("F5",CultureInfo.InvariantCulture)}   Z {Z.ToString("F5",CultureInfo.InvariantCulture)}";}
 internal sealed record Hit(string Object,string Path,string Scene,Point Position,Point Normal,float Distance);
 internal sealed record Snapshot(string Map,string ProfileId,long World,string Utc,Point Player,Point Rotation,string Camera,Hit[] Hits);
 static Snapshot? snapshot;static bool live=true;static float nextRead;static string label="坐标点",notice="";static Vector2 scroll;
 static readonly JsonSerializerOptions json=new(){WriteIndented=true};
 internal static string Home=>Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","coordinates");
 internal static Snapshot Capture(){
  var world=Singleton<GameWorld>.Instance;var player=world?.MainPlayer;if(!RaidClock.Active||player==null)throw new ArgumentException("进入战局后读取坐标。");
  var camera=Camera.main;if(camera==null||camera.name!="FPS Camera")camera=UnityEngine.Object.FindObjectsOfType<Camera>().FirstOrDefault(c=>c.enabled&&c.name=="FPS Camera")??camera;
  Hit[] hits=Array.Empty<Hit>();if(camera!=null){var ray=camera.ViewportPointToRay(new Vector3(.5f,.5f,0));hits=Physics.RaycastAll(ray,12f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider!=null&&!h.collider.transform.IsChildOf(player.transform)).OrderBy(h=>h.distance).Take(8).Select(h=>new Hit(h.collider.name,PathOf(h.collider.transform),h.collider.gameObject.scene.path,Point.Of(h.point),Point.Of(h.normal),h.distance)).ToArray();}
  return new(player.Location??"",player.ProfileId,world!.Pointer.ToInt64(),DateTime.UtcNow.ToString("O"),Point.Of(player.Position),Point.Of(camera!=null?camera.transform.eulerAngles:player.transform.eulerAngles),camera?.name??"",hits);
 }
 static string PathOf(Transform transform){string value=transform.name;for(int depth=0;depth<12&&transform.parent!=null;depth++){transform=transform.parent;value=transform.name+"/"+value;}return value;}
 internal static void Tick(){if(!InterfaceMenu.CoordinatesPage||!live||Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+.25f;try{snapshot=Capture();notice="";}catch(Exception error){snapshot=null;notice=error.Message;}}
 static void Action(Action action){try{action();}catch(Exception error){notice=error.Message;}}
 static Snapshot Current(){var value=snapshot??throw new ArgumentException("请先读取坐标。");var world=Singleton<GameWorld>.Instance;if(!RaidClock.Active||world?.Pointer.ToInt64()!=value.World||world.MainPlayer?.ProfileId!=value.ProfileId)throw new ArgumentException("记录来自先前战局，请重新读取。");return value;}
 static string Json()=>JsonSerializer.Serialize(new{label,coordinates=Current()},json);
 static void Save(){var value=Json();Directory.CreateDirectory(Home);string name=string.Concat(label.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));if(name.Length==0)name="point";if(name.Length>50)name=name[..50];string file=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+name+".json";File.WriteAllText(Path.Combine(Home,file),value);File.WriteAllText(Path.Combine(Home,"latest.json"),value);notice="已保存："+file;}
 internal static void Draw(Rect rect){
  Tick();scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,Math.Max(rect.height-1,550+(snapshot?.Hits.Length??0)*70)));float x=8,y=8,w=rect.width-40;
  InterfaceMenu.Label(new Rect(x,y,w,30),"坐标读取",InterfaceMenu.Accent,21);y+=44;InterfaceMenu.Paragraph(new Rect(x,y,w,46),"先对准目标，再打开此页；Y 为高度，准星命中点可用于精确绑定交互位置。",null,13);y+=60;
  if(InterfaceMenu.Button(new Rect(x,y,(w-12)/2,36),live?"实时刷新：开启":"实时刷新：关闭",live)){live=!live;nextRead=0;}
  if(InterfaceMenu.Button(new Rect(x+(w+12)/2,y,(w-12)/2,36),"读取并固定"))Action(()=>{snapshot=Capture();live=false;notice="坐标已固定，可复制或保存。";});y+=54;
  label=InterfaceMenu.Field(new Rect(x,y,w,34),label);y+=50;
  if(snapshot!=null){InterfaceMenu.Label(new Rect(x,y,w,26),"地图："+snapshot.Map,null,14);y+=34;InterfaceMenu.Label(new Rect(x,y,w,28),snapshot.Player.Line,InterfaceMenu.Accent,17);y+=38;InterfaceMenu.Label(new Rect(x,y,w,24),"朝向："+snapshot.Rotation.Line,null,12);y+=38;float third=(w-16)/3;
   if(InterfaceMenu.Button(new Rect(x,y,third,34),"复制玩家坐标"))Action(()=>{var p=Current().Player;GUIUtility.systemCopyBuffer=string.Join(", ",new[]{p.X,p.Y,p.Z}.Select(n=>n.ToString("G9",CultureInfo.InvariantCulture)));notice="玩家坐标已复制。";});
   if(InterfaceMenu.Button(new Rect(x+third+8,y,third,34),"复制完整 JSON"))Action(()=>{GUIUtility.systemCopyBuffer=Json();notice="完整记录已复制。";});
   if(InterfaceMenu.Button(new Rect(x+2*(third+8),y,third,34),"保存坐标记录"))Action(Save);y+=52;
   InterfaceMenu.Label(new Rect(x,y,w,26),"准星射线命中（由近到远）",InterfaceMenu.Accent,16);y+=38;
   foreach(var hit in snapshot.Hits){InterfaceMenu.Paragraph(new Rect(x,y,w-108,56),hit.Object+" · "+hit.Distance.ToString("F2")+" 米\n"+hit.Position.Line,null,12);if(InterfaceMenu.Button(new Rect(x+w-100,y+5,100,30),"复制命中点"))Action(()=>{Current();GUIUtility.systemCopyBuffer=JsonSerializer.Serialize(hit,json);notice="命中点已复制。";});y+=70;}
   if(snapshot.Hits.Length==0){InterfaceMenu.Label(new Rect(x,y,w,26),"准星前方 12 米内未命中场景物体。",null,12);y+=38;}
  }
  InterfaceMenu.Paragraph(new Rect(x,y,w,66),notice.Length>0?notice:"记录保存在辅助 mod 的 coordinates 文件夹，latest.json 为最近一次保存。",InterfaceMenu.Accent,12);GUI.EndScrollView();
 }
 internal static object Diagnostics(){try{return new{available=true,coordinates=Capture()};}catch(Exception error){return new{available=false,error=error.Message};}}
}