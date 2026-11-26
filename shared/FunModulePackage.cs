using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace Local.HotModules;
internal sealed record FunModuleSpec(string Id,string Name,string Assembly,string Entry,string Api);
internal static class FunModulePackage {
 internal static readonly FunModuleSpec[] Modules=[];
 internal static string Home(string root,string id){
  if(!Regex.IsMatch(id,"^[a-z0-9][a-z0-9._-]{0,63}$")||id is "." or "..")throw new ArgumentException("Invalid module id");
  string modules=Path.GetFullPath(Path.Combine(root,"modules")),home=Path.GetFullPath(Path.Combine(modules,id));
  if(!home.StartsWith(modules+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Module path escapes installation directory");
  CheckPath(root);CheckPath(home);return home;
 }
 static void CheckPath(string path){for(var p=new DirectoryInfo(Path.GetFullPath(path));p!=null;p=p.Parent)if(p.Exists&&(p.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Module path is a link");if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Module file is a link");}
 internal static JsonObject? Manifest(string root,string id){string file=Path.Combine(Home(root,id),"module.json");CheckPath(file);return File.Exists(file)?JsonNode.Parse(File.ReadAllText(file))?.AsObject():null;}
 internal static bool HasSettings(string root,string id,JsonObject manifest){
  if(manifest["assistantSettings"] is {} marked)return marked.GetValue<bool>();
  string revision=manifest["revision"]?.GetValue<string>()??"",entry=manifest["entryAssembly"]?.GetValue<string>()??"";
  if(!Regex.IsMatch(revision,"^[a-zA-Z0-9._-]{1,128}$")||revision is "." or ".."||Path.GetFileName(entry)!=entry||!entry.EndsWith(".module",StringComparison.OrdinalIgnoreCase))return false;
  string file=Path.Combine(Home(root,id),"releases",revision,entry);CheckPath(file);if(!File.Exists(file))return false;
  using var stream=File.OpenRead(file);using var pe=new PEReader(stream);var metadata=pe.GetMetadataReader();
  foreach(var handle in metadata.GetAssemblyDefinition().GetCustomAttributes()){
   var attribute=metadata.GetCustomAttribute(handle);if(attribute.Constructor.Kind!=HandleKind.MemberReference)continue;
   var constructor=metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);if(constructor.Parent.Kind!=HandleKind.TypeReference)continue;
   var type=metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);if(metadata.GetString(type.Namespace)!="System.Reflection"||metadata.GetString(type.Name)!="AssemblyMetadataAttribute")continue;
   var value=metadata.GetBlobReader(attribute.Value);if(value.ReadUInt16()==1&&value.ReadSerializedString()=="HotModuleId"&&value.ReadSerializedString()==id)return true;
  }
  return false;
 }
 internal static void SetEnabled(string root,string id,bool enabled){var manifest=Manifest(root,id)??throw new FileNotFoundException("Module is not installed");manifest["enabled"]=enabled;Publish(Home(root,id),manifest);}
 internal static bool Removing(string root,string id){string file=Path.Combine(Home(root,id),"uninstall.pending.json");CheckPath(file);return File.Exists(file);}
 internal static JsonObject? RemovalManifest(string root,string id){string file=Path.Combine(Home(root,id),"uninstall.pending.json");CheckPath(file);return File.Exists(file)?JsonNode.Parse(File.ReadAllText(file))?.AsObject():null;}
 internal static void BeginRemoval(string root,string id){
  string home=Home(root,id),file=Path.Combine(home,"module.json"),pending=Path.Combine(home,"uninstall.pending.json");CheckPath(file);CheckPath(pending);
  if(File.Exists(pending))return;
  // A missing manifest makes the existing host wait for CanReload and run Shutdown.
  // Keep all payloads available until that host confirms cleanup has finished.
  File.Move(file,pending);File.SetLastWriteTimeUtc(pending,DateTime.UtcNow);
 }
 internal static bool CompleteRemoval(string root,string id){
  string home=Home(root,id),pending=Path.Combine(home,"uninstall.pending.json");CheckPath(pending);
  if(!File.Exists(pending))return false;
  string statusFile=Path.Combine(root,"host-status.json");CheckPath(statusFile);if(!File.Exists(statusFile))return false;
  using var status=JsonDocument.Parse(File.ReadAllBytes(statusFile));var host=status.RootElement;
  if(host.GetProperty("processId").GetInt32()!=Environment.ProcessId||host.GetProperty("updatedUtc").GetDateTime()<File.GetLastWriteTimeUtc(pending)||DateTime.UtcNow-host.GetProperty("updatedUtc").GetDateTime()>TimeSpan.FromSeconds(5))return false;
  if(host.TryGetProperty("discoveryErrors",out var errors)&&errors.TryGetProperty(id,out _))return false;
  foreach(var module in host.GetProperty("modules").EnumerateArray())if(module.GetProperty("id").GetString()==id){
   if(module.GetProperty("ready").GetBoolean()||module.GetProperty("phase").GetString()!="removed"||module.GetProperty("restartRequired").GetBoolean())return false;
  }
  if(File.Exists(Path.Combine(home,"module.json")))throw new IOException("Module was republished during removal");
  CheckTree(home);
  // Home validates the ID and absolute ancestry; reject links anywhere in the
  // removal tree before deleting only this module's installation, never state/data.
  Directory.Delete(home,true);return true;
 }
 static void CheckTree(string directory){CheckPath(directory);foreach(string path in Directory.GetFileSystemEntries(directory)){if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Module removal tree contains a link");CheckPath(path);if(Directory.Exists(path))CheckTree(path);}}
 static void Publish(string home,JsonObject manifest){
  Directory.CreateDirectory(home);string path=Path.Combine(home,"module.json");CheckPath(path);CheckPath(path+".new");
  CheckPath(Path.Combine(home,"previous-manifest.json"));File.WriteAllText(path+".new",manifest.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
  if(File.Exists(path))File.Replace(path+".new",path,Path.Combine(home,"previous-manifest.json"));else File.Move(path+".new",path);
 }
 internal static string Install(string source,string root){
  var info=new FileInfo(source);if(!info.Exists||info.Length>256L*1024*1024||!source.EndsWith(".module",StringComparison.OrdinalIgnoreCase))throw new IOException("Invalid .module file");
  byte[] bytes=File.ReadAllBytes(source);using var pe=new PEReader(new MemoryStream(bytes));var metadata=pe.GetMetadataReader();var definition=metadata.GetAssemblyDefinition();
  string name=metadata.GetString(definition.Name);var fields=new Dictionary<string,string>();
  foreach(var handle in definition.GetCustomAttributes()){
   var attribute=metadata.GetCustomAttribute(handle);if(attribute.Constructor.Kind!=HandleKind.MemberReference)continue;
   var constructor=metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);if(constructor.Parent.Kind!=HandleKind.TypeReference)continue;
   var type=metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);if(metadata.GetString(type.Namespace)!="System.Reflection"||metadata.GetString(type.Name)!="AssemblyMetadataAttribute")continue;
   var blob=metadata.GetBlobReader(attribute.Value);if(blob.ReadUInt16()!=1)continue;string? key=blob.ReadSerializedString(),value=blob.ReadSerializedString();if(key!=null&&value!=null)fields[key]=value;
  }
  var spec=fields.TryGetValue("HotModuleId",out var declared)?new FunModuleSpec(declared,fields.GetValueOrDefault("HotModuleName",declared),name,fields.GetValueOrDefault("HotModuleEntry","") ,""):Modules.SingleOrDefault(m=>m.Assembly==name)??throw new IOException("Module metadata is missing");
  if(!metadata.TypeDefinitions.Select(h=>metadata.GetTypeDefinition(h)).Any(t=>string.Join(".",new[]{metadata.GetString(t.Namespace),metadata.GetString(t.Name)}.Where(s=>s.Length>0))==spec.Entry))throw new IOException("Module entry is missing");
  string version=new Version(definition.Version.Major,definition.Version.Minor,definition.Version.Build).ToString();
  string home=Home(root,spec.Id),revision="r"+Guid.NewGuid().ToString("N"),entry=name+".module";
  if(Removing(root,spec.Id))throw new IOException("Module uninstall is still in progress");
  string release=Path.Combine(home,"releases",revision);CheckPath(release);Directory.CreateDirectory(release);File.WriteAllBytes(Path.Combine(release,entry),bytes);
  bool enabled=Manifest(root,spec.Id)?["enabled"]?.GetValue<bool>()??true;
  var manifest=new JsonObject{["id"]=spec.Id,["name"]=spec.Name,["assistantSettings"]=true,["apiVersion"]=1,["version"]=version,["revision"]=revision,["entryAssembly"]=entry,["entryType"]=spec.Entry,["enabled"]=enabled,["files"]=new JsonObject{[entry]=Convert.ToHexString(SHA256.HashData(bytes))}};
  Publish(home,manifest);return spec.Id;
 }
}
