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
 Console.WriteLine($"Passed {checks} real SaveServer/disk checks in isolated temporary directory.");
}finally{Directory.SetCurrentDirectory(initialDirectory);Directory.Delete(temporary,true);}
