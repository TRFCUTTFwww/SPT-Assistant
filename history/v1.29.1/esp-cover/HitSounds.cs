using System;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT.UI;
using UnityEngine;
using UnityEngine.Networking;
namespace LocalSpt5Esp;
internal static class HitSounds {
 internal static ConfigEntry<bool> Enabled=null!;
 static ConfigEntry<int> mode=null!;static ConfigEntry<float> volume=null!;static ConfigEntry<string> file=null!;
 static UnityWebRequest? loading;static AudioClip? custom;static string observed="",status="",loadingPath="";static DateTime stamp;static float nextPoll,nextHit,nextBuiltin;
 static bool previewAfterLoad,validatedBuiltins;static Vector2 scroll;
 internal static string Home=>Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","Sounds");
 internal static void Initialize(){
  const string section="45. 命中音效";
  Enabled=FeatureConfig.Bind(section,"Hit sound","命中音效",true,"仅在玩家确认命中角色后播放，跳弹及未命中不播放。");
  mode=FeatureConfig.Bind(section,"Hit sound choice","音效选择（0投保／1任务子目标／2自定义）",0,"可在射击 → 命中音效中选择和试听。",new AcceptableValueRange<int>(0,2));
  volume=FeatureConfig.Bind(section,"Hit sound volume","命中音量",.8f,"仍受游戏 UI 音量设置影响。",new AcceptableValueRange<float>(0,1));
  file=FeatureConfig.Bind(section,"Hit sound file","自定义音频路径","","支持本地 WAV、OGG、MP3；可填完整路径或 Sounds 文件夹内的文件名。");Directory.CreateDirectory(Home);
 }
 static string ChosenPath()=>string.IsNullOrWhiteSpace(file.Value)?"":Path.GetFullPath(Path.IsPathRooted(file.Value.Trim(' ','"'))?file.Value.Trim(' ','"'):Path.Combine(Home,file.Value.Trim(' ','"')));
 static void ClearClip(){if(custom!=null)UnityEngine.Object.Destroy(custom);custom=null;}
 static void CancelLoad(){if(loading==null)return;try{loading.Abort();loading.Dispose();}catch{}loading=null;}
 internal static void Shutdown(){CancelLoad();ClearClip();observed="";stamp=default;previewAfterLoad=false;nextHit=0;}
 internal static void Tick(){
  try{
   if(!validatedBuiltins&&Time.unscaledTime>=nextBuiltin){nextBuiltin=Time.unscaledTime+2;if(GUISounds.Exist(out var native)&&native?._UISounds is {} library){var insurance=library.GetUIClip(EUISoundType.InsuranceInsured);var quest=library.GetUIClip(EUISoundType.QuestSubTrackComplete);if(insurance!=null&&quest!=null){validatedBuiltins=true;Plugin.Self.Log.LogInfo($"Hit sound built-ins ready: insurance={insurance.length:F2}s, questSubTrackComplete={quest.length:F2}s; no preview played");}}}
   if(loading!=null&&loading.isDone){
    var request=loading;loading=null;
    try{if(request.result!=UnityWebRequest.Result.Success)throw new IOException(request.error);var clip=DownloadHandlerAudioClip.GetContent(request);if(clip==null||clip.length<=0)throw new IOException("音频未能解码。");ClearClip();custom=clip;status="已加载："+Path.GetFileName(loadingPath);if(previewAfterLoad){previewAfterLoad=false;Play(true);}}
    finally{request.Dispose();}
   }
   if(Time.unscaledTime<nextPoll)return;nextPoll=Time.unscaledTime+1;
   if(mode.Value!=2)return;
   string path=ChosenPath();DateTime changed=File.Exists(path)?File.GetLastWriteTimeUtc(path):default;
   if(path==observed&&stamp==changed)return;
   observed=path;stamp=changed;CancelLoad();ClearClip();
   if(path.Length==0){status="请选择自定义音频；当前回退到投保音效。";return;}
   if(!File.Exists(path))throw new FileNotFoundException("找不到自定义音频；当前回退到投保音效。",path);
   var type=Path.GetExtension(path).ToLowerInvariant() switch{".wav"=>AudioType.WAV,".ogg"=>AudioType.OGGVORBIS,".mp3"=>AudioType.MPEG,_=>throw new ArgumentException("请选择 WAV、OGG 或 MP3 音频。")};
   loadingPath=path;loading=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,type);loading.timeout=15;loading.SendWebRequest();status="正在加载："+Path.GetFileName(path);
  }catch(Exception error){status=error.Message;previewAfterLoad=false;Plugin.Self.Log.LogWarning("Hit sound: "+error.Message);}
 }
 internal static void Confirmed(){if(!Enabled.Value||Time.unscaledTime<nextHit)return;nextHit=Time.unscaledTime+.06f;try{Play(false);}catch(Exception error){status=error.Message;}}
 static void Play(bool preview){
  if(!GUISounds.Exist(out var sounds)||sounds==null){status="游戏声音系统尚未就绪。";return;}
  AudioClip? clip=mode.Value==2?custom:null;
  if(mode.Value==2&&clip==null&&preview&&loading!=null){previewAfterLoad=true;return;}
  clip??=sounds._UISounds?.GetUIClip(mode.Value==1?EUISoundType.QuestSubTrackComplete:EUISoundType.InsuranceInsured);
  if(clip==null){status="所选游戏音效尚未加载。";return;}
  sounds.PlaySound(clip,false,true,volume.Value);
 }
 static void Choose(int value){mode.Value=value;nextPoll=0;previewAfterLoad=false;}
 internal static void Draw(Rect rect){
  float x=rect.x,y=rect.y,w=rect.width;InterfaceMenu.Label(new Rect(x,y,w,32),"命中音效",InterfaceMenu.Accent,21);y+=48;
  if(InterfaceMenu.Button(new Rect(x,y,w,36),Enabled.Value?"命中音效 · 开启":"命中音效 · 关闭",Enabled.Value))Enabled.Value=!Enabled.Value;y+=52;
  string[] choices={"投保完成","任务子目标完成","自定义文件"};float col=(w-16)/3;for(int i=0;i<choices.Length;i++)if(InterfaceMenu.Button(new Rect(x+i*(col+8),y,col,36),choices[i],mode.Value==i))Choose(i);y+=54;
  InterfaceMenu.Label(new Rect(x,y,90,30),"音量",null,14);volume.Value=GUI.HorizontalSlider(new Rect(x+100,y+10,Math.Max(80,w-210),22),volume.Value,0,1);InterfaceMenu.Label(new Rect(x+w-90,y,90,30),(volume.Value*100).ToString("F0")+"%",null,14);y+=48;
  if(InterfaceMenu.Button(new Rect(x,y,180,36),"试听当前音效")){Tick();try{Play(true);}catch(Exception error){status=error.Message;}}
  if(mode.Value==2&&InterfaceMenu.Button(new Rect(x+194,y,180,36),"重新加载音频")){observed="\0";nextPoll=0;Tick();}y+=54;
  if(mode.Value==2){
   InterfaceMenu.Label(new Rect(x,y,w,26),"音频路径（完整路径或文件名）",null,14);y+=34;file.Value=InterfaceMenu.Field(new Rect(x,y,w,34),file.Value);y+=48;
   InterfaceMenu.Paragraph(new Rect(x,y,w,62),"也可把 WAV／OGG／MP3 放入以下目录后点击列表选择：\n"+Home,null,12);y+=78;
   var files=Directory.EnumerateFiles(Home).Where(path=>new[]{".wav",".ogg",".mp3"}.Contains(Path.GetExtension(path).ToLowerInvariant())).ToArray();
   var area=new Rect(x,y,w,Math.Max(60,rect.yMax-y-82));scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,w-20,Math.Max(40,files.Length*42)));for(int i=0;i<files.Length;i++)if(InterfaceMenu.Button(new Rect(0,i*42,w-28,34),Path.GetFileName(files[i]))){file.Value=files[i];nextPoll=0;}GUI.EndScrollView();
  }
  InterfaceMenu.Paragraph(new Rect(x,rect.yMax-68,w,64),status.Length>0?status:"玩家命中角色后播放；护甲挡住的确认命中也会播放。连续弹丸会合并，避免音效叠成噪声。",InterfaceMenu.Accent,13);
 }
}
