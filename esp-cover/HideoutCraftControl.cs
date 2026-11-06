using System;
using System.Collections.Generic;
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
 static ConfigEntry<bool> instant=null!;static Task<string>? request;static ProductionController? controller;static float next;static string status="";static bool pendingSync;
 internal static void Initialize(Plugin plugin){instant=FeatureConfig.Bind("60. 藏身处制作","Instant hideout crafts","藏身处制作秒完成",false);plugin.Patches.Patch(AccessTools.Method(typeof(ProductionController),"Update"),postfix:new HarmonyMethod(typeof(HideoutCraftControl),nameof(Observe)));}
 static void Observe(ProductionController __instance){controller=__instance;if(pendingSync){var app=Singleton<TarkovApplication>.Instance??UnityEngine.Object.FindObjectOfType<TarkovApplication>();var production=app?.GetProfileForLocalGame()?.Hideout?.Production;if(production!=null){pendingSync=false;controller.UpdateProductions(production);}}}
 static void Begin(){if(request==null){request=CharacterDataPanel.Call("/local/gear/data","characterData",new(){{"action","finishCrafts"}});next=Time.unscaledTime+2;}}
 internal static void Tick(){try{
  if(request?.IsCompleted==true){using var doc=JsonDocument.Parse(request.GetAwaiter().GetResult());request=null;var root=doc.RootElement;if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0){status=root.GetProperty("errmsg").GetString()??"制作处理失败";next=Time.unscaledTime+10;return;}
   var data=CharacterDataPanel.Data(root);int count=data.GetProperty("completed").GetInt32();if(count>0)status=$"已完成 {count} 项制作，可前往领取。";
   if(!ItemSpawner.InRaid()){
    var app=Singleton<TarkovApplication>.Instance??UnityEngine.Object.FindObjectOfType<TarkovApplication>();var production=app?.GetProfileForLocalGame()?.Hideout?.Production;
    if(production!=null){bool changed=false;foreach(var row in data.GetProperty("productions").EnumerateArray()){string id=row.GetProperty("id").GetString()!;if(!production.ContainsKey(id))continue;var p=production[id];float progress=(float)row.GetProperty("progress").GetDouble();if(progress<=p.Progress)continue;p.Progress=progress;changed=true;}if(changed){pendingSync=true;if(controller!=null){controller.UpdateProductions(production);pendingSync=false;}}}
   }
  }
  if(instant.Value&&!ItemSpawner.InRaid()&&Time.unscaledTime>=next)Begin();
 }catch(Exception e){request=null;next=Time.unscaledTime+10;status="制作同步失败，请重试。";Plugin.Self.Error(e);}}
 internal static void Draw(Rect rect){if(InterfaceMenu.Button(new Rect(rect.x,rect.y,Math.Max(180,rect.width-178),32),"制作秒完成 · "+(instant.Value?"开启":"关闭"),instant.Value))instant.Value=!instant.Value;if(InterfaceMenu.Button(new Rect(rect.xMax-166,rect.y,166,32),request==null?"完成当前制作":"处理中…")&&request==null)Begin();InterfaceMenu.Label(new Rect(rect.x,rect.y+38,rect.width,25),status.Length>0?status:"普通物品制作；材料照常消耗，完成后手动领取。",null,12);}
 internal static void Shutdown(){if(request!=null)try{request.GetAwaiter().GetResult();}catch{}request=null;controller=null;pendingSync=false;}
}
