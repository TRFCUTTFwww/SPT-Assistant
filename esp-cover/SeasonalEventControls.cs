using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class SeasonalEventControls {
 static string Root=>Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","SPT_Data");
 static string Seasonal=>Path.Combine(Root,"configs","seasonalevents.json");
 static string DarkLab=>Path.Combine(Root,"database","locations","laboratory_dark","base.json");
 static string Home=>Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","event-config-backups");
 static readonly JsonSerializerOptions json=new(){WriteIndented=true};
 static JsonObject? seasonal,dark,originalSeasonal,originalDark;static string seasonalHash="",darkHash="",status="活动设置只在主菜单保存；重启服务端和客户端后生效。";
 static EventMode mode;static bool summon,zombies,cult,division;static Vector2 scroll;
 static JsonObject Read(string path)=>JsonNode.Parse(File.ReadAllText(path))?.AsObject()??throw new ArgumentException("配置文件不是对象："+Path.GetFileName(path));
 static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
 static void Load(){
  seasonal=Read(Seasonal);dark=Read(DarkLab);seasonalHash=Hash(Seasonal);darkHash=Hash(DarkLab);
  string baseline=Path.Combine(Home,"original");originalSeasonal=File.Exists(Path.Combine(baseline,"seasonalevents.json"))?Read(Path.Combine(baseline,"seasonalevents.json")):(JsonObject)seasonal.DeepClone();
  originalDark=File.Exists(Path.Combine(baseline,"laboratory_dark.json"))?Read(Path.Combine(baseline,"laboratory_dark.json")):(JsonObject)dark.DeepClone();
  var halloween=seasonal["events"]!.AsArray().OfType<JsonObject>().FirstOrDefault(e=>e["type"]?.ToString()=="HALLOWEEN");
  summon=halloween?["settings"]?["enableSummoning"]?.GetValue<bool>()==true||seasonal["events"]!.AsArray().OfType<JsonObject>().Any(e=>e["type"]?.ToString()=="HALLOWEENSUMMON"&&e["enabled"]?.GetValue<bool>()==true);zombies=halloween?["settings"]?["zombieSettings"]?["enabled"]?.GetValue<bool>()==true;
  cult=seasonal["eventBossSpawns"]?["halloweennightcult"]?.AsObject().Any(pair=>pair.Value is JsonArray list&&list.Any(e=>EventConfigRules.Number(e?["BossChance"])>0))==true;
  division=dark["BossLocationSpawn"]!.AsArray().Any(e=>EventConfigRules.IsDivision(e?["BossName"]?.ToString()??"")&&EventConfigRules.Number(e?["BossChance"])>0);mode=EventMode.Current;
 }
 static bool MapExists(string key){string map=key.ToLowerInvariant();if(map=="factory4")map="factory4_day";return File.Exists(Path.Combine(Root,"database","locations",map,"base.json"));}
 static string[] Audit(JsonObject value)=>EventConfigRules.Audit(value,role=>Enum.TryParse<WildSpawnType>(role,true,out var parsed)&&Enum.IsDefined(parsed),role=>File.Exists(Path.Combine(Root,"database","bots","types",role.ToLowerInvariant()+".json")),MapExists).ToArray();
 static void Action(Action action){try{action();}catch(Exception error){status=error.Message;FeatureToasts.Show(status);}}
 static void Save(bool restore=false){
  if(RaidClock.Active)throw new ArgumentException("请回到主菜单后保存活动配置。");if(seasonal==null||dark==null)Load();
  if(Hash(Seasonal)!=seasonalHash||Hash(DarkLab)!=darkHash)throw new ArgumentException("配置已被其他程序修改，请先重新读取。");
  var next=restore?(JsonObject)originalSeasonal!.DeepClone():EventConfigRules.Seasonal(seasonal!,originalSeasonal!,mode,summon,zombies,cult);
  var nextDark=restore?(JsonObject)originalDark!.DeepClone():EventConfigRules.Division(dark!,originalDark!,division);
  if(!restore){var problems=Audit(next);if(problems.Length>0)throw new ArgumentException("活动数据检查未通过，未写入：\n"+string.Join("\n",problems.Take(8)));}
  if(JsonNode.DeepEquals(next,seasonal)&&JsonNode.DeepEquals(nextDark,dark)){status="配置没有变化。";return;}
  Directory.CreateDirectory(Home);string baseline=Path.Combine(Home,"original");Directory.CreateDirectory(baseline);
  if(!File.Exists(Path.Combine(baseline,"seasonalevents.json")))File.Copy(Seasonal,Path.Combine(baseline,"seasonalevents.json"));
  if(!File.Exists(Path.Combine(baseline,"laboratory_dark.json")))File.Copy(DarkLab,Path.Combine(baseline,"laboratory_dark.json"));
  string backup=Path.Combine(Home,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(backup);File.Copy(Seasonal,Path.Combine(backup,"seasonalevents.json"));File.Copy(DarkLab,Path.Combine(backup,"laboratory_dark.json"));
  try{Write(Seasonal,next);Write(DarkLab,nextDark);}catch{File.Copy(Path.Combine(backup,"seasonalevents.json"),Seasonal,true);File.Copy(Path.Combine(backup,"laboratory_dark.json"),DarkLab,true);throw;}
  Load();status=restore?"已恢复活动原始配置。请退出游戏、重启服务端，再启动游戏。":"活动配置已保存。请退出游戏、重启服务端，再启动游戏生效。";FeatureToasts.Show(status);
 }
 static void Write(string path,JsonObject value){string temporary=path+".assistant-writing";try{File.WriteAllText(temporary,value.ToJsonString(json),new UTF8Encoding(false));File.Move(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
 static bool Toggle(Rect rect,string text,bool value)=>InterfaceMenu.Button(rect,text+" · "+(value?"开":"关"),value)?!value:value;
 internal static void SetNativeHalloween(bool value){if(!ClientEventVersion.Native)throw new InvalidOperationException("当前客户端不支持原生活动路径。");Load();mode=value?EventMode.Halloween:EventMode.Off;if(value){zombies=true;summon=true;}Save();}
 internal static void Draw(Rect rect){
  if(seasonal==null)Action(Load);float w=rect.width-24;scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,w,850));float y=4;
  InterfaceMenu.Label(new Rect(4,y,w,30),"活动控制",InterfaceMenu.Accent,19);y+=40;
  string current=seasonal?["enableSeasonalEventDetection"]?.GetValue<bool>()==true?"按服务端活动日期启用":"自动检测关闭";
  InterfaceMenu.Paragraph(new Rect(4,y,w,52),"当前磁盘配置："+current+"。以下为下次启动配置；保存后需重启服务端和客户端。",null,13);y+=64;
  var names=new[]{"保持当前设置","关闭季节活动","按原始活动日期","手动万圣节","手动圣诞节"};int columns=Math.Min(names.Length,MenuWorkspaceLayout.Columns(w,140));float cell=(w-8*(columns-1))/columns;
  for(int i=0;i<names.Length;i++)if(InterfaceMenu.Button(new Rect(4+i%columns*(cell+8),y+i/columns*42,cell,34),names[i],mode==(EventMode)i))mode=(EventMode)i;y+=MathF.Ceiling(names.Length/(float)columns)*42+20;
  InterfaceMenu.Label(new Rect(4,y,w,26),"万圣节内容",InterfaceMenu.Accent,16);y+=38;
  bool old=GUI.enabled;GUI.enabled=old&&mode is EventMode.Halloween or EventMode.Calendar;
  summon=Toggle(new Rect(4,y,w,34),"邪教徒召唤事件",summon);y+=44;zombies=Toggle(new Rect(4,y,w,34),"僵尸感染",zombies);y+=44;bool hasCult=seasonal?["eventBossSpawns"]?["halloweennightcult"] is JsonObject;GUI.enabled=GUI.enabled&&hasCult;cult=Toggle(new Rect(4,y,w,34),hasCult?"夜间特殊邪教徒":"当前版本没有独立夜间邪教徒活动",cult);y+=44;GUI.enabled=old;
  InterfaceMenu.Paragraph(new Rect(4,y,w,62),"三个子活动分别控制。保存前检查万圣节角色、模板和地图数据；实际资源加载仍需实战验证。",null,12);y+=76;
  division=Toggle(new Rect(4,y,w,36),"暗实验室黑色军团刷新",division);y+=46;
  InterfaceMenu.Paragraph(new Rect(4,y,w,48),"控制暗实验室中的 Wedge 与军团波次；不改变地图入口。",null,12);y+=62;
  float half=(w-12)/2;if(InterfaceMenu.Button(new Rect(4,y,half,34),"重新读取"))Action(()=>{Load();status="已重新读取磁盘配置。";});
  if(InterfaceMenu.Button(new Rect(16+half,y,half,34),"检查万圣节数据"))Action(()=>{if(seasonal==null)Load();var next=EventConfigRules.Seasonal(seasonal!,originalSeasonal!,mode,summon,zombies,cult);bool enabled=next["enableSeasonalEventDetection"]?.GetValue<bool>()==true&&next["events"]!.AsArray().Any(e=>e?["type"]?.ToString()=="HALLOWEEN"&&e["enabled"]?.GetValue<bool>()==true);var problems=Audit(next);status=!enabled?"当前模式未开启万圣节，无万圣节数据需要检查。":problems.Length==0?"万圣节角色、模板和地图检查通过；实战资源加载仍需验证。":string.Join("\n",problems.Take(10));});y+=48;
  GUI.enabled=old&&!RaidClock.Active&&seasonal!=null;
  if(InterfaceMenu.Button(new Rect(4,y,half,38),"保存活动配置"))Action(()=>Save());if(InterfaceMenu.Button(new Rect(16+half,y,half,38),"恢复活动原始配置"))Action(()=>Save(true));GUI.enabled=old;y+=54;
  InterfaceMenu.Paragraph(new Rect(4,y,w,135),status,InterfaceMenu.Accent,12);GUI.EndScrollView();
 }
}
