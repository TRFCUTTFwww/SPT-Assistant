using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
namespace LocalSpt5Esp;
internal static class FeatureRetirement {
 static bool Retired(ConfigDefinition definition)=>definition.Section=="44. 自动扳机"||definition.Section=="51. 队友"||definition.Section=="25. Aim exclusions"&&definition.Key=="Exclude teammates"||definition.Section=="20. Movement"&&new[]{"Collision flight","Collision flight toggle key","Collision flight speed"}.Contains(definition.Key)||definition.Section=="49. AI 动作导演"&&new[]{"Scene jump","Scene nod","Scene shake","Scene action interval","Scene action amplitude","Scene loop route","Scene relative route","Scene waypoint wait"}.Contains(definition.Key);
 internal static void Apply(ConfigFile config,string directory,ManualLogSource log){try{
  var orphans=AccessTools.Property(typeof(ConfigFile),"OrphanedEntries")?.GetValue(config) as Dictionary<ConfigDefinition,string>;
  var bound=config.Keys.Where(Retired).ToArray();var unused=orphans?.Keys.Where(Retired).ToArray()??Array.Empty<ConfigDefinition>();
  string plans=Path.Combine(directory,"companion-plans.json");if(bound.Length+unused.Length==0&&!File.Exists(plans))return;
  string archive=Path.Combine(directory,"retired","companions-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(archive);
  if(File.Exists(config.ConfigFilePath))File.Copy(config.ConfigFilePath,Path.Combine(archive,"settings-before.cfg"));
  if(File.Exists(plans))File.Move(plans,Path.Combine(archive,"companion-plans.json"));
  foreach(var key in bound)config.Remove(key);foreach(var key in unused)orphans!.Remove(key);
  log.LogInfo("Retired feature settings archived; removed entries="+(bound.Length+unused.Length));
 }catch(Exception error){log.LogWarning("Could not archive retired settings; existing files were retained: "+error.Message);}}
}
