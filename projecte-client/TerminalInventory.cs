using System.Text.Json;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Local.ModHost;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace ProjectE.Client;
// Local copy of the raid-stash implementation. No assistant assembly, API key,
// method invocation or endpoint is needed by this module.
internal sealed class TerminalInventory {
 static TerminalInventory? active;
 readonly ModuleContext context;readonly Action<string> report;
 Task<string>? request;bool loading,ready,opening,ownsScreen,stopping,conflict;
 IntPtr actor;Stash? stash;GamePlayerOwner? owner;
 string profile="",revision="",saved="",sent="";JsonElement root;
 float nextCheck,retryAt,openUntil;Action? finishStop;
 string Journal=>Path.Combine(context.DataDirectory,"terminal-pending-"+profile+".json");
 static Player? Local=>ProjectModule.CurrentRaidPlayer;
 internal TerminalInventory(ModuleContext context,Harmony harmony,Action<string> report){
  this.context=context;this.report=report;active=this;
  var show=typeof(InventoryScreen).GetMethods().Single(m=>m.Name=="Show"&&m.GetParameters().Length==10);
  harmony.Patch(show,prefix:new HarmonyMethod(typeof(TerminalInventory),nameof(BeforeShow)){priority=Priority.Last});
  harmony.Patch(AccessTools.Method(typeof(InventoryScreen),"Close"),postfix:new HarmonyMethod(typeof(TerminalInventory),nameof(Closed)));
  harmony.Patch(AccessTools.Method(typeof(Player.PlayerInventoryController),"FindItemById",[typeof(MongoID),typeof(bool),typeof(bool)]),prefix:new HarmonyMethod(typeof(TerminalInventory),nameof(FindItem)){priority=Priority.First});
  harmony.Patch(typeof(LocalGame).GetMethod("Stop",[typeof(string),typeof(ExitStatus),typeof(string),typeof(float)]),prefix:new HarmonyMethod(typeof(TerminalInventory),nameof(BeforeStop)));
  context.OnShutdown(Shutdown);
 }
 internal bool Opening=>opening;
 internal bool CanReload=>request==null&&!ownsScreen&&(!ready||Snapshot()==saved);
 bool Own(InventoryController? c)=>ready&&c!=null&&c.Pointer==Local?.InventoryController?.Pointer&&Local?.Pointer==actor;
 static void BeforeShow(InventoryController __1,ref CompoundItem __5,ref bool __9){var a=active;if(a?.context.IsActive!=true||!a.Own(__1)||(!a.opening&&__5!=null&&__5.TryCast<Stash>()==null))return;__5=a.stash!;__9=false;a.ownsScreen=true;}
 static bool FindItem(Player.PlayerInventoryController __instance,MongoID __0,ref Option<Item> __result){var a=active;if(a?.context.IsActive==true&&a.Own(__instance)&&a.stash?.TryFindItem(__0.ToString(),out var item)==true&&item!=null){__result=new(item);return false;}return true;}
 static void Closed(){var a=active;if(a?.ownsScreen!=true)return;a.ownsScreen=false;try{a.Save();}catch(Exception e){a.Fail(e.Message);}}
 static bool BeforeStop(LocalGame __instance,string __0,ExitStatus __1,string __2,float __3){
  var a=active;if(a?.context.IsActive!=true||a.stopping||!a.ready||__0!=a.profile)return true;
  try{a.Save();if(a.request==null&&a.Snapshot()==a.saved&&Local?.InventoryController?.HasActiveEvents!=true)return true;
   a.finishStop=()=>{a.stopping=true;try{__instance.Stop(__0,__1,__2,__3);}finally{a.stopping=false;}};return false;
  }catch(Exception e){a.Fail(e.Message);return false;}
 }
 static async Task<string> Call(StorageRequest payload)=>await SPTushonka.Common.Http.RequestHandler.PostJsonAsync("/local/projecte/terminal",JsonSerializer.Serialize(payload)).WaitAsync(TimeSpan.FromSeconds(20));
 string Snapshot(){
  var flat=Singleton<ItemFactory>.Instance.TreeToFlatItems(new Item[]{stash!});
  var data=StorageJson.Normalize(Newtonsoft.Json.JsonConvert.SerializeObject(new Il2CppSystem.Object(flat.Pointer)),out _,out _);
  foreach(var item in data)if(item?["_id"]?.GetValue<string>()==stash!.Id.ToString())foreach(string key in new[]{"parentId","slotId","location"}){item!.AsObject().Remove(key);if(root.TryGetProperty(key,out var value))item[key]=System.Text.Json.Nodes.JsonNode.Parse(value.GetRawText());}
  return data.ToJsonString();
 }
 void Save(){
  if(!ready||request!=null||conflict||Local?.InventoryController?.HasActiveEvents==true)return;
  string current=Snapshot();if(current==saved)return;
  sent=current;var payload=new StorageRequest{Action="save",Profile=profile,Revision=revision,Items=JsonSerializer.Deserialize<JsonElement>(sent)};
  File.WriteAllText(Journal+".new",JsonSerializer.Serialize(payload));File.Move(Journal+".new",Journal,true);request=Call(payload);loading=false;
 }
 internal void Open(GamePlayerOwner? gameOwner){
  var p=Local;if(p?.HealthController?.IsAlive!=true)return;
  if(request!=null||ownsScreen)return;
  if(ready&&Snapshot()!=saved){Save();return;}
  owner=gameOwner??p.GetComponent<GamePlayerOwner>();if(owner==null){Fail("库存输入控制器尚未就绪。");return;}
  actor=p.Pointer;profile=p.ProfileId;openUntil=Time.unscaledTime+22;conflict=false;
  // Always load a fresh revision when explicitly opening, including after use
  // of another stash UI. A pending unconfirmed save is retried first.
  if(File.Exists(Journal)){var pending=JsonSerializer.Deserialize<StorageRequest>(File.ReadAllText(Journal))!;sent=pending.Items!.Value.GetRawText();request=Call(pending);loading=false;}
  else{request=Call(new(){Profile=profile});loading=true;}
 }
 void Load(JsonElement data){
  var factory=Singleton<ItemFactory>.Instance;var normalized=StorageJson.Normalize(data.GetProperty("items").GetRawText(),out _,out _);
  using var doc=JsonDocument.Parse(normalized.ToJsonString());var items=doc.RootElement;
  string id=data.GetProperty("stash").GetString()!;root=items.EnumerateArray().Single(i=>i.GetProperty("_id").GetString()==id).Clone();
  var flat=new Il2CppReferenceArray<JsonType.FlatItem>(items.GetArrayLength());int index=0;
  foreach(var item in items.EnumerateArray()){
   var value=new JsonType.FlatItem{_id=new MongoID(item.GetProperty("_id").GetString()!),_tpl=new MongoID(item.GetProperty("_tpl").GetString()!)};
   if(item.TryGetProperty("parentId",out var parent)&&parent.ValueKind==JsonValueKind.String)value.parentId=new Il2CppSystem.Nullable<MongoID>(new MongoID(parent.GetString()!));
   if(item.TryGetProperty("slotId",out var slot)&&slot.ValueKind==JsonValueKind.String)value.slotId=slot.GetString();
   if(item.TryGetProperty("location",out var location)&&location.ValueKind!=JsonValueKind.Null)value.location=new UnparsedData{JToken=JToken.Parse(location.GetRawText())};
   if(item.TryGetProperty("upd",out var upd)&&upd.ValueKind!=JsonValueKind.Null)value.upd=new UnparsedData{JToken=JToken.Parse(upd.GetRawText())};
   flat[index++]=value;
  }
  var tree=factory.FlatItemsToTree(flat,false,null);if(tree.DeserializationErrors?.Count>0)throw new InvalidOperationException("仓库物品无法完整读取。");
  stash=tree.Items[id].TryCast<Stash>()??throw new InvalidOperationException("仓库读取失败。");
  stash.UpdateBonuses(Local!.Profile.BonusController??throw new InvalidOperationException("仓库容量尚未就绪。"));
  if(stash.Grid.GridWidth<=0||stash.Grid.GridHeight<=0||stash.Grid.Layout.Count!=stash.Grid.GridWidth*stash.Grid.GridHeight)throw new InvalidOperationException("仓库容量无效。");
  stash.CurrentAddress=Local.InventoryController.CreateItemAddress();revision=data.GetProperty("revision").GetString()!;saved=Snapshot();ready=true;Status("");
 }
 internal void Tick(){try{
  var p=Local;
  if(request?.IsCompleted==true){
   var completed=request;request=null;using var document=JsonDocument.Parse(completed.GetAwaiter().GetResult());var data=document.RootElement;if(data.TryGetProperty("data",out var inner))data=inner;
   if(!data.TryGetProperty("ok",out var ok)||!ok.GetBoolean()){conflict=true;throw new InvalidOperationException(data.TryGetProperty("error",out var error)?error.GetString():"终端接口未就绪，请更新服务端。");}
   if(loading){if(p?.Pointer!=actor){Reset();return;}Load(data);loading=false;}
   else{revision=data.GetProperty("revision").GetString()!;saved=sent;File.Delete(Journal);Status("");if(!ready&&p?.Pointer==actor&&openUntil>Time.unscaledTime){loading=true;request=Call(new(){Profile=profile});}}
  }
  if(p?.Pointer!=actor||p.HealthController?.IsAlive!=true){if(request==null)Reset();return;}
  if(ready&&request==null&&Time.unscaledTime>=nextCheck){nextCheck=Time.unscaledTime+1;Save();}
  if(!conflict&&request==null&&File.Exists(Journal)&&Time.unscaledTime>=retryAt){retryAt=Time.unscaledTime+5;var pending=JsonSerializer.Deserialize<StorageRequest>(File.ReadAllText(Journal))!;sent=pending.Items!.Value.GetRawText();request=Call(pending);loading=false;}
  if(finishStop!=null&&request==null&&ready&&Snapshot()==saved&&p.InventoryController?.HasActiveEvents!=true){var finish=finishStop;finishStop=null;finish();return;}
  if(openUntil>0&&Time.unscaledTime>=openUntil){openUntil=0;Fail("无线仓库连接超时。");}
  if(ready&&!conflict&&request==null&&openUntil>Time.unscaledTime&&!p.IsInventoryOpened){
   if(p.HandsController?.GetItem()?.StringTemplateId!=Gadgets.Terminal){openUntil=0;return;}
   opening=true;try{owner!.TranslateCommand(EFT.InputSystem.ECommand.ToggleInventory);}finally{opening=false;}
   if(p.IsInventoryOpened)openUntil=0;
  }
 }catch(Exception e){request=null;loading=false;openUntil=0;retryAt=Time.unscaledTime+5;Fail(e.GetBaseException().Message);}}
 void Status(string error){try{File.WriteAllText(Path.Combine(context.DataDirectory,"terminal-status.json"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,ready,ownsScreen,pending=request!=null,conflict,error,standalone=true}));}catch{}}
 void Fail(string error){report("无线终端："+error);Status(error);}
 void Reset(){ready=false;stash=null;owner=null;actor=IntPtr.Zero;revision=saved=sent="";openUntil=0;ownsScreen=false;finishStop=null;}
 void Shutdown(){if(active==this)active=null;if(ownsScreen)CommonUI.Instance?.InventoryScreen?.Close();if(ready&&request==null)Save();Reset();}
}
