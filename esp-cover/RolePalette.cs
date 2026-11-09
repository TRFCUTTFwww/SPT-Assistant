using System;
namespace LocalSpt5Esp;
internal enum CharacterKind { Boss, Follower, Scav, Raider, Rogue, BlackFox, Pmc, Cultist, Zombie }
internal static class RolePalette {
 internal static CharacterKind Classify(string role,bool savage){
  if(role.Equals("infectedTagilla",StringComparison.OrdinalIgnoreCase)||role.Equals("ravangeZryachiyEvent",StringComparison.OrdinalIgnoreCase))return CharacterKind.Boss;
  if(role.StartsWith("infected",StringComparison.OrdinalIgnoreCase))return CharacterKind.Zombie;
  if(role.StartsWith("bossWedge",StringComparison.OrdinalIgnoreCase)||role.StartsWith("followerWedge",StringComparison.OrdinalIgnoreCase))return CharacterKind.BlackFox;
  if(role.Contains("blackdiv",StringComparison.OrdinalIgnoreCase)||role.Contains("blackfox",StringComparison.OrdinalIgnoreCase))return CharacterKind.BlackFox;
  if(role.StartsWith("sectant",StringComparison.OrdinalIgnoreCase)||role.StartsWith("sectact",StringComparison.OrdinalIgnoreCase))return CharacterKind.Cultist;
  if(role.Equals("pmcBot",StringComparison.OrdinalIgnoreCase))return CharacterKind.Raider;
  if(role.StartsWith("exUsec",StringComparison.OrdinalIgnoreCase))return CharacterKind.Rogue;
  if(LocalRaidShared.RaidSpawnRules.IsFollower(role))return CharacterKind.Follower;
  if(role.StartsWith("boss",StringComparison.OrdinalIgnoreCase))return CharacterKind.Boss;
  if(role.Equals("pmcUSEC",StringComparison.OrdinalIgnoreCase)||role.Equals("pmcBEAR",StringComparison.OrdinalIgnoreCase)||!savage)return CharacterKind.Pmc;
  return CharacterKind.Scav;
 }
 internal static string Name(CharacterKind kind)=>kind switch{
  CharacterKind.Boss=>"BOSS",CharacterKind.Follower=>"保镖",CharacterKind.Scav=>"Scav",CharacterKind.Raider=>"掠夺者",
  CharacterKind.Rogue=>"游荡者",CharacterKind.BlackFox=>"黑色军团",CharacterKind.Cultist=>"邪教徒",CharacterKind.Zombie=>"僵尸",_=>"PMC"
 };
 internal static string Hex(CharacterKind kind)=>kind switch{
  CharacterKind.Boss=>"#FF3030",CharacterKind.Follower=>"#B369FF",CharacterKind.Scav=>"#66D94D",CharacterKind.Raider=>"#FF9E26",
  CharacterKind.Rogue=>"#26D9F2",CharacterKind.BlackFox=>"#FF40C7",CharacterKind.Cultist=>"#E5D442",CharacterKind.Zombie=>"#C6FF00",_=>"#4D80FF"
 };
}
