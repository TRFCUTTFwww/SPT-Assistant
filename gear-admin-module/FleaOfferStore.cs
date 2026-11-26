using System.Text.Json;
using SPTarkov.Server.Core.Utils;

namespace LocalGearMail;

internal sealed record SavedFleaOffer(string Id,string Template,CustomFleaOffers.Draft Draft,string OfferId,int Stock,long EndTime,string State,string? ItemTree=null,long? PausedRemainingSeconds=null);

internal sealed class FleaOfferStore {
 internal static readonly JsonUtil ItemJson=new([new SPTarkov.Server.Core.Utils.Json.SptJsonConverterRegistrator()]);
 readonly string path;
 internal FleaOfferStore(string path){
  this.path=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(this.path)!);
  using var db=Open();
  using(var q=db.Prepare("PRAGMA user_version"))if(q.Row()&&q.Int(0)>1)throw new IOException("报价数据库版本过高，请先更新辅助 MOD。");
  db.Exec("PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS custom_offers(id TEXT PRIMARY KEY,offer_id TEXT NOT NULL,body BLOB NOT NULL); CREATE UNIQUE INDEX IF NOT EXISTS custom_offers_native_id ON custom_offers(offer_id); PRAGMA user_version=1;");
 }
 SqliteSnapshots.Db Open()=>new(path);
 internal SavedFleaOffer[] List(){using var db=Open();using var q=db.Prepare("SELECT body FROM custom_offers ORDER BY rowid DESC");var rows=new List<SavedFleaOffer>();while(q.Row())rows.Add(JsonSerializer.Deserialize<SavedFleaOffer>(q.Bytes(0))??throw new IOException("持久化报价数据无法读取。"));return rows.ToArray();}
 internal SavedFleaOffer? Find(string id){using var db=Open();using var q=db.Prepare("SELECT body FROM custom_offers WHERE id=? OR offer_id=?");q.Text(1,id);q.Text(2,id);return q.Row()?JsonSerializer.Deserialize<SavedFleaOffer>(q.Bytes(0)):null;}
 internal void Put(SavedFleaOffer offer){
  using var db=Open();using var q=db.Prepare("INSERT INTO custom_offers(id,offer_id,body) VALUES(?,?,?) ON CONFLICT(id) DO UPDATE SET offer_id=excluded.offer_id,body=excluded.body");
  q.Text(1,offer.Id);q.Text(2,offer.OfferId);q.Blob(3,JsonSerializer.SerializeToUtf8Bytes(offer));q.Done();
 }
 internal void Delete(string id){using var db=Open();using var q=db.Prepare("DELETE FROM custom_offers WHERE id=?");q.Text(1,id);q.Done();}
}
