using System.Globalization;
namespace LocalFleaShared;
internal readonly record struct FleaOfferLifetime(long EndTime,bool Permanent,long? PausedRemainingSeconds=null) {
 internal const long PermanentEndTime=253402300799;
 internal bool Paused=>PausedRemainingSeconds.HasValue;
 internal bool IsPermanent=>!Paused&&Permanent;
 internal long RemainingSeconds(long now)=>Math.Max(0,PausedRemainingSeconds??EndTime-now);
 internal string EditorMinutes(long now)=>IsPermanent?"-1":((long)Math.Ceiling(RemainingSeconds(now)/60d)).ToString(CultureInfo.InvariantCulture);
 internal string Clock(long now){long seconds=RemainingSeconds(now);return $"{seconds/3600:D2}:{seconds/60%60:D2}:{seconds%60:D2}";}
}
