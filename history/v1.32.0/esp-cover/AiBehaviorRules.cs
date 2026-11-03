using System;
namespace LocalSpt5Esp;
internal static class AiBehaviorRules {internal static bool BossFamily(string role)=>role.StartsWith("boss",StringComparison.OrdinalIgnoreCase)||role.StartsWith("follower",StringComparison.OrdinalIgnoreCase)||role.Contains("blackdiv",StringComparison.OrdinalIgnoreCase)||role.Equals("tagillaHelperAgro",StringComparison.OrdinalIgnoreCase);}
