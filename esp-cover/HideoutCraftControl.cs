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
 static ConfigEntry<bool> instant=null!,instantUpgrade=null!;
 sealed record Craft(ItemsProducerBase Producer,ProducingItemController Item,ProducingItemController.ProducingProcess Process);
 static readonly Dictionary<string,Craft> requestedCrafts=new();
 static Task<string>? request,upgradeRequest;
 static ProductionController? controller;
 static JsonElement? sync,upgradeSync;
 static float next,upgradeNext;
 static string status="",requestProfile="",upgradeStatus="",upgradeProfile="";
 static bool draining;
 internal static void Initialize(Plugin plugin){instant=FeatureConfig.Bind("60. 藏身处制作","Instant hideout crafts","藏身处制作秒完成",false);instantUpgrade=FeatureConfig.Bind("60. 藏身处制作","Instant hideout upgrades","建造/升级秒完成",false);plugin.Patches.Patch(AccessTools.Method(typeof(ProductionController),"Update"),postfix:new HarmonyMethod(typeof(HideoutCraftControl),nameof(Observe)));}
 static TarkovApplication? application;
 static TarkovApplication? App()=>application!=null?application:application=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>()??UnityEngine.Object.FindObjectOfType<TarkovApplication>();
 internal static ProductionController? CurrentController=>App()?._hideout?.ProductionController??controller;
 internal static HideoutRepresentation? CurrentHideout=>App()?._hideout;
 internal static Profile? CurrentProfile=>Profile();
 // The native GetProfileForLocalGame assumes a loaded menu/matchmaker. It
 // can throw during login and character switches; wait for a live controller.
 static Profile? Profile(){
  var app=App();var menu=app?._menuOperation;
  var profile=menu?.QuestController?.Profile;
  if(profile==null&&Singleton<AbstractGame>.Instance?.TryCast<HideoutGame>()!=null)profile=EFT.UI.ItemUiContext.Instance?.QuestController?.Profile;
  return string.IsNullOrEmpty(profile?.Id)?null:profile;
 }
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
 static void Begin(){if(request!=null||sync.HasValue||ItemSpawner.InRaid()||draining||Time.unscaledTime<next)return;var profile=Profile();if(profile==null){status="角色尚未就绪，请稍后重试。";next=Time.unscaledTime+2;return;}requestProfile=profile.Id;requestedCrafts.Clear();controller=App()?._hideout?.ProductionController??controller;if(controller?._areaProducers is {} areas)foreach(var area in areas)foreach(var producer in area.Value)foreach(var pair in producer.ProducingItems){var process=pair.Value._producingProcess;if(process!=null)requestedCrafts[pair.Key]=new(producer,pair.Value,process);}status="正在完成并同步制作…";request=CharacterDataPanel.Call("/local/gear/data","characterData",new(){{"action","finishCrafts"}});next=Time.unscaledTime+2;}
 internal static void Tick(){
  TickUpgrades();
  try{
   if(Time.unscaledTime<next)return;
  if(request?.IsCompleted==true){
   var profile=Profile();if(!ItemSpawner.InRaid()&&profile==null){status="角色尚未就绪，制作结果将在就绪后同步。";next=Time.unscaledTime+2;return;}
   var completed=request;request=null;using var doc=JsonDocument.Parse(completed.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0){status=root.GetProperty("errmsg").GetString()??"制作处理失败";next=Time.unscaledTime+10;return;}
   var data=CharacterDataPanel.Data(root);
   if(ItemSpawner.InRaid()||profile?.Id!=requestProfile||!data.TryGetProperty("profileId",out var owner)||owner.GetString()!=requestProfile){sync=null;requestedCrafts.Clear();status="制作同步已过期，请刷新。";return;}
   sync=data.Clone();
  }
  if(sync.HasValue)Apply();
  if(!sync.HasValue&&!draining&&instant.Value&&!ItemSpawner.InRaid()&&Time.unscaledTime>=next)Begin();
 }catch(Exception error){requestedCrafts.Clear();request=null;sync=null;next=Time.unscaledTime+10;status="制作同步失败，请重试。";Plugin.Self.Error(error);}}
 static bool HasConstructingArea(){
  var areas=App()?._hideout?.Areas;if(areas==null)return false;
  int now=(int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
  foreach(var area in areas)if(area!=null&&area.Constructing&&(!area.CompleteTime.HasValue||area.CompleteTime.Value>now))return true;
  return false;
 }
 static void BeginUpgrades(){
  if(upgradeRequest!=null||upgradeSync.HasValue||ItemSpawner.InRaid()||draining||Time.unscaledTime<upgradeNext)return;
  var profile=Profile();if(profile==null){upgradeStatus="角色尚未就绪，请稍后重试。";upgradeNext=Time.unscaledTime+2;return;}
  upgradeProfile=profile.Id;upgradeStatus="正在完成并同步建造/升级…";
  upgradeRequest=CharacterDataPanel.Call("/local/gear/data","characterData",new(){{"action","finishUpgrades"}});
  upgradeNext=Time.unscaledTime+2;
 }
 static void TickUpgrades(){
  try{
   if(instantUpgrade==null)return;
   if(Time.unscaledTime<upgradeNext&&upgradeRequest?.IsCompleted!=true&&!upgradeSync.HasValue)return;
   if(upgradeRequest?.IsCompleted==true){
    var profile=Profile();if(!ItemSpawner.InRaid()&&profile==null){upgradeStatus="角色尚未就绪，建造状态将在就绪后同步。";upgradeNext=Time.unscaledTime+2;return;}
    var completed=upgradeRequest;upgradeRequest=null;using var doc=JsonDocument.Parse(completed.GetAwaiter().GetResult());var root=doc.RootElement;
    if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0){upgradeStatus=root.GetProperty("errmsg").GetString()??"建造同步失败";upgradeNext=Time.unscaledTime+10;return;}
    var data=CharacterDataPanel.Data(root);
    if(ItemSpawner.InRaid()||profile?.Id!=upgradeProfile||!data.TryGetProperty("profileId",out var owner)||owner.GetString()!=upgradeProfile){upgradeSync=null;upgradeStatus="建造同步已过期，请刷新。";return;}
    upgradeSync=data.Clone();
   }
   if(upgradeSync.HasValue)ApplyUpgrades();
   if(!upgradeSync.HasValue&&!draining&&instantUpgrade.Value&&!ItemSpawner.InRaid()&&Time.unscaledTime>=upgradeNext&&HasConstructingArea())BeginUpgrades();
  }catch(Exception error){upgradeRequest=null;upgradeSync=null;upgradeNext=Time.unscaledTime+10;upgradeStatus="建造同步失败，请重试。";Plugin.Self.Error(error);}
 }
 static void ApplyUpgrades(){
  if(upgradeSync is not {} data)return;
  var profile=Profile();if(!ItemSpawner.InRaid()&&profile==null){upgradeNext=Time.unscaledTime+2;return;}
  if(ItemSpawner.InRaid()||profile?.Id!=upgradeProfile){upgradeSync=null;return;}
  int completedCount=data.TryGetProperty("upgradesCompleted",out var uc)?uc.GetInt32():0;
  int applied=0;
  var hideout=App()?._hideout;
  if(data.TryGetProperty("areas",out var serverAreas)&&serverAreas.ValueKind==JsonValueKind.Array&&hideout!=null){
   var readyTimes=new Dictionary<int,int>();
   foreach(var row in serverAreas.EnumerateArray())if(row.TryGetProperty("constructing",out var c)&&c.GetBoolean())readyTimes[row.GetProperty("type").GetInt32()]=row.GetProperty("completeTime").GetInt32();
   if(hideout.Areas!=null)foreach(var area in hideout.Areas)if(area!=null&&area.Constructing&&readyTimes.TryGetValue(area.Type,out int targetTime)&&(!area.CompleteTime.HasValue||area.CompleteTime.Value>targetTime)){
    area.CompleteTime=new Il2CppSystem.Nullable<int>(targetTime);applied++;
   }
   if(hideout.AreaDatas!=null)foreach(var ad in hideout.AreaDatas)if(ad?.Template!=null&&readyTimes.ContainsKey((int)ad.Template.Type))ad.DecideStatus(ad.CurrentLevel);
  }
  upgradeSync=null;
  upgradeStatus=applied>0?$"已同步完成 {applied} 项区域建造/升级，可前往藏身处确认完成。":completedCount>0?"服务端已完成建造时间推进，进入藏身处即可结算。":"当前没有正在建造或升级的区域。";
 }
 static void Apply(){
  if(sync is not {} data)return;if(controller==null){next=Time.unscaledTime+2;return;}
  var profile=Profile();if(!ItemSpawner.InRaid()&&profile==null){next=Time.unscaledTime+2;return;}if(ItemSpawner.InRaid()||profile?.Id!=requestProfile){sync=null;requestedCrafts.Clear();return;}
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
 internal static int ReadyCount(){
  int count=0;
  if(controller?._areaProducers is {} areas)foreach(var area in areas)foreach(var producer in area.Value)count+=producer.CompleteItemsCount;
  return count;
 }
 internal static bool PrepareForReload(){draining=true;return request==null&&upgradeRequest==null;}
 internal static void Draw(Rect rect){
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y,Math.Max(180,rect.width-178),30),"制作秒完成 · "+(instant.Value?"开启":"关闭"),instant.Value))instant.Value=!instant.Value;
  if(InterfaceMenu.Button(new Rect(rect.xMax-166,rect.y,166,30),request==null?"完成当前制作":"处理中…")&&request==null)Begin();
  InterfaceMenu.Label(new Rect(rect.x,rect.y+32,rect.width,24),status.Length>0?status:"普通物品制作；材料照常消耗，完成后手动领取。",null,12);
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y+60,Math.Max(180,rect.width-178),30),"建造/升级秒完成 · "+(instantUpgrade.Value?"开启":"关闭"),instantUpgrade.Value))instantUpgrade.Value=!instantUpgrade.Value;
  if(InterfaceMenu.Button(new Rect(rect.xMax-166,rect.y+60,166,30),upgradeRequest==null?"完成建造/升级":"处理中…")&&upgradeRequest==null)BeginUpgrades();
  InterfaceMenu.Label(new Rect(rect.x,rect.y+92,rect.width,24),upgradeStatus.Length>0?upgradeStatus:"区域建造与升级需先扣材开工，秒完成只推进到期时间。",null,12);
 }
 internal static void Shutdown(){draining=true;request=null;sync=null;upgradeRequest=null;upgradeSync=null;upgradeStatus="";upgradeProfile="";requestedCrafts.Clear();controller=null;application=null;}
}
