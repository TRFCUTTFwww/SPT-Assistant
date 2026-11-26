using System.Text.Json.Nodes;
using LocalRaidShared;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;
namespace LocalRaidControls;
internal static class ContainerNesting {
 internal static void Apply(TemplateTable templates,JsonUtil json){
  if(!Patches.Read().AllowContainerNesting)return;
  var nodes=templates.Items.ToDictionary(p=>p.Key.ToString(),p=>JsonNode.Parse((json.Serialize(p.Value)??throw new InvalidOperationException("Template serialization returned null")))!.AsObject(),StringComparer.Ordinal);
  bool IsCase(string id){var seen=new HashSet<string>(StringComparer.Ordinal);while(seen.Add(id)){if(id==ContainerNestingRules.CaseClass)return true;if(!nodes.TryGetValue(id,out var node))return false;id=node["_parent"]?.ToString()??"";}return false;}
  int changed=0;
  foreach(var pair in templates.Items.ToArray())if(ContainerNestingRules.Enable(nodes[pair.Key.ToString()],IsCase)){templates.Items[pair.Key]=json.Deserialize<TemplateItem>(nodes[pair.Key.ToString()].ToJsonString())!;changed++;}
  Patches.Log.Info($"Container nesting enabled for {changed} storage-case templates; native grid sizes and cycle checks retained");
 }
}
