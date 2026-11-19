using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
namespace LocalSpt5Esp;

internal sealed record CornerGameData {
 static readonly JsonSerializerOptions compact=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
 static readonly JsonSerializerOptions pretty=new(compact){WriteIndented=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};

 public string State {get;init;}="menu";
 public DateTimeOffset? CapturedAtUtc {get;init;}
 public bool InRaid {get;init;}
 public string Map {get;init;}="";
 public int RaidRemainingSeconds {get;init;}
 public int AliveAi {get;init;}
 public int AlivePlayers {get;init;}
 public double Fps {get;init;}
 public int HideoutCraftsReady {get;init;}
 public bool CultistReady {get;init;}
 public RaidData? Raid {get;init;}
 public Character? Player {get;init;}
 public HideoutData Hideout {get;init;}=new(0,null);
 public PerformanceData? Performance {get;init;}

 internal static readonly string EmptyJson=new CornerGameData().ToJson();
 internal string ToJson()=>JsonSerializer.Serialize(this,compact);
 internal static string FormatJson(string json){
  using var snapshot=JsonDocument.Parse(json);
  return JsonSerializer.Serialize(snapshot.RootElement,pretty);
 }

 internal sealed record RaidData(string MapId,string MapName,double? DurationSeconds,double? ElapsedSeconds,double? RemainingSeconds,
  double? OvertimeSeconds,bool CountdownPaused,string? GameTime,Weather? Weather,Population? Population,Exit[] Exits,
  Train? Train,Airdrop? Airdrop);
 internal sealed record Weather(double? Rain,double? Fog,double? Clouds,double? Temperature,double? WindX,double? WindZ);
 internal sealed record Population(int Alive,int Ai,int Humans);
 internal sealed record Exit(string Id,string Name,string Status,double? DistanceMeters);
 internal sealed record Train(string State,double? ArrivalInSeconds,double? DepartureInSeconds);
 internal sealed record Airdrop(bool NaturalEnabled,double? NextAttemptInSeconds,int? Attempts,int? MaxAttempts,int PendingFlares);
 internal sealed record Character(string? Name,string? Side,int? Level,int? Experience,bool? Alive,Position? Position,double? HeadingDegrees,
  string? Stance,bool? Aiming,Vitals? Vitals,Weapon? Weapon);
 internal sealed record Position(double? X,double? Y,double? Z);
 internal sealed record Gauge(double? Current,double? Max);
 internal sealed record Vitals(Gauge Health,Gauge Energy,Gauge Hydration,Gauge? Stamina,Gauge? HandsStamina,IReadOnlyDictionary<string,Gauge> BodyParts);
 internal sealed record Weapon(string Name,string TemplateId,int? MagazineRounds,int? MagazineCapacity,int ChamberRounds,string? NextAmmo);
 internal sealed record HideoutData(int CraftsReady,Cultist? Cultist);
 internal sealed record Cultist(bool Active,bool Ready);
 internal sealed record PerformanceData(double Fps,double FrameMs,double P99Ms,double PeakMs,int SlowFrames,int SampleCount,double SampleSeconds,double AssistantMs);
}
