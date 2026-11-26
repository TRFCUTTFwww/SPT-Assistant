using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Threading;

namespace LocalSpt5Esp;

internal static class HotUpdateFile {
 internal static bool Publish(string sourcePath,string pluginDirectory,CancellationToken cancellation){
  string runtime=Path.Combine(pluginDirectory,"runtime");
  string target=Path.Combine(runtime,"Local.Spt5.ESP.Core.module");
  using var source=File.OpenRead(sourcePath);
  Validate(source);
  source.Position=0;
  if(MatchesTarget(source,target))return false;

  source.Position=0;
  Directory.CreateDirectory(runtime);
  string temporary=Path.Combine(runtime,Guid.NewGuid().ToString("N")+".tmp");
  try{
   using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))source.CopyTo(output);
   cancellation.ThrowIfCancellationRequested();
   File.Move(temporary,target,true);
   return true;
  }finally{File.Delete(temporary);}
 }

 static bool MatchesTarget(Stream source,string target){
  if(!File.Exists(target))return false;
  using var current=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
  return source.Length==current.Length&&SHA256.HashData(source).AsSpan().SequenceEqual(SHA256.HashData(current));
 }

 static void Validate(Stream source){
  const string error="请选择有效的 SPT Assistant 核心热更新文件。";
  if(source.Length<1024||source.Length>64*1024*1024)throw new InvalidDataException(error);
  try{
   using var pe=new PEReader(source,PEStreamOptions.LeaveOpen);
   if(!pe.HasMetadata)throw new InvalidDataException(error);
   var metadata=pe.GetMetadataReader();
   if(!metadata.IsAssembly||!metadata.StringComparer.Equals(metadata.GetAssemblyDefinition().Name,"Local.Spt5.ESP.Core"))throw new InvalidDataException(error);
   foreach(var handle in metadata.TypeDefinitions){
    var type=metadata.GetTypeDefinition(handle);
    if(!metadata.StringComparer.Equals(type.Namespace,"LocalSpt5Esp")||!metadata.StringComparer.Equals(type.Name,"RuntimeModule"))continue;
    foreach(var implementation in type.GetInterfaceImplementations()){
     var contract=metadata.GetInterfaceImplementation(implementation).Interface;
     if(contract.Kind!=HandleKind.TypeReference)continue;
     var reference=metadata.GetTypeReference((TypeReferenceHandle)contract);
     if(metadata.StringComparer.Equals(reference.Namespace,"LocalSpt5Esp.HotReload")&&metadata.StringComparer.Equals(reference.Name,"IRuntimeModule")&&
        reference.ResolutionScope.Kind==HandleKind.AssemblyReference&&
        metadata.StringComparer.Equals(metadata.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope).Name,"Local.Spt5.ESP"))return;
    }
   }
   throw new InvalidDataException(error);
  }catch(BadImageFormatException exception){throw new InvalidDataException(error,exception);}
 }
}
