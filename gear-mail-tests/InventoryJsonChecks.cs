using System.Text.Json;
using System.Text.Json.Nodes;
using LocalGearMail;
using LocalSpt.Shared;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;

internal static class InventoryJsonChecks {
 internal static void Run(){
  InventoryTreeJsonChecks.Run();
  var json=new JsonUtil([new SPTarkov.Server.Core.Utils.Json.SptJsonConverterRegistrator()]);
  void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS item JSON: "+name);}
  var item=new Item{Id=new MongoId(),Template=new MongoId("5449016a4bdc2d6f028b456f"),ParentId=new MongoId().ToString(),SlotId="main",Upd=new(){StackObjectsCount=7,SpawnedInSession=false,Dogtag=new(){Nickname="Original",Level=4,Side=SPTarkov.Server.Core.Models.Enums.DogtagSide.Bear}}};
  var sibling=new Item{Id=new MongoId(),Template=item.Template,ParentId=item.ParentId,SlotId="main",Upd=new(){StackObjectsCount=3}};
  var inventory=new BotBaseInventory{Items=[item,sibling]};
  string baseline=json.Serialize(item)!,siblingBefore=json.Serialize(sibling)!;
  var edited=InventoryItemJsonRules.Parse(baseline);edited["upd"]!["StackObjectsCount"]=19;edited["upd"]!["Dogtag"]!["Nickname"]="JSON 名字";
  edited["upd"]!["Dogtag"]!["Level"]=100000;edited["upd"]!["extraData"]=new JsonObject{["array"]=new JsonArray(1,true,"text"),["nested"]=new JsonObject{["value"]=5}};
  string revision=InventoryItemJson.Revision(item,json);
  var request=json.Deserialize<CharacterDataRequest>(JsonSerializer.Serialize(new{action="inventoryItemJsonSave",id=item.Id.ToString(),template=item.Template.ToString(),preview=edited.ToJsonString(),revision,categories=new[]{"pmc"}}))!;
  Check(request.Preview==edited.ToJsonString()&&request.Revision==revision,"raw document and item revision bind through the native request converter");
  var saved=InventoryItemJson.Apply(inventory,request.Id,request.Template,request.Preview,request.Revision,json);
  Check(saved.Upd!.StackObjectsCount==19&&saved.Upd.Dogtag!.Nickname=="JSON 名字"&&saved.Upd.Dogtag.Level==100000,"count and nested dogtag data survive native conversion");
  Check(JsonNode.DeepEquals(edited,JsonNode.Parse(json.Serialize(saved)!)),"custom arrays and nested fields survive native save serialization exactly");
  Check(json.Serialize(sibling)==siblingBefore&&ReferenceEquals(inventory.Items!.Last(),sibling),"unrelated inventory items are preserved");
  string unchanged=json.Serialize(inventory)!;
  void Reject(Action action,string name){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected&&json.Serialize(inventory)==unchanged,name);}
  Reject(()=>InventoryItemJson.Apply(inventory,request.Id,request.Template,request.Preview,revision,json),"stale item revisions preserve live data");
  Reject(()=>InventoryItemJson.Find(inventory,request.Id,new MongoId().ToString()),"stale templates cannot address another item");

  // Unrestricted _tpl and placement edits are now supported
  string latest=json.Serialize(saved)!;
  var tplChange=InventoryItemJsonRules.Parse(latest);
  string newTpl="59faff1d86f7746c51718c9c";
  tplChange["_tpl"]=newTpl;
  var reTemplated=InventoryItemJson.Apply(inventory,saved.Id.ToString(),saved.Template.ToString(),tplChange.ToJsonString(),InventoryItemJson.Revision(saved,json),json);
  Check(reTemplated.Template.ToString()==newTpl,"unrestricted item JSON allows changing _tpl");
  unchanged=json.Serialize(inventory)!;
  latest=json.Serialize(reTemplated)!;

  var collideSibling=InventoryItemJsonRules.Parse(latest);
  collideSibling["_id"]=sibling.Id.ToString();
  Reject(()=>InventoryItemJson.Apply(inventory,reTemplated.Id.ToString(),reTemplated.Template.ToString(),collideSibling.ToJsonString(),InventoryItemJson.Revision(reTemplated,json),json),"colliding with an unrelated inventory _id is still prevented");

  foreach(string invalid in new[]{"[]","null","{"})
   Reject(()=>InventoryItemJsonRules.Parse(invalid),"invalid or non-object document rejected");
  foreach(JsonNode invalidCount in new JsonNode[]{JsonValue.Create("invalid")!,new JsonObject()}){
   var invalid=InventoryItemJsonRules.Parse(latest);invalid["upd"]!["StackObjectsCount"]=invalidCount;
   Reject(()=>InventoryItemJson.Apply(inventory,reTemplated.Id.ToString(),reTemplated.Template.ToString(),invalid.ToJsonString(),InventoryItemJson.Revision(reTemplated,json),json),"invalid count rejected without a partial write");
  }
  var wrongType=InventoryItemJsonRules.Parse(latest);wrongType["upd"]!["Dogtag"]!["Level"]="wrong";
  Reject(()=>InventoryItemJson.Apply(inventory,reTemplated.Id.ToString(),reTemplated.Template.ToString(),wrongType.ToJsonString(),InventoryItemJson.Revision(reTemplated,json),json),"native field type errors leave inventory untouched");
  var remove=InventoryItemJsonRules.Parse(latest);remove.Remove("upd");
  var cleared=InventoryItemJson.Apply(inventory,reTemplated.Id.ToString(),reTemplated.Template.ToString(),remove.ToJsonString(),InventoryItemJson.Revision(reTemplated,json),json);
  Check(cleared.Upd==null&&JsonNode.DeepEquals(remove,JsonNode.Parse(json.Serialize(cleared)!)),"an item without upd is accepted and round trips without invented fields");
  Check(InventoryItemJsonRules.RemovesFields(new(){["Tag"]=new JsonObject{["Name"]="old"}},new(){["Tag"]=new JsonObject()}),"native component deletions detected for in-place vs full-tree rebuild");
  Check(InventoryItemJsonRules.Matches(new(){["StackObjectsCount"]=1,["SpawnedInSession"]=false},new()),"native omitted defaults retain a single item and false FIR");

  // Grid cell JSON add normalization (single object, flat array, preset wrapper, Base64)
  var (singleRoots,singleArr)=InventoryItemJsonRules.NormalizeForGridAdd("{\"_tpl\":\"59faff1d86f7746c51718c9c\",\"upd\":{\"StackObjectsCount\":1}}");
  Check(singleRoots.Count==1&&singleArr.Count==1&&singleArr[0]!["_id"]!.GetValue<string>().Length==24,"single object JSON auto-generates a fresh 24-hex _id for grid placement");
  string presetJson="{\"Items\":[{\"_id\":\"oldroot\",\"_tpl\":\"5447a9cd4bdc2dbd208b4567\"},{\"_id\":\"oldchild\",\"_tpl\":\"55d480c04bdc2d1d4e8b456a\",\"parentId\":\"oldroot\",\"slotId\":\"mod_magazine\"}]}";
  var (presetRoots,presetArr)=InventoryItemJsonRules.NormalizeForGridAdd(presetJson);
  Check(presetRoots.Count==1&&presetArr.Count==2&&presetArr[0]!["_id"]!.GetValue<string>()!="oldroot"&&presetArr[1]!["parentId"]!.GetValue<string>()==presetRoots[0],"weapon build Items wrapper remaps all _id and parentId links cleanly");
  string b64=Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(presetJson));
  var (b64Roots,b64Arr)=InventoryItemJsonRules.NormalizeForGridAdd(b64);
  Check(b64Roots.Count==1&&b64Arr.Count==2,"Base64 encoded preset JSON unwraps and normalizes for grid placement");
  var addedToInv=InventoryItemJson.AddItemsToGrid(inventory,presetArr.ToJsonString(),json);
  Check(addedToInv.Count==2&&inventory.Items!.Count==4,"AddItemsToGrid appends normalized item tree to profile inventory");

  string food="{\"_id\":\""+new MongoId()+"\",\"_tpl\":\"5448fee04bdc2dbc018b4567\",\"upd\":{\"FoodDrink\":{\"HpPercent\":600}}}";
  string windows=JsonNode.Parse(food)!.ToJsonString(new JsonSerializerOptions{WriteIndented=true,NewLine="\r\n"});
  string display=InventoryItemJsonRules.Format(windows);
  Check(!display.Contains('\r')&&JsonNode.DeepEquals(JsonNode.Parse(display),JsonNode.Parse(windows)),"Windows item JSON formatting uses LF without changing item data");
  Check(Environment.NewLine!="\r\n"||new JsonSerializerOptions{WriteIndented=true}.NewLine=="\r\n","reproduce Windows default JSON newline setting");
  int oldCaret=windows.IndexOf("600",StringComparison.Ordinal)+3;
  string normalized=JsonEditPath.EditorText(windows);
  int caret=JsonEditPath.EditorPosition(windows,oldCaret);
  string inserted=normalized.Insert(caret,"0");
  Check(JsonNode.Parse(inserted)!["upd"]!["FoodDrink"]!["HpPercent"]!.GetValue<int>()==6000,"typing zero at the numeric caret after CRLF normalization stays on the same line");
  int selection=JsonEditPath.EditorPosition(windows,oldCaret-3);
  Check(normalized[selection..caret]=="600"&&JsonNode.Parse(normalized[..selection]+"6000"+normalized[caret..])!["upd"]!["FoodDrink"]!["HpPercent"]!.GetValue<int>()==6000,"normalizing pasted CRLF preserves selected numeric text and replacement");
  var foodItem=json.Deserialize<Item>(food)!;var foodInventory=new BotBaseInventory{Items=[foodItem]};
  var savedFood=InventoryItemJson.Apply(foodInventory,foodItem.Id.ToString(),foodItem.Template.ToString(),inserted,InventoryItemJson.Revision(foodItem,json),json);
  Check(savedFood.Upd!.FoodDrink!.HpPercent==6000,"an edit at the normalized caret saves 6000 through the real food item converter");
  foreach(var value in new[]{("6000",6000d),("1000",1000d),("100.00",100d),("1e003",1000d),("-1.5e2",-150d)}){
   var parsed=InventoryItemJsonRules.Parse("{\"value\":"+value.Item1+",\"text\":\"0010 / \\\"0002\\\" / C:\\\\0003\",\"001\":\"0004\"}");
   Check(parsed["value"]!.GetValue<double>()==value.Item2&&parsed["text"]!.GetValue<string>()=="0010 / \"0002\" / C:\\0003"&&parsed["001"]!.GetValue<string>()=="0004","numeric editing keeps decimal values and preserves quoted zeros: "+value.Item1);
  }
  foreach(var value in new[]{"00","00010","1.","1e", "+01", "0x10", "NaN", "1 0"}){
   bool rejected=false;try{InventoryItemJsonRules.Parse("{\n\"value\":"+value+"}");}catch(ArgumentException error){rejected=error.Message.Contains("第 2 行")&&error.Message.Contains("列");}
   Check(rejected,"malformed numeric drafts report a line and column: "+value);
  }
  bool splitNumber=false;try{InventoryItemJsonRules.Parse("{\n\"HpPercent\":600\n0\n}");}catch(ArgumentException error){splitNumber=error.Message.Contains("第 3 行")&&error.Message.Contains("列");}
  Check(splitNumber,"a zero on a separate line remains invalid and reports its position");
  foreach(var sample in new[]{("MedKit","HpResource","100","1000"),("FoodDrink","HpPercent","600","6000"),("Resource","Value","80","800"),("RepairKit","Resource","1200","12000"),("Key","NumberOfUsages","2","20")}){
   string raw="{\"_id\":\""+new MongoId()+"\",\"_tpl\":\"544fb45d4bdc2dee738b4568\",\"upd\":{\""+sample.Item1+"\":{\""+sample.Item2+"\":"+sample.Item3+"}}}";
   var resource=json.Deserialize<Item>(raw)!;var resources=new BotBaseInventory{Items=[resource]};
   var updated=InventoryItemJson.Apply(resources,resource.Id.ToString(),resource.Template.ToString(),raw.Replace(":"+sample.Item3+"}",":"+sample.Item4+"}"),InventoryItemJson.Revision(resource,json),json);
   var result=JsonNode.Parse(json.Serialize(updated)!)!;
   Check(result["upd"]![sample.Item1]![sample.Item2]!.GetValue<double>()==double.Parse(sample.Item4,System.Globalization.CultureInfo.InvariantCulture),"appending zero to "+sample.Item1+" usage survives the native save converter");
  }
 }
}
