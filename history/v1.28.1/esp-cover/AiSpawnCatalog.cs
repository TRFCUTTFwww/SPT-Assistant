using System;
namespace LocalSpt5Esp;
internal sealed record AiSpawnOption(string Group,string Name,string Role,string Side="Savage",bool Boss=false);
internal static class AiSpawnCatalog {
 internal static readonly string[] Groups={"普通 AI","BOSS","邪教徒","黑色军团"};
 internal static readonly AiSpawnOption[] Options={
  new("普通 AI","Scav","assault"),new("普通 AI","USEC","pmcUSEC","Usec"),new("普通 AI","BEAR","pmcBEAR","Bear"),new("普通 AI","Raider 掠夺者","pmcBot"),new("普通 AI","Rogue 游荡者","exUsec"),
  new("BOSS","Reshala · 海关","bossBully",Boss:true),new("BOSS","Killa · 商场","bossKilla",Boss:true),new("BOSS","Shturman · 森林","bossKojaniy",Boss:true),new("BOSS","Glukhar · 储备站","bossGluhar",Boss:true),new("BOSS","Sanitar · 海岸线","bossSanitar",Boss:true),new("BOSS","Tagilla · 工厂","bossTagilla",Boss:true),new("BOSS","Knight · 三人组队长","bossKnight",Boss:true),new("BOSS","Big Pipe · 三人组","followerBigPipe"),new("BOSS","Birdeye · 三人组","followerBirdEye"),new("BOSS","Kaban · 卡班","bossBoar",Boss:true),new("BOSS","Kollontay · 警棍","bossKolontay",Boss:true),new("BOSS","Partisan · 老游击","bossPartisan",Boss:true),new("BOSS","Zryachiy · 灯塔","bossZryachiy",Boss:true),
  new("邪教徒","邪教徒战士","sectantWarrior"),new("邪教徒","邪教徒祭司","sectantPriest",Boss:true),new("邪教徒","Oni · 特殊邪教徒","sectantOni",Boss:true),new("邪教徒","Predvestnik · 特殊邪教徒","sectantPredvestnik",Boss:true),new("邪教徒","Prizrak · 特殊邪教徒","sectantPrizrak",Boss:true),
  new("黑色军团","黑色军团干员","blackDivision"),new("黑色军团","Wedge · 黑色军团队长","bossWedge",Boss:true),new("黑色军团","Wedge · 实验室队长","bossWedgeLab",Boss:true),new("黑色军团","实验室随从","followerWedgeLab"),new("黑色军团","Reshala · 军团变体","bossBullyBlackDiv",Boss:true),new("黑色军团","Reshala 随从 · 军团变体","followerBullyBlackDiv"),new("黑色军团","Raider · 军团变体","pmcBotBlackDiv")
 };
}
