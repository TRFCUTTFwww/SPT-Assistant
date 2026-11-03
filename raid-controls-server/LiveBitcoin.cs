using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using HarmonyLib;
using LocalRaidShared;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Callbacks;
namespace LocalRaidControls;
internal static class LiveBitcoin {
 static readonly string Home=System.IO.Path.Combine(AppContext.BaseDirectory,"user","mods","LocalRaidControls");static readonly HttpClient client=new(){Timeout=TimeSpan.FromSeconds(10)};static readonly JsonSerializerOptions json=new(){WriteIndented=true};static volatile BitcoinQuote? quote;static volatile bool enabled;static string error="";static double originalRub;
 internal static double? Effective=>enabled&&quote is {} current?current.GameRub:null;
 static double? Pricing=>Effective??(originalRub>0?originalRub:null);
 internal static void Start(Harmony harmony){void Patch(Type type,string method,string prefix)=>harmony.Patch(AccessTools.Method(type,method),prefix:new HarmonyMethod(typeof(LiveBitcoin),prefix));Patch(typeof(HandbookHelper),"GetTemplatePrice",nameof(Price));foreach(string name in new[]{"GetItemPrice","GetDynamicItemPrice"})Patch(typeof(ItemHelper),name,nameof(NullablePrice));foreach(string name in new[]{"GetStaticItemPrice","GetItemMaxPrice"})Patch(typeof(ItemHelper),name,nameof(Price));harmony.Patch(AccessTools.PropertyGetter(typeof(TemplateTable),"Handbook"),postfix:new HarmonyMethod(typeof(LiveBitcoin),nameof(Handbook)));foreach(string name in new[]{"GetAllFleaPrices","GetAllStaticPrices"})harmony.Patch(AccessTools.Method(typeof(RagfairPriceService),name),postfix:new HarmonyMethod(typeof(LiveBitcoin),nameof(AllPrices)));if(!AppContext.TryGetSwitch("LocalRaidControls.Tests",out var tests)||!tests)_=Loop();}
 static bool Price(MongoId __0,ref double __result){if(__0.ToString()!=BitcoinRules.Template||Pricing is not double price)return true;__result=price;return false;}
 static bool NullablePrice(MongoId __0,ref double? __result){if(__0.ToString()!=BitcoinRules.Template||Pricing is not double price)return true;__result=price;return false;}
 static void Handbook(ref HandbookBase __result){if(__result==null)return;var baseItem=__result.Items.FirstOrDefault(item=>item.Id.ToString()==BitcoinRules.Template);if(baseItem?.Price is double baseline&&baseline>0)originalRub=baseline;if(Effective is not double price)return;__result=__result with{Items=__result.Items.Select(item=>item.Id.ToString()==BitcoinRules.Template?item with{Price=price}:item).ToList()};}
 static void AllPrices(ref Dictionary<MongoId,double> __result){if(Effective is double price)__result=new(__result){[new MongoId(BitcoinRules.Template)]=price};}
 static void Atomic(string name,string value){string file=System.IO.Path.Combine(Home,name);File.WriteAllText(file+".tmp",value);File.Move(file+".tmp",file,true);}
 static void Publish()=>Atomic("bitcoin-status.json",JsonSerializer.Serialize(new BitcoinStatus{Enabled=enabled,Available=quote!=null,OriginalGameRub=originalRub>0?originalRub:null,Quote=quote,Error=error,ServerPid=Environment.ProcessId,UpdatedUtc=DateTime.UtcNow},json));
 static async Task Loop(){client.DefaultRequestHeaders.UserAgent.ParseAdd("SPT-Assistant/1.22");try{Directory.CreateDirectory(Home);string cache=System.IO.Path.Combine(Home,"bitcoin-cache.json");if(File.Exists(cache)){var saved=JsonSerializer.Deserialize<BitcoinQuote>(File.ReadAllText(cache));if(BitcoinRules.Valid(saved))quote=saved;}}catch(Exception e){error="行情缓存读取失败："+e.Message;}DateTime next=DateTime.MinValue,requestSeen=DateTime.MinValue;bool? previous=null;double? previousOriginal=null;for(;;){try{enabled=Patches.Read().LiveBitcoinEnabled;string request=System.IO.Path.Combine(Home,"bitcoin-refresh.request");DateTime requested=File.Exists(request)?File.GetLastWriteTimeUtc(request):DateTime.MinValue;bool manual=requested>requestSeen;if(manual)requestSeen=requested;if(manual||enabled&&DateTime.UtcNow>=next){try{string response=await client.GetStringAsync("https://api.coinbase.com/v2/exchange-rates?currency=BTC");var fresh=BitcoinRules.Parse(response,DateTime.UtcNow);quote=fresh;Atomic("bitcoin-cache.json",JsonSerializer.Serialize(fresh,json));error="";next=DateTime.UtcNow.AddMinutes(5);}catch(Exception e){error="联网失败，"+(quote==null?"暂用原游戏价格。":"保留上次行情。")+" "+e.Message;next=DateTime.UtcNow.AddMinutes(1);}Publish();}else if(previous!=enabled||previousOriginal!=originalRub)Publish();previous=enabled;previousOriginal=originalRub;}catch(Exception e){Patches.Log.Warning("Live BTC: "+e.Message);}await Task.Delay(2000);}}
}
