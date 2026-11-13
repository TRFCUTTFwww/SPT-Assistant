using LocalRaidShared;

namespace LocalQuickhacks;

// Savage is a native side shared by many special AI, not a display faction.
internal static class TargetFaction {
    internal static string Name(string? role,string? side) {
        role??="";
        string faction=RaidSpawnRules.Faction(role);
        if(faction!="other") return faction switch {
            "scav"=>"Scav", "usec"=>"USEC", "bear"=>"BEAR",
            "boss"=>"BOSS", "follower"=>"保镖", "cultist"=>"邪教徒",
            "blackDivision"=>"黑色军团", "raider"=>"掠夺者", "rogue"=>"游荡者", _=>"其他/未知"
        };
        if(role.StartsWith("infected",StringComparison.OrdinalIgnoreCase)) return "感染者";
        string? special=role.ToLowerInvariant() switch {
            "crazyassaultevent"=>"Scav", "peacefullzryachiyevent"=>"BOSS",
            "civilian"=>"平民", "shooterbtr"=>"BTR 射手", "gifter"=>"圣诞老人",
            "spiritwinter"=>"冬季精灵", "spiritspring"=>"春季精灵",
            "vsrf" or "vsrfsniper" or "vsrffight"=>"俄军",
            "arenafighter" or "arenafighterevent"=>"竞技场战士",
            "peacemaker"=>"Peacemaker", "skier"=>"Skier", "sentry"=>"哨兵", _=>null
        };
        if(special!=null) return special;
        // Unknown roles must not silently become ordinary Scavs. Only missing
        // role data can fall back to the broad native side.
        if(!string.IsNullOrWhiteSpace(role)) return "其他/未知";
        return side?.ToLowerInvariant() switch { "usec"=>"USEC", "bear"=>"BEAR", "savage"=>"其他/未知", _=>"未知" };
    }
}
