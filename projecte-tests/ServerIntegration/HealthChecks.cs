using System.Text.Json.Nodes;
internal static class HealthChecks {
 internal static async Task<int> Run(Func<JsonObject,string,Task<JsonNode>> call,string fixture){
  string keyFile=Path.Combine(fixture,"user/mods/LocalGearMail/api-key.txt");if(!File.Exists(keyFile))return 0;
  string key=File.ReadAllText(keyFile).Trim();int count=0;
  async Task<JsonNode> Send(string action,JsonObject? values=null){var p=new JsonObject{["apiKey"]=key,["action"]=action};if(values!=null)p["settings"]=values;return await call(p,"/local/gear/data");}
  bool Ok(JsonNode n)=>n["ok"]?.GetValue<bool>()??false;
  void Check(bool valid,string name){if(!valid)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
  var initial=await Send("healthStatus");Check(Ok(initial),"character health endpoint authenticates isolated profile");var original=initial["health"]!.DeepClone();
  JsonObject Values(JsonNode health){var result=new JsonObject();foreach(var part in health["BodyParts"]!.AsObject()){var h=part.Value!["Health"]!;result[part.Key]=new JsonObject{["maximum"]=h["Maximum"]!.DeepClone(),["current"]=h["Current"]!.DeepClone()};}return result;}
  var values=Values(original);var bad=values.DeepClone().AsObject();bad["Head"]!["maximum"]=0;
  Check(!Ok(await Send("bodyHealth",bad)),"invalid body-part maximum rejected");Check(JsonNode.DeepEquals(original,(await Send("healthStatus"))["health"]),"rejected health edit preserves all original health data");
  try{
   int i=0;foreach(var part in values){part.Value!["maximum"]=101+i;part.Value["current"]=51+i;i++;}
   var saved=await Send("bodyHealth",values.DeepClone().AsObject());Check(Ok(saved),"seven distinct current and maximum health values save together");
   var actual=(await Send("healthStatus"))["health"]!;Check(JsonNode.DeepEquals(values,Values(actual)),"all seven body-part values read back exactly");
   var disk=JsonNode.Parse(File.ReadAllText(Directory.GetFiles(Path.Combine(fixture,"user/profiles"),"*.json").First()))!;
   Check(JsonNode.DeepEquals(values,Values(disk["characters"]!["pmc"]!["Health"]!)),"body health persists in isolated PMC save");
   Check(JsonNode.DeepEquals(original["Hydration"],actual["Hydration"])&&JsonNode.DeepEquals(original["Energy"],actual["Energy"]),"body health edit preserves hydration and energy");
  }finally{Check(Ok(await Send("bodyHealth",Values(original))),"original fixture body health restored");}
  var full=Values(original);full["Energy"]=new JsonObject{["maximum"]=222,["current"]=123};full["Hydration"]=new JsonObject{["maximum"]=177,["current"]=99};
  try{var response=await Send("bodyHealth",full.DeepClone().AsObject());Check(Ok(response),"energy and hydration maximum/current save with body health");var actual=(await Send("healthStatus"))["health"]!;Check(actual["Energy"]!["Maximum"]!.GetValue<double>()==222&&actual["Energy"]!["Current"]!.GetValue<double>()==123&&actual["Hydration"]!["Maximum"]!.GetValue<double>()==177&&actual["Hydration"]!["Current"]!.GetValue<double>()==99,"energy and hydration values persist and read back exactly");var invalid=full.DeepClone().AsObject();invalid["Energy"]!["current"]=223;Check(!Ok(await Send("bodyHealth",invalid)),"energy above maximum rejected without partial mutation");Check(JsonNode.DeepEquals(actual,(await Send("healthStatus"))["health"]),"invalid energy does not partially change body parts or hydration");}
  finally{var restore=Values(original);foreach(string vital in new[]{"Energy","Hydration"})restore[vital]=new JsonObject{["maximum"]=original[vital]!["Maximum"]!.DeepClone(),["current"]=original[vital]!["Current"]!.DeepClone()};Check(Ok(await Send("bodyHealth",restore)),"original energy and hydration restored in isolated fixture");}
  return count;
 }
}
