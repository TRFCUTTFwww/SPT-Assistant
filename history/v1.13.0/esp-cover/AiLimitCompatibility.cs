using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using EFT;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AiLimitCompatibility {
 static ConfigEntry<bool> keepBosses=null!;static FieldInfo? botList,mapping,disabled;static float nextCheck,nextError;static int repaired;
 sealed class Removed {internal IList? List;internal readonly List<object> Items=new();}
 internal static void Initialize(Plugin plugin){keepBosses=FeatureConfig.Bind("34. AI 测试","Keep bosses active","BOSS／黑色军团不受 AILimit 限流",true,"让已完成生成的 BOSS、随从与黑色军团保持运行；不修改它们的阵营仇恨规则。");try{var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("AILimit.AILimitComponent")).FirstOrDefault(t=>t!=null);if(type==null)return;botList=AccessTools.Field(type,"botList");mapping=AccessTools.Field(type,"playerInfoMapping");disabled=AccessTools.Field(type,"disabledBotsLastFrame");plugin.Patches.Patch(AccessTools.Method(type,"UpdateBots"),prefix:new HarmonyMethod(typeof(AiLimitCompatibility),nameof(BeforeLimit)),finalizer:new HarmonyMethod(typeof(AiLimitCompatibility),nameof(AfterLimit)));plugin.Patches.Patch(AccessTools.Method(type,"UpdateBotsWithDisabledList"),prefix:new HarmonyMethod(typeof(AiLimitCompatibility),nameof(BeforeDisable)));plugin.Log.LogInfo("AILimit compatibility ready: completed bosses/followers/Black Division exempted without changing ordinary bot limits");}catch(Exception e){plugin.Log.LogWarning("AILimit compatibility unavailable: "+e.Message);}}
 static Player? PlayerFor(object item){var id=item.GetType().GetProperty("Id")?.GetValue(item);var dict=mapping?.GetValue(null) as IDictionary;if(id==null||dict==null||!dict.Contains(id))return null;var info=dict[id];return info?.GetType().GetProperty("Player")?.GetValue(info) as Player;}
 static bool Exempt(object item){if(!keepBosses.Value)return false;var p=PlayerFor(item);if(p==null||p.HealthController?.IsAlive!=true)return false;var role=p.Profile?.Info?.Settings?.Role.ToString()??"";return AiBehaviorRules.BossFamily(role)&&(item.GetType().GetProperty("eligibleNow")?.GetValue(item) is true);}
 static void BeforeLimit(out Removed __state){__state=new();try{if(botList?.GetValue(null) is not IList list)return;__state.List=list;for(int i=list.Count-1;i>=0;i--)if(list[i] is object item&&Exempt(item)){__state.Items.Add(item);list.RemoveAt(i);Wake(PlayerFor(item));}}catch(Exception e){Warn(e);}}
 static void AfterLimit(Removed? __state){if(__state?.List==null)return;foreach(var item in __state.Items)if(!__state.List.Contains(item))__state.List.Add(item);}
 static void BeforeDisable(object __instance){try{if(disabled?.GetValue(__instance) is not IList list)return;for(int i=list.Count-1;i>=0;i--)if(list[i] is object item&&Exempt(item)){list.RemoveAt(i);Wake(PlayerFor(item));}}catch(Exception e){Warn(e);}}
 static void Wake(Player? p){if(p==null||p.HealthController?.IsAlive!=true)return;var owner=p.AIData?.BotOwner;if(owner==null||owner.StandBy==null)return;if(!p.gameObject.activeSelf||owner.StandBy.StandByType==BotStandByType.paused){owner.StandBy.Activate();owner.StandBy._nextCheckTime=Time.time+10;p.gameObject.SetActive(true);owner.BotState=EBotState.Active;}}
 internal static void Tick(GameWorld? world){if(world==null||Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+1;try{var controller=Comfort.Common.Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController;var covers=controller?.CoversData;if(covers==null||covers.VoxelesArray==null)return;var all=world.AllAlivePlayersList;if(all==null)return;for(int i=0;i<all.Count;i++){var p=all[i];var owner=p?.AIData?.BotOwner;var voxel=owner?.VoxelesPersonalData;if(owner==null||voxel==null||owner.BotState!=EBotState.Active)continue;if((voxel._data==null||voxel._data.VoxelesArray==null)&&covers.GetVoxelSafe(p!.Position,false)!=null){voxel.Activate(covers);repaired++;Plugin.Self.Log.LogInfo("AI navigation personal data restored; total="+repaired);}}}catch(Exception e){Warn(e);}}
 internal static bool HasNavigation(AICoversData? data,Vector3 position){try{return data!=null&&data.VoxelesArray!=null&&data.GetVoxelSafe(position,false)!=null;}catch{return false;}}
 internal static void Draw(Rect rect){if(InterfaceMenu.Button(rect,keepBosses.Value?"BOSS 免限流：开":"BOSS 免限流：关"))keepBosses.Value=!keepBosses.Value;}
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Log.LogWarning("AI compatibility: "+e.Message);}
}
