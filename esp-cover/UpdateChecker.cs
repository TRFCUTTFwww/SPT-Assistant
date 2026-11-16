using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class UpdateChecker {
 record Result(bool Ok,string Tag,string Message,bool Newer);
 static ConfigEntry<bool> automatic=null!,useProxy=null!;
 static ConfigEntry<string> proxy=null!,proxyUser=null!,proxyPassword=null!;
 static Task<Result>? pending;static CancellationTokenSource? stopping;
 static string status="尚未检查更新。";static bool newer;static float next;static Vector2 scroll;
 static string Cache=>Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","update-check.json");
 internal static void Initialize(){
  const string section="66. 版本更新";
  automatic=FeatureConfig.Bind(section,"Automatic update checks","自动检查 GitHub 更新",true,"启动后后台检查 GitHub 发布版本（含本项目实验版）；成功后 24 小时内不重复自动检查，不自动安装。");
  useProxy=FeatureConfig.Bind(section,"Use update proxy","更新检测使用代理",false,"只用于 GitHub 更新检测；启用且地址留空时使用系统代理。");
  proxy=FeatureConfig.Bind(section,"Update proxy address","更新代理地址","","支持 http://127.0.0.1:7890、https:// 或 socks5://；修改后点击检查更新。");
  proxyUser=FeatureConfig.Bind(section,"Update proxy username","代理用户名","","代理需要认证时填写；本地代理一般留空。");
  proxyPassword=FeatureConfig.Bind(section,"Update proxy password","代理密码","","仅用于代理认证，保存在本机配置中，不发送给 GitHub。共享配置前请清除。");
  foreach(var tag in proxyPassword.Description.Tags)if(tag is CnConfig.ConfigurationManagerAttributes attributes)attributes.CustomDrawer=DrawSecret;
  stopping=new();next=Time.unscaledTime+10;newer=false;
  try{using var cache=JsonDocument.Parse(File.ReadAllText(Cache));var root=cache.RootElement;string tag=root.GetProperty("tag").GetString()!;newer=UpdateCheckRules.IsNewer(RuntimeModule.ReleaseVersion,tag);status=(newer?"发现新版本：":"上次检查的 GitHub 版本：")+tag;if(DateTime.UtcNow-root.GetProperty("utc").GetDateTime()<TimeSpan.FromHours(24))next=Time.unscaledTime+(float)Math.Max(60,(root.GetProperty("utc").GetDateTime().AddHours(24)-DateTime.UtcNow).TotalSeconds);}catch{}
 }
 internal static void Tick(){
  if(pending?.IsCompleted==true){var result=pending.GetAwaiter().GetResult();pending=null;status=result.Message;newer=result.Newer;next=Time.unscaledTime+(result.Ok?86400:600);if(result.Ok)try{Directory.CreateDirectory(Path.GetDirectoryName(Cache)!);File.WriteAllText(Cache+".tmp",JsonSerializer.Serialize(new{utc=DateTime.UtcNow,tag=result.Tag}));File.Move(Cache+".tmp",Cache,true);}catch{}}
  if(automatic.Value&&pending==null&&Time.unscaledTime>=next)Check();
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
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-22,920));
  try{float w=rect.width-30;InterfaceMenu.Label(new(0,0,w,32),"版本更新 · "+RuntimeModule.ReleaseVersion,InterfaceMenu.Accent,20);InterfaceMenu.Paragraph(new(0,46,w,65),status,newer?Color.green:null,14);
   bool enabled=GUI.enabled;GUI.enabled=enabled&&pending==null;if(InterfaceMenu.Button(new(0,120,(w-12)/2,36),"检查更新"))Check();GUI.enabled=enabled;
   if(InterfaceMenu.Button(new((w+12)/2,120,(w-12)/2,36),"打开 GitHub 发布页"))Application.OpenURL(UpdateCheckRules.Releases);
   InterfaceMenu.DrawEntry(automatic,new(0,176,w,92));InterfaceMenu.DrawEntry(useProxy,new(0,280,w,92));InterfaceMenu.DrawEntry(proxy,new(0,384,w,92));
   InterfaceMenu.DrawEntry(proxyUser,new(0,488,w,92));
   InterfaceMenu.Label(new(0,596,w,24),"代理密码（可留空）",InterfaceMenu.Accent,14);
   string secret=InterfaceMenu.PasswordField(new(0,630,w-110,32),proxyPassword.Value);
   if(secret!=proxyPassword.Value)proxyPassword.Value=secret;
   if(InterfaceMenu.Button(new(w-100,630,100,32),"清除认证")){proxyUser.Value="";proxyPassword.Value="";}
   InterfaceMenu.Paragraph(new(0,682,w,210),"示例：常见本地代理填写 http://127.0.0.1:7890；SOCKS5 示例 socks5://127.0.0.1:1080。端口以你的代理软件为准。\n本地代理通常无需用户名和密码；公司或远程代理需要认证时再填写。地址留空使用系统代理。密码仅保存在本机配置，共享配置前请清除。\n检查 GitHub Releases（含本项目实验版），不自动安装。",null,13);
  }finally{GUI.EndScrollView();}
 }
 static void DrawSecret(ConfigEntryBase entry){string before=(string)entry.BoxedValue;string after=GUILayout.PasswordField(before,'●',Array.Empty<GUILayoutOption>());if(after!=before)entry.BoxedValue=after;}
 internal static void Reset(){stopping?.Cancel();stopping?.Dispose();stopping=null;pending=null;}
}
