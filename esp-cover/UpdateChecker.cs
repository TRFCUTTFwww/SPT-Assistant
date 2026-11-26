using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using LocalSpt5Esp.HotReload;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class UpdateChecker {
 record Result(bool Ok,string Tag,string Message,bool Newer);
 static ConfigEntry<bool> automatic=null!,useProxy=null!;
 static ConfigEntry<string> proxy=null!,proxyUser=null!,proxyPassword=null!;
 static Task<Result>? pending;static CancellationTokenSource? stopping;
 static IHostServices? host;
 static Task<(string Message,bool Reload)>? localUpdate;
 static string localStatus="";
 static string status="尚未检查更新。";static bool newer;static float next;static Vector2 scroll;
 static string Cache=>Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","update-check.json");
 internal static void Initialize(IHostServices services){
  host=services;
  const string section="66. 版本更新";
  automatic=FeatureConfig.Bind(section,"Automatic update checks","自动检查 GitHub 更新",true,"启动后每 24 小时后台检查一次 GitHub 发布版本。");
  useProxy=FeatureConfig.Bind(section,"Use update proxy","更新检测使用代理",false,"启用且地址留空时使用系统代理。");
  proxy=FeatureConfig.Bind(section,"Update proxy address","更新代理地址","","支持 http://、https:// 或 socks5:// 代理地址。");
  proxyUser=FeatureConfig.Bind(section,"Update proxy username","代理用户名","","代理认证用户名。");
  proxyPassword=FeatureConfig.Bind(section,"Update proxy password","代理密码","","代理认证密码。");
  foreach(var tag in proxyPassword.Description.Tags)if(tag is CnConfig.ConfigurationManagerAttributes attributes)attributes.CustomDrawer=DrawSecret;
  stopping=new();next=Time.unscaledTime+10;newer=false;
  try{using var cache=JsonDocument.Parse(File.ReadAllText(Cache));var root=cache.RootElement;string tag=root.GetProperty("tag").GetString()!;newer=UpdateCheckRules.IsNewer(RuntimeModule.ReleaseVersion,tag);status=(newer?"发现新版本：":"上次检查的 GitHub 版本：")+tag;if(DateTime.UtcNow-root.GetProperty("utc").GetDateTime()<TimeSpan.FromHours(24))next=Time.unscaledTime+(float)Math.Max(60,(root.GetProperty("utc").GetDateTime().AddHours(24)-DateTime.UtcNow).TotalSeconds);}catch{}
 }
 internal static void Tick(){
  if(localUpdate?.IsCompleted==true){
   var result=localUpdate.GetAwaiter().GetResult();localUpdate=null;localStatus=result.Message;
   if(result.Reload)host!.RequestReload();
  }
  if(pending?.IsCompleted==true){var result=pending.GetAwaiter().GetResult();pending=null;status=result.Message;newer=result.Newer;next=Time.unscaledTime+(result.Ok?86400:600);if(result.Ok)try{Directory.CreateDirectory(Path.GetDirectoryName(Cache)!);File.WriteAllText(Cache+".tmp",JsonSerializer.Serialize(new{utc=DateTime.UtcNow,tag=result.Tag}));File.Move(Cache+".tmp",Cache,true);}catch{}}
  if(automatic.Value&&pending==null&&Time.unscaledTime>=next)Check();
 }
 static async Task<(string Message,bool Reload)> SelectLocalUpdate(string directory,CancellationToken token){
  string failure=I18n.Text("读取或安装热更新文件失败。");
  try{
   string? path=await WindowsDialogs.PickOpenFile(I18n.Text("选择本地热更新文件"),
    I18n.Text("核心热更新文件")+" (*.module;*.dll)\0*.module;*.dll\0").WaitAsync(token).ConfigureAwait(false);
   if(path==null)return ("",false);
   bool published=await Task.Run(()=>HotUpdateFile.Publish(path,directory,token),token).ConfigureAwait(false);
   return ("文件已提交，等待热更新…",!published);
  }catch(OperationCanceledException){return ("",false);}
  catch(InvalidDataException error){return (error.Message,false);}
  catch(Exception error){return (failure+" "+error.Message,false);}
 }
 static void Check(){
  if(pending!=null)return;
  bool enabled=useProxy.Value;string address=proxy.Value,user=proxyUser.Value,password=proxyPassword.Value;var token=stopping!.Token;
  status="正在检查 GitHub 发布版本…";
  pending=Task.Run(async()=>{
   try{
    using var handler=new HttpClientHandler{UseProxy=enabled};
    if(enabled){
     if(!string.IsNullOrWhiteSpace(address))handler.Proxy=new WebProxy(UpdateCheckRules.Proxy(address));
     if(!string.IsNullOrEmpty(user)){var credentials=new NetworkCredential(user,password);if(handler.Proxy!=null)handler.Proxy.Credentials=credentials;else handler.DefaultProxyCredentials=credentials;}
    }
    using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(20)};
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SPT-Assistant/"+RuntimeModule.ReleaseVersion);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version","2022-11-28");
    using var response=await client.GetAsync(UpdateCheckRules.Endpoint,HttpCompletionOption.ResponseHeadersRead,token);
    if(response.StatusCode==HttpStatusCode.NotFound)return new Result(false,"","GitHub 尚无可读取的发布版本。",false);
    if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)return new Result(false,"","GitHub 请求受限，请稍后重试。",false);
    response.EnsureSuccessStatusCode();
    await using var input=await response.Content.ReadAsStreamAsync(token);using var bytes=new MemoryStream();var buffer=new byte[8192];int count;
    while((count=await input.ReadAsync(buffer,token))>0){if(bytes.Length+count>1048576)throw new InvalidDataException();bytes.Write(buffer,0,count);}
    using var document=JsonDocument.Parse(bytes.ToArray());var root=document.RootElement;
    string tag="";Version? best=null;
    foreach(var release in root.EnumerateArray()){if(release.GetProperty("draft").GetBoolean())continue;string candidate=release.GetProperty("tag_name").GetString()??"";try{var version=UpdateCheckRules.Parse(candidate);if(best==null||version>best){tag=candidate;best=version;}}catch(ArgumentException){}}
    if(best==null)return new Result(false,"","GitHub 尚无可识别的发布版本。",false);
    bool update=UpdateCheckRules.IsNewer(RuntimeModule.ReleaseVersion,tag);
    return new Result(true,tag,update?"发现新版本："+tag:"当前已是最新版本（GitHub："+tag+"）。",update);
   }catch(ArgumentException error){return new Result(false,"",error.Message,false);}
   catch(OperationCanceledException){return new Result(false,"","检查超时或已取消，请检查网络或代理。",false);}
   catch{return new Result(false,"","更新检查失败；请检查网络和代理后重试。",false);}
  });
 }
 internal static void Draw(Rect rect){
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-22,840));
  try{float w=rect.width-30;InterfaceMenu.Label(new(0,0,w,32),"版本更新 · "+RuntimeModule.ReleaseVersion,InterfaceMenu.Accent,20);
   InterfaceMenu.Label(new(0,32,w,20),$"开发版本 {RuntimeModule.BuildNumber}",null,13);
   InterfaceMenu.Paragraph(new(0,56,w,62),status,newer?Color.green:null,14);
   bool enabled=GUI.enabled;GUI.enabled=enabled&&pending==null;if(InterfaceMenu.Button(new(0,120,(w-12)/2,36),"检查更新"))Check();GUI.enabled=enabled;
   if(InterfaceMenu.Button(new((w+12)/2,120,(w-12)/2,36),"打开 GitHub 发布页"))Application.OpenURL(UpdateCheckRules.Releases);
   GUI.enabled=enabled&&localUpdate==null;
   if(InterfaceMenu.Button(new(0,168,w,36),localUpdate==null?"选择本地热更新文件":"正在选择或检查文件…")){
    localStatus="";localUpdate=SelectLocalUpdate(host!.PluginDirectory,stopping!.Token);
   }
   GUI.enabled=enabled;
   InterfaceMenu.Paragraph(new(0,210,w,46),localStatus.Length>0?localStatus:"选择核心 .module 或 .dll 后应用。",null,13);
   InterfaceMenu.DrawEntry(automatic,new(0,272,w,92));InterfaceMenu.DrawEntry(useProxy,new(0,376,w,92));InterfaceMenu.DrawEntry(proxy,new(0,480,w,92));
   InterfaceMenu.DrawEntry(proxyUser,new(0,584,w,92));
   InterfaceMenu.Label(new(0,692,w,24),"代理密码（可留空）",InterfaceMenu.Accent,14);
   string secret=InterfaceMenu.PasswordField(new(0,726,w-110,32),proxyPassword.Value);
   if(secret!=proxyPassword.Value)proxyPassword.Value=secret;
   if(InterfaceMenu.Button(new(w-100,726,100,32),"清除认证")){proxyUser.Value="";proxyPassword.Value="";}
   InterfaceMenu.Paragraph(new(0,774,w,52),"代理示例：http://127.0.0.1:7890 或 socks5://127.0.0.1:1080；地址留空时使用系统代理。",null,12);
  }finally{GUI.EndScrollView();}
 }
 static void DrawSecret(ConfigEntryBase entry){string before=(string)entry.BoxedValue;string after=GUILayout.PasswordField(before,'●',Array.Empty<GUILayoutOption>());if(after!=before)entry.BoxedValue=after;}
 internal static void Reset(){stopping?.Cancel();stopping?.Dispose();stopping=null;pending=null;localUpdate=null;localStatus="";host=null;}
}
