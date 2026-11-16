using System.Security.Cryptography;
using System.Text;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace ProjectE.Server;
public sealed class StorageApiRequest:StorageRequest,IRequestData {}
#if !SPT_ASSISTANT_SERVER
[Injectable]
#endif
public sealed class TerminalRouter:StaticRouter {
 public TerminalRouter(JsonUtil json,TerminalStorage service):base(json,[new RouteAction<StorageApiRequest>("/local/projecte/terminal",(_,r,s,_,ct)=>service.Handle(r,s,ct))]){}
}
// Copied from the assistant's RaidStashService. This module owns its route,
// authentication, normalization, revision checks, backup and persistence.
#if !SPT_ASSISTANT_SERVER
[Injectable]
#endif
public sealed class TerminalStorage(SaveServer saves,JsonUtil json,HttpResponseUtil http) {
 public async ValueTask<string> Handle(StorageRequest request,MongoId session,CancellationToken ct){
  var gate=ProfileGates.Get(session.ToString());await gate.WaitAsync(ct);
  try{
   var profile=saves.GetProfile(session)??throw new ArgumentException("请先登录存档。");
   var pmc=profile.CharacterData?.PmcData??throw new ArgumentException("PMC 存档尚未就绪。");
   if(request.Profile!=pmc.Id.ToString())throw new ArgumentException("只能打开当前 PMC 的仓库。");
   var inventory=pmc.Inventory??throw new ArgumentException("库存尚未就绪。");
   string root=inventory.Stash?.ToString()??throw new ArgumentException("没有找到仓库。");
   string Digest(List<Item> value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json.Serialize(value)!)));
   string Revision()=>Digest(TerminalStorageRules.Tree(inventory.Items!,root));
   if(request.Action=="save"){
    var raw=request.Items??throw new ArgumentException("缺少仓库物品。");
    var normalized=StorageJson.Normalize(raw.GetRawText(),out _,out _);
    var incoming=json.Deserialize<List<Item>>(normalized.ToJsonString())??throw new ArgumentException("仓库物品格式无效。");
    string current=Revision();
    // A repeated request after a lost response is safe only when it is exactly
    // the already persisted native tree. A stale different tree is rejected.
    if(request.Revision!=current&&Digest(incoming)!=current)throw new ArgumentException("仓库已被其他操作修改，请关闭终端重新连接。");
    var merged=TerminalStorageRules.Merge(inventory.Items!,root,incoming);
    if(Digest(incoming)!=current){
     string directory=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","Minecraft","ProjectE","backups","terminal");Directory.CreateDirectory(directory);
     await File.WriteAllTextAsync(System.IO.Path.Combine(directory,$"{session}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.json"),json.Serialize(profile),ct);
     if(Revision()!=current)throw new ArgumentException("保存期间仓库已变化，请重新连接。");
     var previous=inventory.Items;inventory.Items=merged;
     try{await saves.SaveProfileAsync(session);}catch{inventory.Items=previous;throw;}
    }
   }else if(request.Action!="state")throw new ArgumentException("未知终端操作。");
   return http.GetBody(new{ok=true,stash=root,revision=Revision(),items=request.Action=="state"?TerminalStorageRules.Tree(inventory.Items!,root):null});
  }catch(Exception e){return http.GetBody(new{ok=false,error=e is ArgumentException?e.Message:"仓库保存失败，请重试。"});}
  finally{gate.Release();}
 }
}
