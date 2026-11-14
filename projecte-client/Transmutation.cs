using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Local.ModHost;
using UnityEngine;
namespace ProjectE.Client;
internal sealed partial class Transmutation {
 readonly ModuleContext context;readonly ProjectResources resources;
 readonly Dictionary<string,string> names=[];
 readonly Dictionary<string,TablePrice> prices=[];
 HashSet<string> learned=[];
 internal bool Open{get;private set;}
 internal bool Busy=>pending!=null;
 internal bool Working=>exchange!=null||recovering;
 bool ready,recovering,preflight;float nextRead;
 internal string Notice="F10 打开转换桌 · 1 卢布 = 1 EMC";
 InventoryAccess? access;
 Item[] items=[];long balance;
 string owner="";
 Task<TableReply>? pending;Action<TableReply>? completed;
 Task<Dictionary<string,string>>? locale;
 CursorLockMode oldLock;bool oldVisible;
 TableRequest? exchange;Item? source;ItemAddress? originalAddress;GridItemAddress? destination;Item? output;
 string journal=>Path.Combine(context.DataDirectory,"pending-"+owner+".json");
 internal Transmutation(ModuleContext context,ProjectResources resources){this.context=context;this.resources=resources;context.OnShutdown(()=>SetOpen(false));}
 static async Task<TableReply> Call(TableRequest request){
  string json=JsonSerializer.Serialize(request);Exception? failure=null;
  for(int attempt=0;attempt<2;attempt++)try{
   string response=await SPTushonka.Common.Http.RequestHandler.PostJsonAsync("/local/projecte/table",json).WaitAsync(TimeSpan.FromSeconds(20));
   return TableProtocol.Read(response);
  }catch(Exception e){failure=e;}
  throw new IOException(request.Action=="state"?"无法读取转化桌。请确认服务端已启动并安装 ProjectE，然后重新连接。":"转换请求未确认，请使用恢复转换核对；请勿重复消耗。",failure);
 }
 internal void Toggle(){SetOpen(!Open);}
 internal void SetOpen(bool value){
  if(Open==value)return;if(!value&&Busy&&Working)return;
  if(value){oldLock=Cursor.lockState;oldVisible=Cursor.visible;Open=true;if(!Busy){Read();Load();}resources.Play("tock",Volume);}
  else{Open=false;Cursor.lockState=oldLock;Cursor.visible=oldVisible;}
 }
 internal void KeepCursor(){if(Open){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
 internal float Volume=.65f;
 internal void Tick(){
  if(locale?.IsCompleted==true){try{foreach(var x in locale.GetAwaiter().GetResult())names[x.Key]=x.Value;}catch{}locale=null;}
  if(pending?.IsCompleted==true){var task=pending;var callback=completed;pending=null;completed=null;try{var reply=task.GetAwaiter().GetResult();if(!reply.Ok)throw new ArgumentException(string.IsNullOrWhiteSpace(reply.Error)?"转换桌接口未就绪，请重启服务端后重试。":reply.Error);balance=reply.Balance;learned=reply.Learned.ToHashSet();foreach(var p in reply.Prices)prices[p.Id]=p;callback?.Invoke(reply);}catch(Exception e){recovering=false;Notice=e.Message+(File.Exists(journal)?" 请点击恢复转换。":" 请点击重新连接。");context.Log(LogLevel.Warning,"Transmutation: "+e);WriteStatus();}}
  // Preflight is read-only and runs once when an inventory becomes available.
  if(!preflight&&!Busy&&InventoryAccess.Current()!=null){preflight=true;Read();Load();}
  if(!Open)return;
  var current=InventoryAccess.Current();if(current==null||current.Profile.Id!=owner||current.Controller.Pointer!=access?.Controller.Pointer){if(!Busy){ready=false;Read();if(access!=null)Load();}KeepCursor();return;}
  if(!Busy&&Time.unscaledTime>=nextRead){nextRead=Time.unscaledTime+1;items=current.Items();}
  KeepCursor();
 }
 void Read(){access=InventoryAccess.Current();if(access==null){items=[];ready=false;Notice="库存未就绪：请先进入 PMC 角色库存，或在存活的 PMC 战局中打开。";return;}if(owner!=access.Profile.Id){owner=access.Profile.Id;prices.Clear();learned.Clear();balance=0;selectedItem=selectedLearned="";ready=false;}items=access.Items();}
 void Load(){
  if(Busy||access==null)return;ready=false;Notice="正在连接转化桌，读取 EMC 和知识…";
  pending=Call(new(){Action="state",Profile=owner});completed=_=>{ready=true;Notice=balance==0?"先选择下方库存物品，消耗并学习以获得 EMC。卢布可按 1:1 转换。":"选择下方物品消耗，或选择右侧法阵中的已学物品取出。";if(File.Exists(journal))Notice="存在未确认转换。请先点击“恢复转换”核对上次操作。";WriteStatus();};
  locale=LoadNames();
 }
 static async Task<Dictionary<string,string>> LoadNames(){string response=await SPTushonka.Common.Http.RequestHandler.PostJsonAsync("/client/locale/ch","{}").WaitAsync(TimeSpan.FromSeconds(20));using var doc=JsonDocument.Parse(response);return JsonSerializer.Deserialize<Dictionary<string,string>>((doc.RootElement.TryGetProperty("data",out var d)?d:doc.RootElement).GetRawText())??[];}
 string Name(string id)=>names.GetValueOrDefault(id+" Name")??Swords.Find(id)?.Chinese??id;
 string Restriction(Item item){if(item.QuestItem)return "任务物品不可转换";if(!prices.ContainsKey(item.StringTemplateId))return "没有卢布参考价";if(InventoryAccess.Children(item).Any())return "请先清空内容物或卸下附件";if(item.CurrentAddress?.TryCast<GridItemAddress>()==null)return "请先放进背包格子";if(InventoryAccess.Quality(item)==0)return "已耗尽物品没有 EMC";return "";}
 void WriteStatus(){try{File.WriteAllText(Path.Combine(context.DataDirectory,"table-status.json"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,open=Open,ready,raid=access?.Raid,items=items.Length,convertible=ready?items.Count(i=>Restriction(i).Length==0):0,prices=prices.Count,learned=learned.Count,balance,pending=Busy,recovery=File.Exists(journal),notice=Notice}));}catch(Exception e){context.Log(LogLevel.Warning,"Table diagnostics: "+e.Message);}}
 void Exchange(bool burn){
  if(Busy||!ready||access==null||File.Exists(journal))return;
  try{
   var current=InventoryAccess.Current();if(current==null||current.Profile.Id!=owner||current.Controller.Pointer!=access.Controller.Pointer)throw new ArgumentException("库存会话已变化，请刷新后重试。");
   Read();string blocked=ActionProblem(burn);if(blocked.Length>0)throw new ArgumentException(blocked);int count=int.Parse(quantity);
   var request=new TableRequest{Action="exchange",Operation=Guid.NewGuid().ToString("N"),Profile=owner,Raid=access.Raid,Burn=burn,Count=count};
   source=null;output=null;destination=null;originalAddress=null;
   if(burn){source=items.SingleOrDefault(i=>i.Id.ToString()==selectedItem)??throw new ArgumentException("请选择背包物品。");string reason=Restriction(source);if(reason.Length>0)throw new ArgumentException(reason);if(count>source.StackObjectsCount)throw new ArgumentException("数量不足。");request.Item=source.Id.ToString();request.Template=source.StringTemplateId;request.Quality=InventoryAccess.Quality(source);originalAddress=source.CurrentAddress;}
   else{
    request.Template=selectedLearned;if(!learned.Contains(request.Template))throw new ArgumentException("请选择已学物品。");
    var p=prices[request.Template];if(count>p.Stack)throw new ArgumentException("单次最多取出一个堆叠。");EmcRules.Exchange(balance,EmcRules.Quote(p.Value,count),false);
    var factory=Singleton<ItemFactory>.Instance??throw new ArgumentException("物品工厂未就绪。");output=factory.CreateItem(factory.NextId.ToString(),request.Template,null);output.StackObjectsCount=count;output.SpawnedInSession=false;
    destination=access.Destination(output)??throw new ArgumentException("背包或仓库空间不足，未扣 EMC。");var d=destination.ToDescriptor();request.Parent=d.Container.ParentId.Value.ToString();request.Slot=d.Container.ContainerId;request.X=destination.LocationInGrid.x;request.Y=destination.LocationInGrid.y;request.Rotation=(int)destination.LocationInGrid.r;
   }
   var price=prices[request.Template];EmcRules.Exchange(balance,EmcRules.Quote(price.Value,count,burn?request.Quality:10000),burn);
   exchange=request;SaveJournal(request);pending=Call(request);completed=Finish;Notice="转换中…";
  }catch(Exception e){Notice=e.Message;}
 }
 void SaveJournal(TableRequest request){string next=journal+".next";File.WriteAllText(next,JsonSerializer.Serialize(request));File.Move(next,journal,true);}
 void Finish(TableReply reply){
  var request=exchange!;bool changed=false;int before=source?.StackObjectsCount??0;
  try{
   var current=InventoryAccess.Current();if(current==null||current.Profile.Id!=request.Profile||current.Controller.Pointer!=access?.Controller.Pointer)throw new ArgumentException("库存会话已变化。");
   if(request.Burn){if(source==null||!current.Items().Any(i=>i.Pointer==source.Pointer)||source.StackObjectsCount<request.Count)throw new ArgumentException("原物品已变化。");changed=true;current.Remove(source,request.Count);}
   else {var factory=Singleton<ItemFactory>.Instance;output=factory.CreateItem(reply.Created,request.Template,null);output.StackObjectsCount=request.Count;output.SpawnedInSession=false;changed=true;current.Add(output,destination!);}
   File.Delete(journal);Notice=(request.Burn?"已转换为 EMC 并学习：":"已取出：")+Name(request.Template)+" ×"+request.Count;resources.Play(reply.NewKnowledge?"pecharge":request.Burn?"petransmute":"chargetick",Volume);Read();exchange=null;quantity="1";WriteStatus();
  }catch(Exception e){
   // Native failures are compensated by the server's idempotent undo operation.
   if(changed&&request.Burn&&source!=null){source.StackObjectsCount=before;if(source.CurrentAddress==null&&originalAddress!=null)access!.Add(source,originalAddress);}
   if(changed&&!request.Burn&&output?.CurrentAddress!=null)access!.Remove(output,output.StackObjectsCount);
   pending=Call(new(){Action="undo",Operation=request.Operation,Profile=request.Profile});completed=_=>{File.Delete(journal);exchange=null;Read();Notice="转换未完成，EMC 与物品已撤回："+e.Message;resources.Play("peuncharge",Volume);};Notice="正在撤回转换…";
  }
 }
 void Recover(){
  if(Busy||!File.Exists(journal)||access==null)return;
  try{
   var request=JsonSerializer.Deserialize<TableRequest>(File.ReadAllText(journal))!;if(request.Profile!=owner)throw new ArgumentException("未确认流水属于另一个存档。");
   // Never replay an unconfirmed inventory mutation. Reverse its server receipt;
   // if the request never reached the server, the original inventory is intact.
   recovering=true;pending=Call(new(){Action="undo",Operation=request.Operation,Profile=owner});completed=_=>{File.Delete(journal);recovering=false;exchange=null;Notice="上次未确认转换已撤回。";Read();WriteStatus();};
  }catch(Exception e){Notice=e.Message;}
 }
}
