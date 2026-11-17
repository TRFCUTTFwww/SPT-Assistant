using System;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
// Server-only view. IL2CPP wrappers expose Pointer (IntPtr), which JSON cannot serialize.
internal static class AiServerInfo {
 sealed record Snapshot(string Map,string Updated,AiSpawnInfoRows.Row[] Rows);
 static Task<Snapshot>? request;static Snapshot? snapshot;
 static IntPtr world;static string map="",notice="",search="",lastSearch="";static int group=1,lastGroup=-1,wave,lastWave=-1;
 static float retryAt,layoutWidth=-1,contentHeight;static Vector2 scroll;static bool dirty=true;
 static AiSpawnInfoRows.Row[] visible=Array.Empty<AiSpawnInfoRows.Row>();
 static float[] tops=Array.Empty<float>(),heights=Array.Empty<float>(),titles=Array.Empty<float>();static GUIStyle? measure;
 static void ObserveWorld(){
  var current=Singleton<GameWorld>.Instance;var pointer=RaidClock.Active&&current!=null?current.Pointer:IntPtr.Zero;
  string location=pointer==IntPtr.Zero?"":AiSpawnInfoRows.NormalizeMap(current!.LocationId);
  if(world==pointer&&map==location)return;Reset();world=pointer;map=location;
 }
 static async Task<Snapshot> Read(string location){
  var result=await WorldEditorClient.Call("aiInfo",location).ConfigureAwait(false);
  return await Task.Run(()=>{
   if(AiSpawnInfoRows.NormalizeMap(AiSpawnInfoRows.Get(result,"map").ToString())!=location)throw new InvalidOperationException("服务端返回了其他地图的配置，请刷新重试。");
   string updated=AiSpawnInfoRows.Get(result,"updatedUtc").ToString();if(DateTimeOffset.TryParse(updated,out var time))updated=time.ToLocalTime().ToString("HH:mm:ss");
   return new Snapshot(location,updated,AiSpawnInfoRows.Build(result));
  }).ConfigureAwait(false);
 }
 static void Fetch(){if(request!=null||world==IntPtr.Zero||map.Length==0)return;request=Read(map);notice="正在读取服务端 AI 生成参数…";}
 static void Finish(){
  if(request?.IsCompleted!=true)return;var done=request;request=null;
  try{snapshot=done.GetAwaiter().GetResult();notice="服务端读取成功 · "+snapshot.Updated;dirty=true;}
  catch(Exception error){notice="读取失败："+error.GetBaseException().Message+(snapshot==null?"；15 秒后重试，也可手动刷新。":"；保留上次结果，请刷新重试。");retryAt=Time.unscaledTime+15;}
 }
 internal static void Reset(){
  if(request is {} pending)_=pending.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
  request=null;snapshot=null;world=IntPtr.Zero;map=notice="";retryAt=0;wave=0;scroll=Vector2.zero;visible=Array.Empty<AiSpawnInfoRows.Row>();tops=heights=titles=Array.Empty<float>();dirty=true;measure=null;layoutWidth=-1;
 }
 internal static void Draw(Rect rect){
  ObserveWorld();Finish();if(snapshot==null&&request==null&&Time.unscaledTime>=retryAt)Fetch();
  float x=rect.x,y=rect.y,w=rect.width;
  InterfaceMenu.Label(new(x,y,Math.Max(80,w-126),30),"本局 AI 生成参数",InterfaceMenu.Accent,19);
  bool enabled=GUI.enabled;try{GUI.enabled=enabled&&world!=IntPtr.Zero&&request==null;if(InterfaceMenu.Button(new(rect.xMax-116,y,116,30),request!=null?"读取中…":"刷新服务端"))Fetch();}finally{GUI.enabled=enabled;}y+=38;
  InterfaceMenu.Paragraph(new(x,y,w,42),world==IntPtr.Zero?"进入战局后自动读取当前地图的服务端配置。":"地图："+AiSpawnInfoRows.MapName(map)+" · "+notice,null,12);y+=48;
  InterfaceMenu.Paragraph(new(x,y,w,52),"这里解释服务端当前的 AI 生成配置。人数上限、波次数量和刷新概率决定不同环节，不能直接相加当作本局总人数。进局后改动的配置不一定会重新应用到本局。",null,12);y+=58;
  InterfaceMenu.Label(new(x,y,90,30),"搜索参数",null,12);search=InterfaceMenu.Field(new(x+94,y,Math.Max(60,w-94),30),search);y+=36;
  int columns=Math.Max(1,(int)(w/145));float cell=(w-(columns-1)*6)/columns;
  for(int i=0;i<AiSpawnInfoRows.Groups.Length;i++)if(InterfaceMenu.Button(new(x+i%columns*(cell+6),y+i/columns*32,cell,28),AiSpawnInfoRows.Groups[i],group==i)){group=i;wave=0;scroll=Vector2.zero;}
  y+=((AiSpawnInfoRows.Groups.Length+columns-1)/columns)*32+6;
  if(snapshot==null){InterfaceMenu.Paragraph(new(x,y,w,Math.Max(40,rect.yMax-y)),world==IntPtr.Zero?"此页只读配置。暂停生成等操作仍在「全局控制」。":"尚未取得服务端数据。成功读取后会显示参数值和作用说明。",null,13);return;}
  string waveRoot=group==4?"mapAi.Waves":group==5?"mapAi.BossLocationSpawn":"";
  bool browseWave=waveRoot.Length>0&&string.IsNullOrWhiteSpace(search);
  if(browseWave){
   int count=snapshot.Rows.Count(r=>r.Path.StartsWith(waveRoot+"[",StringComparison.Ordinal)&&r.Path.EndsWith(group==4?".WildSpawnType":".BossName",StringComparison.Ordinal));
   wave=Math.Clamp(wave,0,Math.Max(0,count-1));
   try{GUI.enabled=enabled&&wave>0;if(InterfaceMenu.Button(new(x,y,90,28),"上一批"))wave--;GUI.enabled=enabled&&wave<count-1;if(InterfaceMenu.Button(new(rect.xMax-90,y,90,28),"下一批"))wave++;}finally{GUI.enabled=enabled;}
   InterfaceMenu.Label(new(x+100,y,Math.Max(60,w-200),28),count==0?"没有候选波次":$"第 {wave+1} / {count} 批 · 搜索可跨批次查找",null,12);y+=36;
  }
  if(lastGroup!=group||lastSearch!=search||lastWave!=wave){lastGroup=group;lastSearch=search;lastWave=wave;dirty=true;scroll=Vector2.zero;}
  float width=Math.Max(100,w-26);
  if(dirty||layoutWidth!=width){
   dirty=false;layoutWidth=width;visible=snapshot.Rows.Where(r=>(group==0||r.Group==AiSpawnInfoRows.Groups[group])&&(!browseWave||r.Path==waveRoot||r.Path.StartsWith(waveRoot+"["+wave+"].",StringComparison.Ordinal))&&AiSpawnInfoRows.Matches(r,search)).ToArray();
   measure??=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=12,richText=false};tops=new float[visible.Length];heights=new float[visible.Length];titles=new float[visible.Length];contentHeight=0;
   for(int i=0;i<visible.Length;i++){var row=visible[i];tops[i]=contentHeight;titles[i]=Math.Max(26,measure.CalcHeight(new GUIContent(row.Name+"  ·  "+row.Value),width-20)+8);heights[i]=titles[i]+Math.Max(38,measure.CalcHeight(new GUIContent(row.Help),width-20)+8)+Math.Max(20,measure.CalcHeight(new GUIContent(row.Path),width-20))+18;contentHeight+=heights[i]+8;}
  }
  float viewport=Math.Max(30,rect.yMax-y);scroll=GUI.BeginScrollView(new(x,y,w,viewport),scroll,new Rect(0,0,width,Math.Max(viewport-1,contentHeight)));
  try{
   if(visible.Length==0)InterfaceMenu.Paragraph(new(8,8,width-16,60),"没有匹配参数。可搜索中文含义、英文参数名、角色代号或数值。",null,12);
   for(int i=0;i<visible.Length;i++){float top=tops[i],height=heights[i];if(top+height<scroll.y||top>scroll.y+viewport)continue;var row=visible[i];InterfaceMenu.Surface(new(0,top,width,height),new Color(.2f,.25f,.3f,.16f),6);InterfaceMenu.Paragraph(new(10,top+6,width-20,titles[i]),row.Name+"  ·  "+row.Value,InterfaceMenu.Accent,12);float pathHeight=Math.Max(20,measure!.CalcHeight(new GUIContent(row.Path),width-20));InterfaceMenu.Paragraph(new(10,top+6+titles[i],width-20,height-titles[i]-pathHeight-18),row.Help,null,12);InterfaceMenu.Paragraph(new(10,top+height-pathHeight-6,width-20,pathHeight),row.Path,Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.35f),11);}
  }finally{GUI.EndScrollView();}
 }
}
