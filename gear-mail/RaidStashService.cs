using System.Security.Cryptography;
using System.Text;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public static class RaidStashService {
 public static async ValueTask<string> Handle(CharacterDataRequest request,MongoId session,SptProfile profile,SaveServer saves,JsonUtil json,HttpResponseUtil http,CancellationToken ct){
  var pmc=profile.CharacterData!.PmcData!;var inventory=pmc.Inventory??throw new ArgumentException("仓库尚未就绪。");var root=inventory.Stash?.ToString()??throw new ArgumentException("没有找到仓库。");
  string Revision()=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.Serialize(RaidStashRules.Tree(inventory.Items!,root))!)));
  if(request.Action=="saveRaidStash"){
   if(request.StashRevision!=Revision())throw new ArgumentException("仓库已发生其他改动，请重新进入战局后重试。");
   var merged=RaidStashRules.Merge(inventory.Items!,root,request.StashItems??throw new ArgumentException("缺少仓库物品。"));
   var directory=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","raid-stash");Directory.CreateDirectory(directory);
   var backup=System.IO.Path.Combine(directory,$"{session}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json");await File.WriteAllTextAsync(backup,json.Serialize(profile),ct);
   var previous=inventory.Items;inventory.Items=merged;try{await saves.SaveProfileAsync(session);}catch{inventory.Items=previous;throw;}
  }
  return http.GetBody(new{ok=true,stash=root,revision=Revision(),items=request.Action=="raidStash"?RaidStashRules.Tree(inventory.Items!,root):null});
 }
}
