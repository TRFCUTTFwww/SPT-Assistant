using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.HandBook;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Local.ModHost;
using UnityEngine;
namespace Scp3X.Client;
// Independent catalog refresh keeps the new firearm usable even if a different
// gameplay module is disabled. It updates only the three 3-X item definitions.
internal sealed class CatalogSync(ModuleContext context) {
 Task<string>? probe;
 Il2CppSystem.Threading.Tasks.Task<JsonType.ItemTemplates>? templates;
 Il2CppSystem.Threading.Tasks.Task<Il2CppReferenceArray<GlobalConfiguration.TraderSettings>>? traders;
 Il2CppSystem.Threading.Tasks.Task<HandbookInfo<HandbookData>>? handbook;
 Il2CppSystem.Threading.Tasks.Task? locale;
 string applied="",candidate="";float next,started;IntPtr factoryPointer;
 internal bool Busy=>probe!=null||templates!=null||traders!=null||handbook!=null||locale!=null;
 void Clear(){probe=null;templates=null;traders=null;handbook=null;locale=null;}
 internal void Tick(bool blocked){try{
  if(Busy&&Time.unscaledTime-started>30)throw new IOException("3-X catalog request timeout");
  if(probe?.IsCompleted==true){var response=probe.GetAwaiter().GetResult();probe=null;using var doc=JsonDocument.Parse(response);var data=doc.RootElement;if(data.TryGetProperty("data",out var inner))data=inner;if(data.TryGetProperty("ok",out var ok)&&ok.GetBoolean()&&data.TryGetProperty("sha256",out var hash))candidate=hash.GetString()??"";}
  var factory=Singleton<ItemFactory>.Instance;if(factory==null||blocked||Singleton<GameWorld>.Instance?.MainPlayer?.InventoryController?.HasActiveEvents==true)return;
  if(factory.Pointer!=factoryPointer){factoryPointer=factory.Pointer;applied="";}
  var app=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>();var session=app?.GetClientBackEndSession()?.TryCast<EftClientBackendSession>();if(session==null)return;
  if(!Busy&&candidate.Length>0&&candidate!=applied){started=Time.unscaledTime;templates=session.GetTemplates(null);traders=session.GetTraderSettings<Il2CppReferenceArray<GlobalConfiguration.TraderSettings>>();handbook=session.RequestHandbookInfo(null);locale=session.LoadLocale("ch");}
  if(templates?.IsCompleted==true&&traders?.IsCompleted==true&&handbook?.IsCompleted==true&&locale?.IsCompleted==true){
   if(templates.IsFaulted||traders.IsFaulted||handbook.IsFaulted||locale.IsFaulted)throw new IOException("3-X native catalog refresh failed");
   var incoming=templates.Result??throw new IOException("3-X item template response empty");incoming.Init();
   foreach(string id in DisruptorRules.Templates){var key=new MongoID(id);if(!incoming.ContainsKey(key))throw new IOException("3-X server template absent: "+id);factory.ItemTemplates[key]=incoming[key];}
   foreach(var pair in incoming._typesDictionary)if(!factory.ItemTemplates._typesDictionary.ContainsKey(pair.Key))factory.ItemTemplates._typesDictionary.Add(pair.Key,pair.Value);
   factory._cachedItemNames.Clear();factory._discardLimitsCache.Clear();
   foreach(string id in DisruptorRules.Templates){var item=factory.CreateItem(factory.NextId.ToString(),id,null);if(item?.StringTemplateId!=id)throw new IOException("3-X native item construction failed: "+id);}
   var settings=Singleton<GlobalConfiguration>.Instance;var profile=app!.GetProfileForLocalGame();foreach(var trader in traders.Result){if(trader?.Id!="a7d000000000000000000001")continue;MongoID id=new(trader.Id);settings.TradersSettings[id]=trader;if(!profile.TradersInfo.ContainsKey(id))profile.TradersInfo.Add(id,new Profile.TraderInfo(id,new TraderInfoDescriptor{Unlocked=trader.UnlockedByDefault,LoyaltyLevel=1,Standing=0,SalesSum=0},profile));else profile.TradersInfo[id]._Settings_k__BackingField=trader;}
   session.UpdateTraders();if(app._handbookLoader!=null){app._handbookLoader._handbookInfo=handbook.Result;app._handbookLoader.Apply(app.Session,profile);}
   applied=candidate;Clear();next=Time.unscaledTime+15;Status("");context.Log(LogLevel.Info,"3-X catalog synchronized; three native item instances constructed and Developer refreshed.");
  }
  if(!Busy&&Time.unscaledTime>=next&&!string.IsNullOrWhiteSpace(SPTushonka.Common.Http.RequestHandler.SessionId)){next=Time.unscaledTime+15;started=Time.unscaledTime;probe=SPTushonka.Common.Http.RequestHandler.PostJsonAsync("/local/projecte/runtime","{}").WaitAsync(TimeSpan.FromSeconds(15),context.Stopping);}
 }catch(Exception e){Clear();candidate="";next=Time.unscaledTime+15;Status(e.GetBaseException().Message);context.Log(LogLevel.Warning,"3-X catalog: "+e.GetBaseException().Message);}}
 void Status(string error)=>File.WriteAllText(Path.Combine(context.DataDirectory,"catalog-status.json"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,pid=Environment.ProcessId,sha256=applied,verifiedTemplates=error.Length==0?3:0,error}));
}
