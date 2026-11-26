using System.Text.Json.Nodes;
using LocalGearMail;
using LocalSpt.Shared;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;

internal static class InventoryTreeJsonChecks {
 internal static void Run(){
  var json=new JsonUtil([new SPTarkov.Server.Core.Utils.Json.SptJsonConverterRegistrator()]);int checks=0;
  void Check(bool valid,string name){if(!valid)throw new Exception(name);checks++;Console.WriteLine("PASS item tree JSON: "+name);}
  Item Make(Item? parent,string slot="main")=>new(){Id=new MongoId(),Template=new MongoId("5449016a4bdc2d6f028b456f"),ParentId=parent?.Id.ToString(),SlotId=slot,Upd=new(){StackObjectsCount=1}};
  var stash=Make(null);var bag=Make(stash);var weapon=Make(bag);var optic=Make(weapon,"mod_scope");var magazine=Make(weapon,"mod_magazine");var rounds=Make(magazine,"cartridges");var chamber=Make(weapon,"patron_in_weapon");var innerBag=Make(bag);var food=Make(innerBag);var sibling=Make(stash);
  var inventory=new BotBaseInventory{Items=[stash,rounds,sibling,food,optic,weapon,magazine,bag,chamber,innerBag]};
  string id=bag.Id.ToString(),template=bag.Template.ToString();
  List<Item> Tree()=>InventoryItemJson.Tree(inventory,id,template);
  JsonArray Draft()=>InventoryItemJsonRules.ParseTree(json.Serialize(Tree())!);
  JsonObject Entry(JsonArray data,Item item)=>InventoryItemJsonRules.Index(data)[item.Id.ToString()];
  var tree=Tree();var revision=InventoryItemJson.TreeRevision(tree,json);var baseline=json.Serialize(tree)!;
  Check(tree.Count==8&&tree[0].Id==bag.Id&&tree.Any(item=>item.Id==optic.Id)&&tree.Any(item=>item.Id==rounds.Id)&&tree.Any(item=>item.Id==food.Id),"nested containers, attachments, magazine ammunition and chamber round are included");
  Check(!tree.Any(item=>item.Id==sibling.Id||item.Id==stash.Id),"siblings and ancestors stay outside the selected tree");
  Check(InventoryItemJson.TreeRevision(tree.AsEnumerable().Reverse(),json)==revision,"revision does not depend on inventory order");
  Check(!InventoryItemJsonRules.Format(baseline.Replace("},{","},\r\n{")).Contains('\r'),"array formatting retains LF for caret editing");
  var draft=Draft();Entry(draft,rounds)["upd"]!["StackObjectsCount"]=60;Entry(draft,chamber)["upd"]!["SpawnedInSession"]=true;
  Entry(draft,optic)["upd"]!["customSetting"]=new JsonObject{["marker"]="preserve",["values"]=new JsonArray(1,true,"text")};
  Entry(draft,food)["upd"]!["FoodDrink"]=new JsonObject{["HpPercent"]=6000};
  var request=json.Deserialize<CharacterDataRequest>(System.Text.Json.JsonSerializer.Serialize(new{action="inventoryItemTreeJsonSave",id,template,preview=draft.ToJsonString(),revision,categories=new[]{"scav"}}))!;
  Check(request.Preview==draft.ToJsonString()&&request.Categories[0]=="scav","tree document and revision bind through native PMC/Scav request fields");
  string siblingBefore=json.Serialize(sibling)!;
  var saved=InventoryItemJson.ApplyTree(inventory,id,template,request.Preview,request.Revision,json);
  Check(saved.Single(item=>item.Id==rounds.Id).Upd!.StackObjectsCount==60&&saved.Single(item=>item.Id==chamber.Id).Upd!.SpawnedInSession==true&&saved.Single(item=>item.Id==food.Id).Upd!.FoodDrink!.HpPercent==6000,"child UPD changes save through native item conversion");
  Check(JsonNode.DeepEquals(draft,JsonNode.Parse(json.Serialize(saved)!)),"tree extensions and nested arrays survive native serialization");
  Check(ReferenceEquals(inventory.Items!.Single(item=>item.Id==sibling.Id),sibling)&&json.Serialize(sibling)==siblingBefore,"unrelated live items retain identity and data");
  string stable=json.Serialize(inventory)!;
  void Reject(Action action,string name){bool refused=false;try{action();}catch(ArgumentException){refused=true;}Check(refused&&json.Serialize(inventory)==stable,name);}
  void RejectDraft(JsonArray candidate,string name)=>Reject(()=>InventoryItemJson.ApplyTree(inventory,id,template,candidate.ToJsonString(),InventoryItemJson.TreeRevision(Tree(),json),json),name);
  Reject(()=>InventoryItemJson.ApplyTree(inventory,id,template,draft.ToJsonString(),revision,json),"changes to descendants stale the whole-tree revision");
  var bad=Draft();Entry(bad,bag)["upd"]!["StackObjectsCount"]=9;Entry(bad,food)["upd"]!["FoodDrink"]!["HpPercent"]="invalid";
  RejectDraft(bad,"late child conversion failure leaves every inventory record untouched");
  bad=Draft();bad.Add(bad[0]!.DeepClone());RejectDraft(bad,"duplicate item IDs are rejected atomically");
  bad=Draft();bad.RemoveAt(bad.Count-1);RejectDraft(bad,"missing descendants are rejected atomically");
  bad=Draft();bad.Add(JsonNode.Parse(json.Serialize(sibling)!));RejectDraft(bad,"foreign sibling injection is rejected atomically");
  foreach(string key in new[]{"_id","_tpl","parentId","slotId","location"}){
   bad=Draft();Entry(bad,optic)[key]=key=="parentId"?optic.Id.ToString():new MongoId().ToString();RejectDraft(bad,"child identity and placement remain locked: "+key);
  }
  foreach(string invalid in new[]{"[]","{}","[null]","[1]","[{\"_id\":\"a\",\"upd\":{\"x\":1,\"x\":2}}]",new string('x',InventoryItemJsonRules.TreeLimit+1)})
   Reject(()=>InventoryItemJsonRules.ParseTree(invalid),"empty, non-object, duplicate-field and oversized trees are rejected");
  var reordered=Draft();var reverse=new JsonArray(reordered.Reverse().Select(node=>node!.DeepClone()).ToArray());
  Check(JsonNode.DeepEquals(reordered,InventoryItemJsonRules.ValidateTree(reordered.ToJsonString(),reverse.ToJsonString())),"reordered drafts are normalized to original tree order");
  var large=Draft();Entry(large,bag)["upd"]!["extension"]=new string('x',InventoryItemJsonRules.Limit+1);
  Check(InventoryItemJsonRules.ValidateTree(large.ToJsonString(),large.ToJsonString()).Count==8,"container trees support documents above the old single-item limit");
  var added=Make(innerBag);inventory.Items=inventory.Items!.Append(added).ToList();stable=json.Serialize(inventory)!;
  Reject(()=>InventoryItemJson.ApplyTree(inventory,id,template,reordered.ToJsonString(),InventoryItemJson.TreeRevision(saved,json),json),"new contents invalidate the editing revision");
  inventory.Items=inventory.Items!.Where(item=>item.Id!=added.Id&&item.Id!=optic.Id).ToList();stable=json.Serialize(inventory)!;
  Reject(()=>InventoryItemJson.ApplyTree(inventory,id,template,reordered.ToJsonString(),InventoryItemJson.TreeRevision(saved,json),json),"removed attachments invalidate the editing revision");
  var duplicate=Make(stash);duplicate.Id=food.Id;inventory.Items=inventory.Items!.Append(duplicate).ToList();stable=json.Serialize(inventory)!;
  Reject(()=>Tree(),"an unrelated duplicate ID cannot be replaced as a tree member");
  inventory.Items=inventory.Items!.Where(item=>!ReferenceEquals(item,duplicate)).ToList();inventory.Items!.Single(item=>item.Id==bag.Id).ParentId=food.Id.ToString();stable=json.Serialize(inventory)!;
  Reject(()=>Tree(),"cyclic inventories are rejected without mutation");
  Console.WriteLine($"Passed {checks} item tree JSON checks.");
 }
}
