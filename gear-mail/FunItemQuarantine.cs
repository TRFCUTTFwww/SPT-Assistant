using System.Text.Json.Nodes;
using HarmonyLib;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public static class FunItemQuarantine {
 static SaveServer saves=null!;static JsonUtil json=null!;static TemplateTable templates=null!;static MailSendService mail=null!;
 static readonly string Home=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","fun-items");
 public static void Install(SaveServer server,JsonUtil serializer,TemplateTable table,MailSendService delivery){saves=server;json=serializer;templates=table;mail=delivery;var harmony=new Harmony("local.assistant.fun-quarantine");harmony.UnpatchSelf();harmony.Patch(AccessTools.Method(typeof(SaveServer),"LoadProfileAsync"),prefix:new HarmonyMethod(typeof(FunItemQuarantine),nameof(BeforeLoad)),postfix:new HarmonyMethod(typeof(FunItemQuarantine),nameof(AfterLoad)));}
 static void Backup(string session,string original){Directory.CreateDirectory(Home);File.WriteAllText(System.IO.Path.Combine(Home,session+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N")+".json"),original);}
 static void BeforeLoad(MongoId __0){if(FunItemPolicy.EnabledAtStartup)return;string file=System.IO.Path.Combine(AppContext.BaseDirectory,"user","profiles",__0+".json");if(!File.Exists(file))return;string original=File.ReadAllText(file),clean=FunQuarantineRules.Strip(original,out int count);if(count==0)return;Backup(__0.ToString(),original);string next=file+".assistant-next";File.WriteAllText(next,clean);File.Move(next,file,true);}
 static void AfterLoad(MongoId __0,ref Task __result){if(FunItemPolicy.EnabledAtStartup)__result=RestoreAfterLoad(__0,__result);}
 static async Task RestoreAfterLoad(MongoId session,Task loaded){await loaded;await Process(session);}
 public static async Task ProcessAll(){foreach(var session in saves.GetProfiles().Keys.ToArray())await Process(session);}
 static async Task Process(MongoId session){var profile=saves.GetProfile(session);if(profile==null)return;string original=json.Serialize(profile)!;
  if(!FunItemPolicy.EnabledAtStartup){string clean=FunQuarantineRules.Strip(original,out int count);if(count==0)return;Backup(session.ToString(),original);var converted=json.Deserialize<SptProfile>(clean)!;profile.CharacterData=converted.CharacterData;profile.DialogueRecords=converted.DialogueRecords;profile.ExtensionData=converted.ExtensionData;await saves.SaveProfileAsync(session);return;}
  var raw=JsonNode.Parse(original)!.AsObject();if(raw[FunQuarantineRules.Key] is not JsonArray groups)return;var ready=new List<JsonArray>();var items=new List<Item>();foreach(var group in groups.OfType<JsonArray>()){var parsed=json.Deserialize<List<Item>>(group.ToJsonString())!;if(parsed.All(i=>templates.Items.ContainsKey(i.Template))){ready.Add(group);items.AddRange(parsed);}}if(items.Count==0)return;
  Backup(session.ToString(),original);mail.SendSystemMessageToPlayer(session,"趣味物品已重新启用，以下是此前停用时保管的物品。",items,365L*24*60*60);foreach(var group in ready)groups.Remove(group);profile.ExtensionData??=[];if(groups.Count==0)profile.ExtensionData.Remove(FunQuarantineRules.Key);else profile.ExtensionData[FunQuarantineRules.Key]=System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(groups.ToJsonString());await saves.SaveProfileAsync(session);
 }
}
