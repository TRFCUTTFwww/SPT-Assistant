using System.Text.Json.Nodes;
using LocalGearMail;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
var session=new MongoId("aaaaaaaaaaaaaaaaaaaaaaaa");
var json=new JsonUtil([new SPTarkov.Server.Core.Utils.Json.SptJsonConverterRegistrator()]);
SptProfile Fixture(int quantity)=>json.Deserialize<SptProfile>("""
{"info":{"id":"aaaaaaaaaaaaaaaaaaaaaaaa","username":"isolated-save-test"},"characters":{"pmc":{"_id":"bbbbbbbbbbbbbbbbbbbbbbbb","Inventory":{"stash":"cccccccccccccccccccccccc","items":[{"_id":"cccccccccccccccccccccccc","_tpl":"566abbc34bdc2d92178b4576"},{"_id":"dddddddddddddddddddddddd","_tpl":"5449016a4bdc2d6f028b456f","parentId":"cccccccccccccccccccccccc","slotId":"hideout","upd":{"StackObjectsCount":QUANTITY}}]}},"scav":{"_id":"eeeeeeeeeeeeeeeeeeeeeeee"}},"testExtension":{"marker":"preserve-extension"}}
""".Replace("QUANTITY",quantity.ToString()))!;
int checks=0;
void Check(bool valid,string message){if(!valid)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
var initialDirectory=Directory.GetCurrentDirectory();var temporary=Path.Combine(Path.GetTempPath(),"spt-profile-save-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(temporary,"user","profiles"));Directory.SetCurrentDirectory(temporary);
try{
 var config=Activator.CreateInstance<CoreConfig>();typeof(CoreConfig).GetProperty(nameof(CoreConfig.Features))!.SetValue(config,Activator.CreateInstance<ServerFeatures>());
 var saves=new SaveServer(new FileUtil(),[],json,new HashUtil(null!),null!,null!,null!,config);
 var original=Fixture(3);saves.AddProfile(original);await saves.SaveProfileAsync(session);
 string file=Path.Combine("user","profiles",session+".json");
 long Quantity()=>JsonNode.Parse(File.ReadAllText(file))!["characters"]!["pmc"]!["Inventory"]!["items"]![1]!["upd"]!["StackObjectsCount"]!.GetValue<long>();
 saves.GetProfiles()[session]=Fixture(777);await saves.SaveProfileAsync(session);
 Check(Quantity()==3&&ReferenceEquals(original,saves.GetProfile(session)),"reproduce dictionary-copy bug with actual SPT SaveServer");
 await LiveProfileState.RestoreAndSave(original,Fixture(777),session,saves,json);
 Check(Quantity()==777,"restore writes changed stash quantity to the actual profile file");
 Check(ReferenceEquals(original,saves.GetProfile(session)),"restore preserves live profile identity");
 Check(JsonNode.DeepEquals(JsonNode.Parse(json.Serialize(original)!),JsonNode.Parse(File.ReadAllText(file))),"live profile and saved JSON match including extension data");
 await LiveProfileState.RestoreAndSave(original,Fixture(3),session,saves,json);Check(Quantity()==3,"rollback writes original inventory through the same live instance");
 var invalid=Fixture(888);typeof(Info).GetProperty(nameof(Info.InvalidOrUnloadableProfile))!.SetValue(invalid.ProfileInfo,true);bool refused=false;try{await LiveProfileState.RestoreAndSave(original,invalid,session,saves,json);}catch(IOException){refused=true;}
 Check(refused&&Quantity()==3,"skipped server save is detected instead of reporting false success");
 await LiveProfileState.RestoreAndSave(original,Fixture(3),session,saves,json);
 bool stale=false;try{await LiveProfileState.RestoreAndSave(Fixture(1),Fixture(777),session,saves,json);}catch(InvalidOperationException){stale=true;}
 Check(stale&&Quantity()==3,"stale target is rejected before touching the saved inventory");
 var inventory=original.CharacterData!.PmcData!.Inventory!;
 var item=inventory.Items!.Last();string baseline=json.Serialize(item)!;
 var draft=JsonNode.Parse(baseline)!.AsObject();draft["upd"]!["StackObjectsCount"]=234;
 draft["upd"]!["customJson"]=new JsonObject{["marker"]="preserve",["values"]=new JsonArray(1,true,"text")};
 InventoryItemJson.Apply(inventory,item.Id.ToString(),item.Template.ToString(),draft.ToJsonString(),InventoryItemJson.Revision(item,json),json);
 await saves.SaveProfileAsync(session);
 var disk=JsonNode.Parse(File.ReadAllText(file))!;
 Check(Quantity()==234&&JsonNode.DeepEquals(draft,disk["characters"]!["pmc"]!["Inventory"]!["items"]![1]),"item JSON edits persist count, custom nested fields and arrays through real SaveServer");
 Check(disk["testExtension"]!["marker"]!.GetValue<string>()=="preserve-extension"&&disk["characters"]!["pmc"]!["Inventory"]!["items"]![0]!["_id"]!.GetValue<string>()=="cccccccccccccccccccccccc","item JSON persistence preserves unrelated profile data and inventory roots");
 var saved=inventory.Items!.Last();draft.Remove("upd");
 InventoryItemJson.Apply(inventory,saved.Id.ToString(),saved.Template.ToString(),draft.ToJsonString(),InventoryItemJson.Revision(saved,json),json);
 await saves.SaveProfileAsync(session);
 Check(JsonNode.DeepEquals(draft,JsonNode.Parse(File.ReadAllText(file))!["characters"]!["pmc"]!["Inventory"]!["items"]![1]),"deleting upd saves successfully without requiring component data");
 var attachment=json.Deserialize<SPTarkov.Server.Core.Models.Eft.Common.Tables.Item>("""
 {"_id":"111111111111111111111111","_tpl":"5449016a4bdc2d6f028b456f","parentId":"dddddddddddddddddddddddd","slotId":"mod_magazine","upd":{"StackObjectsCount":1}}
 """)!;
 var ammo=json.Deserialize<SPTarkov.Server.Core.Models.Eft.Common.Tables.Item>("""
 {"_id":"222222222222222222222222","_tpl":"5449016a4bdc2d6f028b456f","parentId":"111111111111111111111111","slotId":"cartridges","upd":{"StackObjectsCount":5}}
 """)!;
 inventory.Items=inventory.Items!.Concat([attachment,ammo]).ToList();await saves.SaveProfileAsync(session);
 string id=saved.Id.ToString(),template=saved.Template.ToString();
 var tree=InventoryItemJson.Tree(inventory,id,template);
 var treeDraft=LocalSpt.Shared.InventoryItemJsonRules.ParseTree(json.Serialize(tree)!);
 var entries=LocalSpt.Shared.InventoryItemJsonRules.Index(treeDraft);
 entries[ammo.Id.ToString()]["upd"]!["StackObjectsCount"]=60;
 entries[attachment.Id.ToString()]["upd"]!["extension"]=new JsonObject{["marker"]="tree-persisted",["array"]=new JsonArray(1,true,"value")};
 InventoryItemJson.ApplyTree(inventory,id,template,treeDraft.ToJsonString(),InventoryItemJson.TreeRevision(tree,json),json);
 await saves.SaveProfileAsync(session);
 var persisted=json.Deserialize<SptProfile>(File.ReadAllText(file))!;
 var savedTree=InventoryItemJson.Tree(persisted.CharacterData!.PmcData!.Inventory!,id,template);
 Check(JsonNode.DeepEquals(treeDraft,JsonNode.Parse(json.Serialize(savedTree)!)),"complete item trees persist child counts and custom fields through real SaveServer");
 Check(InventoryItemJson.TreeRevision(savedTree,json)==InventoryItemJson.TreeRevision(InventoryItemJson.Tree(inventory,id,template),json),"disk verification compares every persisted descendant");
 disk=JsonNode.Parse(File.ReadAllText(file))!;
 Check(disk["testExtension"]!["marker"]!.GetValue<string>()=="preserve-extension"&&disk["characters"]!["pmc"]!["Inventory"]!["items"]![0]!["_id"]!.GetValue<string>()=="cccccccccccccccccccccccc","tree writes preserve profile extensions and unrelated inventory roots");
 Console.WriteLine($"Passed {checks} real SaveServer/disk checks in isolated temporary directory.");
}finally{Directory.SetCurrentDirectory(initialDirectory);Directory.Delete(temporary,true);}
