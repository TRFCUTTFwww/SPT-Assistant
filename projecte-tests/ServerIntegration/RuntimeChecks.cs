using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
internal static class RuntimeChecks {
 internal static async Task<int> Run(Func<JsonObject,string,Task<JsonNode>> call,string fixture,string game,string profile,string session,HttpClient client){
  int checks=0;
  void Check(bool valid,string name){if(!valid)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
  bool Ok(JsonNode node)=>node["ok"]?.GetValue<bool>()??node["Ok"]?.GetValue<bool>()??false;
  Task<JsonNode> Table(JsonObject payload)=>call(payload,"/local/projecte/table");
  Task<JsonNode> Store(JsonObject payload){payload["profile"]=profile;return call(payload,"/local/projecte/terminal");}
  var catalog=await call(new(),"/client/items");
  const string dev="a7d000000000000000000001",white="a7e000000000000000000104",ruble="5449016a4bdc2d6f028b456f";
  string[] bags=[white,..Enumerable.Range(301,15).Select(i=>"a7e"+i.ToString().PadLeft(21,'0'))];
  var gridIds=new HashSet<string>();
  foreach(string id in bags){var p=catalog[id]!["_props"]!;var grid=p["Grids"]![0]!;Check(p["Width"]!.GetValue<int>()==1&&p["Height"]!.GetValue<int>()==1&&grid["_props"]!["cellsH"]!.GetValue<int>()==13&&grid["_props"]!["cellsV"]!.GetValue<int>()==8,"native independent 104-cell bag: "+id);gridIds.Add(grid["_id"]!.GetValue<string>());}
  Check(gridIds.Count==16,"different bag templates have distinct native grid IDs");
  var original=JsonNode.Parse(File.ReadAllText(Path.Combine(fixture,"SPT_Data/database/templates/items.json")))!.AsObject().Select(x=>x.Key).ToHashSet();
  var assort=await call(new(),"/client/trading/api/getTraderAssort/"+dev);var offers=assort["items"]!.AsArray().Where(x=>x!["parentId"]?.GetValue<string>()=="hideout").ToArray();
  Check(offers.Length>=32&&offers.All(x=>!original.Contains(x!["_tpl"]!.GetValue<string>())),"Developer sells all public mod goods and no vanilla goods");
  var jaeger=await call(new(),"/client/trading/api/getTraderAssort/5c0647fdd443bc2504c2d371");
  Check(!jaeger["items"]!.AsArray().Any(x=>x!["_tpl"]!.GetValue<string>().StartsWith("a7")),"mod offers removed from original Jaeger");
  var settings=await call(new(),"/client/trading/api/traderSettings");
  Check(settings.AsArray().Any(x=>x!["_id"]!.GetValue<string>()==dev&&x["nickname"]!.GetValue<string>()=="Developer"),"Developer appears in native trader settings");
  using(var imageRequest=new HttpRequestMessage(HttpMethod.Get,"https://127.0.0.1:6968/files/trader/avatar/developer.png")){
   imageRequest.Headers.Add("Cookie","PHPSESSID="+session);using var response=await client.SendAsync(imageRequest);var image=await response.Content.ReadAsByteArrayAsync();
   Check(response.IsSuccessStatusCode&&SHA256.HashData(image).SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(fixture,"user/mods/LocalGearMail/Minecraft/ProjectE/Assets/developer.png")))),"Developer avatar serves the exact user-supplied image");
  }
  var state=await Store(new(){["action"]="state"});Check(Ok(state),"wireless terminal works without assistant server mod");
  var rejected=await call(new(){["action"]="state",["profile"]="000000000000000000000000"},"/local/projecte/terminal");Check(!Ok(rejected),"terminal rejects a different PMC profile");
  var before=state["items"]!.DeepClone();string beforeRevision=state["revision"]!.GetValue<string>();string stash=state["stash"]!.GetValue<string>();
  var unchanged=await Store(new(){["action"]="save",["revision"]=beforeRevision,["items"]=before.DeepClone()});Check(Ok(unchanged)&&unchanged["revision"]!.GetValue<string>()==beforeRevision,"no-op terminal save preserves revision");
  var duplicate=before.DeepClone().AsArray();duplicate.Add(duplicate[0]!.DeepClone());
  Check(!Ok(await Store(new(){["action"]="save",["revision"]=beforeRevision,["items"]=duplicate})),"terminal rejects duplicate item IDs");
  string NewId()=>Guid.NewGuid().ToString("N")[..24];string firstBag=NewId(),secondBag=NewId(),coinId=NewId();
  var injected=before.DeepClone().AsArray();
  var stashRoot=before.AsArray().Single(x=>x!["_id"]!.GetValue<string>()==stash)!;
  var gridDefinition=catalog[stashRoot["_tpl"]!.GetValue<string>()]!["_props"]!["Grids"]![0]!;
  string gridName=gridDefinition["_name"]!.GetValue<string>();int width=gridDefinition["_props"]!["cellsH"]!.GetValue<int>(),height=gridDefinition["_props"]!["cellsV"]!.GetValue<int>();
  var occupied=new HashSet<(int,int)>();
  foreach(var item in before.AsArray().Where(x=>x?["parentId"]?.GetValue<string>()==stash&&x?["location"] is JsonObject)){
   var loc=item!["location"]!;int x=loc["x"]!.GetValue<int>(),y=loc["y"]!.GetValue<int>();var props=catalog[item["_tpl"]!.GetValue<string>()]!["_props"]!;
   int w=props["Width"]!.GetValue<int>(),h=props["Height"]!.GetValue<int>();if(loc["r"]?.ToString() is "1" or "Vertical")(w,h)=(h,w);
   for(int xx=x;xx<x+w;xx++)for(int yy=y;yy<y+h;yy++)occupied.Add((xx,yy));
  }
  var free=(from y in Enumerable.Range(0,height) from x in Enumerable.Range(0,width) where !occupied.Contains((x,y)) select (X:x,Y:y)).Take(2).ToArray();Check(free.Length==2,"isolated stash has two real free cells for bag fixtures");
  // This fixture-only hierarchy verifies per-instance persistence even for two
  // bags of the SAME color. Restore the exact original tree in finally.
  JsonObject Container(string id,int index)=>new(){["_id"]=id,["_tpl"]=white,["parentId"]=stash,["slotId"]=gridName,["location"]=new JsonObject{["x"]=free[index].X,["y"]=free[index].Y,["r"]=0},["upd"]=new JsonObject{["StackObjectsCount"]=1}};
  injected.Add(Container(firstBag,0));injected.Add(Container(secondBag,1));
  injected.Add(new JsonObject{["_id"]=coinId,["_tpl"]=ruble,["parentId"]=firstBag,["slotId"]="main",["location"]=new JsonObject{["x"]=0,["y"]=0,["r"]=0},["upd"]=new JsonObject{["StackObjectsCount"]=1}});
  try{
   var saved=await Store(new(){["action"]="save",["revision"]=beforeRevision,["items"]=injected.DeepClone()});Check(Ok(saved)&&saved["revision"]!.GetValue<string>()!=beforeRevision,"terminal persists a changed stash tree");
   var replay=await Store(new(){["action"]="save",["revision"]=beforeRevision,["items"]=injected.DeepClone()});Check(Ok(replay)&&replay["revision"]!.GetValue<string>()==saved["revision"]!.GetValue<string>(),"lost terminal response can be retried idempotently");
   var stale=before.DeepClone().AsArray();stale[0]!["upd"]=new JsonObject{["StackObjectsCount"]=9};
   Check(!Ok(await Store(new(){["action"]="save",["revision"]=beforeRevision,["items"]=stale})),"stale different stash snapshot cannot overwrite newer storage");
   var fresh=await Store(new(){["action"]="state"});var items=fresh["items"]!.AsArray();
   Check(items.Count(x=>x?["parentId"]?.GetValue<string>()==firstBag)==1&&items.All(x=>x?["parentId"]?.GetValue<string>()!=secondBag),"same-color bag instances persist independent contents");
  }finally{var latest=await Store(new(){["action"]="state"});var restored=await Store(new(){["action"]="save",["revision"]=latest["revision"]!.DeepClone(),["items"]=before.DeepClone()});Check(Ok(restored),"fixture stash restored after independent-container checks");}
  long Balance(JsonNode node)=>node["Balance"]!.GetValue<long>();
  long baseline=Balance(await Table(new(){["Action"]="state"}));
  var funding=await Table(new(){["Action"]="exchange",["Operation"]=Guid.NewGuid().ToString("N"),["Raid"]=true,["Burn"]=true,["Template"]=ruble,["Count"]=1000,["Quality"]=10000});Check(Ok(funding),"fixture artifact budget funded");
  string operation=Guid.NewGuid().ToString("N");var effect=new JsonObject{["Action"]="artifact",["Operation"]=operation,["Raid"]=true,["Template"]="a7e000000000000000000110",["Count"]=1,["Quality"]=0};
  var paid=await Table(effect);Check(Ok(paid)&&Balance(paid)==baseline+936,"body stone debits exactly one funded effect");
  var repeated=await Table(effect);Check(Ok(repeated)&&Balance(repeated)==Balance(paid),"artifact retries cannot debit twice");
  var invalid=await Table(new(){["Action"]="artifact",["Operation"]=Guid.NewGuid().ToString("N"),["Template"]="a7e000000000000000000113",["Count"]=0,["Quality"]=3});Check(!Ok(invalid)&&Balance(await Table(new(){["Action"]="state"}))==baseline+936,"invalid watch charge cannot spend EMC");
  var drain=await Table(new(){["Action"]="exchange",["Operation"]=Guid.NewGuid().ToString("N"),["Raid"]=true,["Burn"]=false,["Template"]=ruble,["Count"]=936});Check(Ok(drain)&&Balance(drain)==baseline,"artifact test wallet returned to baseline");
  var runtime=await call(new(),"/local/projecte/runtime");Check(Ok(runtime)&&runtime["ready"]!.GetValue<bool>(),"stable server host reports ready");
  if(runtime["integrated"]?.GetValue<bool>()==true){
   Check(runtime["hotReload"]?.GetValue<bool>()==false&&runtime["version"]?.GetValue<string>()=="2.3.2","Minecraft server belongs to Assistant and independent hot loading is disabled");
   Check(!File.Exists(Path.Combine(fixture,"user/mods/ProjectE/Local.ProjectE.Logic.module")),"old standalone MC logic module is absent");
   Check(Ok(await Table(new(){["Action"]="state"})),"table remains available without a standalone module");
   return checks;
  }
  int generation=runtime["generation"]!.GetValue<int>();string goodHash=runtime["sha256"]!.GetValue<string>();
  string module=Path.Combine(fixture,"user/mods/ProjectE/Local.ProjectE.Logic.module");byte[] good=await File.ReadAllBytesAsync(module);
  try{
   await File.WriteAllTextAsync(module,"invalid candidate fixture");await Task.Delay(1200);
   var failed=await call(new(),"/local/projecte/runtime");Check(Ok(failed)&&failed["generation"]!.GetValue<int>()==generation&&failed["sha256"]!.GetValue<string>()==goodHash,"invalid server module keeps the last working generation");
   Check(Ok(await Table(new(){["Action"]="state"})),"table endpoint stays available after rejected hot update");
  }finally{await File.WriteAllBytesAsync(module,good);await Task.Delay(1200);await call(new(),"/local/projecte/runtime");}
  return checks;
 }
}
