using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;
public static class ServerSettingsService {
 static readonly object SettingsGate=new();
 static readonly JsonSerializerOptions Pretty=new(){WriteIndented=true};
 static readonly JsonDocumentOptions ParseOptions=new(){AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip};
 public static string Handle(CharacterDataRequest request,HttpResponseUtil http,SPTarkov.Server.Core.Models.Eft.Common.PmcData pmc){
  lock(SettingsGate){
  string root=Path.Combine(AppContext.BaseDirectory,"SPT_Data","configs"),directory=Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","server-settings");
  var store=new ServerSettingsStore(root,directory);if(request.Action=="serverSave")store.Save(request.Settings,request.Revision);
  var snapshot=store.Read();return http.GetBody(new{ok=true,revision=snapshot.Revision,restartRequired=store.RestartPending(snapshot.Revision),settings=snapshot.Rows.Select(r=>new{id=r.Id,group=r.Group,name=r.Name,help=r.Help,value=r.Value,minimum=r.Minimum,maximum=r.Maximum,integer=r.Integer,boolean=r.Boolean,minusOne=r.MinusOne}),pmcKarma=KarmaRules.Pmc(pmc),scavKarma=KarmaRules.Scav(pmc),minimumScavKarma=store.ScavBounds().Min,maximumScavKarma=store.ScavBounds().Max});
  }
 }
 public static (double Min,double Max) ScavBounds()=>new ServerSettingsStore(Path.Combine(AppContext.BaseDirectory,"SPT_Data","configs"),"").ScavBounds();
 public sealed record Row(string Id,string Group,string Name,string Help,JsonElement Value,double Minimum,double Maximum,bool Integer,bool Boolean,bool MinusOne);
 public sealed record Snapshot(string Revision,Row[] Rows);
 public sealed class ServerSettingsStore(string root,string directory){
  string FilePath(string file)=>Path.Combine(root,file+".json");
  Dictionary<string,byte[]> Files()=>ServerSettingsRules.Definitions.Select(d=>d.File).Distinct().Where(f=>File.Exists(FilePath(f))).ToDictionary(f=>f,f=>File.ReadAllBytes(FilePath(f)),StringComparer.Ordinal);
  static JsonObject Parse(byte[] bytes)=>JsonNode.Parse(bytes,documentOptions:ParseOptions) as JsonObject??throw new ArgumentException("配置文件不是 JSON 对象。");
  public Snapshot Read(){var files=Files();var trees=files.ToDictionary(p=>p.Key,p=>Parse(p.Value));var rows=ServerSettingsRules.Definitions.Where(d=>trees.ContainsKey(d.File)&&ServerSettingsRules.Read(trees[d.File],d.Path)!=null).Select(d=>new Row(d.Id,d.Group,d.Name,d.Help,JsonSerializer.SerializeToElement(ServerSettingsRules.Read(trees[d.File],d.Path)),d.Minimum,d.Maximum,d.Integer,d.Boolean,d.MinusOne)).ToArray();return new(ServerSettingsRules.Revision(files),rows);}
  public (double Min,double Max) ScavBounds(){try{var trader=Parse(File.ReadAllBytes(FilePath("trader")));double min=trader["fence"]!["playerRepMin"]!.GetValue<double>(),max=trader["fence"]!["playerRepMax"]!.GetValue<double>();if(double.IsFinite(min)&&double.IsFinite(max)&&min<=max)return(min,max);}catch{}return(-7,15);}
  static string Boot=>System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().ToString("O");
  public bool RestartPending(string revision){try{using var marker=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"pending.json")));return marker.RootElement.GetProperty("boot").GetString()==Boot&&marker.RootElement.GetProperty("revision").GetString()==revision;}catch{return false;}}
  public void Save(Dictionary<string,JsonElement> changes,string revision){
   if(changes.Count==0||changes.Count>ServerSettingsRules.Definitions.Length)throw new ArgumentException("请先修改至少一个配置项。");
   var files=Files();if(revision!=ServerSettingsRules.Revision(files))throw new ArgumentException("服务端配置已被修改，请刷新后重试。");
   var trees=files.ToDictionary(p=>p.Key,p=>Parse(p.Value));var changed=new HashSet<string>();
   foreach(var entry in changes){var definition=ServerSettingsRules.Definitions.SingleOrDefault(d=>d.Id==entry.Key)??throw new ArgumentException("不支持修改此配置项。");if(!trees.TryGetValue(definition.File,out var tree))throw new ArgumentException("当前服务端没有此配置文件。");ServerSettingsRules.Apply(tree,definition,entry.Value);changed.Add(definition.File);}
   var next=changed.ToDictionary(f=>f,f=>Encoding.UTF8.GetBytes(trees[f].ToJsonString(Pretty)+"\n"));
   string backup=Path.Combine(directory,"backups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")[..8]);Directory.CreateDirectory(backup);
   foreach(string file in changed)File.WriteAllBytes(Path.Combine(backup,file+".json"),files[file]);
   var written=new List<string>();try{
    if(ServerSettingsRules.Revision(Files())!=revision)throw new ArgumentException("服务端配置已被修改，请刷新后重试。");
    foreach(string file in changed){if(!File.ReadAllBytes(FilePath(file)).SequenceEqual(files[file]))throw new ArgumentException("服务端配置已被修改，请刷新后重试。");string temp=FilePath(file)+".assistant-new";try{File.WriteAllBytes(temp,next[file]);File.Move(temp,FilePath(file),true);written.Add(file);}finally{if(File.Exists(temp))File.Delete(temp);}}
    Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"pending.json"),JsonSerializer.Serialize(new{boot=Boot,revision=ServerSettingsRules.Revision(Files())}));
   }catch{foreach(string file in written)if(File.ReadAllBytes(FilePath(file)).SequenceEqual(next[file]))File.WriteAllBytes(FilePath(file),files[file]);throw;}
  }
 }
}
