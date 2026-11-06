using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
namespace LocalSpt5Esp;
internal enum EventMode { Current,Off,Calendar,Halloween,Christmas }
internal static class EventConfigRules {
 internal static bool IsDivision(string role)=>role is "bossWedgeLab" or "followerWedgeLab" or "pmcBotBlackDiv" or "blackDivision" or "bossBullyBlackDiv" or "followerBullyBlackDiv";
 internal static JsonObject Seasonal(JsonObject current,JsonObject original,EventMode mode,bool summon,bool zombies,bool cult){
  var result=(JsonObject)current.DeepClone();if(mode==EventMode.Current)return result;
  var events=result["events"]?.AsArray()??throw new ArgumentException("服务端活动列表缺失。");
  var originals=original["events"]?.AsArray()??throw new ArgumentException("原始活动列表缺失。");
  bool separateSummon=events.OfType<JsonObject>().Any(e=>e["type"]?.ToString()=="HALLOWEENSUMMON");
  result["enableSeasonalEventDetection"]=mode!=EventMode.Off;
  string desired=mode==EventMode.Halloween?"HALLOWEEN":"CHRISTMAS";bool picked=false;
  foreach(var entry in events.OfType<JsonObject>()){
   string type=entry["type"]?.GetValue<string>()??"";bool chosen=false;
   if(mode==EventMode.Calendar){
    var seed=originals.OfType<JsonObject>().FirstOrDefault(e=>e["name"]?.ToString()==entry["name"]?.ToString());
    if(seed!=null)foreach(string key in new[]{"enabled","startDay","startMonth","endDay","endMonth"})entry[key]=seed[key]?.DeepClone();
    chosen=entry["enabled"]?.GetValue<bool>()==true;
   }else{
    chosen=mode!=EventMode.Off&&!picked&&type==desired;if(chosen)picked=true;
    if(mode==EventMode.Halloween&&type=="HALLOWEENSUMMON")chosen=summon;
    entry["enabled"]=chosen;
    if(chosen){entry["startDay"]="1";entry["startMonth"]="1";entry["endDay"]="31";entry["endMonth"]="12";}
   }
   if(mode==EventMode.Calendar&&type=="HALLOWEENSUMMON"&&!summon)entry["enabled"]=false;
   if(chosen&&type=="HALLOWEEN"){
    var settings=entry["settings"]?.AsObject()??throw new ArgumentException("万圣节设置缺失。");if(!separateSummon||settings.ContainsKey("enableSummoning"))settings["enableSummoning"]=summon;
    var infection=settings["zombieSettings"]?.AsObject()??throw new ArgumentException("僵尸活动设置缺失。");infection["enabled"]=zombies;
   }
  }
  if(mode is EventMode.Halloween or EventMode.Christmas&&!picked)throw new ArgumentException("服务端没有对应活动。");
  if(mode is EventMode.Halloween or EventMode.Calendar)SetCult(result,original,cult);
  return result;
 }
 static void SetCult(JsonObject result,JsonObject original,bool enabled){
  var groups=result["eventBossSpawns"]?["halloweennightcult"]?.AsObject();if(groups==null){if(enabled)throw new ArgumentException("服务端没有夜间邪教徒活动数据。");return;}
  foreach(var map in groups)if(map.Value is JsonArray bosses)for(int i=0;i<bosses.Count;i++)if(bosses[i] is JsonObject boss){
   var seeds=original["eventBossSpawns"]?["halloweennightcult"]?[map.Key]?.AsArray();var seed=seeds!=null&&i<seeds.Count?seeds[i]:null;
   boss["BossChance"]=enabled?(seed?["BossChance"]?.DeepClone()??JsonValue.Create(100)):JsonValue.Create(0);
  }
 }
 internal static JsonObject Division(JsonObject current,JsonObject original,bool enabled){
  var result=(JsonObject)current.DeepClone();var bosses=result["BossLocationSpawn"]?.AsArray()??throw new ArgumentException("暗实验室刷新表缺失。");
  var originals=original["BossLocationSpawn"]?.AsArray();int found=0;
  for(int i=0;i<bosses.Count;i++)if(bosses[i] is JsonObject boss&&IsDivision(boss["BossName"]?.GetValue<string>()??"")){
   found++;double chance=originals!=null&&i<originals.Count?Number(originals[i]?["BossChance"],100):100;boss["BossChance"]=enabled?(chance>0?chance:100):0;
  }
  if(found==0&&enabled)throw new ArgumentException("暗实验室没有黑色军团刷新数据。");return result;
 }
 internal static double Number(JsonNode? value,double fallback=0)=>value!=null&&double.TryParse(value.ToString(),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double result)?result:fallback;
 internal static List<string> Audit(JsonObject seasonal,Func<string,bool> nativeRole,Func<string,bool> template,Func<string,bool> mapExists){
  var errors=new HashSet<string>(StringComparer.Ordinal);if(seasonal["enableSeasonalEventDetection"]?.GetValue<bool>()!=true)return errors.ToList();
  var events=seasonal["events"]?.AsArray()??throw new ArgumentException("活动列表缺失。");
  var halloween=events.OfType<JsonObject>().FirstOrDefault(e=>e["type"]?.ToString()=="HALLOWEEN"&&e["enabled"]?.GetValue<bool>()==true);
  if(halloween==null)return errors.ToList();var settings=halloween["settings"];
  void Role(string? role){if(string.IsNullOrEmpty(role))return;if(!nativeRole(role))errors.Add("客户端缺少角色："+role);string profile=seasonal["eventBotMapping"]?[role]?.GetValue<string>()??role;if(!template(profile))errors.Add("服务端缺少角色模板："+role+" → "+profile);}
  var groups=new List<string>();if(seasonal["eventBossSpawns"]?["halloweennightcult"] is JsonObject)groups.Add("halloweennightcult");
  if(settings?["enableSummoning"]?.GetValue<bool>()==true||events.OfType<JsonObject>().Any(e=>e["type"]?.ToString()=="HALLOWEENSUMMON"&&e["enabled"]?.GetValue<bool>()==true))groups.Add("halloweensummon");
  bool zombies=settings?["zombieSettings"]?["enabled"]?.GetValue<bool>()==true;if(zombies)groups.Add("halloweenzombies");
  foreach(string group in groups){var maps=seasonal["eventBossSpawns"]?[group]?.AsObject();if(maps==null){errors.Add("缺少活动刷新组："+group);continue;}
   foreach(var map in maps){if(!mapExists(map.Key)){errors.Add("活动地图不存在："+map.Key);continue;}if(map.Value is not JsonArray bosses)continue;
    foreach(var boss in bosses.OfType<JsonObject>()){if(Number(boss["BossChance"])<=0)continue;Role(boss["BossName"]?.GetValue<string>());string escort=boss["BossEscortAmount"]?.ToString()??"0";if(escort.Split(',').Any(s=>int.TryParse(s,out int count)&&count>0))Role(boss["BossEscortType"]?.GetValue<string>());}
   }
  }
  if(zombies&&settings?["zombieSettings"]?["mapInfectionAmount"] is JsonObject infection)foreach(var map in infection){double amount=Number(map.Value,double.NaN);if(!double.IsFinite(amount)||amount<0||amount>100)errors.Add("感染比例须在 0～100："+map.Key);if(!mapExists(map.Key))errors.Add("感染地图不存在："+map.Key);}
  return errors.OrderBy(s=>s,StringComparer.Ordinal).ToList();
 }
}
