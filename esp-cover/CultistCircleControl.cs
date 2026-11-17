using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BepInEx.Configuration;
using EFT.Hideout;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class CultistCircleControl {
 static ConfigEntry<bool> preview=null!,instant=null!;
 sealed record Capture(CultistsProducer Producer,ProducingItemController Item,ProducingItemController.ProducingProcess Process);
 static readonly Dictionary<string,Capture> captures=new();
 static Task<string>? request;static JsonElement? snapshot;static string owner="",requestedOwner="",status="";static bool finishing,draining;static float next,visibleUntil;static Vector2 scroll;
 internal static void Initialize(Plugin plugin){
  preview=FeatureConfig.Bind("69. 仪式圈","Preview cultist rewards","提前预览仪式圈奖励",true,"献祭开始后查看本次已生成的奖励；不会重新抽取或提前领取。");
  instant=FeatureConfig.Bind("69. 仪式圈","Instant cultist circle","仪式圈自动秒完成",false,"献祭材料照常消耗；仪式完成后仍需手动领取奖励。");
 }
 static void Begin(bool finish){
  if(request!=null||draining||ItemSpawner.InRaid())return;var profile=HideoutCraftControl.CurrentProfile;if(profile==null){status="角色尚未就绪，请稍后重试。";return;}
  var payload=new Dictionary<string,object?>{{"action",finish?"finishCultist":"cultistStatus"}};
  if(finish){if(snapshot is not {} data||!data.GetProperty("active").GetBoolean()||owner!=profile.Id)return;payload["id"]=data.GetProperty("production").GetProperty("id").GetString()!;payload["revision"]=data.GetProperty("revision").GetString()!;}
  requestedOwner=profile.Id;finishing=finish;captures.Clear();
  if(HideoutCraftControl.CurrentController?._areaProducers is {} areas)foreach(var area in areas)foreach(var raw in area.Value){var producer=raw.TryCast<CultistsProducer>();if(producer==null)continue;foreach(var pair in producer.ProducingItems)if(pair.Value._producingProcess is {} process)captures[pair.Key]=new(producer,pair.Value,process);}
  request=CharacterDataPanel.Call("/local/gear/data","characterData",payload);next=Time.unscaledTime+3;
 }
 internal static void Tick(){try{
  var profile=HideoutCraftControl.CurrentProfile;
  if(ItemSpawner.InRaid()||owner.Length>0&&profile?.Id!=owner){snapshot=null;owner="";}
  if(request?.IsCompleted==true){var done=request;request=null;using var doc=JsonDocument.Parse(done.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0)throw new InvalidOperationException(root.GetProperty("errmsg").GetString());
   var data=CharacterDataPanel.Data(root);
   if(ItemSpawner.InRaid()||profile?.Id!=requestedOwner||data.GetProperty("profileId").GetString()!=requestedOwner){snapshot=null;status="仪式状态已过期，请刷新。";return;}
   owner=requestedOwner;snapshot=data.Clone();status=data.GetProperty("active").GetBoolean()?data.GetProperty("production").GetProperty("availableForFinish").GetBoolean()?"仪式已完成，请前往仪式圈领取奖励。":"仪式进行中，可以预览本次奖励。":"当前没有进行中的仪式。";
   if(finishing)Apply(data);captures.Clear();
  }
  if(!draining&&!ItemSpawner.InRaid()&&Time.unscaledTime>=next&&(instant.Value||preview.Value&&Time.unscaledTime<visibleUntil)){
   bool finish=instant.Value&&snapshot is {} s&&owner==profile?.Id&&s.GetProperty("active").GetBoolean()&&!s.GetProperty("production").GetProperty("availableForFinish").GetBoolean();Begin(finish);
  }
 }catch(Exception error){request=null;snapshot=null;captures.Clear();next=Time.unscaledTime+10;status="仪式圈请求失败，请刷新重试。";Plugin.Self.Error(error);}}
 static void Apply(JsonElement data){
  if(!data.GetProperty("active").GetBoolean())return;var row=data.GetProperty("production");string id=row.GetProperty("id").GetString()!;
  if(!captures.TryGetValue(id,out var captured)||!captured.Producer.ProducingItems.TryGetValue(id,out var live)){status="服务端仪式已完成；重新打开藏身处可刷新领取状态。";return;}
  var profile=HideoutCraftControl.CurrentProfile;long start=row.GetProperty("startTimestamp").GetInt64();int duration=row.GetProperty("productionTime").GetInt32();
  if(!CultistCircleSyncRules.CanApply(profile?.Id??"",requestedOwner,ItemSpawner.InRaid(),captured.Item.Pointer.ToInt64(),live.Pointer.ToInt64(),captured.Process.Pointer.ToInt64(),live._producingProcess?.Pointer.ToInt64()??0,live._producingProcess?.BaseProductionTime??0,start,duration,row.GetProperty("availableForFinish").GetBoolean(),row.GetProperty("inProgress").GetBoolean())){status="服务端仪式已完成；重新打开藏身处可刷新领取状态。";return;}
  if(!captured.Producer.TryUpdateProduction(id,new ProductionData{RecipeId=id,StartTimestamp=checked((int)start),ProductionTime=duration,Progress=0,InProgress=false,AvailableForFinish=true})){status="服务端仪式已完成；重新打开藏身处可刷新领取状态。";return;}
  if(profile?.Hideout?.Production is {} productions&&productions.TryGetValue(id,out var p)&&p.StartTimestamp==start&&p.ProductionTime==duration&&!p.Interrupted){p.Progress=0;p.InProgress=false;p.AvailableForFinish=true;}
 }
 internal static void Draw(Rect rect){
  visibleUntil=Time.unscaledTime+1;float w=rect.width-24;scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,w,Math.Max(rect.height-1,340+(snapshot?.GetProperty("items").GetArrayLength()??0)*38)));
  try{InterfaceMenu.Label(new(4,0,w,32),"仪式圈",InterfaceMenu.Accent,20);InterfaceMenu.Paragraph(new(4,40,w-8,56),"献祭开始后查看本次已生成的奖励；不会重新抽取或提前领取。",null,13);
   if(InterfaceMenu.Button(new(4,104,w-8,36),I18n.Text("提前预览仪式圈奖励")+" · "+I18n.Text(preview.Value?"开启":"关闭"),preview.Value))preview.Value=!preview.Value;
   if(InterfaceMenu.Button(new(4,150,w-8,36),I18n.Text("仪式圈自动秒完成")+" · "+I18n.Text(instant.Value?"开启":"关闭"),instant.Value)){instant.Value=!instant.Value;next=0;}
   InterfaceMenu.Paragraph(new(4,194,w-8,44),"献祭材料照常消耗；仪式完成后仍需手动领取奖励。",null,12);
   bool enabled=GUI.enabled;GUI.enabled=enabled&&request==null&&!ItemSpawner.InRaid();try{if(InterfaceMenu.Button(new(4,244,(w-20)/2,36),"刷新仪式状态"))Begin(false);GUI.enabled=GUI.enabled&&snapshot is {} s&&s.GetProperty("active").GetBoolean()&&!s.GetProperty("production").GetProperty("availableForFinish").GetBoolean();if(InterfaceMenu.Button(new(12+(w-20)/2,244,(w-20)/2,36),"立即完成本次仪式"))Begin(true);}finally{GUI.enabled=enabled;}
   InterfaceMenu.Paragraph(new(4,290,w-8,44),ItemSpawner.InRaid()?"请回到主菜单后查看仪式圈。":request!=null?"正在读取或同步仪式状态…":status.Length>0?status:"刷新状态后可预览本次奖励。",null,13);
   if(preview.Value&&snapshot is {} current){float y=342;foreach(var item in current.GetProperty("items").EnumerateArray()){string tpl=item.GetProperty("tpl").GetString()!;InterfaceMenu.Label(new(4,y,w-8,34),LootDisplay.TemplateName(tpl)+" × "+item.GetProperty("count").GetRawText(),null,14,translate:false);y+=38;}}
  }finally{GUI.EndScrollView();}
 }
 internal static bool PrepareForReload(){draining=true;return request==null;}
 internal static void Shutdown(){draining=true;request=null;snapshot=null;captures.Clear();owner=requestedOwner="";}
}
