using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
namespace LocalGearMail;

public static class CharacterDataRules {
 public const string Tarcoin="69dd0de23dfe95d9e70b5ebb";
 public static long PhysicalCoins(PmcData pmc)=>Coins(pmc).Sum(item=>(long)(item.Upd?.StackObjectsCount??1));
 static List<Item> Coins(PmcData pmc)=>pmc.Inventory?.Items?.Where(item=>item.Template.ToString()==Tarcoin).ToList()??[];
 public static int Deposit(PmcData pmc,long expectedCoins,int expectedBalance){
  var coins=Coins(pmc);long amount=PhysicalCoins(pmc);int balance=pmc.TarCoinBalance??0;
  if(amount!=expectedCoins||balance!=expectedBalance)throw new ArgumentException("数量或余额已变化，请刷新后重试。");
  if(coins.Any(item=>(item.Upd?.StackObjectsCount??1)<=0)||amount<=0)throw new ArgumentException("没有可转入的 TC 币。");
  if(balance<0||amount>int.MaxValue-(long)balance)throw new ArgumentException("转入后超出钱包容量，请先减少实体币数量。");
  var ids=coins.Select(item=>item.Id).ToHashSet();
  if(pmc.Inventory!.Items!.Any(item=>item.ParentId!=null&&ids.Any(id=>id.ToString()==item.ParentId)))throw new ArgumentException("TC 币数据异常，无法转入。");
  pmc.Inventory!.Items!.RemoveAll(item=>ids.Contains(item.Id));pmc.InsuredItems?.RemoveAll(item=>item.ItemId.HasValue&&ids.Contains(item.ItemId.Value));
  pmc.TarCoinBalance=balance+(int)amount;return (int)amount;
 }
 public static void Wallet(PmcData pmc,int target,int expected){if(target<0)throw new ArgumentException("余额不能为负数。");if((pmc.TarCoinBalance??0)!=expected)throw new ArgumentException("余额已变化，请刷新后重试。");pmc.TarCoinBalance=target;}
 public static void Skill(PmcData pmc,string id,double level){
  if(!double.IsFinite(level)||level<0||level>51)throw new ArgumentException("技能等级请输入 0～51。");
  var skill=pmc.Skills?.Common?.SingleOrDefault(skill=>skill.Id.ToString()==id)??throw new ArgumentException("未找到该技能。");
  skill.Progress=level*100;skill.PointsEarnedDuringSession=0;skill.LastAccess=0;
 }
 public static void Achievement(PmcData pmc,string id,bool unlocked,IEnumerable<Achievement> definitions,long now){
  if(!definitions.Any(entry=>entry.Id.ToString()==id))throw new ArgumentException("未知成就。");
  pmc.Achievements??=[];var key=new MongoId(id);if(unlocked)pmc.Achievements.TryAdd(key,now);else pmc.Achievements.Remove(key);
 }
}
