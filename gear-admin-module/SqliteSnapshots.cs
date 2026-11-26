using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
namespace LocalGearMail;
public sealed record SnapshotInfo(string Id,string Session,string Nickname,string Created,string Label,string Sha256,int Bytes);
// Windows 10/11 ships this SQLite library. No external database process or
// package download is needed. Each operation owns its connection/statement.
public sealed class SqliteSnapshots {
 readonly string path;
 public SqliteSnapshots(string path){this.path=path;Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);using var db=Open();using(var query=db.Prepare("PRAGMA user_version")){if(query.Row()&&query.Int(0)>1)throw new IOException("备份库来自较新版本，请先更新辅助 MOD。");}db.Exec("PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL; CREATE TABLE IF NOT EXISTS snapshots(id TEXT PRIMARY KEY,session TEXT NOT NULL,nickname TEXT NOT NULL,created TEXT NOT NULL,label TEXT NOT NULL,sha256 TEXT NOT NULL,body BLOB NOT NULL); PRAGMA user_version=1;");}
 Db Open()=>new(path);
 public SnapshotInfo Add(string session,string nickname,string label,string json){var body=Encoding.UTF8.GetBytes(json);if(body.Length>128*1024*1024)throw new ArgumentException("存档超过 128 MB，无法备份。");var row=new SnapshotInfo(Guid.NewGuid().ToString("N"),session,nickname,DateTime.UtcNow.ToString("O"),label,Convert.ToHexString(SHA256.HashData(body)),body.Length);using var db=Open();using var q=db.Prepare("INSERT INTO snapshots(id,session,nickname,created,label,sha256,body) VALUES(?,?,?,?,?,?,?)");q.Text(1,row.Id);q.Text(2,session);q.Text(3,nickname);q.Text(4,row.Created);q.Text(5,label);q.Text(6,row.Sha256);q.Blob(7,body);q.Done();return row;}
 public SnapshotInfo[] List(){using var db=Open();using var q=db.Prepare("SELECT id,session,nickname,created,label,sha256,length(body) FROM snapshots ORDER BY created DESC LIMIT 200");var rows=new List<SnapshotInfo>();while(q.Row())rows.Add(new(q.String(0),q.String(1),q.String(2),q.String(3),q.String(4),q.String(5),q.Int(6)));return rows.ToArray();}
 public string Read(string id){if(id.Length!=32||!id.All(Uri.IsHexDigit))throw new ArgumentException("请选择有效备份。");using var db=Open();using var q=db.Prepare("SELECT sha256,body FROM snapshots WHERE id=?");q.Text(1,id);if(!q.Row())throw new ArgumentException("备份不存在。");var digest=q.String(0);var body=q.Bytes(1);if(body.Length>128*1024*1024||Convert.ToHexString(SHA256.HashData(body))!=digest)throw new ArgumentException("备份校验失败，未修改存档。");return Encoding.UTF8.GetString(body);}
 public bool Delete(string id){
  if(id.Length!=32||!id.All(Uri.IsHexDigit))throw new ArgumentException("请选择有效备份。");
  using var db=Open();
  using var q=db.Prepare("DELETE FROM snapshots WHERE id=@id");
  q.Text(1,id.ToLowerInvariant());
  q.Done();
  using var c=db.Prepare("SELECT changes()");
  return c.Row()&&c.Int(0)>0;
 }
 public int TrimPrefix(string session,string prefix,int keep){
  if(keep<1)keep=1;
  string escaped=prefix.Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_");
  using var db=Open();
  var victimIds=new List<string>();
  using(var q=db.Prepare($"SELECT id FROM snapshots WHERE session=@session AND label LIKE @prefix ESCAPE '\\' ORDER BY created DESC, rowid DESC LIMIT -1 OFFSET {keep}")){
   q.Text(1,session);q.Text(2,escaped+"%");
   while(q.Row())victimIds.Add(q.String(0));
  }
  foreach(var vid in victimIds){using var dq=db.Prepare("DELETE FROM snapshots WHERE id=@id");dq.Text(1,vid);dq.Done();}
  return victimIds.Count;
 }
 internal sealed class Db:IDisposable {
  IntPtr handle;
  public Db(string path){int code=Native.sqlite3_open_v2(Utf8(path),out handle,2|4|0x10000,IntPtr.Zero);if(code!=0){Dispose();throw new IOException("SQLite 数据库无法打开（"+code+"）。");}Native.sqlite3_busy_timeout(handle,10000);}
  public void Check(int code){if(code!=0)throw new IOException("SQLite 操作失败（"+code+"），未完成备份。");}
  public void Exec(string sql){int code=Native.sqlite3_exec(handle,Utf8(sql),IntPtr.Zero,IntPtr.Zero,out var error);try{Check(code);}finally{if(error!=IntPtr.Zero)Native.sqlite3_free(error);}}
  public Query Prepare(string sql){Check(Native.sqlite3_prepare_v2(handle,Utf8(sql),-1,out var statement,IntPtr.Zero));return new(this,statement);}
  public void Dispose(){if(handle!=IntPtr.Zero){Native.sqlite3_close_v2(handle);handle=IntPtr.Zero;}}
 }
 internal sealed class Query(Db owner,IntPtr statement):IDisposable {
  public void Text(int n,string text){byte[] bytes=Utf8(text);owner.Check(Native.sqlite3_bind_text(statement,n,bytes,bytes.Length-1,new IntPtr(-1)));}
  public void Blob(int n,byte[] bytes)=>owner.Check(Native.sqlite3_bind_blob(statement,n,bytes,bytes.Length,new IntPtr(-1)));
  public bool Row(){int code=Native.sqlite3_step(statement);if(code==100)return true;if(code==101)return false;owner.Check(code);return false;}
  public void Done(){int code=Native.sqlite3_step(statement);if(code!=101)owner.Check(code);}
  public string String(int n)=>Marshal.PtrToStringUTF8(Native.sqlite3_column_text(statement,n))??"";
  public int Int(int n)=>Native.sqlite3_column_int(statement,n);
  public byte[] Bytes(int n){int size=Native.sqlite3_column_bytes(statement,n);if(size<0||size>128*1024*1024)throw new IOException("备份大小无效。");var bytes=new byte[size];if(size>0)Marshal.Copy(Native.sqlite3_column_blob(statement,n),bytes,0,size);return bytes;}
  public void Dispose()=>Native.sqlite3_finalize(statement);
 }
 static byte[] Utf8(string s)=>Encoding.UTF8.GetBytes(s+"\0");
 static class Native {
  const string Library="winsqlite3.dll";
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int sqlite3_open_v2(byte[] path,out IntPtr db,int flags,IntPtr vfs);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int sqlite3_close_v2(IntPtr db);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int sqlite3_busy_timeout(IntPtr db,int ms);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int sqlite3_exec(IntPtr db,byte[] sql,IntPtr callback,IntPtr arg,out IntPtr error);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern void sqlite3_free(IntPtr p);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int sqlite3_prepare_v2(IntPtr db,byte[] sql,int length,out IntPtr query,IntPtr tail);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int sqlite3_bind_text(IntPtr q,int index,byte[] data,int bytes,IntPtr destructor);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int sqlite3_bind_blob(IntPtr q,int index,byte[] data,int bytes,IntPtr destructor);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int sqlite3_step(IntPtr q);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int sqlite3_finalize(IntPtr q);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern IntPtr sqlite3_column_text(IntPtr q,int n);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern IntPtr sqlite3_column_blob(IntPtr q,int n);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int sqlite3_column_bytes(IntPtr q,int n);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]internal static extern int sqlite3_column_int(IntPtr q,int n);
 }
}
