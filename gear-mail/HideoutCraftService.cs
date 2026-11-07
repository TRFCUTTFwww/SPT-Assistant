using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public static class HideoutCraftService {
 static HashSet<MongoId>? ordinary;
 public static async ValueTask<string> Handle(CharacterDataRequest request,MongoId session,SptProfile profile,SaveServer saves,JsonUtil json,HttpResponseUtil http,CancellationToken ct){
  var hideout=profile.CharacterData!.PmcData!.Hideout??throw new ArgumentException("藏身处尚未就绪。");
  var previous=hideout.Production;
  string before=json.Serialize(previous)!;
  var copy=json.Deserialize<Dictionary<MongoId,SPTarkov.Server.Core.Models.Eft.Common.Tables.Production?>>(before)??[];
  ordinary??=HideoutCraftRules.OrdinaryRecipes(json.Deserialize<SPTarkov.Server.Core.Models.Eft.Hideout.HideoutProductionData>(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"SPT_Data","database","hideout","production.json")))!.Recipes??[]);
  var selected=copy.Where(pair=>ordinary.Contains(pair.Key)).ToArray();
  int count=request.Action=="finishCrafts"?HideoutCraftRules.Finish(selected.Select(pair=>pair.Value)):0;
  if(count>0){
   var directory=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","backups","hideout-crafts");Directory.CreateDirectory(directory);
   await File.WriteAllTextAsync(System.IO.Path.Combine(directory,$"{session}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json"),json.Serialize(profile),ct);
   if(!ReferenceEquals(previous,hideout.Production)||json.Serialize(previous)!=before)throw new ArgumentException("制作状态已更新，请重试。");
   hideout.Production=copy;try{await saves.SaveProfileAsync(session);}catch{hideout.Production=previous;throw;}
  }
  return http.GetBody(new{ok=true,profileId=profile.CharacterData!.PmcData!.Id.ToString(),completed=count,productions=selected.Where(pair=>HideoutCraftRules.Eligible(pair.Value)).Select(pair=>new{id=pair.Key.ToString(),progress=pair.Value!.Progress??0,productionTime=pair.Value.ProductionTime??0,startTimestamp=pair.Value.StartTimestamp??0,availableForFinish=pair.Value.AvailableForFinish==true})});
 }
}
