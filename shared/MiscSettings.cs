using System;
namespace LocalMiscShared;
public sealed record MiscSettings {
 public bool GoodBrother {get;init;}
 public bool Connections {get;init;}
 public bool FreeInsurance {get;init;}
 public bool GuaranteedInsurance {get;init;}
 public bool SniperBlind {get;init;}
 public bool ScavSecure {get;init;}
 public bool ScavNoCooldown {get;init;}
 public string ScavSecureTemplate {get;init;}="544a11ac4bdc2d470e8b456a";
 public bool FreeFlea {get;init;}
 public bool ExpressInsurance {get;init;}
 public bool UnlimitedTraderPurchases {get;init;}
 public static bool RestrictedMap(string? id)=>id is "laboratory" or "laboratory_dark" or "laboratory_event" or "labyrinth" or "icebreaker";
 public static double StandingDelta(bool enabled,bool scav,double delta)=>enabled&&scav&&double.IsFinite(delta)&&delta<0?-delta:delta;
}
