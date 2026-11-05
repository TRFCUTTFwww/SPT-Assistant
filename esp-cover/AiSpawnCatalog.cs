using System;
namespace LocalSpt5Esp;
internal sealed record AiSpawnOption(string Group,string Name,string Role,string Side="Savage",bool Boss=false);
internal static class AiSpawnCatalog {
 internal static readonly string[] Groups={"普通 AI","BOSS","邪教徒","黑色军团","僵尸"};
 internal static readonly AiSpawnOption[] Options={
  new("普通 AI","Scav","assault"),new("普通 AI","USEC","pmcUSEC","Usec"),new("普通 AI","BEAR","pmcBEAR","Bear"),new("普通 AI","Raider 掠夺者","pmcBot"),new("普通 AI","Rogue 游荡者","exUsec"),
  new("僵尸","感染 Scav","infectedAssault"),new("僵尸","感染 PMC","infectedPmc"),new("僵尸","感染平民","infectedCivil"),
  new("BOSS","Reshala · 海关","bossBully",Boss:true),new("BOSS","Killa · 商场","bossKilla",Boss:true),new("BOSS","Shturman · 森林","bossKojaniy",Boss:true),new("BOSS","Glukhar · 储备站","bossGluhar",Boss:true),new("BOSS","Sanitar · 海岸线","bossSanitar",Boss:true),new("BOSS","Tagilla · 工厂","bossTagilla",Boss:true),new("BOSS","Knight · 三人组队长","bossKnight",Boss:true),new("BOSS","Big Pipe · 三人组","followerBigPipe"),new("BOSS","Birdeye · 三人组","followerBirdEye"),new("BOSS","Kaban · 卡班","bossBoar",Boss:true),new("BOSS","Kollontay · 警棍","bossKolontay",Boss:true),new("BOSS","Partisan · 老游击","bossPartisan",Boss:true),new("BOSS","Zryachiy · 灯塔","bossZryachiy",Boss:true),
  new("BOSS","Killa · 迷宫（狂暴）","bossKillaAgro",Boss:true),new("BOSS","Tagilla · 迷宫（狂暴）","bossTagillaAgro",Boss:true),
  new("BOSS","感染大锤 · 镰刀","infectedTagilla",Boss:true),
  new("BOSS","复仇兹拉奇 · 活动","ravangeZryachiyEvent",Boss:true),
  new("邪教徒","邪教徒战士","sectantWarrior"),new("邪教徒","邪教徒祭司","sectantPriest",Boss:true),new("邪教徒","Oni · 特殊邪教徒","sectantOni",Boss:true),new("邪教徒","Predvestnik · 特殊邪教徒","sectantPredvestnik",Boss:true),new("邪教徒","Prizrak · 特殊邪教徒","sectantPrizrak",Boss:true),
  new("黑色军团","黑色军团干员","blackDivision"),new("黑色军团","军团突击成员 · 随机变体（bossWedge）","bossWedge",Boss:true),new("黑色军团","Wedge 本人 · 实验室（固定 Wedge）","bossWedgeLab",Boss:true),new("黑色军团","实验室随从","followerWedgeLab"),new("黑色军团","军团成员 · Laser／Viper 等","bossBullyBlackDiv",Boss:true),new("黑色军团","军团随从 · Pike／Obsidian 等","followerBullyBlackDiv"),new("黑色军团","军团掠夺者 · Revenant／Raiden 等","pmcBotBlackDiv")
 };
}
