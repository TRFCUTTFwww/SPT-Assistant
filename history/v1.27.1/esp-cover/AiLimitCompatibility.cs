using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AiLimitCompatibility {
 static ConfigEntry<bool> keepBosses=null!;static FieldInfo? botList,mapping,disabled,botDistance;static float nextCheck,nextError;static int repaired;
 static readonly Dictionary<Type,PropertyInfo?> idProperties=new(),playerProperties=new(),eligibleProperties=new();
 static readonly Dictionary<object,Player?> exemptions=new();static int cachedFrame=-1;static Player? local;static float radiusSquared;
 sealed class Removed {internal IList? List;internal readonly List<object> Items=new();}
 internal static void Initialize(Plugin plugin){keepBosses=FeatureConfig.Bind("34. AI 测试","Keep bosses active","附近／交战 BOSS 保持活动",true,"仅在 AILimit 当前地图活动半径内，或正在看见、攻击玩家的 BOSS、随从及黑色军团绕过数量限流；远处仍可休眠，不修改阵营仇恨。");try{var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("AILimit.AILimitComponent")).FirstOrDefault(t=>t!=null);if(type==null)return;botList=AccessTools.Field(type,"botList");mapping=AccessTools.Field(type,"playerInfoMapping");disabled=AccessTools.Field(type,"disabledBotsLastFrame");botDistance=AccessTools.Field(type,"botDistance");plugin.Patches.Patch(AccessTools.Method(type,"UpdateBots"),prefix:new HarmonyMethod(typeof(AiLimitCompatibility),nameof(BeforeLimit)),finalizer:new HarmonyMethod(typeof(AiLimitCompatibility),nameof(AfterLimit)));plugin.Patches.Patch(AccessTools.Method(type,"UpdateBotsWithDisabledList"),prefix:new HarmonyMethod(typeof(AiLimitCompatibility),nameof(BeforeDisable)));plugin.Log.LogInfo("AILimit compatibility ready: nearby or visible-engaged bosses only; distant boss standby preserved");}catch(Exception e){plugin.Log.LogWarning("AILimit compatibility unavailable: "+e.Message);}}
 static object? Property(object value,string name,Dictionary<Type,PropertyInfo?> cache){var type=value.GetType();if(!cache.TryGetValue(type,out var info)){info=type.GetProperty(name);cache[type]=info;}return info?.GetValue(value);}
 static Player? PlayerFor(object item){var id=Property(item,"Id",idProperties);var dict=mapping?.GetValue(null) as IDictionary;if(id==null||dict==null||!dict.Contains(id))return null;var info=dict[id];return info==null?null:Property(info,"Player",playerProperties) as Player;}
 static void Prepare(){if(cachedFrame==Time.frameCount)return;cachedFrame=Time.frameCount;exemptions.Clear();local=Singleton<GameWorld>.Instance?.MainPlayer;float radius=botDistance?.GetValue(null) is float value&&float.IsFinite(value)&&value>0?value:0;radiusSquared=radius*radius;}
 static Player? Exempt(object item){Prepare();if(exemptions.TryGetValue(item,out var cached))return cached;Player? result=null;var p=PlayerFor(item);if(p?.HealthController?.IsAlive==true&&AiActorControl.ForcedCombatActive(p)){exemptions[item]=p;return p;}if(keepBosses.Value&&local!=null&&local.HealthController?.IsAlive==true&&p?.HealthController?.IsAlive==true&&Property(item,"eligibleNow",eligibleProperties) is true&&AiBehaviorRules.BossFamily(p.Profile?.Info?.Settings?.Role.ToString()??"")){
  bool nearby=radiusSquared>0&&(p.Position-local.Position).sqrMagnitude<=radiusSquared;var enemy=nearby?null:p.AIData?.BotOwner?.Memory?.GoalEnemy;
  bool engaged=enemy?.Person?.ProfileId==local.ProfileId&&(enemy.IsVisible||enemy.CanShoot);if(nearby||engaged)result=p;
 }exemptions[item]=result;return result;}
 static void BeforeLimit(out Removed? __state){__state=null;try{if(botList?.GetValue(null) is not IList list)return;for(int i=list.Count-1;i>=0;i--)if(list[i] is object item&&Exempt(item) is Player p){__state??=new Removed{List=list};__state.Items.Add(item);list.RemoveAt(i);Wake(p);}}catch(Exception e){Warn(e);}}
 static void AfterLimit(Removed? __state){if(__state?.List==null)return;foreach(var item in __state.Items)if(!__state.List.Contains(item))__state.List.Add(item);}
 static void BeforeDisable(object __instance){try{if(disabled?.GetValue(__instance) is not IList list)return;for(int i=list.Count-1;i>=0;i--)if(list[i] is object item&&Exempt(item) is Player p){list.RemoveAt(i);Wake(p);}}catch(Exception e){Warn(e);}}
 internal static void Wake(Player p){if(p.HealthController?.IsAlive!=true)return;var owner=p.AIData?.BotOwner;if(owner?.StandBy==null)return;if(!p.gameObject.activeSelf||owner.StandBy.StandByType==BotStandByType.paused){owner.StandBy.Activate();owner.StandBy._nextCheckTime=Time.time+10;p.gameObject.SetActive(true);owner.BotState=EBotState.Active;}}
 internal static void Tick(GameWorld? world){if(world==null||Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+2;try{var controller=Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController;var covers=controller?.CoversData;if(covers?.VoxelesArray==null)return;var all=world.AllAlivePlayersList;if(all==null)return;for(int i=0;i<all.Count;i++){var p=all[i];var owner=p?.AIData?.BotOwner;var voxel=owner?.VoxelesPersonalData;if(owner==null||voxel==null||owner.BotState!=EBotState.Active)continue;if((voxel._data==null||voxel._data.VoxelesArray==null)&&covers.GetVoxelSafe(p!.Position,false)!=null){voxel.Activate(covers);repaired++;Plugin.Self.Log.LogInfo("AI navigation personal data restored; total="+repaired);}}}catch(Exception e){Warn(e);}}
 internal static bool HasNavigation(AICoversData? data,Vector3 position){try{return data?.VoxelesArray!=null&&data.GetVoxelSafe(position,false)!=null;}catch{return false;}}
 internal static void Draw(Rect rect){if(InterfaceMenu.Button(rect,keepBosses.Value?"附近／交战 BOSS 保持活动：开":"附近／交战 BOSS 保持活动：关"))keepBosses.Value=!keepBosses.Value;}
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Log.LogWarning("AI compatibility: "+e.Message);}
}
