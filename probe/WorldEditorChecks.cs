using System;
using System.Linq;
using System.Text.Json.Nodes;
using LocalSpt5Esp;
internal static class WorldEditorChecks {
 internal static void Run(){
  void Check(bool value,string reason){if(!value)throw new Exception(reason);}
  void Reject(string text,string root){try{int id=100;EditorItemRules.Remap(text,root,()=> (++id).ToString("x24"),out _);}catch(ArgumentException){return;}throw new Exception("Invalid item tree was accepted.");}
  const string root="000000000000000000000001",tpl="5449016a4bdc2d6f028b456f";
  var source=new JsonArray();
  for(int i=1;i<=5;i++){var entry=new JsonObject{["_id"]=i.ToString("x24"),["_tpl"]=tpl,["parentId"]=(i==1?99:i-1).ToString("x24"),["slotId"]=i==1?"hideout":"main",["location"]=new JsonObject{["x"]=i,["y"]=2,["r"]=0},["upd"]=new JsonObject{["StackObjectsCount"]=7,["SpawnedInSession"]=true,["Repairable"]=new JsonObject{["Durability"]=38,["MaxDurability"]=50}}};source.Add(entry);}
  string original=source.ToJsonString();int seed=100;var copied=EditorItemRules.Remap(original,root,()=> (++seed).ToString("x24"),out string fresh);
  Check(original==source.ToJsonString()&&copied.Count==5&&fresh!=root,"copy leaves source untouched and allocates a new complete tree");
  Check(copied[0]!["parentId"]==null&&copied[0]!["slotId"]==null&&copied[0]!["location"]==null,"world root is detached from its previous inventory address");
  for(int i=1;i<copied.Count;i++){Check(copied[i]!["parentId"]!.GetValue<string>()==copied[i-1]!["_id"]!.GetValue<string>(),"nested equipment, grids and attachments keep their new parent");Check(copied[i]!["location"]!.ToJsonString()==source[i]!["location"]!.ToJsonString(),"nested grid placements remain intact");}
  for(int i=0;i<copied.Count;i++)Check(copied[i]!["upd"]!.ToJsonString()==source[i]!["upd"]!.ToJsonString(),"stack, durability and found-in-raid state survive copying");
  var duplicate=JsonNode.Parse(original)!.AsArray();duplicate[1]!["_id"]=root;Reject(duplicate.ToJsonString(),root);
  var cycle=JsonNode.Parse(original)!.AsArray();cycle[1]!["parentId"]=5.ToString("x24");Reject(cycle.ToJsonString(),root);
  var orphan=JsonNode.Parse(original)!.AsArray();orphan[1]!["parentId"]=99.ToString("x24");Reject(orphan.ToJsonString(),root);
  Reject("[]",root);Reject(original,99.ToString("x24"));
  try{EditorItemRules.Remap(original,root,()=>root,out _);throw new Exception("ID collision accepted");}catch(ArgumentException){}
  Check(EditorItemRules.Distance(2,-20)==1&&EditorItemRules.Distance(80,20)==100&&EditorItemRules.Distance(10,1)>10,"holding distance clamps both ends and responds to wheel");
  try{EditorItemRules.Distance(float.NaN,1);throw new Exception("Invalid distance accepted");}catch(ArgumentException){}
  var gate=new MenuInputGate();gate.SetOpen(true,1);gate.SetOpen(false,2);gate.Tick(3,true);Check(gate.Blocked,"initiating menu click cannot drop a moved entity or fire");gate.Tick(4,false);Check(!gate.Blocked,"release restores camera movement input");
  Check(MenuSections.Pages("开发工具").Contains("编辑模式")&&MenuSections.Group("63. 场景编辑")=="开发工具"&&MenuSections.OnPage("开发工具","编辑模式","63. 场景编辑","Editor mode"),"editor config is discoverable from its development page");
  Console.WriteLine("PASS editor nested item cloning, fresh identities, detached root, unchanged source, state/grid preservation, corrupt-tree rejection, distance and input-release boundaries");
 }
}
