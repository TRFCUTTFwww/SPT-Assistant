using System.Text.Json;
using System.Reflection;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services.Modding.Custom;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace ProjectE.Server;
public sealed class HotEntry {
 readonly Startup startup;readonly DeveloperTrader developer;readonly TableService table;readonly TerminalStorage terminal;
 public HotEntry(CustomItemService custom,TemplateTable templates,TradersTable traders,SaveServer saves,ItemHelper helper,JsonUtil json,HttpResponseUtil http,LocaleTable locales,TraderConfig config,ImageRouter images,Action<string> log,Assembly owner){startup=new(custom,templates,traders,log,json,owner);developer=new(templates,traders,locales,config,images,json);table=new(templates,saves,helper,json,http);terminal=new(saves,json,http);}
 public Task Initialize(CancellationToken ct)=>startup.OnLoadAsync(ct);
 public void Rebuild()=>developer.Rebuild();
 public ValueTask<string> Handle(string route,string payload,MongoId session,CancellationToken ct)=>route=="table"?table.Handle(JsonSerializer.Deserialize<TableRequest>(payload,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!,session,ct):terminal.Handle(JsonSerializer.Deserialize<StorageRequest>(payload,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!,session,ct);
}
