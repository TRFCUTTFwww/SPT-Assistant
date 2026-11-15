using Scp3X;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json.Nodes;
int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
Check(DisruptorRules.Templates.Distinct().Count()==3&&DisruptorRules.Templates.All(id=>id.Length==24&&id.All(Uri.IsHexDigit)),"native unique MongoIds");
Check(DisruptorRules.Capacity==5,"five charges including chamber");
Check(DisruptorRules.CanFire(true,true,true,true,false,true),"living local ready player can fire");
foreach(int bit in Enumerable.Range(0,6)){bool[] flags=[true,true,true,true,false,true];flags[bit]=!flags[bit];Check(!DisruptorRules.CanFire(flags[0],flags[1],flags[2],flags[3],flags[4],flags[5]),"fire boundary "+bit);}
Check(DisruptorRules.ConfirmedKill(DisruptorRules.Weapon,false,false,"self","self"),"matching confirmed 3-X death");
Check(!DisruptorRules.ConfirmedKill(DisruptorRules.DonorWeapon,false,false,"self","self"),"donor firearm kills never disintegrate");
Check(!DisruptorRules.ConfirmedKill(DisruptorRules.Weapon,true,false,"self","self"),"surviving target never disintegrates");
Check(!DisruptorRules.ConfirmedKill(DisruptorRules.Weapon,false,true,"self","self"),"own player never disintegrates");
Check(!DisruptorRules.ConfirmedKill(DisruptorRules.Weapon,false,false,"other","self")&&!DisruptorRules.ConfirmedKill(DisruptorRules.Weapon,false,false,"",""),"unattributed and other-player deaths excluded");
Check(DisruptorRules.MayEraseCorpse(1,1,"victim","victim",false),"confirmed matching corpse can be removed after animation");
Check(!DisruptorRules.MayEraseCorpse(1,2,"victim","victim",false)&&!DisruptorRules.MayEraseCorpse(0,0,"victim","victim",false),"raid changes and missing world prohibit corpse deletion");
Check(!DisruptorRules.MayEraseCorpse(1,1,"victim","neighbor",false)&&!DisruptorRules.MayEraseCorpse(1,1,"","",false),"neighbor and unidentified corpses are never deleted");
Check(!DisruptorRules.MayEraseCorpse(1,1,"victim","victim",true),"revived or living actor inventory is never erased");
Check(DisruptorRules.DissolveProgress(-1)==0&&DisruptorRules.DissolveProgress(100)==1&&DisruptorRules.DissolveProgress(float.NaN)==1,"bounded finite disintegration progress");
float previous=0;for(int i=0;i<100;i++){float next=DisruptorRules.DissolveProgress(i*.03f);if(next<previous)throw new Exception("Non-monotonic disintegration");previous=next;}Check(previous==1,"disintegration progresses monotonically to completion");
if(args.Length==2){
 string fixture=Path.GetFullPath(args[0]),game=Path.GetFullPath(args[1]);if(!fixture.Contains("fixture",StringComparison.OrdinalIgnoreCase)||fixture.StartsWith(game,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Use an isolated fixture");
 AssemblyLoadContext.Default.Resolving+=(c,n)=>{string p=Path.Combine(game,"BepInEx/core",n.Name+".dll");return File.Exists(p)?c.LoadFromAssemblyPath(p):null;};
 var common=Assembly.LoadFrom(Path.Combine(game,"BepInEx/plugins/sptushonka/SPTushonka.Common.dll"));var shuffle=common.GetType("SPTushonka.Common.Http.Shuffle",true)!;var flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
 byte[] Pack(byte[] d)=>(byte[])shuffle.GetMethod("Pack",flags)!.Invoke(null,[d])!;byte[] Unpack(byte[] d)=>(byte[])shuffle.GetMethod("Unpack",flags)!.Invoke(null,[d])!;
 string profile=Directory.GetFiles(Path.Combine(fixture,"user/profiles"),"*.json").First(),session=Path.GetFileNameWithoutExtension(profile);
 using var handler=new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,_,_,_)=>true};using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(35)};
 async Task<JsonNode> Call(string path){using var request=new HttpRequestMessage(HttpMethod.Post,"https://127.0.0.1:6968"+path);request.Headers.Add("Cookie","PHPSESSID="+session);request.Content=new ByteArrayContent(Pack(Encoding.UTF8.GetBytes("{}")));request.Content.Headers.ContentType=new("application/json");using var response=await client.SendAsync(request);byte[] data=Unpack(await response.Content.ReadAsByteArrayAsync());if(data.Length>1&&data[0]==0x78){using var z=new System.IO.Compression.ZLibStream(new MemoryStream(data),System.IO.Compression.CompressionMode.Decompress);using var output=new MemoryStream();z.CopyTo(output);data=output.ToArray();}var root=JsonNode.Parse(data)!;if(root["err"]?.GetValue<int>() is >0)throw new Exception(root.ToJsonString());return root["data"]??root;}
 var runtime=await Call("/local/projecte/runtime");Check(runtime["ready"]!.GetValue<bool>(),"fixture server hot host ready");
 var items=await Call("/client/items");foreach(string id in DisruptorRules.Templates)Check(items[id]!=null,"registered template "+id);
 var book=await Call("/client/handbook/templates");foreach(string id in DisruptorRules.Templates)Check(book["Items"]!.AsArray().Count(x=>x!["Id"]!.GetValue<string>()==id)==1,"exactly one published handbook entry "+id);
 var gun=items[DisruptorRules.Weapon]!["_props"]!;var mag=items[DisruptorRules.Magazine]!["_props"]!;
 Check(gun["weapClass"]!.GetValue<string>()=="assaultRifle"&&items[DisruptorRules.Weapon]!["_parent"]!.GetValue<string>()==items[DisruptorRules.RifleCategorySource]!["_parent"]!.GetValue<string>(),"3-X equips in a primary rifle slot");
 Check(gun["defAmmo"]!.GetValue<string>()==DisruptorRules.Ammo&&gun["defMagType"]!.GetValue<string>()==DisruptorRules.Magazine,"native default fusion ammo and magazine");
 Check(gun["Slots"]![0]!["_props"]!["filters"]![0]!["Filter"]![0]!.GetValue<string>()==DisruptorRules.Magazine,"gun accepts custom magazine");
 Check(gun["Chambers"]![0]!["_props"]!["filters"]![0]!["Filter"]![0]!.GetValue<string>()==DisruptorRules.Ammo,"chamber accepts fusion charge");
 Check(mag["Cartridges"]![0]!["_max_count"]!.GetValue<int>()==4&&mag["Cartridges"]![0]!["_props"]!["filters"]![0]!["Filter"]![0]!.GetValue<string>()==DisruptorRules.Ammo,"four reserve charges and valid magazine filter");
 Check(gun["Prefab"]!["path"]!.GetValue<string>()==items[DisruptorRules.DonorWeapon]!["_props"]!["Prefab"]!["path"]!.GetValue<string>(),"native prefab resource remains loadable");
 const string dev="a7d000000000000000000001";
 var settings=await Call("/client/trading/api/traderSettings");var trader=settings.AsArray().Single(x=>x!["_id"]!.GetValue<string>()==dev)!;Check(trader["location"]!.GetValue<string>()=="N/A","Developer base location N/A");
 foreach(string language in new[]{"en","ch"}){var locale=await Call("/client/locale/"+language);Check(locale[dev+" Location"]!.GetValue<string>()=="N/A"&&locale[dev+" Description"]!.GetValue<string>()=="N/A","Developer localized location and description "+language);}
 var assort=await Call("/client/trading/api/getTraderAssort/"+dev);var offers=assort["items"]!.AsArray();
 foreach(string id in DisruptorRules.Templates)Check(offers.Count(x=>x!["_tpl"]!.GetValue<string>()==id&&x["parentId"]?.GetValue<string>()=="hideout")==1,"exactly one Developer offer "+id);
 var root=offers.Single(x=>x!["_tpl"]!.GetValue<string>()==DisruptorRules.Weapon&&x["parentId"]?.GetValue<string>()=="hideout")!;
 string rootId=root["_id"]!.GetValue<string>();var magazine=offers.Single(x=>x?["parentId"]?.GetValue<string>()==rootId&&x["slotId"]?.GetValue<string>()=="mod_magazine")!;
 Check(magazine["_tpl"]!.GetValue<string>()==DisruptorRules.Magazine,"assembled offer includes fusion magazine");
 var reserve=offers.Single(x=>x?["parentId"]?.GetValue<string>()==magazine["_id"]!.GetValue<string>())!;
 Check(reserve["upd"]!["StackObjectsCount"]!.GetValue<int>()==4&&offers.Any(x=>x?["parentId"]?.GetValue<string>()==rootId&&x["slotId"]?.GetValue<string>()=="patron_in_weapon"&&x["_tpl"]!.GetValue<string>()==DisruptorRules.Ammo),"purchased weapon has four reserve charges and one chambered");
 Check(assort["barter_scheme"]![rootId]![0]![0]!["count"]!.GetValue<int>()==DisruptorRules.WeaponPrice,"correct Developer purchase price");
 var jaeger=await Call("/client/trading/api/getTraderAssort/5c0647fdd443bc2504c2d371");Check(!jaeger["items"]!.AsArray().Any(x=>DisruptorRules.Templates.Contains(x!["_tpl"]!.GetValue<string>())),"3-X offers moved off Jaeger");
 string module=Path.Combine(fixture,"user/mods/ProjectE/Local.ProjectE.Logic.module");byte[] original=await File.ReadAllBytesAsync(module);int generation=runtime["generation"]!.GetValue<int>();
 try{await File.WriteAllBytesAsync(module,[..original,0]);await Task.Delay(1200);var reloaded=await Call("/local/projecte/runtime");Check(reloaded["generation"]!.GetValue<int>()>generation&&reloaded["error"]!.GetValue<string>()=="","server logic reloads while running");var reloadedAssort=await Call("/client/trading/api/getTraderAssort/"+dev);Check(reloadedAssort["items"]!.AsArray().Count(x=>x!["_tpl"]!.GetValue<string>()==DisruptorRules.Weapon&&x["parentId"]?.GetValue<string>()=="hideout")==1,"hot reload does not duplicate 3-X offers");var reloadedLocale=await Call("/client/locale/ch");Check(reloadedLocale[dev+" Location"]!.GetValue<string>()=="N/A"&&reloadedLocale[dev+" Description"]!.GetValue<string>()=="N/A","N/A survives reload with an existing Developer trader");}
 finally{await File.WriteAllBytesAsync(module,original);}
}
Console.WriteLine($"Passed {checks} 3-X checks.");
