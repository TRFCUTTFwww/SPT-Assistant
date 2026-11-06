using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class RaidStash {
 static ConfigEntry<bool> enabled=null!;
 static Task<string>? request;static bool loading,ready,stopping;static IntPtr actor;static Stash? stash;
 static string revision="",saved="",sent="";static JsonElement stashRoot;static float next,check;static Action? finishStop;
 internal static Il2CppSystem.Nullable<MongoID> ParentId(string id)=>new(new MongoID(id));
 internal static void DrawToggle(Rect rect){if(InterfaceMenu.Button(rect,"战局仓库 · "+(enabled.Value?"开":"关"),enabled.Value))enabled.Value=!enabled.Value;}
 internal static void Initialize(Plugin plugin){
  const string sample="5449016a4bdc2d6f028b456f";var parent=ParentId(sample);var flat=new JsonType.FlatItem{parentId=parent};if(!flat.parentId.HasValue||flat.parentId.Value.ToString()!=sample)throw new InvalidOperationException("Raid stash parent ID marshalling failed");plugin.Log.LogInfo("Raid stash parent ID marshalling verified");
  enabled=FeatureConfig.Bind("59. 战局仓库","Access stash in raid","战局 Tab 右侧访问仓库",true);
  var show=typeof(InventoryScreen).GetMethods().Single(m=>m.Name=="Show"&&m.GetParameters().Length==10);
  plugin.Patches.Patch(show,prefix:new HarmonyMethod(typeof(RaidStash),nameof(BeforeShow)));
  plugin.Patches.Patch(AccessTools.Method(typeof(InventoryScreen),"Close"),postfix:new HarmonyMethod(typeof(RaidStash),nameof(Closed)));
  plugin.Patches.Patch(AccessTools.Method(typeof(Player.PlayerInventoryController),"FindItemById",new[]{typeof(MongoID),typeof(bool),typeof(bool)}),prefix:new HarmonyMethod(typeof(RaidStash),nameof(FindItem)));
  plugin.Patches.Patch(typeof(LocalGame).GetMethod("Stop",new[]{typeof(string),typeof(ExitStatus),typeof(string),typeof(float)}),prefix:new HarmonyMethod(typeof(RaidStash),nameof(BeforeStop)));
 }
 static Player? Local=>Singleton<GameWorld>.Instance?.MainPlayer;
 static bool Own(InventoryController? controller)=>ready&&controller!=null&&controller.Pointer==Local?.InventoryController?.Pointer&&Local?.Pointer==actor;
 static void BeforeShow(InventoryController __1,ref CompoundItem __5,ref bool __9){try{if(!enabled.Value||!Own(__1)||!RaidClock.Active||__5!=null)return;__5=stash!;__9=false;}catch(Exception e){Plugin.Self.Error(e);}}
 static bool FindItem(Player.PlayerInventoryController __instance,MongoID __0,ref Option<Item> __result){try{if(Own(__instance)&&stash!=null&&stash.TryFindItem(__0.ToString(),out var item)&&item!=null){__result=new Option<Item>(item);return false;}}catch(Exception e){Plugin.Self.Error(e);}return true;}
 static void Closed(){try{if(ready)Save();}catch(Exception e){Fail(e.Message);}}
 static Task<string> Call(Dictionary<string,object?> payload)=>CharacterDataPanel.Call("/local/gear/data","characterData",payload);
 static string Snapshot(){var flat=Singleton<ItemFactory>.Instance.TreeToFlatItems(new Item[]{stash!});var json=System.Text.Json.Nodes.JsonNode.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(new Il2CppSystem.Object(flat.Pointer)))!.AsArray();foreach(var entry in json){if(entry?["_id"]?.GetValue<string>()!=stash!.Id.ToString())continue;foreach(string key in new[]{"parentId","slotId","location"}){entry.AsObject().Remove(key);if(stashRoot.TryGetProperty(key,out var value))entry[key]=System.Text.Json.Nodes.JsonNode.Parse(value.GetRawText());}}return json.ToJsonString();}
 static void Save(){if(!ready||request!=null||Local?.InventoryController?.HasActiveEvents==true)return;string current=Snapshot();if(current==saved)return;sent=current;request=Call(new(){{"action","saveRaidStash"},{"stashRevision",revision},{"stashItems",JsonSerializer.Deserialize<JsonElement>(current)}});}
 static bool BeforeStop(LocalGame __instance,string __0,ExitStatus __1,string __2,float __3){
  if(stopping||!ready||__0!=Local?.ProfileId)return true;
  if(__1==ExitStatus.MissingInAction&&RaidClock.PreventTimeout.Value&&!RaidEnd.ManualEnding&&RaidClock.Remaining is <=1)return true;
  try{Save();if(request==null&&Local?.InventoryController?.HasActiveEvents!=true)return true;finishStop=()=>{stopping=true;try{__instance.Stop(__0,__1,__2,__3);}finally{stopping=false;}};return false;}catch(Exception e){Fail(e.Message);return false;}
 }
 static void Load(JsonElement data){
  var factory=Singleton<ItemFactory>.Instance;var items=data.GetProperty("items");stashRoot=items.EnumerateArray().Single(i=>i.GetProperty("_id").GetString()==data.GetProperty("stash").GetString()).Clone();var flat=new Il2CppReferenceArray<JsonType.FlatItem>(items.GetArrayLength());int index=0;
  foreach(var item in items.EnumerateArray()){
   var native=new JsonType.FlatItem{_id=new MongoID(item.GetProperty("_id").GetString()!),_tpl=new MongoID(item.GetProperty("_tpl").GetString()!)};
   if(item.TryGetProperty("parentId",out var parent)&&parent.ValueKind==JsonValueKind.String)native.parentId=ParentId(parent.GetString()!);
   if(item.TryGetProperty("slotId",out var slot)&&slot.ValueKind==JsonValueKind.String)native.slotId=slot.GetString();
   if(item.TryGetProperty("location",out var location)&&location.ValueKind!=JsonValueKind.Null)native.location=new UnparsedData{JToken=JToken.Parse(location.GetRawText())};
   if(item.TryGetProperty("upd",out var upd)&&upd.ValueKind!=JsonValueKind.Null)native.upd=new UnparsedData{JToken=JToken.Parse(upd.GetRawText())};
   flat[index++]=native;
  }
  var tree=factory.FlatItemsToTree(flat,false,null);if(tree.DeserializationErrors?.Count>0)throw new InvalidOperationException("仓库物品无法完整读取。");stash=tree.Items[data.GetProperty("stash").GetString()!].TryCast<Stash>()??throw new InvalidOperationException("仓库读取失败。");
  if(stash.Id.ToString()!=data.GetProperty("stash").GetString())throw new InvalidOperationException("仓库编号不一致。");
  stash.CurrentAddress=Local!.InventoryController.CreateItemAddress();revision=data.GetProperty("revision").GetString()!;saved=Snapshot();ready=true;
  FeatureToasts.Show("仓库已就绪，按 Tab 访问");Plugin.Self.Log.LogInfo("Raid stash ready; items="+flat.Length);
 }
 static void Fail(string reason){FeatureToasts.Show("战局仓库："+reason);Plugin.Self.Log.LogWarning("Raid stash: "+reason);}
 internal static void Tick(){try{
  var local=Local;if(!RaidClock.Active||local?.IsYourPlayer!=true){if(request==null||request.IsCompleted)Reset();return;}
  if(actor!=local.Pointer){if(request!=null&&!request.IsCompleted)return;Reset();actor=local.Pointer;}
  if(request?.IsCompleted==true){bool wasLoading=loading;request.GetAwaiter().GetResult();using var doc=JsonDocument.Parse(request.Result);request=null;loading=false;var root=doc.RootElement;
   if(root.TryGetProperty("err",out var error)&&error.GetInt32()!=0){Fail(root.TryGetProperty("errmsg",out var message)?message.GetString()??"保存失败":"保存失败");next=Time.unscaledTime+15;return;}
   var data=CharacterDataPanel.Data(root);if(wasLoading)Load(data);else{revision=data.GetProperty("revision").GetString()!;saved=sent;}
   if(finishStop!=null){Save();if(request==null&&Local?.InventoryController?.HasActiveEvents!=true){var stop=finishStop;finishStop=null;stop();}return;}
  }
  if((!enabled.Value&&!ready)||Time.unscaledTime<next)return;
  if(!ready&&request==null&&local.Side!=EPlayerSide.Savage){loading=true;request=Call(new(){{"action","raidStash"}});next=Time.unscaledTime+15;}
  else if(ready&&finishStop!=null&&request==null){Save();if(request==null&&Local?.InventoryController?.HasActiveEvents!=true){var stop=finishStop;finishStop=null;stop();}}
   if(ready&&request==null&&Time.unscaledTime>=check&&CommonUI.Instance?.InventoryScreen?.gameObject.activeInHierarchy==true&&CommonUI.Instance.InventoryScreen._lootItem?.Pointer==stash?.Pointer){check=Time.unscaledTime+2;Save();}
 }catch(Exception e){request=null;loading=false;next=Time.unscaledTime+15;Fail(e.Message);}}
 internal static void Reset(){bool wasReady=ready;ready=false;if(wasReady&&stash!=null&&CommonUI.Instance?.InventoryScreen?._lootItem?.Pointer==stash.Pointer)CommonUI.Instance.InventoryScreen.Close();request=null;loading=ready=false;stash=null;actor=IntPtr.Zero;revision=saved=sent="";finishStop=null;next=0;}
 internal static void Shutdown(){if(ready&&request==null)Save();if(request!=null){try{using var document=JsonDocument.Parse(request.GetAwaiter().GetResult());if(document.RootElement.TryGetProperty("err",out var error)&&error.GetInt32()!=0)Plugin.Self.Log.LogError("Raid stash save failed during unload");}catch(Exception e){Plugin.Self.Error(e);}request=null;}Reset();}
}
