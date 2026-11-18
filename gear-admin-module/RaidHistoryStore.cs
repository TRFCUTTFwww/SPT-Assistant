using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace LocalGearMail;
public sealed record HistoryItem(string Id,string Template,string Parent,string Slot,int Count,JsonElement Raw);
public sealed record HistoryKill(string Name,string Side,string Role,int Level,string Weapon,string BodyPart,double Distance,string Time);
public sealed record RaidHistoryRecord {
 public string Id{get;init;}="";public string ServerId{get;init;}="";public string Map{get;init;}="";public string Side{get;init;}="";public string Nickname{get;init;}="";
 public string Started{get;init;}="";public string Ended{get;init;}="";public string Status{get;init;}="PENDING";public string Exit{get;init;}="";public double? Seconds{get;init;}
 public string[] Route{get;init;}=[];public string TransitDestination{get;init;}="";public string TransitionRaidId{get;init;}="";
 public bool EntryKnown{get;init;}public bool Settled{get;init;}public HistoryItem[] Entry{get;init;}=[];public HistoryItem[] Returned{get;init;}=[];public HistoryItem[] Loot{get;init;}=[];public HistoryItem[] Final{get;init;}=[];public HistoryKill[] Kills{get;init;}=[];
}
public static class RaidHistoryRules {
 public static string[] Route(IEnumerable<string>? visited,string current){var route=(visited??[]).Where(s=>!string.IsNullOrWhiteSpace(s)).Select(s=>s.Trim().ToLowerInvariant()).ToList();current=(current??"").Trim().ToLowerInvariant();if(current.Length>0&&(route.Count==0||route[^1]!=current))route.Add(current);return route.ToArray();}
 public static string Id(string session,string server)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(session+"\n"+server)));
 public static JsonElement Field(JsonElement root,string key){if(root.ValueKind==JsonValueKind.Object)foreach(var p in root.EnumerateObject())if(p.Name.Equals(key,StringComparison.OrdinalIgnoreCase))return p.Value;return default;}
 public static string Text(JsonElement root,string key){var value=Field(root,key);return value.ValueKind==JsonValueKind.String?value.GetString()??"":value.ValueKind==JsonValueKind.Number?value.ToString():"";}
 public static double Number(JsonElement root,string key){var v=Field(root,key);return v.ValueKind==JsonValueKind.Number&&v.TryGetDouble(out var n)&&double.IsFinite(n)?n:0;}
 // Include only carried inventory trees. Stash, sorting table and quest stash never belong to a raid.
 public static HistoryItem[] Inventory(JsonElement inventory){
  var array=Field(inventory,"items");if(array.ValueKind!=JsonValueKind.Array)return [];
  var all=array.EnumerateArray().ToArray();var roots=new HashSet<string>(new[]{Text(inventory,"equipment"),Text(inventory,"questRaidItems")}.Where(s=>s.Length>0));
  var ids=new HashSet<string>(roots);bool changed;do{changed=false;foreach(var item in all)if(ids.Contains(Text(item,"parentId")))changed|=ids.Add(Text(item,"_id"));}while(changed);
  return all.Where(i=>ids.Contains(Text(i,"_id"))&&!roots.Contains(Text(i,"_id"))).Select(i=>new HistoryItem(Text(i,"_id"),Text(i,"_tpl"),Text(i,"parentId"),Text(i,"slotId"),Math.Max(1,(int)Number(Field(i,"upd"),"StackObjectsCount")),i.Clone())).ToArray();
 }
 public static HistoryItem[] Returned(HistoryItem[] final,HistoryItem[] saved){var ids=saved.Select(i=>i.Id).ToHashSet();return final.Where(i=>ids.Contains(i.Id)).ToArray();}
 public static HistoryItem[] Loot(HistoryItem[] entry,HistoryItem[] returned)=>returned.Select(i=>i with{Count=Math.Max(0,i.Count-(entry.FirstOrDefault(e=>e.Id==i.Id)?.Count??0))}).Where(i=>i.Count>0).ToArray();
 public static HistoryKill[] Kills(JsonElement stats){var victims=Field(Field(stats,"eft"),"Victims");return victims.ValueKind!=JsonValueKind.Array?[]:victims.EnumerateArray().Select(v=>new HistoryKill(Text(v,"Name"),Text(v,"Side"),Text(v,"Role"),(int)Number(v,"Level"),Text(v,"Weapon"),Text(v,"BodyPart"),Number(v,"Distance"),Text(v,"Time"))).ToArray();}
}
public sealed class RaidHistoryStore {
 readonly string path;
 public RaidHistoryStore(string path){this.path=path;Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);using var db=new SqliteSnapshots.Db(path);using(var q=db.Prepare("PRAGMA user_version")){if(q.Row()&&q.Int(0)>1)throw new IOException("战局数据库版本过高。");}db.Exec("PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS raids(id TEXT PRIMARY KEY,session TEXT NOT NULL,started TEXT NOT NULL,map TEXT NOT NULL,status TEXT NOT NULL,body BLOB NOT NULL); CREATE INDEX IF NOT EXISTS raids_session_time ON raids(session,started DESC,id DESC); PRAGMA user_version=1;");}
 public RaidHistoryRecord? Find(string session,string id){using var db=new SqliteSnapshots.Db(path);using var q=db.Prepare("SELECT body FROM raids WHERE session=? AND id=?");q.Text(1,session);q.Text(2,id);return q.Row()?JsonSerializer.Deserialize<RaidHistoryRecord>(q.Bytes(0)):null;}
 public void Put(string session,RaidHistoryRecord raid){using var db=new SqliteSnapshots.Db(path);db.Exec("BEGIN IMMEDIATE");using(var existing=db.Prepare("SELECT body FROM raids WHERE id=?")){existing.Text(1,raid.Id);if(existing.Row()&&JsonSerializer.Deserialize<RaidHistoryRecord>(existing.Bytes(0))?.Settled==true){db.Exec("COMMIT");return;}}using var q=db.Prepare("INSERT INTO raids(id,session,started,map,status,body) VALUES(?,?,?,?,?,?) ON CONFLICT(id) DO UPDATE SET status=excluded.status,body=excluded.body WHERE raids.session=excluded.session");q.Text(1,raid.Id);q.Text(2,session);q.Text(3,raid.Started.Length>0?raid.Started:raid.Ended);q.Text(4,raid.Map);q.Text(5,raid.Status);q.Blob(6,JsonSerializer.SerializeToUtf8Bytes(raid));q.Done();db.Exec("COMMIT");}
 public object List(string session,int page,string map,string status){page=Math.Clamp(page,0,100000);using var db=new SqliteSnapshots.Db(path);const string filter=" WHERE session=? AND (?='' OR map=?) AND (?='' OR status=?)";
  void Bind(SqliteSnapshots.Query q){q.Text(1,session);q.Text(2,map);q.Text(3,map);q.Text(4,status);q.Text(5,status);}
  int total,survived=0,kills=0;using(var count=db.Prepare("SELECT COUNT(*), COALESCE(SUM(CASE WHEN status='SURVIVED' THEN 1 ELSE 0 END),0) FROM raids"+filter)){Bind(count);if(count.Row()){total=count.Int(0);survived=count.Int(1);}else total=0;}
  using(var sumQ=db.Prepare("SELECT body FROM raids"+filter+" ORDER BY started DESC,id DESC LIMIT 1000")){Bind(sumQ);while(sumQ.Row()){var r=JsonSerializer.Deserialize<RaidHistoryRecord>(sumQ.Bytes(0));if(r!=null)kills+=r.Kills.Length;}}
  using var query=db.Prepare("SELECT body FROM raids"+filter+" ORDER BY started DESC,id DESC LIMIT 20 OFFSET "+(page*20));Bind(query);var rows=new List<object>();while(query.Row()){var r=JsonSerializer.Deserialize<RaidHistoryRecord>(query.Bytes(0))!;rows.Add(new{r.Id,r.Map,r.Side,r.Nickname,r.Started,r.Ended,r.Status,r.Seconds,r.Settled,kills=r.Kills.Length,loot=r.Loot.Sum(i=>i.Count)});}return new{rows,total,page,pageSize=20,summary=new{total,survived,kills}};
 }
 public RaidHistoryRecord[] Export(string session,string map,string status,int limit=500){
  limit=Math.Clamp(limit,1,2000);using var db=new SqliteSnapshots.Db(path);const string filter=" WHERE session=? AND (?='' OR map=?) AND (?='' OR status=?)";
  using var query=db.Prepare("SELECT body FROM raids"+filter+" ORDER BY started DESC,id DESC LIMIT "+limit);
  query.Text(1,session);query.Text(2,map);query.Text(3,map);query.Text(4,status);query.Text(5,status);
  var rows=new List<RaidHistoryRecord>();while(query.Row())if(JsonSerializer.Deserialize<RaidHistoryRecord>(query.Bytes(0)) is {} r)rows.Add(r);return rows.ToArray();
 }
}
