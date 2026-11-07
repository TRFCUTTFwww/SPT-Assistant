using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Hideout;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class HideoutCraftControl {
 static ConfigEntry<bool> instant=null!;
 sealed record Craft(ItemsProducerBase Producer,ProducingItemController Item,ProducingItemController.ProducingProcess Process);
 static readonly Dictionary<string,Craft> requestedCrafts=new();
 static Task<string>? request;
 static ProductionController? controller;
 static JsonElement? sync;
 static float next;
 static string status="",requestProfile="";
 static bool draining;
 internal static void Initialize(Plugin plugin){instant=FeatureConfig.Bind("60. 藏身处制作","Instant hideout crafts","藏身处制作秒完成",false);plugin.Patches.Patch(AccessTools.Method(typeof(ProductionController),"Update"),postfix:new HarmonyMethod(typeof(HideoutCraftControl),nameof(Observe)));}
 static TarkovApplication? application;
 static TarkovApplication? App()=>application!=null?application:application=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>()??UnityEngine.Object.FindObjectOfType<TarkovApplication>();
 static Profile? Profile()=>(App()?._menuOperation?.QuestController??EFT.UI.ItemUiContext.Instance?.QuestController)?.Profile??App()?.GetProfileForLocalGame();
 internal static object Diagnostics(){
  var profile=Profile();var rows=new List<object>();
  if(profile?.Hideout?.Production is {} productions)foreach(var pair in productions){var p=pair.Value;rows.Add(new{id=pair.Key,p.Progress,p.StartTimestamp,p.ProductionTime,p.InProgress,p.Interrupted,p.AvailableForFinish,p.RecipeId});}
  var live=new List<object>();var storage=new List<object>();
  if(controller?._areaProducers is {} areas)foreach(var area in areas)foreach(var producer in area.Value){
   storage.Add(new{area=producer.AreaType.ToString(),producer.HasCompletedItems,producer.CompleteItemsCount});
   foreach(var item in producer.ProducingItems){var p=item.Value;live.Add(new{id=item.Key,pointer=p.Pointer.ToInt64(),p.Progress,p.AvailableForFinish,duration=p._producingProcess?.BaseProductionTime,left=p._producingProcess?.TimeLeft,inProgress=p._producingProcess?.IsInProgress});}
  }
  return new{live,storage,profile=profile?.Id,inRaid=ItemSpawner.InRaid(),observedController=controller!=null,currentController=App()?._hideout?.ProductionController!=null,requestPending=request!=null,pendingSync=sync.HasValue,status,rows};
 }
 static bool HasCompleted(string id){
  if(controller?._areaProducers is {} areas)foreach(var area in areas)foreach(var producer in area.Value)if(producer.Schemes.ContainsKey(id)&&producer.CompleteItemsStorage?.GetItemsCount(id)>0)return true;
  return false;
 }
 static void Observe(ProductionController __instance){controller=__instance;}
 static void Begin(){if(request!=null||ItemSpawner.InRaid()||draining)return;var profile=Profile();if(profile==null){status="角色尚未就绪，请稍后重试。";next=Time.unscaledTime+2;return;}requestProfile=profile.Id;requestedCrafts.Clear();controller=App()?._hideout?.ProductionController??controller;if(controller?._areaProducers is {} areas)foreach(var area in areas)foreach(var producer in area.Value)foreach(var pair in producer.ProducingItems){var process=pair.Value._producingProcess;if(process!=null)requestedCrafts[pair.Key]=new(producer,pair.Value,process);}status="正在完成并同步制作…";request=CharacterDataPanel.Call("/local/gear/data","characterData",new(){{"action","finishCrafts"}});next=Time.unscaledTime+2;}
 internal static void Tick(){try{
  if(request?.IsCompleted==true){var completed=request;request=null;using var doc=JsonDocument.Parse(completed.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0){status=root.GetProperty("errmsg").GetString()??"制作处理失败";next=Time.unscaledTime+10;return;}
   var data=CharacterDataPanel.Data(root);
   if(ItemSpawner.InRaid()||Profile()?.Id!=requestProfile||!data.TryGetProperty("profileId",out var owner)||owner.GetString()!=requestProfile){sync=null;status="制作同步已过期，请刷新。";return;}
   sync=data.Clone();Apply();
  }
  if(!draining&&instant.Value&&!ItemSpawner.InRaid()&&Time.unscaledTime>=next)Begin();
 }catch(Exception error){requestedCrafts.Clear();request=null;sync=null;next=Time.unscaledTime+10;status="制作同步失败，请重试。";Plugin.Self.Error(error);}}
 static void Apply(){
  if(sync is not {} data||controller==null)return;
  var profile=Profile();if(ItemSpawner.InRaid()||profile?.Id!=requestProfile){sync=null;requestedCrafts.Clear();return;}
  int ready=0,applied=0,stale=0,stored=0;
  foreach(var row in data.GetProperty("productions").EnumerateArray()){
   if(!row.GetProperty("availableForFinish").GetBoolean())continue;ready++;
   string id=row.GetProperty("id").GetString()!;if(HasCompleted(id)){stored++;continue;}
   if(!requestedCrafts.TryGetValue(id,out var captured)||!captured.Producer.ProducingItems.TryGetValue(id,out var live))continue;
   long start=row.GetProperty("startTimestamp").GetInt64();int duration=row.GetProperty("productionTime").GetInt32();double progress=row.GetProperty("progress").GetDouble();
   // The login profile omits crafts started in this session. Bind the response
   // to the exact live item AND process captured before sending the request.
   if(!HideoutCraftSyncRules.CanApplyLive(profile.Id,requestProfile,ItemSpawner.InRaid(),captured.Item.Pointer.ToInt64(),live.Pointer.ToInt64(),captured.Process.Pointer.ToInt64(),live._producingProcess?.Pointer.ToInt64()??0,live._producingProcess?.BaseProductionTime??0,start,duration,progress)){stale++;continue;}
   var update=new ProductionData{Progress=duration,ProductionTime=duration,StartTimestamp=checked((int)start),InProgress=true,RecipeId=id,AvailableForFinish=true};
   if(!captured.Producer.TryUpdateProduction(id,update)){stale++;continue;}
   var production=profile.Hideout?.Production;
   if(production!=null&&production.TryGetValue(id,out var p)&&HideoutCraftSyncRules.CanApply(profile.Id,requestProfile,false,p.InProgress,p.Interrupted,p.StartTimestamp,start,p.ProductionTime,duration,progress)){p.Progress=duration;p.AvailableForFinish=true;}
   applied++;
  }
  sync=null;requestedCrafts.Clear();
  status=stored>0?$"已有 {stored} 项制作可领取。":applied>0?$"已同步 {applied} 项制作，可前往领取。":stale>0?"制作已重新开始，已忽略旧结果，请重试。":ready>0?$"服务端已有 {ready} 项完成；本地没有匹配的进行中制作，请重新打开藏身处。":"当前没有需要完成的普通制作。";
  if(applied>0||stale>0)Plugin.Self.Log.LogInfo($"Hideout craft sync: serverReady={ready}, applied={applied}, stale={stale}; live item/process validated");
 }
 internal static bool PrepareForReload(){draining=true;return request==null;}
 internal static void Draw(Rect rect){if(InterfaceMenu.Button(new Rect(rect.x,rect.y,Math.Max(180,rect.width-178),32),"制作秒完成 · "+(instant.Value?"开启":"关闭"),instant.Value))instant.Value=!instant.Value;if(InterfaceMenu.Button(new Rect(rect.xMax-166,rect.y,166,32),request==null?"完成当前制作":"处理中…")&&request==null)Begin();InterfaceMenu.Label(new Rect(rect.x,rect.y+38,rect.width,25),status.Length>0?status:"普通物品制作；材料照常消耗，完成后手动领取。",null,12);}
 internal static void Shutdown(){draining=true;request=null;sync=null;requestedCrafts.Clear();controller=null;application=null;}
}