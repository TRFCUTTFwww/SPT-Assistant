using System;
namespace LocalMiscShared;
public sealed record MiscSettings {
 public bool FunItemsEnabled {get;init;}=true;
 public bool GoodBrother {get;init;}
 public bool Connections {get;init;}
 public bool FreeInsurance {get;init;}
 public bool GuaranteedInsurance {get;init;}
 public bool SniperBlind {get;init;}
 public bool UnlimitedLightkeeperVisit {get;init;}
 public bool ScavSecure {get;init;}
 public bool ScavNoCooldown {get;init;}
 public string ScavSecureTemplate {get;init;}="544a11ac4bdc2d470e8b456a";
 public bool FreeFlea {get;init;}
 public bool InstantFleaSales {get;init;}
 public bool ClearanceTraders {get;init;}
 public bool ClearanceMarket {get;init;}
 public bool DiplomaticImmunity {get;init;}
 public bool ExpressInsurance {get;init;}
 public bool UnlimitedTraderPurchases {get;init;}
 public static bool Diplomacy(bool enabled,bool raid,bool localScav,bool localTarget,bool localAi,bool aiScav,string role)=>enabled&&raid&&localScav&&localTarget&&localAi&&role is not ("pmcUSEC" or "pmcBEAR" or "shooterBTR")&&(aiScav||role.StartsWith("boss",StringComparison.OrdinalIgnoreCase)||role.StartsWith("follower",StringComparison.OrdinalIgnoreCase)||role.StartsWith("sectant",StringComparison.OrdinalIgnoreCase));
 public static bool RestrictedMap(string? id)=>id is "laboratory" or "laboratory_dark" or "laboratory_event" or "labyrinth" or "icebreaker";
 public static double StandingDelta(bool enabled,bool scav,double delta)=>enabled&&scav&&double.IsFinite(delta)&&delta<0?-delta:delta;
}
