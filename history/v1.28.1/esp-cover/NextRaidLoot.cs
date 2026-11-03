using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
 internal static string ServerStatus {get{if(Time.unscaledTime>=nextStatus){nextStatus=Time.unscaledTime+3;serverStatus="服务端模块尚未加载，需重启服务端一次。";try{using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(Home,"server-status.json")));int pid=json.RootElement.GetProperty("serverPid").GetInt32();if(json.RootElement.GetProperty("version").GetString()=="1.4.0"&&Process.GetProcessesByName("SPT.Server").Any(p=>p.Id==pid)&&json.RootElement.GetProperty("ready").GetBoolean())serverStatus="服务端模块已就绪；保存后用于下一次生成。";}catch{}}return serverStatus;}}
 internal static void Tick(){if(Time.unscaledTime<nextHeartbeat)return;nextHeartbeat=Time.unscaledTime+2;try{var file=Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","runtime","raid-state.json");File.WriteAllText(file+".tmp",JsonSerializer.Serialize(new{inRaid=RaidClock.Active,utc=DateTime.UtcNow}));File.Move(file+".tmp",file,true);}catch{}}
 static void Load(){var value=Read();enabled=value.LootEnabled;loose=value.LooseMultiplier.ToString(CultureInfo.InvariantCulture);containers=value.ContainerMultiplier.ToString(CultureInfo.InvariantCulture);valuable=value.HighValueMultiplier.ToString(CultureInfo.InvariantCulture);threshold=value.ValueThreshold.ToString(CultureInfo.InvariantCulture);loaded=true;}
 static void Apply(){var value=Read();bool prior=value.LootEnabled;value.LootEnabled=enabled;value.LooseMultiplier=Number(loose);value.ContainerMultiplier=Number(containers);value.HighValueMultiplier=Number(valuable);value.ValueThreshold=Number(threshold);Save(value);if(prior!=enabled)FeatureToasts.Show("下次战局物资设置",enabled);else FeatureToasts.Show("物资设置已保存 · 下次生成生效");notice=enabled?"已保存；下次生成应用，当前战局物品不重新生成。":"已保存关闭状态；后续生成使用原始物资规则。";}
 static int page;static bool noAi,noLoot,aiEnabled;static readonly System.Collections.Generic.Dictionary<string,string> factions=new();
 static void LoadAdvanced(){var value=Read();noAi=value.NoAi;noLoot=value.NoLoot;aiEnabled=value.AiEnabled;factions.Clear();foreach(string faction in RaidSpawnRules.Factions)factions[faction]=value.FactionMultipliers.GetValueOrDefault(faction,1).ToString(CultureInfo.InvariantCulture);}
 static void ApplyAdvanced(){var value=Read();value.NoAi=noAi;value.NoLoot=noLoot;value.AiEnabled=aiEnabled;value.FactionMultipliers=factions.ToDictionary(pair=>pair.Key,pair=>Number(pair.Value));Save(value);notice="已保存，下次战局生成时生效。";FeatureToasts.Show(notice);}
 static void Defaults(){if(page==0){noAi=noLoot=aiEnabled=false;foreach(string faction in RaidSpawnRules.Factions)factions[faction]="1";}else{enabled=false;loose=containers=valuable="1";threshold="100000";}notice="已恢复本页默认值，点击保存后生效。";FeatureToasts.Show(notice);}
 internal static void Draw(Rect rect){try{if(!loaded){Load();LoadAdvanced();}}catch(Exception e){notice=e.Message;loaded=true;}float x=rect.x,y=rect.y,w=rect.width;InterfaceMenu.Label(new Rect(x,y,w,32),"战局高级设置",InterfaceMenu.Accent,21);y+=46;float tab=(w-24)/4;var labels=new[]{"战局与 AI","物资刷新","单物品权重","真实 BTC"};for(int i=0;i<labels.Length;i++)if(InterfaceMenu.Button(new Rect(x+i*(tab+8),y,tab,36),labels[i],page==i)){page=i;scroll=Vector2.zero;}y+=52;if(page is 2 or 3){var viewport=new Rect(x,y,w,Math.Max(100,rect.yMax-y));float height=Math.Max(viewport.height-1,610);scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,w-20,height));var panel=new Rect(0,0,w-20,height);if(page==2)ItemLootWeights.Draw(panel);else BitcoinControl.Draw(panel);GUI.EndScrollView();return;}
  var area=new Rect(x,y,w,Math.Max(100,rect.yMax-y-142));scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,w-20,Math.Max(area.height-1,page==0?636:414)));float left=8,top=8,width=w-38;
  if(page==0){
   InterfaceMenu.Paragraph(new Rect(left,top,width,40),"设置下一次战局。0 不刷新 · 1 原始设置 · 2 两倍。",null,13);top+=52;float col=(width-12)/2;
   if(InterfaceMenu.Button(new Rect(left,top,col,38),noAi?"无 AI 战局 · 开启":"无 AI 战局 · 关闭",noAi)){noAi=!noAi;FeatureToasts.Show("无 AI 战局："+(noAi?"开启":"关闭")+" · 待保存");}
   if(InterfaceMenu.Button(new Rect(left+col+12,top,col,38),noLoot?"无物资战局 · 开启":"无物资战局 · 关闭",noLoot)){noLoot=!noLoot;FeatureToasts.Show("无物资战局："+(noLoot?"开启":"关闭")+" · 待保存");}top+=52;
   InterfaceMenu.Paragraph(new Rect(left,top,width,48),"无 AI 关闭自然刷新，仍可手动添加。无物资同时移除必刷／任务拾取物与自然空投。",null,12);top+=62;
   bool wasEnabled=GUI.enabled;GUI.enabled=wasEnabled&&!noAi;if(InterfaceMenu.Button(new Rect(left,top,width,36),aiEnabled?"自定义阵营刷新比例 · 开启":"自定义阵营刷新比例 · 关闭",aiEnabled)){aiEnabled=!aiEnabled;FeatureToasts.Show("阵营刷新比例："+(aiEnabled?"开启":"关闭")+" · 待保存");}top+=50;
   InterfaceMenu.Paragraph(new Rect(left,top,width,52),"普通波次调整人数，带首领的队伍调整出现概率（最高 100%）；保镖调整随从人数。地图没有的阵营不会额外生成。",null,12);top+=64;
   GUI.enabled=wasEnabled&&!noAi&&aiEnabled;int index=0;foreach(string faction in RaidSpawnRules.Factions){float bx=left+index%2*(col+12),by=top+index/2*58;InterfaceMenu.Label(new Rect(bx,by,col-112,32),RaidSpawnRules.Name(faction),null,13);string raw=factions.GetValueOrDefault(faction,"1");factions[faction]=InterfaceMenu.Field(new Rect(bx+col-106,by,106,32),raw);index++;}GUI.enabled=wasEnabled;
  }else{
   InterfaceMenu.Paragraph(new Rect(left,top,width,44),"设置物资数量和高价值物品权重。无物资战局开启时优先不生成物资。",null,13);top+=58;
   if(InterfaceMenu.Button(new Rect(left,top,250,36),enabled?"自定义物资设置 · 开启":"自定义物资设置 · 关闭",enabled)){enabled=!enabled;FeatureToasts.Show("物资自定义："+(enabled?"开启":"关闭")+" · 待保存");}top+=54;
   void Field(string title,ref string value,string hint){InterfaceMenu.Label(new Rect(left,top,width*.43f,30),title,null,13);value=InterfaceMenu.Field(new Rect(left+width*.45f,top,140,32),value);InterfaceMenu.Label(new Rect(left+width*.45f+154,top,width*.55f-154,30),hint,null,11);top+=52;}
   Field("散落物刷新倍率",ref loose,"非负数");Field("容器内物资倍率",ref containers,"非负数");Field("高价值物品权重倍率",ref valuable,"正数，1 为原始权重");Field("高价值估值门槛（卢布）",ref threshold,"按辅助模组估值表");
  }
  GUI.EndScrollView();float bottom=rect.yMax-128;float button=(w-24)/3;if(InterfaceMenu.Button(new Rect(x,bottom,button,36),"保存到下次战局"))try{if(page==0)ApplyAdvanced();else Apply();}catch(Exception e){notice=e.Message;FeatureToasts.Show(notice);}if(InterfaceMenu.Button(new Rect(x+button+12,bottom,button,36),"重新读取"))try{Load();LoadAdvanced();notice="已读取保存值。";}catch(Exception e){notice=e.Message;}if(InterfaceMenu.Button(new Rect(x+2*(button+12),bottom,button,36),"恢复本页默认"))Defaults();InterfaceMenu.Paragraph(new Rect(x,bottom+50,w,74),notice+"\n"+ServerStatus,InterfaceMenu.Accent,12);
 }
}
