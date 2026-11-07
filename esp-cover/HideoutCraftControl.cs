using System;
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
 static Task<string>? request;
 static ProductionController? controller;
 static JsonElement? sync;
 static float next;
 static string status="",requestProfile="";
 static bool draining;
 internal static void Initialize(Plugin plugin){instant=FeatureConfig.Bind("60. 藏身处制作","Instant hideout crafts","藏身处制作秒完成",false);plugin.Patches.Patch(AccessTools.Method(typeof(ProductionController),"Update"),postfix:new HarmonyMethod(typeof(HideoutCraftControl),nameof(Observe)));}
 static Profile? Profile(){var app=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>();return app?.GetProfileForLocalGame();}
 static void Observe(ProductionController __instance){controller=__instance;try{Apply();}catch(Exception error){sync=null;Plugin.Self.Error(error);}}
 static void Begin(){if(request!=null||ItemSpawner.InRaid()||draining)return;var profile=Profile();if(profile==null){status="角色尚未就绪，请稍后重试。";next=Time.unscaledTime+2;return;}requestProfile=profile.Id;request=CharacterDataPanel.Call("/local/gear/data","characterData",new(){{"action","finishCrafts"}});next=Time.unscaledTime+2;}
 internal static void Tick(){try{
  if(request?.IsCompleted==true){var completed=request;request=null;using var doc=JsonDocument.Parse(completed.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0){status=root.GetProperty("errmsg").GetString()??"制作处理失败";next=Time.unscaledTime+10;return;}
   var data=CharacterDataPanel.Data(root);
   if(ItemSpawner.InRaid()||Profile()?.Id!=requestProfile||!data.TryGetProperty("profileId",out var owner)||owner.GetString()!=requestProfile){sync=null;status="制作同步已过期，请刷新。";return;}
   sync=data.Clone();int count=data.GetProperty("completed").GetInt32();status=count>0?$"已完成 {count} 项制作，可前往领取。":"当前没有需要完成的普通制作。";
  }
  if(!draining&&instant.Value&&!ItemSpawner.InRaid()&&Time.unscaledTime>=next)Begin();
 }catch(Exception error){request=null;sync=null;next=Time.unscaledTime+10;status="制作同步失败，请重试。";Plugin.Self.Error(error);}}
 static void Apply(){
  if(sync is not {} data||controller==null)return;
  var profile=Profile();if(ItemSpawner.InRaid()||profile?.Id!=requestProfile){sync=null;return;}var production=profile.Hideout?.Production;if(production==null)return;
  var updates=new Il2CppSystem.Collections.Generic.Dictionary<string,ProductionData>();int applied=0;
  foreach(var row in data.GetProperty("productions").EnumerateArray()){
   string id=row.GetProperty("id").GetString()!;if(!production.ContainsKey(id))continue;var p=production[id];
   long start=row.GetProperty("startTimestamp").GetInt64();int duration=row.GetProperty("productionTime").GetInt32();double progress=row.GetProperty("progress").GetDouble();if(!row.GetProperty("availableForFinish").GetBoolean())continue;
   if(!HideoutCraftSyncRules.CanApply(profile.Id,requestProfile,ItemSpawner.InRaid(),p.InProgress,p.Interrupted,p.StartTimestamp,start,p.ProductionTime,duration,progress))continue;
   // Equal profile progress still needs a live producer refresh. In 1.3 the
   // native producer also consumes AvailableForFinish, separately from progress.
   p.Progress=duration;p.AvailableForFinish=true;
   updates[id]=new ProductionData{Progress=duration,ProductionTime=duration,StartTimestamp=p.StartTimestamp,InProgress=p.InProgress,RecipeId=p.RecipeId,Products=p.Products,Interrupted=p.Interrupted,Code=p.Code,Decoded=p.Decoded,AvailableForFinish=true};applied++;
  }
  sync=null;if(applied==0)return;controller.UpdateProductions(updates);status=$"已同步 {applied} 项制作，可前往领取。";Plugin.Self.Log.LogInfo($"Hideout craft sync: completed native producer data={applied}; profile/start/duration validated");
 }
 internal static bool PrepareForReload(){draining=true;return request==null;}
 internal static void Draw(Rect rect){if(InterfaceMenu.Button(new Rect(rect.x,rect.y,Math.Max(180,rect.width-178),32),"制作秒完成 · "+(instant.Value?"开启":"关闭"),instant.Value))instant.Value=!instant.Value;if(InterfaceMenu.Button(new Rect(rect.xMax-166,rect.y,166,32),request==null?"完成当前制作":"处理中…")&&request==null)Begin();InterfaceMenu.Label(new Rect(rect.x,rect.y+38,rect.width,25),status.Length>0?status:"普通物品制作；材料照常消耗，完成后手动领取。",null,12);}
 internal static void Shutdown(){draining=true;request=null;sync=null;controller=null;}
}