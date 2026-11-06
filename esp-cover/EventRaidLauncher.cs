using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using UnityEngine;

namespace LocalSpt5Esp;
internal static class EventRaidLauncher {
 static string Home=>Path.Combine(BepInEx.Paths.PluginPath,"HalloweenEventPort");
 static readonly string[] modes={"normal","dark","halloween2024","event"};
 static readonly string[] names={"常规","黑色军团断电","2024万圣节","2026万圣节"};
 static string mode="normal",lights="default",notice="",lightStatus="进入实验室后可控制灯光。";
 static bool installed,attempted,ready,preparing,factoryEvent;static float nextRead;static Vector2 scroll;
 static Action? pump;
 static GameObject? driver;
 static Type? infectionFlags;static MethodInfo? reloadInfection;static float nextCoreCheck;
 static Action? infectionPrimer;
 static MethodInfo? refreshLocations,modeSignature;static int observedServer;static float nextServerPoll;
 internal static void Tick(){
  if(ClientEventVersion.Native){NativeEventControl.Tick();return;}if(!ClientEventVersion.LegacyPort)return;
  if(pump!=null){pump();if(Time.unscaledTime>=nextCoreCheck){nextCoreCheck=Time.unscaledTime+.5f;var core=BotInternalSettingsController.Core;if(core!=null&&infectionFlags?.GetField("configured",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null) is true&&infectionFlags.GetField("enabled",BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null) is bool desired&&core.ACTIVE_HALLOWEEN_ZOMBIES_EVENT!=desired)reloadInfection?.Invoke(null,null);}infectionPrimer?.Invoke();RefreshAfterServerChange();return;}
  if(attempted)return;string file=Path.Combine(Home,"Local.HalloweenEventPort.LabModes.dll");if(!File.Exists(file))return;attempted=true;
  try{var assembly=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="Local.HalloweenEventPort.LabModes")??Assembly.LoadFrom(file);assembly.GetType("HalloweenEventPort.LabModes.Entry",true)!.GetMethod("Install",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,new object[]{Home});var runtime=assembly.GetType("HalloweenEventPort.LabModes.LaboratoryRuntime",true)!;
   driver=GameObject.Find("HalloweenEventPort.LaboratoryModes.Driver");if(driver==null){driver=new GameObject("HalloweenEventPort.LaboratoryModes.Driver");UnityEngine.Object.DontDestroyOnLoad(driver);}
   var get=typeof(GameObject).GetMethods(BindingFlags.Public|BindingFlags.Instance).Single(m=>m.Name=="GetComponent"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==0).MakeGenericMethod(runtime);
   var instance=get.Invoke(driver,null);if(instance==null){var add=typeof(GameObject).GetMethods(BindingFlags.Public|BindingFlags.Instance).Single(m=>m.Name=="AddComponent"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==0).MakeGenericMethod(runtime);instance=add.Invoke(driver,null);}
   if(instance==null)throw new InvalidOperationException("Laboratory runtime component could not be attached");pump=(Action)Delegate.CreateDelegate(typeof(Action),instance,runtime.GetMethod("Update")!);infectionFlags=assembly.GetType("HalloweenEventPort.LabModes.InfectionFlags");reloadInfection=infectionFlags?.GetMethod("CoreLoaded",BindingFlags.Static|BindingFlags.Public);refreshLocations=runtime.GetMethod("Fetch",BindingFlags.Static|BindingFlags.NonPublic);modeSignature=runtime.GetMethod("Signature",BindingFlags.Static|BindingFlags.NonPublic);
   string primerFile=Path.Combine(Home,"Local.HalloweenEventPort.InfectionCompat.0.1.10.module");if(!File.Exists(primerFile))primerFile=Path.Combine(Home,"Local.HalloweenEventPort.InfectionCompat.dll");if(File.Exists(primerFile)){var required=AssemblyName.GetAssemblyName(primerFile).Version;var primerAssembly=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="Local.HalloweenEventPort.InfectionCompat"&&a.GetName().Version==required)??Assembly.Load(File.ReadAllBytes(primerFile));var primer=primerAssembly.GetType("HalloweenEventPort.InfectionCompat.Primer",true)!;primer.GetMethod("Initialize")!.Invoke(null,new object[]{Home});infectionPrimer=(Action)Delegate.CreateDelegate(typeof(Action),primer.GetMethod("Tick")!);}
   installed=true;Plugin.Self.Log.LogInfo("Laboratory mode driver attached and retained");}
  catch(Exception failure){notice="实验室组件未能加载："+failure.GetBaseException().Message;Plugin.Self.Log.LogError(failure);}
 }
 static void RefreshAfterServerChange(){
  if(Time.unscaledTime<nextServerPoll||RaidClock.Active)return;nextServerPoll=Time.unscaledTime+3;
  try{var app=Comfort.Common.Singleton<EFT.TarkovApplication>.Instance??UnityEngine.Object.FindObjectOfType<EFT.TarkovApplication>();if(app?._menuOperation==null||app._localMatchmakerOperation!=null)return;
   using var server=JsonDocument.Parse(File.ReadAllText(Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime/user/mods/HalloweenEventPort/server-status.json")));int pid=server.RootElement.GetProperty("serverPid").GetInt32();if(pid==observedServer||!Array.Exists(System.Diagnostics.Process.GetProcessesByName("SPT.Server"),p=>p.Id==pid))return;
   refreshLocations?.Invoke(null,new object[]{app,modeSignature!.Invoke(null,null)!});observedServer=pid;
  }catch(Exception error){Plugin.Self.Log.LogWarning("Map mode refresh: "+error.GetBaseException().Message);}
 }
 static JsonObject Settings(){string file=Path.Combine(Home,"settings.json");return File.Exists(file)?JsonNode.Parse(File.ReadAllText(file))!.AsObject():new();}
 static void Save(string key,JsonNode value){var settings=Settings();settings[key]=value;string file=Path.Combine(Home,"settings.json");File.WriteAllText(file+".tmp",settings.ToJsonString());File.Move(file+".tmp",file,true);nextRead=0;}
 static void Read(){
  if(Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+.5f;ready=preparing=false;
  try{var settings=Settings();mode=settings["LaboratoryMode"]?.GetValue<string>()??"normal";lights=settings["LaboratoryLights"]?.GetValue<string>()??"default";factoryEvent=settings["FactoryEvent"]?.GetValue<bool>()==true;
   using var server=JsonDocument.Parse(File.ReadAllText(Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","user","mods","HalloweenEventPort","server-status.json")));
   var version=server.RootElement.GetProperty("version").GetString();bool serverReady=Version.TryParse(version,out var parsed)&&parsed>=new Version(0,3,5)&&server.RootElement.GetProperty("ready").GetBoolean()&&Array.Exists(System.Diagnostics.Process.GetProcessesByName("SPT.Server"),p=>p.Id==server.RootElement.GetProperty("serverPid").GetInt32());
   using var status=JsonDocument.Parse(File.ReadAllText(Path.Combine(Home,"lab-mode-status.json")));
   var root=status.RootElement;ready=installed&&serverReady&&root.GetProperty("ready").GetBoolean()&&root.GetProperty("clientPid").GetInt32()==Environment.ProcessId;preparing=root.GetProperty("preparing").GetBoolean();lightStatus=root.GetProperty("lighting").GetString()??lightStatus;
   string error=root.GetProperty("error").GetString()??"";if(error.Length>0)notice="实验室配置同步失败，请查看 lab-mode-status.json。";
  }catch{lightStatus=installed?"等待实验室服务端组件就绪。":"等待实验室组件加载。";}
 }
 static void Action(Action action){try{action();}catch(Exception failure){notice=failure.Message;FeatureToasts.Show(notice);}}
 internal static void Draw(Rect rect){if(ClientEventVersion.Native){NativeEventControl.Draw(rect);return;}if(!ClientEventVersion.LegacyPort){InterfaceMenu.Paragraph(rect,"该客户端版本没有已验证的活动控制路径："+ClientEventVersion.Version);return;}scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,1020));DrawBody(new Rect(4,4,rect.width-32,1000));GUI.EndScrollView();}
 static void DrawBody(Rect rect){
  Read();float x=rect.x,y=rect.y,w=rect.width;InterfaceMenu.Label(new Rect(x,y,w,30),"实验室",InterfaceMenu.Accent,20);y+=44;
  InterfaceMenu.Paragraph(new Rect(x,y,w,48),"选择模式后，按游戏原流程准备实验室战局。开局前会应用所选地图与配置。",null,13);y+=62;
  bool enabled=GUI.enabled;GUI.enabled=enabled&&ready&&!RaidClock.Active&&!preparing;float half=(w-12)/2;
  for(int i=0;i<modes.Length;i++){int selected=i;if(InterfaceMenu.Button(new Rect(x+i%2*(half+12),y+i/2*54,half,42),names[i],mode==modes[i]))Action(()=>{Save("LaboratoryMode",modes[selected]);mode=modes[selected];notice="已选择："+names[selected]+"。正常准备实验室战局即可。";});}y+=122;GUI.enabled=enabled;
  string description=mode switch{"dark"=>"断电实验室与黑色军团。","halloween2024"=>"旧实验室感染区与僵尸。","event"=>"2026 活动实验室；沿用 2024 僵尸生成表，并加入感染大锤。",_=>"常规实验室。"};InterfaceMenu.Paragraph(new Rect(x,y,w,46),description,null,13);y+=64;
  PasscodeHints.DrawOption(new Rect(x,y,w,38));y+=54;
  InterfaceMenu.Label(new Rect(x,y,w,28),"实验室灯光",InterfaceMenu.Accent,17);y+=40;
  GUI.enabled=enabled&&ready;var lightModes=new[]{"default","on","off"};var lightNames=new[]{"地图默认","开灯","关灯"};float third=(w-16)/3;
  for(int i=0;i<3;i++){int selected=i;if(InterfaceMenu.Button(new Rect(x+i*(third+8),y,third,38),lightNames[i],lights==lightModes[i]))Action(()=>{var location=Comfort.Common.Singleton<EFT.GameWorld>.Instance?.MainPlayer?.Location;if(RaidClock.Active&&location is not "laboratory" and not "laboratory_dark")throw new ArgumentException("请进入实验室后控制灯光。");Save("LaboratoryLights",lightModes[selected]);lights=lightModes[selected];});}GUI.enabled=enabled;y+=52;
  InterfaceMenu.Paragraph(new Rect(x,y,w,60),lightStatus,null,12);y+=76;
  InterfaceMenu.Label(new Rect(x,y,w,28),"工厂",InterfaceMenu.Accent,17);y+=40;GUI.enabled=enabled&&ready&&!RaidClock.Active&&!preparing;
  if(InterfaceMenu.Button(new Rect(x,y,half,38),"常规",!factoryEvent))Action(()=>Save("FactoryEvent",false));
  if(InterfaceMenu.Button(new Rect(x+half+12,y,half,38),"2026万圣节",factoryEvent))Action(()=>Save("FactoryEvent",true));GUI.enabled=enabled;y+=54;
  InterfaceMenu.Paragraph(new Rect(x,y,w,100),notice.Length>0?notice:ready?"实验室组件已就绪。":lightStatus,InterfaceMenu.Accent,12);
 }
}
