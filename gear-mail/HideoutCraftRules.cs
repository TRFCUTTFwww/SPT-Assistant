using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Common;
namespace LocalGearMail;
public static class HideoutCraftRules {
 public static HashSet<MongoId> OrdinaryRecipes(IEnumerable<SPTarkov.Server.Core.Models.Eft.Hideout.HideoutProduction> recipes)=>recipes.Where(r=>!r.Continuous&&r.AreaType is not (SPTarkov.Server.Core.Models.Enums.Hideout.HideoutAreas.BitcoinFarm or SPTarkov.Server.Core.Models.Enums.Hideout.HideoutAreas.WaterCollector or SPTarkov.Server.Core.Models.Enums.Hideout.HideoutAreas.ScavCase or SPTarkov.Server.Core.Models.Enums.Hideout.HideoutAreas.CircleOfCultists)).Select(r=>r.Id).ToHashSet();
 public static bool Eligible(Production? p)=>p?.InProgress==true&&p.Interrupted!=true&&p.SptIsContinuous!=true&&p.SptIsScavCase!=true&&p.SptIsCultistCircle!=true&&p.ProductionTime is >0;
 public static int Finish(IEnumerable<Production?> productions){int count=0;foreach(var p in productions){if(!Eligible(p)||(p!.Progress??0)>=p.ProductionTime!.Value)continue;p.Progress=p.ProductionTime.Value;count++;}return count;}
}
