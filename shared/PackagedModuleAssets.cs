using System.Reflection;
using System.Security.Cryptography;
using Local.ModHost;
namespace Local.HotModules;
internal static class PackagedModuleAssets {
 internal static ModuleContext Open(ModuleContext owner,Assembly assembly){
  string home=Path.Combine(owner.DataDirectory,"assets",assembly.ManifestModule.ModuleVersionId.ToString("N"));
  for(var parent=new DirectoryInfo(home);parent!=null;parent=parent.Parent)if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Module asset path is a link");
  Directory.CreateDirectory(home);
  foreach(string resource in assembly.GetManifestResourceNames().Where(n=>n.StartsWith("HotModuleAssets/",StringComparison.Ordinal))){
   string relative=resource[16..].Replace('\\','/');
   if(relative.Split('/').Any(p=>p is "" or "." or "..")||relative.Contains(':'))throw new IOException("Invalid module asset path");
   string path=Path.GetFullPath(Path.Combine(home,"Assets",relative));
   string current=home;
   foreach(string part in ("Assets/"+relative).Split('/')){current=Path.Combine(current,part);if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Module asset path is a link");}
   using var stream=assembly.GetManifestResourceStream(resource)!;using var buffer=new MemoryStream();stream.CopyTo(buffer);byte[] bytes=buffer.ToArray();
   if(File.Exists(path)&&SHA256.HashData(File.ReadAllBytes(path)).AsSpan().SequenceEqual(SHA256.HashData(bytes)))continue;
   Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path+".new",bytes);File.Move(path+".new",path,true);
  }
  var context=new ModuleContext(owner.Id,home,owner.DataDirectory,owner.PatchId,owner.Log);
  owner.OnShutdown(context.Release);return context;
 }
}
