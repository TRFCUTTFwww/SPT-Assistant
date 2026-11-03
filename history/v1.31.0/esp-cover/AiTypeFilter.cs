using System;
namespace LocalSpt5Esp;
internal enum AiTypeFilter { All,Scav,Pmc,Usec,Bear,Boss,Follower,Cultist,BlackDivision,Raider,Rogue }
internal static class AiTypeRules {
 internal static string Name(AiTypeFilter filter)=>filter switch {AiTypeFilter.All=>"全部",AiTypeFilter.Scav=>"Scav",AiTypeFilter.Pmc=>"全部 PMC",AiTypeFilter.Usec=>"USEC",AiTypeFilter.Bear=>"BEAR",AiTypeFilter.Boss=>"BOSS",AiTypeFilter.Follower=>"保镖",AiTypeFilter.Cultist=>"邪教徒",AiTypeFilter.BlackDivision=>"黑色军团",AiTypeFilter.Raider=>"掠夺者",_=>"游荡者"};
 internal static bool Matches(AiTypeFilter filter,string role,bool savage){if(filter==AiTypeFilter.All)return true;var kind=RolePalette.Classify(role,savage);return filter switch {AiTypeFilter.Scav=>kind==CharacterKind.Scav,AiTypeFilter.Pmc=>kind==CharacterKind.Pmc,AiTypeFilter.Usec=>kind==CharacterKind.Pmc&&role.Equals("pmcUSEC",StringComparison.OrdinalIgnoreCase),AiTypeFilter.Bear=>kind==CharacterKind.Pmc&&role.Equals("pmcBEAR",StringComparison.OrdinalIgnoreCase),AiTypeFilter.Boss=>kind==CharacterKind.Boss,AiTypeFilter.Follower=>kind==CharacterKind.Follower,AiTypeFilter.Cultist=>kind==CharacterKind.Cultist,AiTypeFilter.BlackDivision=>kind==CharacterKind.BlackFox,AiTypeFilter.Raider=>kind==CharacterKind.Raider,AiTypeFilter.Rogue=>kind==CharacterKind.Rogue,_=>false};}
}
