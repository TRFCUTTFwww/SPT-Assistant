using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Hideout;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class CultistCircleControl {
 static ConfigEntry<bool> preview=null!,instant=null!,collectInHideout=null!;
 sealed record Capture(CultistsProducer Producer,ProducingItemController Item,ProducingItemController.ProducingProcess Process);
 static readonly Dictionary<string,Capture> captures=new();
 static Task<string>? request;static JsonElement? snapshot;static string owner="",requestedOwner="",status="";static bool draining;static float next,visibleUntil;static Vector2 scroll;
 internal static Action<JsonElement>? SyncStash;
 internal static Func<JsonElement,bool>? OpenStash;
 internal static bool InHideout=>Singleton<AbstractGame>.Instance?.TryCast<HideoutGame>()!=null||Singleton<GameWorld>.Instance?.TryCast<HideoutGameWorld>()!=null;
 static IEnumerable<CultistsProducer> CurrentProducers(){
  if(HideoutCraftControl.CurrentController?._areaProducers is not {} areas)yield break;
  foreach(var area in areas)foreach(var raw in area.Value)
   if(raw.TryCast<CultistsProducer>() is {} producer)yield return producer;
 }
 internal static void Initialize(Plugin plugin){
  preview=FeatureConfig.Bind("69. 仪式圈","Preview cultist rewards","提前预览仪式圈奖励",true,"献祭开始后查看本次已生成的奖励；不会重新抽取或提前领取。");
  instant=FeatureConfig.Bind("69. 仪式圈","Instant cultist circle","仪式圈自动秒完成",false,"献祭材料照常消耗；仪式完成后仍需手动领取奖励。");
  collectInHideout=FeatureConfig.Bind("69. 仪式圈","Allow cultist rewards in hideout","身处藏身处时可获得仪式圈物品",true,"在藏身处同步已完成仪式的领取状态，无需退出再进入；奖励仍由原生界面手动领取。");
 }
 static void Begin(bool finish){
  if(request!=null||draining||ItemSpawner.InRaid()||Time.unscaledTime<next)return;var profile=HideoutCraftControl.CurrentProfile;if(profile==null){status="角色尚未就绪，请稍后重试。";next=Time.unscaledTime+2;return;}
  var payload=new Dictionary<string,object?>{{"action",finish?"finishCultist":"cultistStatus"}};
  if(finish){if(snapshot is not {} data||!data.GetProperty("active").GetBoolean()||owner!=profile.Id)return;payload["id"]=data.GetProperty("production").GetProperty("id").GetString()!;payload["revision"]=data.GetProperty("revision").GetString()!;}
  requestedOwner=profile.Id;captures.Clear();
  foreach(var producer in CurrentProducers())foreach(var pair in producer.ProducingItems)
   if(pair.Value._producingProcess is {} process)captures[pair.Key]=new(producer,pair.Value,process);
  request=CharacterDataPanel.Call("/local/gear/data","characterData",payload);next=Time.unscaledTime+3;
 }
 internal static void Tick(){try{
  // Gate all native reads as well as requests after an error.
  if(Time.unscaledTime<next||request is {IsCompleted:false})return;
  bool watchHideout=collectInHideout.Value&&InHideout;
  if(request==null&&(ItemSpawner.InRaid()||!instant.Value&&!watchHideout&&(!preview.Value||Time.unscaledTime>=visibleUntil))){snapshot=null;owner="";return;}
  var profile=HideoutCraftControl.CurrentProfile;
  if(!ItemSpawner.InRaid()&&profile==null){status="角色尚未就绪，请稍后重试。";next=Time.unscaledTime+2;return;}
  if(ItemSpawner.InRaid()||owner.Length>0&&profile?.Id!=owner){snapshot=null;owner="";}
  if(request?.IsCompleted==true){var done=request;request=null;using var doc=JsonDocument.Parse(done.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0)throw new InvalidOperationException(root.GetProperty("errmsg").GetString());
   var data=CharacterDataPanel.Data(root);
   if(ItemSpawner.InRaid()||profile?.Id!=requestedOwner||data.GetProperty("profileId").GetString()!=requestedOwner){snapshot=null;status="仪式状态已过期，请刷新。";return;}
   bool active=data.GetProperty("active").GetBoolean();
   bool ready=active&&Ready(data.GetProperty("production"));
   owner=requestedOwner;snapshot=data.Clone();status=active?ready?"仪式已完成，请前往仪式圈领取奖励。":"仪式进行中，可以预览本次奖励。":"当前没有进行中的仪式。";
   if(ready&&!Apply(data,profile!))status="服务端仪式已完成；正在同步藏身处领取状态。";
   if(watchHideout&&(!active||ready))SyncStash?.Invoke(data);captures.Clear();
  }
  if(!draining&&!ItemSpawner.InRaid()&&Time.unscaledTime>=next&&(instant.Value||watchHideout||preview.Value&&Time.unscaledTime<visibleUntil)){
   bool finish=instant.Value&&snapshot is {} s&&owner==profile?.Id&&s.GetProperty("active").GetBoolean()&&!Ready(s.GetProperty("production"));Begin(finish);
  }
 }catch(Exception error){request=null;snapshot=null;captures.Clear();next=Time.unscaledTime+10;status="仪式圈请求失败，请刷新重试。";Plugin.Self.Error(error);}}
 static bool Ready(JsonElement row)=>row.GetProperty("availableForFinish").GetBoolean()&&!row.GetProperty("inProgress").GetBoolean();
 static bool CanOpenStash(JsonElement data)=>
  (!data.GetProperty("active").GetBoolean()||Ready(data.GetProperty("production")))&&
  data.TryGetProperty("stashItems",out var items)&&items.ValueKind==JsonValueKind.Array&&items.GetArrayLength()>0;
 static void OpenRewards(){
  try{
   if(draining||!collectInHideout.Value||!InHideout||ItemSpawner.InRaid()||snapshot is not {} data||
      HideoutCraftControl.CurrentProfile?.Id!=owner||!CanOpenStash(data)||OpenStash?.Invoke(data)!=true){
    status="仪式圈奖励尚未同步到当前物品栏。";return;
   }
   InterfaceMenu.Close();
  }catch(Exception error){status="无法打开仪式圈物品转移界面。";Plugin.Self.Error(error);}
 }
 static bool Apply(JsonElement data,Profile profile){
  var row=data.GetProperty("production");string id=row.GetProperty("id").GetString()!;
  long start=row.GetProperty("startTimestamp").GetInt64();int duration=row.GetProperty("productionTime").GetInt32();
  if(profile.Hideout?.Production is {} productions&&productions.TryGetValue(id,out var local)&&
     (local.StartTimestamp!=start||local.Interrupted))return false;
  foreach(var producer in CurrentProducers()){
   if(producer.CompleteItemsStorage?.GetItemsCount(id)>0)return true;
   if(!captures.TryGetValue(id,out var captured)||producer.Pointer!=captured.Producer.Pointer||
      !producer.ProducingItems.TryGetValue(id,out var live)||live._producingProcess is not {} process)continue;
   if(!CultistCircleSyncRules.CanApply(profile.Id,requestedOwner,ItemSpawner.InRaid(),captured.Item.Pointer.ToInt64(),live.Pointer.ToInt64(),captured.Process.Pointer.ToInt64(),process.Pointer.ToInt64(),process.BaseProductionTime,start,duration,row.GetProperty("availableForFinish").GetBoolean(),row.GetProperty("inProgress").GetBoolean()))return false;
   if(!live.AvailableForFinish||process.IsInProgress){
    // The live controller derives its timer from Progress / ProductionTime.
    // The persisted SPT ritual uses its own zero-progress completion marker.
    var update=new ProductionData{RecipeId=id,StartTimestamp=checked((int)start),ProductionTime=duration,Progress=duration,InProgress=false,AvailableForFinish=true};
    if(!producer.TryUpdateProduction(id,update))return false;
   }
   if(!live.AvailableForFinish||process.IsInProgress)return false;
   if(profile.Hideout?.Production is {} current&&current.TryGetValue(id,out var p)&&p.StartTimestamp==start&&!p.Interrupted){
    p.ProductionTime=duration;p.Progress=0;p.InProgress=false;p.AvailableForFinish=true;
   }
   return true;
  }
  return false;
 }
 internal static void Draw(Rect rect){
  visibleUntil=Time.unscaledTime+1;float w=rect.width-24;
  bool showRewards=!draining&&collectInHideout.Value&&InHideout&&!ItemSpawner.InRaid()&&snapshot is {} rewards&&CanOpenStash(rewards);
  float itemsTop=showRewards?438:394;
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,w,Math.Max(rect.height-1,itemsTop+(snapshot?.GetProperty("items").GetArrayLength()??0)*38)));
  try{InterfaceMenu.Label(new(4,0,w,32),"仪式圈",InterfaceMenu.Accent,20);InterfaceMenu.Paragraph(new(4,40,w-8,56),"献祭开始后查看本次已生成的奖励；不会重新抽取或提前领取。",null,13);
   if(InterfaceMenu.Button(new(4,104,w-8,36),I18n.Text("提前预览仪式圈奖励")+" · "+I18n.Text(preview.Value?"开启":"关闭"),preview.Value))preview.Value=!preview.Value;
   if(InterfaceMenu.Button(new(4,150,w-8,36),I18n.Text("仪式圈自动秒完成")+" · "+I18n.Text(instant.Value?"开启":"关闭"),instant.Value))instant.Value=!instant.Value;
   InterfaceMenu.Paragraph(new(4,194,w-8,44),"献祭材料照常消耗；仪式完成后仍需手动领取奖励。",null,12);
   if(InterfaceMenu.Button(new(4,244,w-8,36),I18n.Text("身处藏身处时可获得仪式圈物品")+" · "+I18n.Text(collectInHideout.Value?"开启":"关闭"),collectInHideout.Value))collectInHideout.Value=!collectInHideout.Value;
   bool enabled=GUI.enabled;GUI.enabled=enabled&&request==null&&!ItemSpawner.InRaid()&&Time.unscaledTime>=next;try{if(InterfaceMenu.Button(new(4,296,(w-20)/2,36),"刷新仪式状态"))Begin(false);GUI.enabled=GUI.enabled&&snapshot is {} s&&s.GetProperty("active").GetBoolean()&&!Ready(s.GetProperty("production"));if(InterfaceMenu.Button(new(12+(w-20)/2,296,(w-20)/2,36),"立即完成本次仪式"))Begin(true);}finally{GUI.enabled=enabled;}
   InterfaceMenu.Paragraph(new(4,342,w-8,44),ItemSpawner.InRaid()?"请回到主菜单或藏身处后查看仪式圈。":request!=null?"正在读取或同步仪式状态…":status.Length>0?status:"刷新状态后可预览本次奖励。",null,13);
   if(showRewards&&InterfaceMenu.Button(new(4,394,w-8,36),"打开仪式圈奖励转移"))OpenRewards();
   if(preview.Value&&snapshot is {} current){float y=itemsTop;foreach(var item in current.GetProperty("items").EnumerateArray()){string tpl=item.GetProperty("tpl").GetString()!;InterfaceMenu.Label(new(4,y,w-8,34),LootDisplay.TemplateName(tpl)+" × "+item.GetProperty("count").GetRawText(),null,14,translate:false);y+=38;}}
  }finally{GUI.EndScrollView();}
 }
 internal static CornerGameData.Cultist? ReadSnapshot(Profile profile){
  if(snapshot is not {} current||owner!=profile.Id)return null;
  bool active=current.GetProperty("active").GetBoolean();
  return new(active,active&&Ready(current.GetProperty("production")));
 }
 internal static bool PrepareForReload(){draining=true;return request==null;}
 internal static void Shutdown(){draining=true;request=null;snapshot=null;captures.Clear();owner=requestedOwner="";}
}
