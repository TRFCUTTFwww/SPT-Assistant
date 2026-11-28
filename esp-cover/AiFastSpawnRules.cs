using System;
using System.Collections.Generic;

namespace LocalSpt5Esp;

public static class AiFastSpawnRules {
 static readonly HashSet<string> FixedBossRoles=new(StringComparer.OrdinalIgnoreCase){
  "bossBully","bossKilla","bossKillaAgro","bossKojaniy","bossGluhar","bossSanitar",
  "bossTagilla","bossTagillaAgro","infectedTagilla","bossKnight","followerBigPipe","followerBirdEye",
  "bossBoar","bossKolontay","bossPartisan","bossZryachiy","ravangeZryachiyEvent",
  "sectantPriest","sectantOni","sectantPredvestnik","sectantPrizrak",
  "bossWedgeLab","bossScourgeEventLab","gifter","peacemaker","skier"
 };

 static readonly HashSet<string> WesternCallsignRoles=new(StringComparer.OrdinalIgnoreCase){
  "pmcUSEC","exUsec","exUsecFree","pmcBot","blackDivision","bossWedge","followerWedgeLab",
  "bossBullyBlackDiv","followerBullyBlackDiv","pmcBotBlackDiv","arenaFighterEvent"
 };

 static readonly HashSet<string> BearCallsignRoles=new(StringComparer.OrdinalIgnoreCase){
  "pmcBEAR","vsRF","vsRFSniper"
 };

 static readonly string[] WesternCallsigns={
  "Vanguard","Kestrel","Striker","Nomad","Havoc","Specter","Warden","Falcon",
  "Reaper","Crosshair","Cipher","Titan","Maverick","Talon","Outlaw","Raptor",
  "Sentinel","Apex","Dagger","Kodiak","Phantom","Ronin","Vector","Cobra",
  "Anvil","Castor","Drifter","Tracer","Lynx","Gryphon","Bishop","Harrier"
 };

 static readonly string[] BearCallsigns={
  "Voron","Berkut","Grom","Bars","Sokol","Taiga","Vityaz","Grad",
  "Buran","Korsar","Sever","Volk","Molot","Smerch","Zubr","Tungus",
  "Kedr","Irbis","Krechet","Rubin","Granit","Ural","Strelok","Bagor"
 };

 static readonly string[] ScavNames={
  "Артём Косой","Витя Лом","Серёга Хмурый","Дима Гвоздь","Коля Кабан","Паша Шустрый",
  "Толя Ржавый","Игорь Седой","Олег Тихий","Миша Крюк","Саня Белый","Женя Туман",
  "Лёха Шрам","Антон Хват","Рома Крот","Гена Болт","Юра Череп","Вася Пыль",
  "Денис Клин","Макс Сыч","Борис Грач","Стас Жук","Федя Сухой","Кирилл Дым"
 };

 public static int MaxBatchSize(string side,bool hasRevival)=>hasRevival?1:4;

 public static bool ShouldSettleImmediately(
  bool activationCompleted,
  bool faultedOrCanceled,
  int confirmedInBatch,
  int expectedInBatch,
  float elapsedSinceCompleted){
  if(!activationCompleted)return false;
  if(!faultedOrCanceled&&expectedInBatch>0&&confirmedInBatch>=expectedInBatch)return true;
  return elapsedSinceCompleted>=0.75f;
 }

 public static bool AcceptsDonorRole(
  string requestedRole,
  string requestedSide,
  string donorRole,
  string donorSide){
  if(string.IsNullOrWhiteSpace(requestedRole)||string.IsNullOrWhiteSpace(requestedSide)||
     string.IsNullOrWhiteSpace(donorRole)||string.IsNullOrWhiteSpace(donorSide))
   return false;
  if(AiPmcProfileRules.IsPmc(requestedRole,requestedSide))
   return AiPmcProfileRules.Accepts(requestedRole,requestedSide,donorRole,donorSide);
  return string.Equals(requestedRole,donorRole,StringComparison.Ordinal);
 }

 public static bool IsFixedBossNickname(string role)=>
  !string.IsNullOrWhiteSpace(role)&&FixedBossRoles.Contains(role);

 public static bool AllowsUnarmedDonor(string role){
  if(string.IsNullOrWhiteSpace(role))return false;
  return role.StartsWith("infected",StringComparison.OrdinalIgnoreCase)||
   role.StartsWith("sectant",StringComparison.OrdinalIgnoreCase)||
   role.Equals("spiritWinter",StringComparison.OrdinalIgnoreCase)||
   role.Equals("spiritSpring",StringComparison.OrdinalIgnoreCase)||
   role.Equals("civilian",StringComparison.OrdinalIgnoreCase);
 }

 public static string GenerateVariedNickname(string role,string side,string originalNickname,int sequence){
  if(IsFixedBossNickname(role))return originalNickname;
  if(sequence<=0&&!string.IsNullOrWhiteSpace(originalNickname))return originalNickname;
  string[] pool=string.Equals(side,"Usec",StringComparison.OrdinalIgnoreCase)||WesternCallsignRoles.Contains(role)
   ?WesternCallsigns
   :string.Equals(side,"Bear",StringComparison.OrdinalIgnoreCase)||BearCallsignRoles.Contains(role)
    ?BearCallsigns
    :ScavNames;
  int idx=Math.Abs(unchecked(sequence*7+originalNickname.GetHashCode(StringComparison.Ordinal)))%pool.Length;
  string candidate=pool[idx];
  if(string.Equals(candidate,originalNickname,StringComparison.Ordinal)&&pool.Length>1)
   candidate=pool[(idx+1)%pool.Length];
  return candidate;
 }

 public static int SelectSeedSlot(int seedCount,int cursor)=>
  seedCount<=0?-1:Math.Abs(cursor)%seedCount;

 public static bool ShouldQueueReplenish(int readySeedCount,int targetSeedCount,bool warmTaskBusy)=>
  !warmTaskBusy&&targetSeedCount>0&&readySeedCount<targetSeedCount;
}
