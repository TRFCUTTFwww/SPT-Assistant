using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AiServerInfo {
 sealed record Row(string Path,string Value);
 static Task<JsonElement>? request;static JsonElement? data;static string map="",notice="",filter="",role="",sourceMap="";static Vector2 scroll;static Row[] rows=Array.Empty<Row>();
 static IntPtr requestedWorld;static float nextLive;
 static readonly HashSet<string> groups=new(StringComparer.Ordinal){"mapAi","botConfig","pmcConfig","currentRaid"};
 static readonly JsonSerializerOptions json=new(){WriteIndented=true};
 static void Fetch(){if(request!=null)return;var current=Singleton<GameWorld>.Instance;if(!RaidClock.Active||current==null){notice="请进入战局后读取当前地图配置。";return;}sourceMap=current.LocationId;requestedWorld=current.Pointer;request=WorldEditorClient.Call("aiInfo",sourceMap,role:role.Trim());notice="正在读取服务端实时配置…";}
 static void Finish(){if(request?.IsCompleted!=true)return;var pending=request;request=null;try{var result=pending.GetAwaiter().GetResult();var current=Singleton<GameWorld>.Instance;if(!RaidClock.Active||current?.LocationId!=sourceMap||current.Pointer!=requestedWorld)return;data=result;map=sourceMap;notice="服务端读取时间："+result.GetProperty("updatedUtc").GetString();Rebuild();}catch(Exception e){notice=e.GetBaseException().Message;}}
 static object Scalars(object? owner){var result=new Dictionary<string,object?>();if(owner==null)return result;foreach(var p in owner.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance)){if(p.GetIndexParameters().Length>0||!(p.PropertyType.IsEnum||p.PropertyType.IsPrimitive||p.PropertyType==typeof(string)))continue;try{var v=p.GetValue(owner);result[p.Name]=p.PropertyType.IsEnum?v?.ToString():v;}catch{}}return result;}
 static object CurrentRaid(){
  var current=Singleton<GameWorld>.Instance;var actors=new List<object>();if(current?.AllAlivePlayersList is {} list)for(int i=0;i<list.Count;i++){var p=list[i];if(p==null||p.IsYourPlayer||p.HealthController?.IsAlive!=true||p.AIData?.BotOwner==null)continue;actors.Add(new{role=p.Profile?.Info?.Settings?.Role.ToString(),difficulty=Scalars(p.Profile?.Info?.Settings)});}
  var settings=UnityEngine.Object.FindFirstObjectByType<TarkovApplication>()?._raidSettings;
  return new{map=current?.LocationId,raidActive=RaidClock.Active,naturalSpawningPaused=RaidSpawnControl.SpawnPaused,noAi=RaidSpawnControl.NoAiActive,interceptedCalls=RaidSpawnControl.BlockedSpawns,botSettings=Scalars(settings?.BotSettings),wavesSettings=Scalars(settings?.WavesSettings),aliveActors=actors,customDifficulty=AiDifficultyControl.Read(),customApplied=AiDifficultyControl.AppliedCount};
 }
 static void Flatten(JsonElement value,string path,List<Row> result){if(value.ValueKind==JsonValueKind.Object){foreach(var p in value.EnumerateObject())Flatten(p.Value,path.Length==0?p.Name:path+"."+p.Name,result);}else if(value.ValueKind==JsonValueKind.Array){int i=0;foreach(var item in value.EnumerateArray())Flatten(item,path+"["+(i++)+"]",result);if(i==0)result.Add(new(path,"[]"));}else result.Add(new(path,value.ToString()));}
 static void Rebuild(){var list=new List<Row>();using var live=JsonDocument.Parse(JsonSerializer.Serialize(CurrentRaid()));Flatten(live.RootElement,"currentRaid",list);if(data is {} value)foreach(var p in value.EnumerateObject())if(p.Name!="ok")Flatten(p.Value,p.Name,list);rows=list.ToArray();}
 internal static void Reset(){request=null;data=null;rows=Array.Empty<Row>();map=sourceMap="";notice="";}
 internal static void Draw(Rect rect){
  Finish();if(RaidClock.Active&&Time.unscaledTime>=nextLive){nextLive=Time.unscaledTime+1;try{Rebuild();}catch(Exception e){notice=e.Message;}}var current=Singleton<GameWorld>.Instance;if(map.Length>0&&(!RaidClock.Active||current?.LocationId!=map)){data=null;map="";rows=Array.Empty<Row>();}
  float x=rect.x,y=rect.y,w=rect.width;bool old=GUI.enabled;
  InterfaceMenu.Label(new(x,y,w,30),"本局 AI 与服务端配置",InterfaceMenu.Accent,19);y+=38;RaidSpawnControl.DrawSpawnPause(new(x,y,w,34));y+=42;
  InterfaceMenu.Paragraph(new(x,y,w,48),"暂停仅拦截后续自然生成，保留场上 AI；手动生成可用。恢复不补发已拦截批次。配置区分本局客户端快照与服务端当前值，服务端改动可能下局才生效。",null,12);y+=56;
  GUI.enabled=old&&request==null&&RaidClock.Active;if(InterfaceMenu.Button(new(x,y,110,30),"读取 / 刷新"))Fetch();
  if(InterfaceMenu.Button(new(x+120,y,110,30),"复制完整信息")){try{GUIUtility.systemCopyBuffer=JsonSerializer.Serialize(new{currentRaid=CurrentRaid(),server=data},json);notice="已复制客户端快照与服务端信息。";}catch(Exception e){notice=e.Message;}}
  GUI.enabled=old;role=InterfaceMenu.Field(new(x+240,y,Math.Max(60,w-240),30),role);y+=38;
  InterfaceMenu.Paragraph(new(x,y,w,38),"右侧输入角色代号后刷新，可读取该角色完整配置；留空列出角色目录。可搜索 difficulty、max、limit、bear、usec、scav、wave 或参数路径。",null,11);y+=44;
  filter=InterfaceMenu.Field(new(x,y,w,30),filter);y+=38;InterfaceMenu.Paragraph(new(x,y,w,34),notice,null,11);y+=40;
  if(data==null&&rows.Length==0&&RaidClock.Active)try{Rebuild();}catch(Exception e){notice=e.Message;}
  var visible=rows.Where(r=>filter.Length==0?groups.Contains(r.Path.Split('.')[0]):(r.Path+" "+r.Value).Contains(filter,StringComparison.OrdinalIgnoreCase)).ToArray();
  var groupNames=new[]{"currentRaid","mapAi","botConfig","pmcConfig","locationConfig","core","assistant","role","roles"};
  var labels=new[]{"本局实际值","地图 AI / 波次","Bot 全局配置","PMC / 阵营","地图全局配置","AI 核心","助手服务端","指定角色","角色目录"};
  float cell=(w-21)/4;for(int i=0;i<groupNames.Length;i++){string name=groupNames[i];if(InterfaceMenu.Button(new(x+i%4*(cell+7),y+i/4*34,cell,28),labels[i],groups.Contains(name))){if(!groups.Add(name))groups.Remove(name);}}
  y+=110;float height=Math.Max(60,rect.yMax-y);scroll=GUI.BeginScrollView(new(x,y,w,height),scroll,new Rect(0,0,w-22,visible.Length*52+40));
  try{float cw=w-28;int first=Math.Max(0,(int)(scroll.y/52)-1),last=Math.Min(visible.Length,first+(int)(height/52)+4);for(int i=first;i<last;i++){var row=visible[i];if(i%2==0)InterfaceMenu.Surface(new(0,i*52,cw,50),new Color(.2f,.25f,.3f,.15f),2);InterfaceMenu.Label(new(6,i*52,cw-12,22),row.Path,InterfaceMenu.Accent,11);InterfaceMenu.Paragraph(new(6,i*52+23,cw-12,25),row.Value,null,12);}}finally{GUI.EndScrollView();}
 }
}
