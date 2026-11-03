using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using LocalRaidShared;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class NextRaidLoot {
 internal static string Home=>Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","user","mods","LocalRaidControls");
 static string FilePath=>Path.Combine(Home,"settings.json");
 static string loose="1",containers="1",valuable="1",threshold="100000",notice="";static bool enabled,loaded;static Vector2 scroll;
 static float nextStatus,nextHeartbeat;static string serverStatus="正在检查服务端模块…";
 internal static RaidSettings Read(){if(!File.Exists(FilePath))return new();var value=JsonSerializer.Deserialize<RaidSettings>(File.ReadAllText(FilePath))??new();value.Validate();return value;}
 internal static void Save(RaidSettings value){value.Validate();Directory.CreateDirectory(Home);if(File.Exists(FilePath))File.Copy(FilePath,FilePath+".bak",true);File.WriteAllText(FilePath+".tmp",JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true}));File.Move(FilePath+".tmp",FilePath,true);}
 static double Number(string raw)=>double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)&&double.IsFinite(value)?value:throw new ArgumentException("请填写有效数字。");
 internal static string ServerStatus {get{if(Time.unscaledTime>=nextStatus){nextStatus=Time.unscaledTime+3;serverStatus="服务端模块尚未加载，需重启服务端一次。";try{using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(Home,"server-status.json")));int pid=json.RootElement.GetProperty("serverPid").GetInt32();if(Process.GetProcessesByName("SPT.Server").Any(p=>p.Id==pid)&&json.RootElement.GetProperty("ready").GetBoolean())serverStatus="服务端模块已就绪；保存后用于下一次生成。";}catch{}}return serverStatus;}}
 internal static void Tick(){if(Time.unscaledTime<nextHeartbeat)return;nextHeartbeat=Time.unscaledTime+2;try{var file=Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","runtime","raid-state.json");File.WriteAllText(file+".tmp",JsonSerializer.Serialize(new{inRaid=RaidClock.Active,utc=DateTime.UtcNow}));File.Move(file+".tmp",file,true);}catch{}}
 static void Load(){var value=Read();enabled=value.LootEnabled;loose=value.LooseMultiplier.ToString(CultureInfo.InvariantCulture);containers=value.ContainerMultiplier.ToString(CultureInfo.InvariantCulture);valuable=value.HighValueMultiplier.ToString(CultureInfo.InvariantCulture);threshold=value.ValueThreshold.ToString(CultureInfo.InvariantCulture);loaded=true;}
 static void Apply(){var value=Read();bool prior=value.LootEnabled;value.LootEnabled=enabled;value.LooseMultiplier=Number(loose);value.ContainerMultiplier=Number(containers);value.HighValueMultiplier=Number(valuable);value.ValueThreshold=Number(threshold);Save(value);if(prior!=enabled)FeatureToasts.Show("下次战局物资设置",enabled);else FeatureToasts.Show("物资设置已保存 · 下次生成生效");notice=enabled?"已保存；下次生成应用，当前战局物品不重新生成。":"已保存关闭状态；后续生成使用原始物资规则。";}
 internal static void Draw(Rect rect){try{if(!loaded)Load();}catch(Exception e){notice=e.Message;loaded=true;}scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,Math.Max(rect.height-1,530)));float w=rect.width-30,y=8;
  InterfaceMenu.Label(new Rect(8,y,w,30),"下次战局 · 物资刷新",InterfaceMenu.Text,19);y+=44;InterfaceMenu.Paragraph(new Rect(8,y,w,55),"调整下一次战局生成的物资数量，以及高价值候选物品的相对权重。不会刷新当前战局，也不会向不兼容的容器硬塞物品。",InterfaceMenu.Text,13);y+=65;
  if(InterfaceMenu.Button(new Rect(8,y,230,32),enabled?"自定义物资设置：开启":"自定义物资设置：关闭")){enabled=!enabled;FeatureToasts.Show("物资自定义："+(enabled?"开启":"关闭")+" · 待保存");}y+=48;
  void Field(string title,ref string value,string hint){InterfaceMenu.Label(new Rect(8,y,w*.43f,28),title,InterfaceMenu.Text,13);value=InterfaceMenu.Field(new Rect(w*.45f,y,140,29),value);InterfaceMenu.Label(new Rect(w*.45f+154,y,w*.55f-154,28),hint,InterfaceMenu.Text,11);y+=46;}
  Field("散落物刷新倍率",ref loose,"0～5，1 为原始数量");Field("容器内物资倍率",ref containers,"0～5，受容器容量限制");Field("高价值物品权重倍率",ref valuable,"0.1～20，1 为原始权重");Field("高价值估值门槛（卢布）",ref threshold,"与辅助模组估值表一致");
  InterfaceMenu.Paragraph(new Rect(8,y,w,50),"权重倍率不等于固定爆率百分比：实际概率仍由候选池、原始权重、地图限制和容器空间决定；任务强制生成物品保持原规则。",InterfaceMenu.Text,12);y+=64;
  if(InterfaceMenu.Button(new Rect(8,y,180,34),"保存到下次战局"))try{Apply();}catch(Exception e){notice=e.Message;}if(InterfaceMenu.Button(new Rect(202,y,150,34),"重新读取"))try{Load();notice="已读取保存值。";}catch(Exception e){notice=e.Message;}y+=46;InterfaceMenu.Paragraph(new Rect(8,y,w,68),notice+"\n"+ServerStatus,InterfaceMenu.Accent,12);GUI.EndScrollView();
 }
}
