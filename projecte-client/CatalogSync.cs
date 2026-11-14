using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.HandBook;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Local.ModHost;
using UnityEngine;
using NativeTask=Il2CppSystem.Threading.Tasks.Task;
namespace ProjectE.Client;
internal sealed class CatalogSync {
 readonly ModuleContext context;
 Task<string>? probe;
 Il2CppSystem.Threading.Tasks.Task<JsonType.ItemTemplates>? templates;
 Il2CppSystem.Threading.Tasks.Task<Il2CppReferenceArray<GlobalConfiguration.TraderSettings>>? traders;
 Il2CppSystem.Threading.Tasks.Task<HandbookInfo<HandbookData>>? handbook;
 NativeTask? locale;TarkovApplication? app;EftClientBackendSession? session;
 string applied="",candidate="";float nextCheck;IntPtr factoryPointer;int added,updated,verified;
 internal bool Busy=>probe!=null||templates!=null||traders!=null||handbook!=null||locale!=null;
 internal CatalogSync(ModuleContext context){this.context=context;}
 internal void Refresh(){applied="";nextCheck=0;}
 internal void Tick(bool enabled,bool blocked){try{
  if(!context.IsActive)return;
  if(probe?.IsCompleted==true){var task=probe;probe=null;using var doc=JsonDocument.Parse(task.GetAwaiter().GetResult());var data=doc.RootElement;if(data.TryGetProperty("data",out var inner))data=inner;
   if(data.TryGetProperty("ok",out var ok)&&ok.GetBoolean()&&data.TryGetProperty("sha256",out var hash))candidate=hash.GetString()??"";
  }
  var factory=Singleton<ItemFactory>.Instance;if(factory==null||blocked||InventoryAccess.Current()?.Controller.HasActiveEvents==true)return;
  if(factoryPointer!=factory.Pointer){factoryPointer=factory.Pointer;applied="";}
  app??=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>();
  session=app?.GetClientBackEndSession()?.TryCast<EftClientBackendSession>();if(session==null)return;
  if(!Busy&&candidate.Length>0&&candidate!=applied){
   templates=session.GetTemplates(null);traders=session.GetTraderSettings<Il2CppReferenceArray<GlobalConfiguration.TraderSettings>>();handbook=session.RequestHandbookInfo(null);locale=session.LoadLocale("ch");
  }
  if(templates?.IsCompleted==true&&traders?.IsCompleted==true&&handbook?.IsCompleted==true&&locale?.IsCompleted==true){
   if(templates.IsFaulted||traders.IsFaulted||handbook.IsFaulted||locale.IsFaulted)throw new IOException("客户端目录刷新未完成，请稍后重试。");
   var incoming=templates.Result??throw new IOException("服务端未返回物品模板。");incoming.Init();added=updated=verified=0;
   foreach(var pair in incoming){string id=pair.Key.ToString();if(!id.StartsWith("a7",StringComparison.Ordinal))continue;
    if(factory.ItemTemplates.ContainsKey(pair.Key))updated++;else added++;
    factory.ItemTemplates[pair.Key]=pair.Value;
   }
   foreach(var pair in incoming._typesDictionary)if(!factory.ItemTemplates._typesDictionary.ContainsKey(pair.Key))factory.ItemTemplates._typesDictionary.Add(pair.Key,pair.Value);
   factory._cachedItemNames.Clear();factory._discardLimitsCache.Clear();
   foreach(string id in Swords.All.Select(x=>x.Id).Concat(Gadgets.All.Select(x=>x.Id))){if(!factory.ItemTemplates.ContainsKey(new MongoID(id)))continue;var item=factory.CreateItem(factory.NextId.ToString(),id,null);if(item?.StringTemplateId!=id)throw new IOException("新物品模板无法实例化："+id);verified++;}
   var settings=Singleton<GlobalConfiguration>.Instance;var profile=app!.GetProfileForLocalGame();
   foreach(var setting in traders.Result){if(setting==null)continue;MongoID id=new(setting.Id);settings.TradersSettings[id]=setting;
    if(!profile.TradersInfo.ContainsKey(id))profile.TradersInfo.Add(id,new Profile.TraderInfo(id,new TraderInfoDescriptor{Unlocked=setting.UnlockedByDefault,LoyaltyLevel=1,Standing=0,SalesSum=0},profile));
    else profile.TradersInfo[id]._Settings_k__BackingField=setting;
   }
   session.UpdateTraders();
   if(app._handbookLoader!=null){app._handbookLoader._handbookInfo=handbook.Result;app._handbookLoader.Apply(app.Session,profile);}
   applied=candidate;Status("");context.Log(LogLevel.Info,$"Catalog synchronized without relogin: {added} added, {updated} updated, {verified} template instances verified.");ClearTasks();nextCheck=Time.unscaledTime+10;
  }
  if(enabled&&!Busy&&Time.unscaledTime>=nextCheck&&!string.IsNullOrWhiteSpace(SPTushonka.Common.Http.RequestHandler.SessionId)){nextCheck=Time.unscaledTime+10;probe=SPTushonka.Common.Http.RequestHandler.PostJsonAsync("/local/projecte/runtime","{}").WaitAsync(TimeSpan.FromSeconds(15));}
 }catch(Exception e){Status(e.GetBaseException().Message);context.Log(LogLevel.Warning,"Catalog sync: "+e.GetBaseException().Message);candidate="";ClearTasks();nextCheck=Time.unscaledTime+15;}}
 void ClearTasks(){probe=null;templates=null;traders=null;handbook=null;locale=null;}
 void Status(string error){File.WriteAllText(Path.Combine(context.DataDirectory,"catalog-status.json"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,sha256=applied,added,updated,verified,error,reloginRequired=false}));}
}
