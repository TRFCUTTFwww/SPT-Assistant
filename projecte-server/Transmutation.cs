using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
namespace ProjectE.Server;
public sealed class ApiRequest:TableRequestBase,IRequestData {}
// IRequestData belongs to the server; the shared DTO stays usable by the client.
public class TableRequestBase:IRequestData {
 [JsonExtensionData] public Dictionary<string,JsonElement> Values{get;set;}=[];
 public TableRequest Read()=>JsonSerializer.Deserialize<TableRequest>(JsonSerializer.Serialize(Values),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
}
[Injectable]
public sealed class TableRouter:StaticRouter {
 public TableRouter(JsonUtil json,TableService service):base(json,[new RouteAction<ApiRequest>("/local/projecte/table",(_,r,s,_,ct)=>service.Handle(r.Read(),s,ct))]){}
}
public sealed class Wallet {
 public long Balance{get;set;}
 public long Revision{get;set;}
 public HashSet<string> Learned{get;set;}=[EmcRules.Ruble];
 public Dictionary<string,Receipt> Receipts{get;set;}=[];
}
public sealed class Receipt {
 public long Revision{get;set;}
 public string Digest{get;set;}="";
 public string Created{get;set;}="";
 public long Before{get;set;}
 public long After{get;set;}
 public bool NewKnowledge{get;set;}
 public bool Undone{get;set;}
 public string Template{get;set;}="";
 // Preserve native JSON, including MongoId/location converters, across receipts.
 public string InventoryBeforeJson{get;set;}="";
 public string InventoryAfter{get;set;}="";
}
[Injectable]
public sealed class TableService(TemplateTable templates,SaveServer saves,ItemHelper helper,JsonUtil json,HttpResponseUtil http) {
 const string Key="localProjectETransmutation";
 static readonly ConcurrentDictionary<string,SemaphoreSlim> Gates=new();
 TablePrice[]? prices;
 public TablePrice[] Prices()=>prices??=templates.Handbook!.Items!.Where(x=>x.Price>0&&templates.Items.ContainsKey(x.Id)).Select(x=>{
  var p=templates.Items[x.Id].Properties;using var doc=JsonDocument.Parse(json.Serialize(p)!);var props=doc.RootElement;
  bool quest=props.TryGetProperty("QuestItem",out var q)&&q.ValueKind==JsonValueKind.True;
  int stack=props.TryGetProperty("StackMaxSize",out var n)?Math.Max(1,(int)n.GetDouble()):1;
  return new TablePrice(x.Id.ToString(),quest?0:Math.Max(1,(long)Math.Round(x.Price??0)),stack);
 }).Where(p=>p.Value>0).GroupBy(x=>x.Id).Select(x=>x.First()).Where(x=>x.Id!=EmcRules.Ruble).Append(new(EmcRules.Ruble,1,500000)).ToArray();
 static string Digest(TableRequest r)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r))));
 public async ValueTask<string> Handle(TableRequest request,MongoId session,CancellationToken ct){
  var gate=ProfileGates.Get(session.ToString());await gate.WaitAsync(ct);
  try{
   var profile=saves.GetProfile(session)??throw new ArgumentException("请先登录存档。");
   var pmc=profile.CharacterData?.PmcData??throw new ArgumentException("PMC 存档未就绪。");
   if(request.Profile!=pmc.Id.ToString())throw new ArgumentException("转换桌仅支持当前 PMC 存档。");
   var inventory=pmc.Inventory??throw new ArgumentException("库存未就绪。");
   var wallet=profile.ExtensionData?.TryGetValue(Key,out var stored)==true?JsonSerializer.Deserialize<Wallet>(JsonSerializer.Serialize(stored))??new():new Wallet();
   TableReply Reply(string created="",bool learned=false)=>new(){Ok=true,Balance=wallet.Balance,Learned=wallet.Learned.ToArray(),Prices=request.Action=="state"?Prices():[],Created=created,NewKnowledge=learned};
   if(request.Action=="state")return http.GetBody(Reply());
   if(!Guid.TryParseExact(request.Operation,"N",out _))throw new ArgumentException("无效的转换流水号。");
   var oldItems=inventory.Items;var oldExtension=profile.ExtensionData;List<Item>? changed=null;
   if(request.Action=="undo"){
    if(!wallet.Receipts.TryGetValue(request.Operation,out var receipt)){
     receipt=new(){Before=wallet.Balance,After=wallet.Balance,Revision=wallet.Revision,Undone=true};wallet.Receipts[request.Operation]=receipt;
    }
    if(!receipt.Undone){
     if(wallet.Revision!=receipt.Revision)throw new ArgumentException("转换流水已变化，无法自动撤回；请保留流水号。");
     if(receipt.InventoryBeforeJson.Length>0){if(json.Serialize(inventory.Items)!=receipt.InventoryAfter)throw new ArgumentException("库存已变化，无法自动撤回。");changed=json.Deserialize<List<Item>>(receipt.InventoryBeforeJson)??throw new ArgumentException("库存快照无效。");}
     wallet.Balance=receipt.Before;if(receipt.NewKnowledge)wallet.Learned.Remove(receipt.Template);receipt.Undone=true;wallet.Revision++;
    }
   }else if(request.Action=="artifact"){
    if(wallet.Receipts.TryGetValue(request.Operation,out var previous)){
     if(previous.Digest!=Digest(request)||previous.Undone)throw new ArgumentException("流水已撤回或请求内容不一致。");
     return http.GetBody(Reply());
    }
    long cost=ArtifactRules.Cost(request.Template,request.Count,request.Quality);
    long before=wallet.Balance;wallet.Balance=EmcRules.Exchange(before,cost,false);
    foreach(var older in wallet.Receipts.Values){older.InventoryBeforeJson="";older.InventoryAfter="";}
    wallet.Receipts.Add(request.Operation,new(){Digest=Digest(request),Before=before,After=wallet.Balance,Revision=++wallet.Revision,Template=request.Template});
    while(wallet.Receipts.Count>64)wallet.Receipts.Remove(wallet.Receipts.Keys.First());
   }else if(request.Action=="exchange"){
    if(wallet.Receipts.TryGetValue(request.Operation,out var previous)){
     if(previous.Digest!=Digest(request)||previous.Undone)throw new ArgumentException("转换流水已被使用。");
     return http.GetBody(Reply(previous.Created,previous.NewKnowledge));
    }
    var price=Prices().SingleOrDefault(p=>p.Id==request.Template)??throw new ArgumentException("此物品没有有效的卢布参考价。");
    if(request.Count<1||request.Count>price.Stack)throw new ArgumentException("单次数量超过一个原生堆叠。");
    if(!request.Burn&&!wallet.Learned.Contains(request.Template))throw new ArgumentException("请先消耗此物品以学习。");
    var receipt=new Receipt{Digest=Digest(request),Before=wallet.Balance,Template=request.Template};
    if(!request.Raid){
     receipt.InventoryBeforeJson=json.Serialize(inventory.Items)!;
     var items=json.Deserialize<List<Item>>(json.Serialize(inventory.Items)!)!;
     if(request.Burn){
      var item=items.SingleOrDefault(i=>i.Id.ToString()==request.Item&&i.Template.ToString()==request.Template)??throw new ArgumentException("物品已移动或不存在。");
      if(items.Any(i=>i.ParentId==item.Id.ToString()))throw new ArgumentException("请先清空容器、弹匣或卸下附件。");
      if(item.SlotId is null or "SecuredContainer" or "Scabbard" or "FirstPrimaryWeapon" or "SecondPrimaryWeapon" or "Holster" or "ArmorVest" or "Headwear" or "Backpack" or "TacticalVest" or "Pockets")throw new ArgumentException("请将物品先放进背包或仓库。");
      int count=(int)(item.Upd?.StackObjectsCount??1);if(count<request.Count)throw new ArgumentException("物品数量不足。");
      request.Quality=Quality(item);
      if(count==request.Count)items.Remove(item);else {item.Upd??=new();item.Upd.StackObjectsCount=count-request.Count;}
     }else{
      ValidateDestination(items,request);
      receipt.Created=new MongoId().ToString();
      var location=JsonSerializer.Serialize(new{x=request.X,y=request.Y,r=request.Rotation});
      var generated=new Item{Id=receipt.Created,Template=request.Template,ParentId=request.Parent,SlotId=request.Slot,Upd=helper.GenerateUpdForItem(templates.Items[request.Template])??new()};
      generated.Upd.StackObjectsCount=request.Count;generated.Upd.SpawnedInSession=false;
      // Server Item.Location is a polymorphic object; use the native JSON converter.
      var raw=System.Text.Json.Nodes.JsonNode.Parse(json.Serialize(generated)!)!;raw["location"]=System.Text.Json.Nodes.JsonNode.Parse(location);generated=json.Deserialize<Item>(raw.ToJsonString())!;
      items.Add(generated);
     }
     receipt.InventoryAfter=json.Serialize(items)!;changed=items;
    }else if(!request.Burn)receipt.Created=new MongoId().ToString();
    long amount=EmcRules.Quote(price.Value,request.Count,request.Burn?request.Quality:10000);
    wallet.Balance=EmcRules.Exchange(wallet.Balance,amount,request.Burn);
    if(request.Burn)receipt.NewKnowledge=wallet.Learned.Add(request.Template);
    receipt.After=wallet.Balance;receipt.Revision=++wallet.Revision;
    foreach(var older in wallet.Receipts.Values){older.InventoryBeforeJson="";older.InventoryAfter="";}
    wallet.Receipts.Add(request.Operation,receipt);
    // Bound receipts without discarding the newest rollback record.
    while(wallet.Receipts.Count>64)wallet.Receipts.Remove(wallet.Receipts.Keys.First());
   }else throw new ArgumentException("未知转换操作。");
   if(changed!=null)inventory.Items=changed;
   profile.ExtensionData=oldExtension==null?new():new(oldExtension);profile.ExtensionData[Key]=wallet;
   try{await saves.SaveProfileAsync(session);}catch{inventory.Items=oldItems;profile.ExtensionData=oldExtension;throw;}
   var completed=wallet.Receipts[request.Operation];return http.GetBody(Reply(completed.Created,completed.NewKnowledge));
  }catch(Exception error){return http.GetBody(new TableReply{Ok=false,Error=error.Message});}
  finally{gate.Release();}
 }
 int Quality(Item item){
  using var p=JsonDocument.Parse(json.Serialize(templates.Items[item.Template].Properties)!);using var u=JsonDocument.Parse(json.Serialize(item.Upd??new())!);double ratio=1;
  void Resource(string component,string remaining,string maximum,bool usages=false){
   if(!p.RootElement.TryGetProperty(maximum,out var max)||max.ValueKind!=JsonValueKind.Number||max.GetDouble()<=0)return;
   if(!u.RootElement.TryGetProperty(component,out var c)||!c.TryGetProperty(remaining,out var value)||value.ValueKind!=JsonValueKind.Number)return;
   double fraction=value.GetDouble()/max.GetDouble();ratio=Math.Min(ratio,usages?1-fraction:fraction);
  }
  Resource("Repairable","Durability","Durability");Resource("MedKit","HpResource","MaxHpResource");Resource("FoodDrink","HpPercent","MaxResource");Resource("Key","NumberOfUsages","MaximumNumberOfUsage",true);
  return (int)Math.Clamp(double.IsFinite(ratio)?ratio*10000:0,0,10000);
 }
 void ValidateDestination(List<Item> items,TableRequest r){
  var parent=items.SingleOrDefault(i=>i.Id.ToString()==r.Parent)??throw new ArgumentException("目标容器不存在。");
  if(r.Rotation is <0 or >1||r.X<0||r.Y<0)throw new ArgumentException("无效的目标位置。");
  using var props=JsonDocument.Parse(json.Serialize(templates.Items[parent.Template].Properties)!);
  var grids=props.RootElement.GetProperty("Grids");JsonElement? definition=null;
  foreach(var grid in grids.EnumerateArray())if(grid.GetProperty("_name").GetString()==r.Slot)definition=grid.GetProperty("_props");
  if(definition==null)throw new ArgumentException("目标格子不存在。");
  var target=templates.Items[r.Template].Properties!;int width=(int)target.Width,height=(int)target.Height;if(r.Rotation==1)(width,height)=(height,width);
  int gridWidth=definition.Value.GetProperty("cellsH").GetInt32(),gridHeight=definition.Value.GetProperty("cellsV").GetInt32();
  if(r.X>gridWidth||r.Y>gridHeight||r.X+width>gridWidth||r.Y+height>gridHeight)throw new ArgumentException("目标格子空间不足。");
  foreach(var other in items.Where(i=>i.ParentId==r.Parent&&i.SlotId==r.Slot)){
   using var doc=JsonDocument.Parse(json.Serialize(other)!);if(!doc.RootElement.TryGetProperty("location",out var loc)||loc.ValueKind!=JsonValueKind.Object)continue;
   int x=loc.GetProperty("x").GetInt32(),y=loc.GetProperty("y").GetInt32(),rotation=loc.TryGetProperty("r",out var rot)?rot.ValueKind==JsonValueKind.Number?rot.GetInt32():rot.GetString()=="Vertical"?1:0:0;
   var size=templates.Items[other.Template].Properties!;int w=(int)size.Width,h=(int)size.Height;if(rotation==1)(w,h)=(h,w);
   if(r.X<x+w&&r.X+width>x&&r.Y<y+h&&r.Y+height>y)throw new ArgumentException("目标格子已被占用。");
  }
 }
}
