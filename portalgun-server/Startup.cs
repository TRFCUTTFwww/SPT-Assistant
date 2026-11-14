using System.Text.Json.Nodes;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;
using SPTarkov.Server.Core.Utils;

namespace LocalPortalGun;

public record Metadata:IModMetadata {
    public string ModGuid{get;init;}="local.portalgun.items";
    public string Name{get;init;}="Aperture Portal Gun";
    public string Author{get;init;}="Local";
    public List<string>? Contributors{get;init;}
    public SemanticVersioning.Version Version{get;init;}=new("0.1.0");
    public SemanticVersioning.Range SptVersion{get;init;}=new("~5.0.0");
    public bool HasPrepatcher{get;init;}
    public List<string>? Incompatibilities{get;init;}
    public Dictionary<string,SemanticVersioning.Range>? ModDependencies{get;init;}
    public string? Url{get;init;}
    public string License{get;init;}="MIT";
}

[Injectable(TypePriority=OnLoadOrder.GameCallbacks+1)]
public class Startup(CustomItemService custom,TemplateTable templates,TradersTable traders,JsonUtil json,ISptLogger<Startup> log):IOnLoad {
    public Task OnLoadAsync(CancellationToken cancellationToken) {
        var source=JsonNode.Parse(json.Serialize(templates.Items[new MongoId(PortalIdentity.BaseWeapon)])!)!.AsObject();
        var item=json.Deserialize<TemplateItem>(PortalItemRules.Create(source).ToJsonString())!;
        var result=custom.CreateItem(new NewItemDetails {
            NewItem=item,AddToHandbook=true,AddToFleaPriceDb=true,AddToWeaponShelf=false,
            HandbookPriceRoubles=1000,FleaPriceRoubles=1000,HandbookParentId="5b5f792486f77447ed5636b3",
            Locales=new() {
                ["en"]=new(){Name="Aperture Science Handheld Portal Device",ShortName="Portal Gun",Description="Equip in the pistol slot. Left mouse: blue portal. Right mouse: orange portal. Backspace: clear portals. Walk into a paired portal to travel. Requires the Portal Gun client module. No ammunition required."},
                ["ch"]=new(){Name="光圈科技手持传送装置",ShortName="传送枪",Description="独立传送枪，装备到手枪栏后切出使用。左键蓝门，右键橙门，Backspace 清除双门。靠近并走进配对的门即可传送。不需要弹药。需要客户端 Portal Gun 模块。"}
            }
        },typeof(Startup).Assembly);
        if(!result.Success)throw new InvalidOperationException("Portal item registration failed: "+string.Join("; ",result.Errors??[]));
        var assort=traders.GetTrader(new MongoId(PortalIdentity.Mechanic))?.Assort??throw new InvalidOperationException("Mechanic assort unavailable");
        var offer=new MongoId(PortalIdentity.Offer);
        if(!assort.Items.Any(i=>i.Id==offer))assort.Items.Add(new Item {
            Id=offer,Template=new MongoId(PortalIdentity.Template),ParentId="hideout",SlotId="hideout",
            Upd=new(){UnlimitedCount=true,StackObjectsCount=999999,Repairable=new(){Durability=100,MaxDurability=100}}
        });
        assort.BarterScheme[offer]=[[new(){Template=new MongoId(PortalIdentity.Roubles),Count=1000}]];
        assort.LoyalLevelItems[offer]=1;
        string home=System.IO.Path.Combine(AppContext.BaseDirectory,"user/mods/LocalPortalGun");Directory.CreateDirectory(home);
        File.WriteAllText(System.IO.Path.Combine(home,"server-status.json"),System.Text.Json.JsonSerializer.Serialize(new {ready=true,version="0.1.0",serverPid=Environment.ProcessId,template=PortalIdentity.Template,trader=PortalIdentity.Mechanic,price=1000,utc=DateTime.UtcNow}));
        log.Success("Portal Gun 0.1.0: standalone pistol item / Mechanic LL1 / 1000 RUB / client module required");
        return Task.CompletedTask;
    }
}
