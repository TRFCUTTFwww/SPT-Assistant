using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
namespace LocalGearMail;
public static class KarmaRules {
 public const string Fence="579dc571d53a0658a154fbec";
 public static double Pmc(PmcData pmc)=>pmc.KarmaValue??.5;
 public static double Scav(PmcData pmc)=>pmc.TradersInfo?.TryGetValue(new MongoId(Fence),out var trader)==true?trader.Standing??0:0;
 public static void Apply(PmcData pmc,string kind,double value,double? expected,double minScav,double maxScav){
  if(kind is not ("pmc" or "scav"))throw new ArgumentException("未知业力类型。");
  double current=kind=="pmc"?Pmc(pmc):Scav(pmc);if(expected==null||expected.Value!=current)throw new ArgumentException("业力值已改变，请刷新后重试。");
  double min=kind=="pmc"?0:minScav,max=kind=="pmc"?1:maxScav;
  if(!double.IsFinite(value)||value<min||value>max)throw new ArgumentException("业力值超出当前规则范围。");
  if(kind=="pmc")pmc.KarmaValue=value;
  else{if(pmc.TradersInfo?.TryGetValue(new MongoId(Fence),out var fence)!=true)throw new ArgumentException("未找到 Fence 好感数据。");fence!.Standing=value;}
 }
}
