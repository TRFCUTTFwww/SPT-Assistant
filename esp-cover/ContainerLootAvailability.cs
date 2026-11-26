using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ContainerLootAvailability {
 static readonly HashSet<string> templates=new(StringComparer.Ordinal);
 static Task<JsonElement>? pending;static string map="",requestedMap="",error="";static bool ready,bridge;static float retry;
 internal static void Read(GameWorld world){
  if(pending?.IsCompleted==true){var done=pending;pending=null;try{var result=done.GetAwaiter().GetResult();if(requestedMap==world.LocationId){templates.Clear();foreach(var tpl in result.GetProperty("serverLootTemplates").EnumerateArray())templates.Add(tpl.GetString()!);map=requestedMap;ready=true;bridge=result.GetProperty("serverLootReady").GetBoolean();error="";}}catch(Exception e){error=e.GetBaseException().Message;retry=Time.unscaledTime+10;}}
  if(pending==null&&(map!=world.LocationId||!ready)&&Time.unscaledTime>=retry){requestedMap=world.LocationId;pending=WorldEditorClient.Call("catalog",requestedMap);}
 }
 internal static bool Allows(GameWorld world,string template)=>ready&&bridge&&map==world.LocationId&&templates.Contains(template);
 internal static string Reason(GameWorld world,string template)=>Allows(world,template)?"服务端物资表可用。":error.Length>0?"服务端物资配置读取失败："+error:!ready||map!=world.LocationId?"正在检查服务端物资表；可先选择空容器或自定义物资。":!bridge?"服务端容器生成接口未就绪；可选择空容器或自定义物资。":"当前地图没有此容器的物资表；可选择空容器或自定义物资。";
 internal static void Reset(){if(pending is {} task)_=task.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);pending=null;templates.Clear();map=requestedMap=error="";ready=bridge=false;retry=0;}
}
