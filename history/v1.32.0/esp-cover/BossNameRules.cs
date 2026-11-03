using System;
namespace LocalSpt5Esp;
internal enum BossNameLanguage { English,Russian }
internal static class BossNameRules {
 internal static string Resolve(string role,string original,BossNameLanguage language){
  var names=role.ToLowerInvariant() switch {
   "bossbully"=>("Reshala","Решала"),"bosskilla" or "bosskillaagro"=>("Killa","Килла"),"bosskojaniy"=>("Shturman","Штурман"),"bossgluhar"=>("Glukhar","Глухарь"),"bosssanitar"=>("Sanitar","Санитар"),"bosstagilla" or "bosstagillaagro"=>("Tagilla","Тагилла"),"bossboar"=>("Kaban","Кабан"),"bosskolontay"=>("Kollontay","Коллонтай"),"bosspartisan"=>("Partisan","Партизан"),"bosszryachiy"=>("Zryachiy","Зрячий"),_=>((string?)null,(string?)null)
  };
  return (language==BossNameLanguage.English?names.Item1:names.Item2)??original;
 }
}
