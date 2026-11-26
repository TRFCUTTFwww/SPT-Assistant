using System.Text.Json;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class BtrInstantTransfer {
 static readonly HashSet<string> acknowledged=new(StringComparer.Ordinal);
 static Task<string>? request;static IntPtr world;static float next;static bool dirty=true;
 static int delivered;static string status="";static string lastError="";static Action? finishStop;static bool stopping;
 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(TransferItemsController),nameof(TransferItemsController.MoveItemsFromTempStashToTransferStash)),postfix:new HarmonyMethod(typeof(BtrInstantTransfer),nameof(Transferred)));
  plugin.Patches.Patch(typeof(LocalGame).GetMethod("Stop",new[]{typeof(string),typeof(ExitStatus),typeof(string),typeof(float)}),prefix:new HarmonyMethod(typeof(BtrInstantTransfer),nameof(BeforeStop)));
 }
 static bool BeforeStop(LocalGame __instance,string __0,ExitStatus __1,string __2,float __3){
  if(stopping||__0!=Singleton<GameWorld>.Instance?.MainPlayer?.ProfileId||!BtrServices.InstantTransfer.Value&&request==null)return true;
  if(!RaidClock.AllowAutomaticEnd(__instance,__0,__1))return false;
  // Finish an already submitted delivery before native settlement reads its
  // receipts. A failed request falls back to the native end-of-raid path.
  Tick();if(request==null)return true;
  finishStop=()=>{stopping=true;try{__instance.Stop(__0,__1,__2,__3);}finally{stopping=false;}};return false;
 }
 static void Transferred(TransferItemsController __instance,Player __0){if(__instance.TryCast<BtrTransferItemsController>()!=null&&__0?.IsYourPlayer==true){dirty=true;next=0;}}
 internal static void Tick(){try{
  if(request?.IsCompleted==true){var completed=request;request=null;using var doc=JsonDocument.Parse(completed.GetAwaiter().GetResult());var root=doc.RootElement;var data=CharacterDataPanel.Data(root);
   if(root.TryGetProperty("err",out var error)&&error.GetInt32()!=0)throw new InvalidOperationException(root.TryGetProperty("errmsg",out var message)?message.GetString():"BTR delivery failed");
   if(!data.TryGetProperty("ok",out var ok)||!ok.GetBoolean())throw new InvalidOperationException("BTR delivery not acknowledged");
   foreach(var id in data.GetProperty("acknowledged").EnumerateArray())acknowledged.Add(id.GetString()!);
   int count=data.GetProperty("delivered").GetInt32();delivered+=count;status="已送达";lastError="";
   if(count>0){FeatureToasts.Show("BTR 托运物品已通过邮件送达");Plugin.Self.Log.LogInfo("BTR in-raid delivery acknowledged: items="+count);}dirty=true;next=0;
  }
  if(finishStop!=null&&request==null){var stop=finishStop;finishStop=null;stop();return;}
  var current=Singleton<GameWorld>.Instance;var player=current?.MainPlayer;
  if(!RaidClock.Active||player?.IsYourPlayer!=true)return;
  if(world!=current!.Pointer){if(request!=null)return;world=current.Pointer;acknowledged.Clear();delivered=0;dirty=true;next=0;}
  if(request!=null||!BtrServices.InstantTransfer.Value||Time.unscaledTime<next)return;next=Time.unscaledTime+1;
  var transfer=current.BtrController?.TransferItemsController;
  if(transfer==null||!transfer.TryGetTransferContainer(player.ProfileId,out var container)||container==null)return;
  // Only the committed transfer container is eligible. The open UI's temporary
  // stash still belongs to the player until native payment/transfer succeeds.
  var roots=container.Grid.Items.Where(i=>!acknowledged.Contains(i.Id.ToString())).ToArray();
  if(roots.Length==0){dirty=false;return;}
  var flat=Singleton<ItemFactory>.Instance.TreeToFlatItems(roots);
  var serialized=Newtonsoft.Json.JsonConvert.SerializeObject(new Il2CppSystem.Object(flat.Pointer));
  var normalized=LocalSpt.Shared.RaidStashJson.Normalize(serialized,out _,out _);
  request=CharacterDataPanel.Call("/local/gear/data","characterData",new(){["action"]="btrInstantDeliver",["stashItems"]=JsonSerializer.Deserialize<JsonElement>(normalized.ToJsonString())},SPTushonka.Common.Http.RequestHandler.SessionId);
  status="正在送达";dirty=false;
 }catch(Exception e){request=null;dirty=true;next=Time.unscaledTime+5;status=e.Message;if(lastError!=e.Message){Plugin.Self.Log.LogWarning("BTR in-raid delivery: "+e.Message);lastError=e.Message;}if(finishStop!=null){var stop=finishStop;finishStop=null;stop();}}}
 internal static bool PrepareForReload()=>request==null&&finishStop==null;
 internal static object Diagnostics()=>new{enabled=BtrServices.InstantTransfer.Value,pending=request!=null,dirty,acknowledged=acknowledged.Count,delivered,status};
 internal static void Reset(){finishStop=null;stopping=false;acknowledged.Clear();request=null;world=IntPtr.Zero;next=0;dirty=true;delivered=0;status="";}
}
