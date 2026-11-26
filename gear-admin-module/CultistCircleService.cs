using System.IO;
using System.Text.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using Path=System.IO.Path;
namespace LocalGearMail;
public static class CultistCircleService {
 static HashSet<MongoId>? recipes;
 public static async ValueTask<string> Handle(CharacterDataRequest request,MongoId session,SptProfile profile,SaveServer saves,JsonUtil json,HttpResponseUtil http,CancellationToken ct){
  var pmc=profile.CharacterData!.PmcData!;var hideout=pmc.Hideout??throw new ArgumentException("藏身处尚未就绪。");
  recipes??=(json.Deserialize<HideoutProductionData>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"SPT_Data","database","hideout","production.json")))?.CultistRecipes??[]).Select(recipe=>recipe.Id).ToHashSet();
  var previous=hideout.Production;var candidates=(previous??[]).Where(pair=>recipes.Contains(pair.Key)&&CultistCircleRules.Eligible(pair.Value)).ToArray();
  if(candidates.Length>1)throw new ArgumentException("仪式状态不明确，请重新登录角色。");
  var selected=candidates.SingleOrDefault();var production=selected.Value;var rewards=production==null?[]:CultistCircleRules.Rewards(pmc);string revision=production==null?"":CultistCircleRules.Revision(pmc.Id??throw new ArgumentException("角色尚未就绪，请稍后重试。"),selected.Key,production,rewards);bool completed=false;
  if(request.Action=="finishCultist"){
   if(production==null)throw new ArgumentException("当前没有可完成的仪式。");
   CultistCircleRules.RequireCurrent(request.Id,request.Revision,selected.Key,revision);
   if(production.AvailableForFinish!=true||production.InProgress==true){
    string before=json.Serialize(previous)!;var copy=json.Deserialize<Dictionary<MongoId,Production?>>(before)??throw new ArgumentException("仪式状态无法读取。");
    var updated=copy[selected.Key]??throw new ArgumentException("当前没有可完成的仪式。");completed=CultistCircleRules.Finish(updated);
    string inventoryBefore=json.Serialize(pmc.Inventory)!;var directory=Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","cultist-circle");Directory.CreateDirectory(directory);
    await File.WriteAllTextAsync(Path.Combine(directory,$"{session}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json"),json.Serialize(profile),ct);
    if(!ReferenceEquals(previous,hideout.Production)||json.Serialize(previous)!=before||json.Serialize(pmc.Inventory)!=inventoryBefore)throw new ArgumentException("仪式已经改变，请刷新预览后重试。");
    hideout.Production=copy;try{await saves.SaveProfileAsync(session);}catch{hideout.Production=previous;throw;}production=updated;
   }
  }
  string stashId="";JsonElement stashItems=default;
  if(production==null||production.AvailableForFinish==true){
   var tree=CultistCircleRules.StashTree(pmc,out stashId);
   stashItems=JsonSerializer.Deserialize<JsonElement>(tree.Length==0?"[]":json.Serialize(tree)??"[]");
  }else stashItems=JsonSerializer.Deserialize<JsonElement>("[]");
  return http.GetBody(new{ok=true,profileId=pmc.Id.ToString(),active=production!=null,completed,revision,production=production==null?null:new{id=selected.Key.ToString(),startTimestamp=production.StartTimestamp??0,productionTime=production.ProductionTime??0,progress=production.Progress??0,inProgress=production.InProgress==true,availableForFinish=production.AvailableForFinish==true},items=rewards.Select(item=>new{id=item.Id.ToString(),tpl=item.Template.ToString(),count=Math.Max(1,item.Upd?.StackObjectsCount??1)}),stashId,stashItems});
 }
}
