using Comfort.Common;
using EFT;
using UnityEngine;
using System.Text.Json;
namespace AllTheModium.Client;
public sealed partial class SwordModule {
 readonly Dictionary<string,object> modelApplications=[];
 bool diagnosticsDirty;
 float nextDiagnostics;
 void RecordApplication(string id,GameObject root,int count){if(count==0)return;modelApplications[id]=new{root=root.name,lods=count,utc=DateTime.UtcNow,mesh=assets[id].Mesh.name,shader=assets[id].Material.shader.name};diagnosticsDirty=true;}
 bool diagnosed;
 float recordedBrightness=-1,recordedOpacity=-1;
 void Diagnose(){
  try{
  if(recordedBrightness!=brightnessPercent.Value||recordedOpacity!=opacityPercent.Value){
   recordedBrightness=brightnessPercent.Value;recordedOpacity=opacityPercent.Value;
   var models=assets.Select(p=>new{template=p.Key,shader=p.Value.Material.shader.name,texture=p.Value.Texture.name,width=p.Value.Texture.width,height=p.Value.Texture.height,blackPixels=p.Value.Texture.GetPixels32().Count(c=>c.a>0&&c.r==0&&c.g==0&&c.b==0)}).ToArray();
   File.WriteAllText(Path.Combine(context!.DataDirectory,"appearance-status.json"),JsonSerializer.Serialize(new{version=Version,utc=DateTime.UtcNow,brightness=recordedBrightness,opacity=recordedOpacity,models},new JsonSerializerOptions{WriteIndented=true}));
  }
  if(diagnosticsDirty&&Time.unscaledTime>=nextDiagnostics){nextDiagnostics=Time.unscaledTime+1;diagnosticsDirty=false;File.WriteAllText(Path.Combine(context!.DataDirectory,"model-applications.json"),JsonSerializer.Serialize(new{version=Version,utc=DateTime.UtcNow,modelApplications},new JsonSerializerOptions{WriteIndented=true}));}
  if(diagnosed||Singleton<ItemFactory>.Instance==null||Singleton<ObjectsFactory>.Instance==null)return;
  diagnosed=true;
   var factory=Singleton<ItemFactory>.Instance;var objects=Singleton<ObjectsFactory>.Instance;
   var items=new List<object>();
   foreach(string id in Swords.All.Select(s=>s.Id).Prepend(Swords.BaseTemplate)){
    try{
     var item=factory.CreateItem(factory.NextId.ToString(),id,null);var template=item.Template;
     var resources=new List<object>();foreach(var key in template.AllResources)resources.Add(new{key.path,key.rcid,type=ObjectsFactory.CurrentItemTemplates.GetResourceTypeByBundlePath(key.path)?.ResourceType.ToString()});
     items.Add(new{id,type=item.GetIl2CppType().FullName,prefab=template.Prefab?.path,prefabRcid=template.Prefab?.rcid,use=template.UsePrefab?.path,useRcid=template.UsePrefab?.rcid,resources});
    }catch(Exception e){items.Add(new{id,error=e.ToString()});}
   }
   File.WriteAllText(Path.Combine(context!.DataDirectory,"model-diagnostics.json"),JsonSerializer.Serialize(new{version=Version,utc=DateTime.UtcNow,ignorePools=objects.IgnorePools,pools=objects._pools.Count,items},new JsonSerializerOptions{WriteIndented=true}));
  }catch(Exception e){context!.Log(Local.ModHost.LogLevel.Warning,"Sword diagnostics: "+e);}
 }
}
