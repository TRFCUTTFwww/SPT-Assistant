using System.Text.Json.Nodes;
using LocalGearMail;
using LocalRaidShared;
int checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
void Reject(Action action,string name){try{action();}catch(ArgumentException){Check(true,name);return;}throw new Exception(name);}
JsonObject Profile(string prefix,string nickname){
 JsonObject Character(string side)=>new(){["_id"]=prefix+side,["aid"]=prefix=="source"?1:2,["savage"]=prefix+"scav",["Info"]=new JsonObject{["Nickname"]=nickname,["Level"]=prefix=="source"?50:1,["Experience"]=prefix=="source"?900000:0},["Health"]=new JsonObject{["Energy"]=new JsonObject{["Maximum"]=200,["Current"]=123},["Hydration"]=new JsonObject{["Maximum"]=150,["Current"]=88}},["Skills"]=new JsonObject{["Common"]=new JsonArray(new JsonObject{["Id"]="Strength",["Progress"]=1000})},["Quests"]=new JsonArray(new JsonObject{["qid"]="quest1",["status"]=2}),["Hideout"]=new JsonObject{["Areas"]=new JsonArray()},["Inventory"]=new JsonObject{["equipment"]=prefix+side+"equip",["stash"]=prefix+side+"stash",["fastPanel"]=new JsonObject{["4"]=prefix+side+"gear",["5"]=prefix+side+"coin"},["items"]=new JsonArray(new JsonObject{["_id"]=prefix+side+"equip",["_tpl"]="equip"},new JsonObject{["_id"]=prefix+side+"gear",["_tpl"]="gun",["parentId"]=prefix+side+"equip",["slotId"]="FirstPrimaryWeapon"},new JsonObject{["_id"]=prefix+side+"stash",["_tpl"]="stash"},new JsonObject{["_id"]=prefix+side+"bag",["_tpl"]="bag",["parentId"]=prefix+side+"stash",["slotId"]="hideout",["location"]=new JsonObject{["x"]=0,["y"]=0,["r"]=0}},new JsonObject{["_id"]=prefix+side+"coin",["_tpl"]="coin",["parentId"]=prefix+side+"bag",["slotId"]="main",["location"]=new JsonObject{["x"]=1,["y"]=2,["r"]=1},["upd"]=new JsonObject{["StackObjectsCount"]=777,["SpawnedInSession"]=true}})}};
 return new(){["_id"]=prefix+"session",["info"]=new JsonObject{["id"]=prefix+"session",["password"]="test-only-"+prefix},["inraid"]=new JsonObject{["location"]="none"},["characters"]=new JsonObject{["pmc"]=Character("pmc"),["scav"]=Character("scav")},["projecteLedger"]=new JsonObject{["emc"]=12345,["owner"]=prefix+"pmc"}};
}
var source=Profile("source","源角色");var target=Profile("target","新角色");string sourceRaw=source.ToJsonString(),targetRaw=target.ToJsonString();
var result=JsonNode.Parse(ProfileRestoreRules.Build(sourceRaw,targetRaw,["stash"],_=>true))!;
var inventory=result["characters"]!["pmc"]!["Inventory"]!;var items=inventory["items"]!.AsArray();
Check(result["info"]!.ToJsonString()==target["info"]!.ToJsonString(),"stash restore preserves new account credentials and identity");
Check(result["characters"]!["pmc"]!["Info"]!.ToJsonString()==target["characters"]!["pmc"]!["Info"]!.ToJsonString(),"stash-only restore keeps new character progression");
Check(items.Any(i=>i!["_id"]!.GetValue<string>()=="targetpmcgear"),"equipped items remain unchanged");
Check(items.Select(i=>i!["_id"]!.GetValue<string>()).Distinct().Count()==items.Count,"restored IDs cannot collide with equipped items");
var bag=items.Single(i=>i!["_tpl"]!.GetValue<string>()=="bag")!;var coin=items.Single(i=>i!["_tpl"]!.GetValue<string>()=="coin")!;
Check(bag["parentId"]!.GetValue<string>()=="targetpmcstash"&&coin["parentId"]!.GetValue<string>()==bag["_id"]!.GetValue<string>(),"nested containers bind to remapped parents and the new stash root");
Check(coin["_id"]!.GetValue<string>()!="sourcepmccoin"&&coin["upd"]!["StackObjectsCount"]!.GetValue<int>()==777&&coin["upd"]!["SpawnedInSession"]!.GetValue<bool>(),"item state, quantity and FIR survive ID remapping");
Check(inventory["fastPanel"]!["4"]!.GetValue<string>()=="targetpmcgear"&&inventory["fastPanel"]!["5"]==null,"only dangling quick-slot references are removed");
Check(result["characters"]!["scav"]!.ToJsonString()==target["characters"]!["scav"]!.ToJsonString(),"PMC stash restore leaves Scav intact");
var all=JsonNode.Parse(ProfileRestoreRules.Build(sourceRaw,targetRaw,["all"],_=>true))!;
Check(all["_id"]!.GetValue<string>()=="targetsession"&&all["info"]!.ToJsonString()==target["info"]!.ToJsonString(),"full restore retains new account login and session");
Check(all["characters"]!["pmc"]!["_id"]!.GetValue<string>()=="targetpmc"&&all["characters"]!["pmc"]!["aid"]!.GetValue<int>()==2&&all["characters"]!["pmc"]!["savage"]!.GetValue<string>()=="targetscav","full restore retains both character IDs and account IDs");
Check(all["characters"]!["pmc"]!["Info"]!["Level"]!.GetValue<int>()==50&&all["projecteLedger"]!["emc"]!.GetValue<int>()==12345&&all["projecteLedger"]!["owner"]!.GetValue<string>()=="targetpmc","full restore includes progression, mod extension data and translated ownership");
Check(!all["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray().Any(i=>i!["_id"]!.GetValue<string>().StartsWith("source",StringComparison.Ordinal)),"full cross-profile restore remaps inventory and root IDs");
Check(source.ToJsonString()==sourceRaw&&target.ToJsonString()==targetRaw,"planning does not mutate either input snapshot");
Reject(()=>ProfileRestoreRules.Build(sourceRaw,targetRaw,[],_=>true),"empty selection rejected");
Reject(()=>ProfileRestoreRules.Build(sourceRaw,targetRaw,["all","stash"],_=>true),"ambiguous all-plus-subset selection rejected");
Reject(()=>ProfileRestoreRules.Build(sourceRaw,targetRaw,["unknown"],_=>true),"unknown category rejected");
Reject(()=>ProfileRestoreRules.Build(sourceRaw,targetRaw,["stash"],id=>id!="bag"),"missing mod templates abort restore");
var duplicate=source.DeepClone();duplicate["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray().Add(duplicate["characters"]!["pmc"]!["Inventory"]!["items"]![0]!.DeepClone());
Reject(()=>ProfileRestoreRules.Build(duplicate.ToJsonString(),targetRaw,["all"],_=>true),"duplicate item IDs rejected");
var orphan=source.DeepClone();orphan["characters"]!["pmc"]!["Inventory"]!["items"]![4]!["parentId"]="missing";
Reject(()=>ProfileRestoreRules.Build(orphan.ToJsonString(),targetRaw,["all"],_=>true),"orphaned containers rejected");
var cycle=source.DeepClone();cycle["characters"]!["pmc"]!["Inventory"]!["items"]![3]!["parentId"]="sourcepmccoin";
Reject(()=>ProfileRestoreRules.Build(cycle.ToJsonString(),targetRaw,["all"],_=>true),"container cycles rejected");
foreach(string category in new[]{"skills","progress","health","quests","traders"})Check(JsonNode.Parse(ProfileRestoreRules.Build(sourceRaw,targetRaw,[category],_=>true))!["characters"]!["pmc"]!["Inventory"]!.ToJsonString()==target["characters"]!["pmc"]!["Inventory"]!.ToJsonString(),category+" subset does not replace inventory");
var work=Path.Combine(Path.GetTempPath(),"assistant-sqlite-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
var store=new SqliteSnapshots(Path.Combine(work,"profiles.sqlite"));var saved=store.Add("source","源角色","嵌套物品与中文 ' ; DROP TABLE snapshots; --",sourceRaw);
Check(store.List().Length==1&&store.Read(saved.Id)==sourceRaw,"SQLite roundtrip preserves exact UTF-8 snapshot and safely binds SQL-like labels");
Check(File.ReadAllBytes(Path.Combine(work,"profiles.sqlite")).Take(16).SequenceEqual(System.Text.Encoding.ASCII.GetBytes("SQLite format 3\0")),"backup is a real portable SQLite database");
var reopened=new SqliteSnapshots(Path.Combine(work,"profiles.sqlite"));Check(reopened.Read(saved.Id)==sourceRaw,"snapshot survives connection close and reopen");
Reject(()=>store.Read("../profiles.sqlite"),"lookup rejects path traversal");
Reject(()=>store.Read(new string('a',32)),"missing backup ID rejected");
var many=await Task.WhenAll(Enumerable.Range(0,8).Select(i=>Task.Run(()=>store.Add("target","新角色","并发 "+i,targetRaw))));Check(store.List().Length==9&&many.Select(s=>s.Id).Distinct().Count()==8,"concurrent writers preserve all snapshots");
var dbCopy=Path.Combine(work,"copied.sqlite");File.Copy(Path.Combine(work,"profiles.sqlite"),dbCopy);Check(new SqliteSnapshots(dbCopy).Read(saved.Id)==sourceRaw,"single database file can be moved to another installation");
var corrupted=File.ReadAllBytes(dbCopy);var marker=System.Text.Encoding.UTF8.GetBytes("sourcepmc");int damaged=0;for(int i=0;i<corrupted.Length-marker.Length;i++)if(corrupted.AsSpan(i,marker.Length).SequenceEqual(marker)){corrupted[i]^=1;damaged++;}Check(damaged>0,"locate snapshot content for corruption simulation");File.WriteAllBytes(dbCopy,corrupted);Reject(()=>new SqliteSnapshots(dbCopy).Read(saved.Id),"SHA256 catches a corrupted snapshot before restore");
new RaidSettings{NextRaidMinutes=60}.Validate();Check(true,"pre-raid duration accepts minutes");foreach(double invalid in new[]{-1,double.NaN,double.PositiveInfinity,10081})Reject(()=>new RaidSettings{NextRaidMinutes=invalid}.Validate(),"invalid duration rejected: "+invalid);
if(args.Length>0&&File.Exists(args[0])){string real=File.ReadAllText(args[0]);var typed=JsonNode.Parse(real)!.AsObject();ProfileRestoreRules.Validate(typed,null);var migrated=ProfileRestoreRules.Build(real,real,["stash"],_=>true);Check(JsonNode.Parse(migrated)!["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray().Count==typed["characters"]!["pmc"]!["Inventory"]!["items"]!.AsArray().Count,"real isolated fixture profile supports stash restore");Check(ProfileRestoreRules.Build(real,real,["all"],_=>true).Length>0,"real isolated fixture profile supports full restore");}
var a1=store.Add("target","T","战前自动 · woods",targetRaw);var a2=store.Add("target","T","战前自动 · customs",targetRaw);var a3=store.Add("target","T","战前自动 · factory",targetRaw);
Check(store.TrimPrefix("target","战前自动",2)==1&&store.List().Any(s=>s.Id==a3.Id)&&!store.List().Any(s=>s.Id==a1.Id),"trim retains newest N auto backups");
Check(store.Delete(a2.Id)&&!store.Delete(a2.Id),"delete removes backup idempotently");
Reject(()=>store.Delete("bad-id"),"delete rejects invalid backup id");
Console.WriteLine($"Passed {checks} backup and pre-raid checks. Test database: {work}");
