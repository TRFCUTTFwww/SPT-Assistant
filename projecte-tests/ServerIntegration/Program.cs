using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json.Nodes;
if(args.Length!=2)throw new ArgumentException("Expected isolated fixture server directory and game directory");
string fixture=Path.GetFullPath(args[0]),game=Path.GetFullPath(args[1]);
if(!fixture.Contains("fixture",StringComparison.OrdinalIgnoreCase)||fixture.StartsWith(game,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Integration mutations must use an isolated fixture.");
AssemblyLoadContext.Default.Resolving+=(context,name)=>{string p=Path.Combine(game,"BepInEx/core",name.Name+".dll");return File.Exists(p)?context.LoadFromAssemblyPath(p):null;};
var common=Assembly.LoadFrom(Path.Combine(game,"BepInEx/plugins/sptushonka/SPTushonka.Common.dll"));var shuffle=common.GetType("SPTushonka.Common.Http.Shuffle",true)!;var flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
byte[] Pack(byte[] data)=>(byte[])shuffle.GetMethod("Pack",flags)!.Invoke(null,[data])!;
byte[] Unpack(byte[] data)=>(byte[])shuffle.GetMethod("Unpack",flags)!.Invoke(null,[data])!;
string file=Directory.GetFiles(Path.Combine(fixture,"user/profiles"),"*.json").First();
var profile=JsonNode.Parse(File.ReadAllText(file))!;string id=profile["characters"]!["pmc"]!["_id"]!.GetValue<string>(),session=Path.GetFileNameWithoutExtension(file);
using var handler=new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,_,_,_)=>true};using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(30)};
int checks=0;void Check(bool valid,string label){if(!valid)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
async Task<JsonNode> Call(JsonObject payload,string path="/local/projecte/table"){
 if(!payload.ContainsKey("Profile")&&!payload.ContainsKey("profile"))payload["Profile"]=id;using var request=new HttpRequestMessage(HttpMethod.Post,"https://127.0.0.1:6968"+path);request.Headers.Add("Cookie","PHPSESSID="+session);request.Content=new ByteArrayContent(Pack(Encoding.UTF8.GetBytes(payload.ToJsonString())));request.Content.Headers.ContentType=new("application/json");using var response=await client.SendAsync(request);byte[] bytes=await response.Content.ReadAsByteArrayAsync();if(bytes.Length==0)throw new IOException("Empty shuffled response");bytes=Unpack(bytes);if(bytes.Length>1&&bytes[0]==0x78){using var stream=new System.IO.Compression.ZLibStream(new MemoryStream(bytes),System.IO.Compression.CompressionMode.Decompress);using var output=new MemoryStream();stream.CopyTo(output);bytes=output.ToArray();}
 var root=JsonNode.Parse(bytes)!;return root["data"]??root;
}
bool Ok(JsonNode n)=>n["Ok"]?.GetValue<bool>()??n["ok"]?.GetValue<bool>()??false;
long Balance(JsonNode n)=>n["Balance"]!.GetValue<long>();
var runtime=await Call(new(),"/local/projecte/runtime");
if(runtime["funItemsEnabled"]?.GetValue<bool>()==false){
 Check(!Ok(await Call(new(){["Action"]="state"})),"disabled fun items reject transmutation requests");
 var disabledCatalog=await Call(new(),"/client/items");Check(disabledCatalog["a7e000000000000000000113"]!=null,"disabled mode keeps existing fun-item templates to prevent profile pruning");
 var disabledAssort=await Call(new(),"/client/trading/api/getTraderAssort/a7d000000000000000000001");Check(!disabledAssort["items"]!.AsArray().Any(i=>i?["_tpl"]?.GetValue<string>() is string tpl&&(tpl.StartsWith("a7e000")||tpl.StartsWith("a7a000"))),"disabled mode removes fun goods from Developer offers");
 string adminKey=File.ReadAllText(Path.Combine(fixture,"user/mods/LocalGearMail/api-key.txt")).Trim();
 var misc=await Call(new(){["apiKey"]=adminKey,["action"]="miscStatus"},"/local/gear/data");
 var change=await Call(new(){["apiKey"]=adminKey,["action"]="miscSave",["revision"]=misc["revision"]!.GetValue<string>(),["settings"]=new JsonObject{["FunItemsEnabled"]=true}},"/local/gear/data");Check(Ok(change),"fun-item preference can be saved through the authenticated misc API");
 Check((await Call(new(),"/local/projecte/runtime"))["funItemsEnabled"]!.GetValue<bool>()==false&&!Ok(await Call(new(){["Action"]="state"})),"saved fun-item preference remains pending until server restart");
 Console.WriteLine($"Passed {checks} disabled fun-item server checks.");return;
}
Check(runtime["funItemsEnabled"]?.GetValue<bool>()==true,"enabled fun-item state is reported by the running server");
const string ruble="5449016a4bdc2d6f028b456f";
var state=await Call(new(){["Action"]="state"});Check(Ok(state),"authenticated state endpoint");long baseline=Balance(state);Check(state["Prices"]!.AsArray().Any(n=>n!["Id"]!.GetValue<string>()==ruble&&n["Value"]!.GetValue<long>()==1),"runtime ruble quote is exactly one EMC");
Check(state["Prices"]!.AsArray().Count>4000,"original EFT catalog available");Check(state["Prices"]!.AsArray().Count(n=>n!["Id"]!.GetValue<string>().StartsWith("a7e000"))==28,"four weapons and twenty-four utilities in runtime catalog");
var catalog=await Call(new(),"/client/items");
Check(catalog["a7e000000000000000000113"]!["_parent"]!.GetValue<string>()=="5447e1d04bdc2dff2f8b4567","time watch is registered as a handheld melee-slot utility");
Check(new[]{"a73c00000000000000000001","a73c00000000000000000002","a73c00000000000000000003"}.All(tpl=>catalog[tpl]==null),"retired 3-X weapon, magazine and ammunition are absent from the live catalog");
foreach(string tpl in new[]{"a7e000000000000000000001","a7e000000000000000000002","a7e000000000000000000003","a7e000000000000000000004","a7a000000000000000000001","a7a000000000000000000002","a7a000000000000000000003","a7a000000000000000000004"}){var p=catalog[tpl]!["_props"]!;Check(p["Width"]!.GetValue<int>()==1&&p["Height"]!.GetValue<int>()==2,"pixel weapon uses one by two inventory cells: "+tpl);}
// SPT omits numeric default values when serializing templates; absent damage means zero.
foreach(string tpl in new[]{"a7e000000000000000000101","a7e000000000000000000102","a7e000000000000000000103"}){var p=catalog[tpl]!["_props"]!;Check(p["Width"]!.GetValue<int>()==1&&p["Height"]!.GetValue<int>()==1&&(p["KnifeHitSlashDam"]?.GetValue<double>()??0)==0&&(p["KnifeHitStabDam"]?.GetValue<double>()??0)==0,"handheld utility is one cell and cannot deal native knife damage: "+tpl);}
string operation=Guid.NewGuid().ToString("N");var raid=new JsonObject{["Action"]="exchange",["Operation"]=operation,["Raid"]=true,["Burn"]=true,["Template"]=ruble,["Count"]=25,["Quality"]=10000};
var first=await Call(raid);Check(Ok(first)&&Balance(first)==baseline+25,"raid ledger credits exact amount");
var repeat=await Call(raid);Check(Ok(repeat)&&Balance(repeat)==baseline+25,"duplicate receipt cannot credit twice");
var undo=await Call(new(){["Action"]="undo",["Operation"]=operation});Check(Ok(undo)&&Balance(undo)==baseline,"failed native operation can reverse ledger");
undo=await Call(new(){["Action"]="undo",["Operation"]=operation});Check(Ok(undo)&&Balance(undo)==baseline,"repeated undo is idempotent");
var replay=await Call(raid);Check(!Ok(replay),"undone receipt cannot be replayed");
operation=Guid.NewGuid().ToString("N");var negative=await Call(new(){["Action"]="exchange",["Operation"]=operation,["Raid"]=true,["Burn"]=true,["Template"]=ruble,["Count"]=-10});Check(!Ok(negative),"negative stack rejected without changing wallet");
var items=profile["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray();var cash=items.First(n=>n!["_tpl"]!.GetValue<string>()==ruble&&(n["upd"]?["StackObjectsCount"]?.GetValue<double>()??1)>=10);string cashId=cash!["_id"]!.GetValue<string>();
long CountCash(){var p=JsonNode.Parse(File.ReadAllText(file))!;return (long)(p["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray().First(n=>n!["_id"]!.GetValue<string>()==cashId)!["upd"]!["StackObjectsCount"]!.GetValue<double>());}
long before=CountCash();operation=Guid.NewGuid().ToString("N");var lobby=new JsonObject{["Action"]="exchange",["Operation"]=operation,["Raid"]=false,["Burn"]=true,["Item"]=cashId,["Template"]=ruble,["Count"]=10,["Quality"]=10000};
var burned=await Call(lobby);Check(Ok(burned)&&Balance(burned)==baseline+10&&CountCash()==before-10,"lobby burn updates profile item count and wallet together");
var burnedAgain=await Call(lobby);Check(Ok(burnedAgain)&&CountCash()==before-10,"lobby replay cannot consume twice");
string invalidOp=Guid.NewGuid().ToString("N");var invalid=await Call(new(){["Action"]="exchange",["Operation"]=invalidOp,["Raid"]=false,["Burn"]=false,["Template"]=ruble,["Count"]=1,["Parent"]=profile["characters"]!["pmc"]!["Inventory"]!["stash"]!.GetValue<string>(),["Slot"]="hideout",["X"]=999,["Y"]=999});Check(!Ok(invalid),"out of bounds destination rejected");
state=await Call(new(){["Action"]="state"});Check(Balance(state)==baseline+10,"failed output leaves wallet unchanged");
undo=await Call(new(){["Action"]="undo",["Operation"]=operation});Check(Ok(undo)&&Balance(undo)==baseline&&CountCash()==before,"lobby rollback restores both inventory and wallet");
Check(Balance(await Call(new(){["Action"]="state"}))==baseline,"persisted wallet returns to baseline");
var fresh=JsonNode.Parse(File.ReadAllText(file))!;var inventory=fresh["characters"]!["pmc"]!["Inventory"]!;string stash=inventory["stash"]!.GetValue<string>();
var currentItems=inventory["items"]!.AsArray();var stashItem=currentItems.Single(n=>n!["_id"]!.GetValue<string>()==stash)!;
var templates=JsonNode.Parse(File.ReadAllText(Path.Combine(fixture,"SPT_Data/database/templates/items.json")))!;
var grid=templates[stashItem["_tpl"]!.GetValue<string>()]!["_props"]!["Grids"]![0]!;string slot=grid["_name"]!.GetValue<string>();int gw=grid["_props"]!["cellsH"]!.GetValue<int>(),gh=grid["_props"]!["cellsV"]!.GetValue<int>();
bool[,] occupied=new bool[gw,gh];foreach(var item in currentItems.Where(n=>n?["parentId"]?.GetValue<string>()==stash&&n?["slotId"]?.GetValue<string>()==slot)){
 var loc=item!["location"];if(loc is not JsonObject)continue;int x=loc["x"]!.GetValue<int>(),y=loc["y"]!.GetValue<int>();string rotation=loc["r"]?.ToString()??"0";int r=rotation is "1" or "Vertical"?1:0;var props=templates[item["_tpl"]!.GetValue<string>()]!["_props"]!;int w=props["Width"]!.GetValue<int>(),h=props["Height"]!.GetValue<int>();if(r==1)(w,h)=(h,w);for(int xx=x;xx<Math.Min(x+w,gw);xx++)for(int yy=y;yy<Math.Min(y+h,gh);yy++)occupied[xx,yy]=true;
}
int freeX=-1,freeY=-1;for(int y=0;y<gh&&freeX<0;y++)for(int x=0;x<gw;x++)if(!occupied[x,y]){freeX=x;freeY=y;break;}Check(freeX>=0,"isolated fixture has a real free stash cell");
operation=Guid.NewGuid().ToString("N");lobby["Operation"]=operation;Check(Ok(await Call(lobby)),"second lobby funding conversion");
string takeId=Guid.NewGuid().ToString("N");var take=new JsonObject{["Action"]="exchange",["Operation"]=takeId,["Raid"]=false,["Burn"]=false,["Template"]=ruble,["Count"]=10,["Parent"]=stash,["Slot"]=slot,["X"]=freeX,["Y"]=freeY,["Rotation"]=0};
var taken=await Call(take);Check(Ok(taken)&&Balance(taken)==baseline,"normal output deducts EMC");string created=taken["Created"]!.GetValue<string>();
var saved=JsonNode.Parse(File.ReadAllText(file))!;var createdItem=saved["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray().Single(n=>n!["_id"]!.GetValue<string>()==created)!;
Check(createdItem["_tpl"]!.GetValue<string>()==ruble&&createdItem["parentId"]!.GetValue<string>()==stash&&createdItem["upd"]!["StackObjectsCount"]!.GetValue<double>()==10,"output is a persisted independent inventory item");
Check(createdItem["upd"]!["SpawnedInSession"]!.GetValue<bool>()==false,"transmuted output is not marked found in raid");
var takeAgain=await Call(take);Check(Ok(takeAgain)&&takeAgain["Created"]!.GetValue<string>()==created&&Balance(takeAgain)==baseline,"output retry reuses receipt and item ID");
var reversed=await Call(new(){["Action"]="undo",["Operation"]=takeId});Check(Ok(reversed)&&Balance(reversed)==baseline+10,"failed native insertion can reverse output transaction");
saved=JsonNode.Parse(File.ReadAllText(file))!;Check(!saved["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray().Any(n=>n!["_id"]!.GetValue<string>()==created),"output rollback removes generated item");
take["Operation"]=Guid.NewGuid().ToString("N");Check(Ok(await Call(take)),"output can be retried as a fresh transaction after rollback");
Check(Balance(await Call(new(){["Action"]="state"}))==baseline,"successful ruble round trip preserves original EMC balance");
checks+=await RuntimeChecks.Run(Call,fixture,game,id,session,client);
checks+=await HealthChecks.Run(Call,fixture);
checks+=await BackupChecks.Run(Call,fixture);
Console.WriteLine($"Passed {checks} isolated server integration checks.");
