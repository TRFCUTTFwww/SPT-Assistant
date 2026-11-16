using System.Text.Json.Nodes;
internal static class FleaChecks {
 const string Ruble="5449016a4bdc2d6f028b456f",Bandage="544fb25a4bdc2dfb738b4567",Ammo="560d5e524bdc2d25448b4571";
 internal static string ItemId(int i)=>"a231f1ea000000000000"+i.ToString("x4");
 internal static void Prepare(string fixture){
  if(!fixture.Contains("fixture",StringComparison.OrdinalIgnoreCase))throw new Exception("Isolated fixture required");
  string file=Directory.GetFiles(Path.Combine(fixture,"user/profiles"),"*.json").Single();
  string backup=Path.Combine(fixture,"flea-test-original-profile.json");if(File.Exists(backup))throw new Exception("Restore prior flea fixture backup first");
  File.Copy(file,backup);
  var profile=JsonNode.Parse(File.ReadAllText(file))!;var pmc=profile["characters"]!["pmc"]!;pmc["Info"]!["Level"]=70;
  pmc["RagfairInfo"]!["offers"]=new JsonArray();
  var inventory=pmc["Inventory"]!;var items=inventory["items"]!.AsArray();
  foreach(var cash in items.Where(n=>n?["_tpl"]?.GetValue<string>()==Ruble).ToArray())items.Remove(cash);
  for(int i=1;i<=8;i++)items.Add(new JsonObject{["_id"]=ItemId(i),["_tpl"]=i==8?Ammo:Bandage,["parentId"]=inventory["stash"]!.DeepClone(),["slotId"]="hideout",["location"]=new JsonObject{["x"]=i-1,["y"]=0,["r"]=0},["upd"]=new JsonObject{["StackObjectsCount"]=i==8?5:1,["SpawnedInSession"]=true}});
  File.WriteAllText(file,profile.ToJsonString());Console.WriteLine("Prepared isolated no-cash flea profile with eight disposable listings.");
 }
 internal static async Task<int> Run(Func<JsonObject,string,Task<JsonNode>> call,string fixture){
  int count=0;void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
  string file=Directory.GetFiles(Path.Combine(fixture,"user/profiles"),"*.json").Single();
  JsonNode Read()=>JsonNode.Parse(File.ReadAllText(file))!;
  Check(File.Exists(Path.Combine(fixture,"flea-test-original-profile.json"))&&Read()["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray().Any(n=>n?["_id"]?.GetValue<string>()==ItemId(1)),"flea mutations use a prepared isolated fixture");
  string key=File.ReadAllText(Path.Combine(fixture,"user/mods/LocalGearMail/api-key.txt")).Trim();
  async Task<JsonNode> Admin(string action,JsonObject? settings=null,string? revision=null)=>await call(new(){["apiKey"]=key,["action"]=action,["settings"]=settings,["revision"]=revision},"/local/gear/data");
  var initial=await Admin("miscStatus");var settings=initial["settings"]!.DeepClone();
  bool Ok(JsonNode n)=>n["ok"]?.GetValue<bool>()??false;
  async Task Toggle(bool instant){var current=await Admin("miscStatus");Check(Ok(await Admin("miscSave",new(){["FreeFlea"]=true,["InstantFleaSales"]=instant},current["revision"]!.GetValue<string>())),"native misc settings save: instant="+instant);}
  async Task Flush(){var current=await Admin("miscStatus");if(!Ok(await Admin("miscSave",new(),current["revision"]!.GetValue<string>())))throw new Exception("Fixture flush failed");}
  IEnumerable<JsonNode> Messages(JsonNode node){if(node is JsonObject o){if(o["text"]!=null&&o["items"]!=null)yield return o;foreach(var p in o)if(p.Value!=null)foreach(var n in Messages(p.Value))yield return n;}else if(node is JsonArray a)foreach(var child in a)if(child!=null)foreach(var n in Messages(child))yield return n;}
  long Cash(JsonNode node){long total=0;if(node is JsonObject o){if(o["_tpl"]?.GetValue<string>()==Ruble)total+=(long)(o["upd"]?["StackObjectsCount"]?.GetValue<double>()??1);foreach(var p in o)if(p.Value!=null)total+=Cash(p.Value);}else if(node is JsonArray a)foreach(var n in a)if(n!=null)total+=Cash(n);return total;}
  async Task List(int[] ids,bool pack,int price,int expected){
   var before=Read();long cash=Cash(before["dialogues"]??new JsonObject());int mails=Messages(before["dialogues"]??new JsonObject()).Count(n=>n["text"]!.ToString().Contains("Assistant"));
   var req=new JsonObject{["Action"]="RagFairAddOffer",["items"]=new JsonArray(ids.Select(i=>(JsonNode?)JsonValue.Create(ItemId(i))).ToArray()),["requirements"]=new JsonArray(new JsonObject{["_tpl"]=Ruble,["count"]=price}),["sellInOnePiece"]=pack};
   var result=await call(new(){["data"]=new JsonArray(req),["tm"]=0},"/client/game/profile/items/moving");
   Check((result["warnings"]?.AsArray().Count??0)==0&&result["err"]==null,"native flea listing succeeds with no cash: "+string.Join(',',ids)+" pack="+pack+" result="+result.ToJsonString()[..Math.Min(160,result.ToJsonString().Length)]);
   // Profile writes are awaited by the admin API and expose native mail/reputation results.
   await Flush();var after=Read();
   Check(!after["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray().Any(n=>ids.Any(i=>n?["_id"]?.GetValue<string>()==ItemId(i))),"native listing removes the exact sold inventory items");
   Check(!after["characters"]!["pmc"]!["RagfairInfo"]!["offers"]!.AsArray().Any(n=>n?["items"]?.AsArray().Any(i=>ids.Any(id=>i?["_id"]?.GetValue<string>()==ItemId(id)))==true),"instant completion leaves no active or ghost offer");
   Check(Cash(after["dialogues"]??new JsonObject())-cash==expected,"native Ragman payout is exact: "+expected+" rubles");
   Check(Messages(after["dialogues"]??new JsonObject()).Count(n=>n["text"]!.ToString().Contains("Assistant"))>mails,"native sold-item mail identifies buyer as Assistant");
  }
  try{
   await Toggle(true);await List([1],false,100,100);await List([2,3,4],true,1000,1000);await List([5,6],false,200,400);
   var rejected=await call(new(){["data"]=new JsonArray(new JsonObject{["Action"]="RagFairAddOffer",["items"]=new JsonArray(ItemId(8)),["requirements"]=new JsonArray(new JsonObject{["_tpl"]=Ruble,["count"]=100}),["sellInOnePiece"]=false}),["tm"]=0},"/client/game/profile/items/moving");
   Check((rejected["warnings"]?.AsArray().Count??0)>0&&Read()["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray().Any(n=>n?["_id"]?.GetValue<string>()==ItemId(8)),"instant sale preserves native flea item/category restrictions");
   await Toggle(false);
   var normal=await call(new(){["data"]=new JsonArray(new JsonObject{["Action"]="RagFairAddOffer",["items"]=new JsonArray(ItemId(7)),["requirements"]=new JsonArray(new JsonObject{["_tpl"]=Ruble,["count"]=1000000}),["sellInOnePiece"]=false}),["tm"]=0},"/client/game/profile/items/moving");
   Check((normal["warnings"]?.AsArray().Count??0)==0&&normal["err"]==null,"disabled instant sales still permit native listing");await Flush();
   Check(Read()["characters"]!["pmc"]!["RagfairInfo"]!["offers"]!.AsArray().Any(n=>n?["items"]?.AsArray().Any(i=>i?["_id"]?.GetValue<string>()==ItemId(7))==true),"disabled instant sale retains normal market offer");
  }finally{var current=await Admin("miscStatus");Check(Ok(await Admin("miscSave",settings.AsObject(),current["revision"]!.GetValue<string>())),"original fixture misc settings restored");}
  return count;
 }
}
