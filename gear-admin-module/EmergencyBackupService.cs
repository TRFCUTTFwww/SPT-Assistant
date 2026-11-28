using LocalSpt.Shared;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public static class EmergencyBackupService {
 public static string Handle(CharacterDataRequest request,MongoId session,SptProfile profile,JsonUtil json,HttpResponseUtil http,TemplateTable templates){
  if(request.Categories.Length!=1||request.Categories[0] is not ("pmc" or "scav"))throw new ArgumentException("救援角色类型无效。");
  var copy=json.Deserialize<SptProfile>(json.Serialize(profile))??throw new ArgumentException("无法创建救援快照。");var character=request.Categories[0]=="pmc"?copy.CharacterData?.PmcData:copy.CharacterData?.ScavData;
  if(character?.Id.ToString()!=request.Id)throw new ArgumentException("救援角色身份不匹配。");var inventory=character.Inventory??throw new ArgumentException("角色库存尚未就绪。");string root=inventory.Equipment?.ToString()??throw new ArgumentException("装备根容器缺失。");
  var raw=request.StashItems??throw new ArgumentException("缺少装备快照。");
  var normalized=RaidStashJson.Normalize(raw.GetRawText(),out _,out _);var items=json.Deserialize<List<Item>>(normalized.ToJsonString())??throw new ArgumentException("救援物品格式无效。");
  // Merge only into an immutable backup, never the live profile or stash.
  var original=inventory.Items!.Single(i=>i.Id.ToString()==root);var replacement=items.SingleOrDefault(i=>i.Id.ToString()==root)??throw new ArgumentException("装备根容器不匹配。");replacement.ParentId=original.ParentId;replacement.SlotId=original.SlotId;replacement.Location=original.Location;
  inventory.Items=RaidStashRules.Merge(inventory.Items!,root,items);string body=json.Serialize(copy)!;var store=new SqliteSnapshots(System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","profiles.sqlite"));var row=store.Add(session.ToString(),ProfileRestoreRules.Nickname(body),"错误救援 · 战局装备（"+request.Categories[0]+"）",body);
  return http.GetBody(new{ok=true,backup=row.Id,sha256=row.Sha256,itemCount=items.Count,message="装备快照已写入 SQLite；幸存结算由游戏原生流程另行提交。"});
 }
}
