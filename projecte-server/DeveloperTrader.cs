using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;
namespace ProjectE.Server;
#if !SPT_ASSISTANT_SERVER
[Injectable(TypePriority=OnLoadOrder.TraderRegistration+5)]
#endif
public sealed class DeveloperTrader(TemplateTable templates,TradersTable traders,LocaleTable locales,TraderConfig config,ImageRouter images,JsonUtil json):IOnLoad {
 bool localeRegistered;
 public const string Id="a7d000000000000000000001";
 static string Offer(string key)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Developer:"+key))).ToLowerInvariant()[..24];
 public Task OnLoadAsync(CancellationToken ct){Rebuild();return Task.CompletedTask;}
 public void Rebuild(){
  var source=traders.GetTrader("5c0647fdd443bc2504c2d371")??throw new InvalidOperationException("Trader base missing");
  var baseData=JsonNode.Parse(json.Serialize(source.Base)!)!;
  baseData["_id"]=Id;baseData["name"]="Developer";baseData["surname"]="Developer";baseData["nickname"]="Developer";baseData["location"]="N/A";baseData["description"]="N/A";
  baseData["avatar"]="/files/trader/avatar/developer.png";baseData["unlockedByDefault"]=true;baseData["availableInRaid"]=false;baseData["customization_seller"]=false;baseData["medic"]=false;
  if(baseData["loyaltyLevels"] is JsonArray levels&&levels.Count>0){var first=levels[0]!.DeepClone();first["minLevel"]=1;first["minSalesSum"]=0;first["minStanding"]=0;baseData["loyaltyLevels"]=new JsonArray(first);}
  var previous=traders.GetTrader(Id);
  var trader=new Trader{Base=json.Deserialize<TraderBase>(baseData.ToJsonString())!,Assort=new(){Items=[],BarterScheme=[],LoyalLevelItems=[]},QuestAssort=new(){{"Started",new()},{"Success",new()},{"Fail",new()}},Dialogue=[]};
  using var originals=JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory,"SPT_Data","database","templates","items.json")));
  var vanilla=originals.RootElement.EnumerateObject().Select(x=>x.Name).ToHashSet();
  var prices=templates.Handbook!.Items!.Where(x=>x.Price>0).GroupBy(x=>x.Id.ToString()).ToDictionary(x=>x.Key,x=>x.First().Price!.Value);
  var custom=templates.Items.Where(x=>!vanilla.Contains(x.Key.ToString())&&prices.ContainsKey(x.Key.ToString())&&x.Value.Properties?.QuestItem!=true).Select(x=>x.Key.ToString()).ToHashSet();
  var all=traders.Values.Where(t=>t.Assort!=null).ToArray();
  bool FunAvailable(string id){
#if SPT_ASSISTANT_SERVER
   return LocalGearMail.FunItemPolicy.EnabledAtStartup||!LocalGearMail.FunItemPolicy.IsFunTemplate(id);
#else
   return true;
#endif
  }
  foreach(string template in custom.Where(FunAvailable).Order()){
   var original=all.SelectMany(t=>t.Assort.Items.Select(item=>(Trader:t,Item:item))).FirstOrDefault(x=>x.Item.Template.ToString()==template&&x.Item.ParentId=="hideout");
   var origin=original.Trader;var rootItem=original.Item;
   MongoId rootOffer=Offer(template);
   if(origin!=null&&rootItem!=null){
    var tree=TerminalStorageRules.Tree(origin.Assort.Items,rootItem.Id.ToString());
    var ids=tree.ToDictionary(x=>x.Id.ToString(),x=>x.Id==rootItem.Id?rootOffer.ToString():Offer(template+":"+x.Id));
    foreach(var item in tree){var copied=json.Deserialize<Item>(json.Serialize(item)!)!;copied.Id=ids[item.Id.ToString()];copied.ParentId=item.Id==rootItem.Id?"hideout":ids[item.ParentId!];if(item.Id==rootItem.Id){copied.SlotId="hideout";copied.Upd??=new();copied.Upd.StackObjectsCount=999999;copied.Upd.UnlimitedCount=true;}trader.Assort.Items.Add(copied);}
    if(origin.Assort.BarterScheme.TryGetValue(rootItem.Id,out var scheme))trader.Assort.BarterScheme[rootOffer]=scheme;
   }else trader.Assort.Items.Add(new(){Id=rootOffer,Template=template,ParentId="hideout",SlotId="hideout",Upd=new(){StackObjectsCount=999999,UnlimitedCount=true}});
   if(!trader.Assort.BarterScheme.ContainsKey(rootOffer))trader.Assort.BarterScheme[rootOffer]=[[new(){Template=EmcRules.Ruble,Count=prices[template]}]];
   trader.Assort.LoyalLevelItems[rootOffer]=1;
  }
  foreach(var original in all.Where(t=>t.Base?.Id.ToString()!=Id)){
   var roots=original.Assort.Items.Where(i=>i.ParentId=="hideout"&&custom.Contains(i.Template.ToString())).ToArray();
   foreach(var rootItem in roots){var remove=TerminalStorageRules.Tree(original.Assort.Items,rootItem.Id.ToString()).Select(i=>i.Id).ToHashSet();original.Assort.Items.RemoveAll(i=>remove.Contains(i.Id));original.Assort.BarterScheme.Remove(rootItem.Id);original.Assort.LoyalLevelItems.Remove(rootItem.Id);}
  }
  traders[Id]=trader;
  config.UpdateTime.RemoveAll(x=>x.TraderId.ToString()==Id);config.UpdateTime.Add(new(){TraderId=Id,Seconds=new MinMax<int>(3600,3600)});
  images.AddRoute("/files/trader/avatar/developer",System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","Minecraft","ProjectE","Assets","developer.png"));
  if(!localeRegistered){foreach(var locale in locales.Global.Values){locale.AddTransformer(data=>{if(data==null)return data;data[Id+" FullName"]="Developer";data[Id+" FirstName"]="Developer";data[Id+" Nickname"]="Developer";data[Id+" Location"]="N/A";data[Id+" Description"]="N/A";return data;});locale.Clear();}localeRegistered=true;}
  string directory=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","Minecraft","ProjectE");Directory.CreateDirectory(directory);
  File.WriteAllText(System.IO.Path.Combine(directory,"developer-status.json"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,id=Id,name="Developer",templates=custom.Order().ToArray(),offers=trader.Assort!.LoyalLevelItems.Count,vanillaOffers=0}));
 }
}
