using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class RaidParameterPanel {
 static readonly Page ai=new("AI"),raid=new("战局");static bool editAi;
 internal static void Reset(){ai.Reset();raid.Reset();editAi=false;}
 internal static void DrawAi(Rect rect){float w=(rect.width-8)/2;if(InterfaceMenu.Button(new(rect.x,rect.y,w,30),"生成参数说明",!editAi))editAi=false;if(InterfaceMenu.Button(new(rect.x+w+8,rect.y,w,30),"修改参数 / 配置文件",editAi))editAi=true;rect.yMin+=38;if(editAi)ai.Draw(rect);else AiServerInfo.Draw(rect);}
 internal static void DrawRaid(Rect rect)=>raid.Draw(rect);
 sealed class Page(string group){
  static readonly string[] Maps={"bigmap","woods","shoreline","interchange","rezervbase","lighthouse","tarkovstreets","laboratory","labyrinth","factory4_day","factory4_night","sandbox","sandbox_high"};
  string map="bigmap",revision="",notice="",search="",name="默认方案",confirm="",operation="";int presetIndex;bool started;IntPtr world;GUIStyle? style;bool dirty=true;float layoutWidth=-1;JsonElement[] visible=Array.Empty<JsonElement>();float[] heights=Array.Empty<float>();
  Task<JsonElement>? pending;JsonElement[] rows=Array.Empty<JsonElement>();string[] presets=Array.Empty<string>();Vector2 scroll;readonly Dictionary<string,string> drafts=new();
  internal void Reset(){if(pending is {} task)_=task.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);pending=null;rows=Array.Empty<JsonElement>();drafts.Clear();revision=notice=confirm="";started=false;world=IntPtr.Zero;scroll=Vector2.zero;presets=Array.Empty<string>();presetIndex=0;dirty=true;name=map+"_"+group;}
  void Request(string action){try{
   if(pending!=null)return;if(drafts.Count>0&&action is "read" or "load" or "defaults" or "save"){if(confirm!=action){confirm=action;notice=action=="save"?"保存的是已应用参数；请先应用修改，或再次点击以保存服务端当前值。":"还有未应用的输入；再次点击将丢弃输入并继续。";return;}}
   var edits=new Dictionary<string,object>();if(action=="apply")foreach(var row in rows){string id=row.GetProperty("id").GetString()!;if(!drafts.TryGetValue(id,out string? text))continue;if(row.GetProperty("boolean").GetBoolean())edits[id]=bool.Parse(text);else {if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out double v)||!double.IsFinite(v))throw new ArgumentException("请输入有效数字："+row.GetProperty("name").GetString());edits[id]=v;}}
   confirm="";operation=action;pending=WorldEditorClient.Call("parameters",map,parameters:new{operation=action,group,name,revision,edits});notice="正在访问服务端…";
  }catch(Exception e){notice=e.GetBaseException().Message;}}
  void Finish(){if(pending?.IsCompleted!=true)return;var done=pending;pending=null;try{var data=done.GetAwaiter().GetResult();if(data.GetProperty("map").GetString()!=map)throw new InvalidOperationException("地图不匹配，请刷新。");revision=data.GetProperty("revision").GetString()!;rows=data.GetProperty("rows").EnumerateArray().Where(r=>r.GetProperty("group").GetString()==group).Select(r=>r.Clone()).ToArray();presets=data.GetProperty("presets").EnumerateArray().Select(p=>p.GetString()!).ToArray();drafts.Clear();dirty=true;notice=operation switch{"apply"=>"已应用并持久保存；下次地图生成时生效。","save"=>"方案已保存到服务端 parameter-presets/"+name+".json。","load"=>"方案已载入并应用；下次地图生成时生效。","defaults"=>"已恢复当前地图的默认值及本分组助手默认值。",_=>"已读取服务端参数。"};}catch(Exception e){notice="操作失败："+e.GetBaseException().Message+"；可手动刷新重试。";}}
  internal void Draw(Rect r){
   var current=Singleton<GameWorld>.Instance;var pointer=RaidClock.Active&&current!=null?current.Pointer:IntPtr.Zero;if(pointer!=world){if(pointer!=IntPtr.Zero)map=AiSpawnInfoRows.NormalizeMap(current!.LocationId);Reset();world=pointer;}
   Finish();if(!started){started=true;if(name=="默认方案")name=map+"_"+group;Request("read");}float x=r.x,y=r.y,w=r.width;bool enabled=GUI.enabled;
   InterfaceMenu.Label(new(x,y,w,28),group=="AI"?"服务端 AI 参数与方案":"战局信息 · 服务端参数",InterfaceMenu.Accent,18);y+=34;
   try{GUI.enabled=enabled&&pending==null&&drafts.Count==0;int index=Array.IndexOf(Maps,map);if(InterfaceMenu.Button(new(x,y,70,28),"上一图")){map=Maps[(index+Maps.Length-1)%Maps.Length];Reset();world=pointer;}if(InterfaceMenu.Button(new(r.xMax-70,y,70,28),"下一图")){map=Maps[(index+1)%Maps.Length];Reset();world=pointer;}GUI.enabled=enabled;InterfaceMenu.Label(new(x+78,y,w-156,28),AiSpawnInfoRows.MapName(map)+(pointer!=IntPtr.Zero?" · 本局 "+AiSpawnInfoRows.MapName(current!.LocationId):" · 可在进局前配置"),null,12);y+=34;
   InterfaceMenu.Paragraph(new(x,y,w,54),"地图参数按地图保存，助手覆盖也仅作用于所选地图。应用 / 载入 / 恢复默认从下次地图生成生效；当前已生成的 AI、物资和计时不会重建。",null,12);y+=60;
   GUI.enabled=enabled&&pending==null;float cell=(w-18)/4;
   if(InterfaceMenu.Button(new(x,y,cell,28),"刷新"))Request("read");GUI.enabled=enabled&&pending==null&&drafts.Count>0;if(InterfaceMenu.Button(new(x+cell+6,y,cell,28),"应用修改 ("+drafts.Count+")"))Request("apply");GUI.enabled=enabled&&pending==null&&revision.Length>0;
   if(InterfaceMenu.Button(new(x+2*(cell+6),y,cell,28),"恢复默认"))Request("defaults");if(InterfaceMenu.Button(new(x+3*(cell+6),y,cell,28),"撤销输入")){drafts.Clear();confirm="";}y+=34;
   name=InterfaceMenu.Field(new(x,y,Math.Max(100,w-190),28),name);if(InterfaceMenu.Button(new(r.xMax-182,y,88,28),"保存到文件"))Request("save");if(InterfaceMenu.Button(new(r.xMax-88,y,88,28),"载入文件"))Request("load");y+=34;
   if(presets.Length>0){if(InterfaceMenu.Button(new(x,y,100,26),"切换已存方案")){presetIndex=(presetIndex+1)%presets.Length;name=presets[presetIndex];}InterfaceMenu.Label(new(x+108,y,w-108,26),"已存 "+presets.Length+" 个 · "+name,null,11);y+=30;}
   GUI.enabled=enabled;InterfaceMenu.Paragraph(new(x,y,w,46),notice,null,12);y+=52;
   InterfaceMenu.Label(new(x,y,70,28),"搜索参数",null,12);string next=InterfaceMenu.Field(new(x+76,y,w-76,28),search);if(next!=search){search=next;scroll=Vector2.zero;dirty=true;}y+=34;
   float width=Math.Max(150,w-22),height=Math.Max(30,r.yMax-y);style??=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=12};if(dirty||layoutWidth!=width){dirty=false;layoutWidth=width;visible=rows.Where(row=>string.IsNullOrWhiteSpace(search)||(row.GetProperty("name").GetString()+" "+row.GetProperty("id").GetString()+" "+row.GetProperty("help").GetString()).Contains(search,StringComparison.OrdinalIgnoreCase)).ToArray();
   heights=visible.Select(row=>Math.Max(30,style.CalcHeight(new GUIContent(row.GetProperty("name").GetString()),width-20))+Math.Max(40,style.CalcHeight(new GUIContent(row.GetProperty("help").GetString()),width-20))+86).ToArray();}scroll=GUI.BeginScrollView(new(x,y,w,height),scroll,new Rect(0,0,width,Math.Max(height-1,heights.Sum())));
   try{float top=0;for(int i=0;i<visible.Length;i++){var row=visible[i];float h=heights[i];if(top+h>=scroll.y&&top<scroll.y+height){string id=row.GetProperty("id").GetString()!,value=row.GetProperty("value").ToString();bool boolean=row.GetProperty("boolean").GetBoolean();string draft=drafts.GetValueOrDefault(id,value);InterfaceMenu.Surface(new(0,top,width,h-6),new Color(.2f,.25f,.3f,.16f),6);float title=Math.Max(30,style.CalcHeight(new GUIContent(row.GetProperty("name").GetString()),width-20));InterfaceMenu.Paragraph(new(10,top+4,width-20,title),row.GetProperty("name").GetString()!,InterfaceMenu.Accent,12);InterfaceMenu.Paragraph(new(10,top+title+4,width-20,h-title-86),row.GetProperty("help").GetString()!,null,12);GUI.enabled=enabled&&pending==null;float inputY=top+h-76;if(boolean){bool v=bool.TryParse(draft,out bool b)&&b;if(InterfaceMenu.Button(new(10,inputY,110,28),v?"开启":"关闭",v))draft=(!v).ToString();}else draft=InterfaceMenu.Field(new(10,inputY,Math.Min(170,width*.4f),28),draft);if(draft!=value)drafts[id]=draft;else drafts.Remove(id);GUI.enabled=enabled;InterfaceMenu.Label(new(10+(boolean?120:Math.Min(170,width*.4f)+10),inputY,width-(boolean?140:Math.Min(170,width*.4f)+30),28),"默认 "+(boolean?(row.GetProperty("defaultValue").GetBoolean()?"开启":"关闭"):row.GetProperty("defaultValue").ToString())+(boolean?"":" · "+row.GetProperty("minimum")+"～"+row.GetProperty("maximum")),null,11);InterfaceMenu.Label(new(10,top+h-42,width-20,30),id.Replace('/','.'),null,10);}top+=h;}}
   finally{GUI.EndScrollView();}
   }finally{GUI.enabled=enabled;}
  }
 }
}
