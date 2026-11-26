using System.Security.Cryptography;
using System.Text;
using LocalSpt.Shared;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public static class RaidStashService {
 public static async ValueTask<string> Handle(CharacterDataRequest request,MongoId session,SptProfile profile,SaveServer saves,JsonUtil json,HttpResponseUtil http,CancellationToken ct){
  var pmc=profile.CharacterData!.PmcData!;var inventory=pmc.Inventory??throw new ArgumentException("仓库尚未就绪。");var root=inventory.Stash?.ToString()??throw new ArgumentException("没有找到仓库。");
  string Revision()=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.Serialize(RaidStashRules.Tree(inventory.Items!,root))!)));
  int repairedLocations=0,repairedUpdates=0;
  if(request.Action is "saveRaidStash" or "repairRaidStash"){
   List<Item> merged;
   if(request.Action=="saveRaidStash"){
   if(request.StashRevision!=Revision())throw new ArgumentException("仓库已发生其他改动，请重新进入战局后重试。");
    var raw=request.StashItems??throw new ArgumentException("缺少仓库物品。");
    var normalized=RaidStashJson.Normalize(raw.GetRawText(),out _,out _);
    merged=RaidStashRules.Merge(inventory.Items!,root,json.Deserialize<List<Item>>(normalized.ToJsonString())??throw new ArgumentException("仓库物品格式无效。"));
   }else{
    var normalized=RaidStashJson.Normalize(json.Serialize(inventory.Items!)!,out repairedLocations,out repairedUpdates);
    merged=json.Deserialize<List<Item>>(normalized.ToJsonString())??throw new ArgumentException("仓库物品格式无效。");
    if(merged.Count!=inventory.Items!.Count||!merged.Select(i=>i.Id).SequenceEqual(inventory.Items.Select(i=>i.Id)))throw new ArgumentException("仓库修复校验失败。");
   }
   var directory=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","raid-stash");Directory.CreateDirectory(directory);
   var backup=System.IO.Path.Combine(directory,$"{session}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json");await File.WriteAllTextAsync(backup,json.Serialize(profile),ct);
   var previous=inventory.Items;inventory.Items=merged;try{await saves.SaveProfileAsync(session);}catch{inventory.Items=previous;throw;}
  }
  return http.GetBody(new{ok=true,stash=root,revision=Revision(),repairedLocations,repairedUpdates,itemCount=inventory.Items!.Count,items=request.Action=="raidStash"?RaidStashRules.Tree(inventory.Items!,root):null});
 }
}
