using System.Text.Json.Nodes;
internal static class BackupChecks {
 internal static async Task<int> Run(Func<JsonObject,string,Task<JsonNode>> call,string fixture){
  int checks=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
  string key=File.ReadAllText(Path.Combine(fixture,"user/mods/LocalGearMail/api-key.txt")).Trim();
  Task<JsonNode> Send(string action,string id="",string[]? categories=null,string revision="",string preview="")=>call(new(){["apiKey"]=key,["action"]=action,["id"]=id,["categories"]=new JsonArray((categories??[]).Select(s=>(JsonNode?)JsonValue.Create(s)).ToArray()),["revision"]=revision,["preview"]=preview},"/local/gear/data");
  bool Ok(JsonNode value)=>value["ok"]?.GetValue<bool>()==true;
  var list=await Send("backupList");Check(Ok(list)&&list["backups"] is JsonArray,"authenticated SQLite backup list endpoint");
  string label="isolated-backup-test-"+Guid.NewGuid().ToString("N");var created=await Send("backupCreate",label);Check(Ok(created),"native profile snapshot commits to SQLite");
  var row=created["backups"]!.AsArray().Single(n=>n!["Label"]!.GetValue<string>()==label)!;string id=row["Id"]!.GetValue<string>();
  string file=Directory.GetFiles(Path.Combine(fixture,"user/profiles"),"*.json").First();var initial=JsonNode.Parse(File.ReadAllText(file))!;
  Check(File.ReadAllBytes(Path.Combine(fixture,"user/mods/LocalGearMail/backups/profiles.sqlite")).Take(16).SequenceEqual(System.Text.Encoding.ASCII.GetBytes("SQLite format 3\0")),"running SPT service writes an actual SQLite file");
  async Task<JsonNode> Restore(string[] categories){var p=await Send("backupPreview",id,categories);Check(Ok(p),"server validates restore plan for "+string.Join(",",categories));return await Send("backupRestore",id,categories,p["revision"]!.GetValue<string>(),p["preview"]!.GetValue<string>());}
  var preview=await Send("backupPreview",id,["stash"]);Check(Ok(preview),"native deserializer accepts a remapped stash preview");
  var stale=await Send("backupRestore",id,["stash"],"stale",preview["preview"]!.GetValue<string>());Check(!Ok(stale),"restore endpoint rejects stale target revision");
  Check(JsonNode.DeepEquals(initial,JsonNode.Parse(File.ReadAllText(file))),"rejected restore leaves persisted profile unchanged");
  var forged=await Send("backupRestore",id,["all"],preview["revision"]!.GetValue<string>(),preview["preview"]!.GetValue<string>());Check(!Ok(forged),"preview for stash cannot authorize an all-data restore");
  try{
   var restored=await Restore(["stash"]);Check(Ok(restored)&&restored["requiresClientRestart"]!.GetValue<bool>(),"actual stash restore persists and requires client relogin");
   var after=JsonNode.Parse(File.ReadAllText(file))!;
   Check(JsonNode.DeepEquals(initial["info"],after["info"])&&JsonNode.DeepEquals(initial["characters"]!["pmc"]!["Info"],after["characters"]!["pmc"]!["Info"]),"persisted stash restore preserves login and character progress");
   var beforeItems=initial["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray();var afterItems=after["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray();Check(beforeItems.Count==afterItems.Count,"actual stash restore preserves total native item count");
   var snapshots=await Send("backupList");Check(snapshots["backups"]!.AsArray().Any(n=>n!["Id"]!.GetValue<string>()==restored["rollback"]!.GetValue<string>()&&n["Label"]!.GetValue<string>()=="还原前自动备份"),"successful restore retains a durable rollback snapshot");
  }finally{var full=await Restore(["all"]);Check(Ok(full),"full restore returns isolated profile to original snapshot");}
  var final=JsonNode.Parse(File.ReadAllText(file))!;
  // SPT's RefillExpiredLimits moves an idle/full battle-pass reset clock to
  // now + interval on ordinary periodic updates. Preserve every counter and
  // compare the rest exactly; a partially consumed clock must stay exact.
  foreach(string side in new[]{"pmc","scav"})if(initial["characters"]?[side]?["battlePassDocumentLimitData"] is JsonObject clocks)foreach(var clock in clocks){var actual=final["characters"]?[side]?["battlePassDocumentLimitData"]?[clock.Key];if(actual!=null&&clock.Value?["remainingLimit"]?.ToString()==clock.Value?["totalLimit"]?.ToString()&&clock.Value?["totalLimit"]!=null)actual["nextResetTime"]=clock.Value!["nextResetTime"]?.DeepClone();}
  Check(JsonNode.DeepEquals(initial["characters"],final["characters"]),"complete character roundtrip retains all data apart from native idle daily-reset clocks");
  return checks;
 }
}
