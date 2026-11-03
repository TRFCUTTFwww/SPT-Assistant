using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using UnityEngine;
using Effect=EFT.HealthSystem.ActiveHealthController.Effect;
using Stimulator=EFT.HealthSystem.ActiveHealthController.Stimulator;
using BuffSettings=EFT.HealthSystem.EffectsSettings.StimulatorSettings.StimulatorBuffSettings;
namespace LocalSpt5Esp;
internal static class CharacterBuffs {
 sealed record Preset(string Key,string Name);
 sealed record Row(long Pointer,int Id,string Type,string Name,string Part,string State,string Remaining,float Strength,string[] Details,bool Removable);
 static readonly List<Row> rows=new();static readonly List<Preset> presets=new();
 static readonly Dictionary<string,string> presetNames=new(StringComparer.Ordinal);
 static Dictionary<long,int> added=new();static readonly Dictionary<string,Dictionary<long,int>> additions=new();static readonly HashSet<long> expanded=new();
 sealed record AiSelection(LiveActorReference Actor,IntPtr Health);
 static readonly List<AiSelection> aiTargets=new();static int aiView;static string batchNotice="";static IntPtr trackingWorld;static float nextAiView;
 static Player? aiTarget;static IntPtr aiWorld,aiPointer;static string aiProfile="";
 internal static bool AiOpen=>aiTargets.Count>0;
 static bool Batch=>InterfaceMenu.AiPage&&aiTargets.Count>1;
 static bool CanApply=>Batch?RaidClock.Active&&Singleton<GameWorld>.Instance?.Pointer==aiWorld:Current()!=null;
 internal static void CloseAi(){aiTargets.Clear();aiTarget=null;aiWorld=aiPointer=IntPtr.Zero;aiProfile=batchNotice="";nextAiView=0;ClearSnapshot();}
 internal static void SelectAi(GameWorld world,Player player)=>SelectAi(world,new[]{player});
 internal static void SelectAi(GameWorld world,IEnumerable<Player> players){var selected=players.GroupBy(p=>p.ProfileId,StringComparer.Ordinal).Select(g=>g.First()).ToArray();if(selected.Length==0)throw new ArgumentException("请先选择 AI。");var next=new List<AiSelection>();foreach(var player in selected){if(player.IsYourPlayer||!player.IsAI)throw new ArgumentException("请选择当前战局的 AI。");var reference=new LiveActorReference(world,player);reference.Resolve();var health=player.ActiveHealthController??throw new ArgumentException("所选 AI 的状态尚未就绪。");next.Add(new(reference,health.Pointer));}CloseAi();aiTargets.AddRange(next);aiWorld=world.Pointer;page=aiTargets.Count>1?1:0;SetAiView(0);}
 static Player Resolve(AiSelection selected){var actor=selected.Actor.Resolve();if(actor.IsYourPlayer||!actor.IsAI||actor.ActiveHealthController?.Pointer!=selected.Health)throw new ArgumentException("所选 AI 的状态已改变，请重新选择。");return actor;}
 static Dictionary<long,int> Tracking(GameWorld world,Player player){if(trackingWorld!=world.Pointer){additions.Clear();trackingWorld=world.Pointer;}string key=BuffRules.TrackingKey(world.Pointer.ToInt64(),player.ActiveHealthController.Pointer.ToInt64(),player.ProfileId);if(!additions.TryGetValue(key,out var tracked)){tracked=new();additions[key]=tracked;}return tracked;}
 static void SetAiView(int index){var actor=Resolve(aiTargets[index]);aiTarget=actor;aiPointer=actor.Pointer;aiProfile=actor.ProfileId;aiView=index;Bind(Singleton<GameWorld>.Instance,actor);Scan(actor.ActiveHealthController);}
 static void CycleAiView(int direction){for(int step=1;step<=aiTargets.Count;step++){int index=(aiView+direction*step%aiTargets.Count+aiTargets.Count)%aiTargets.Count;try{SetAiView(index);return;}catch{}}notice="所选 AI 已死亡、离开或状态改变，请重新选择。";}
 static Player? SelectedPlayer(GameWorld? world){if(!AiOpen||!InterfaceMenu.AiPage)return world?.MainPlayer;if(aiTarget==null||world?.Pointer!=aiWorld||aiTarget.Pointer!=aiPointer||aiTarget.ProfileId!=aiProfile||aiTarget.IsYourPlayer||!aiTarget.IsAI||aiTarget.ActiveHealthController?.Pointer!=aiTargets[aiView].Health)return null;var all=world.AllAlivePlayersList;if(all!=null)for(int i=0;i<all.Count;i++)if(all[i]?.Pointer==aiPointer)return aiTarget;return null;}
 static void Bind(GameWorld world,Player player){ClearSnapshot();worldPointer=world.Pointer;healthPointer=player.ActiveHealthController.Pointer;profile=player.ProfileId;added=Tracking(world,player);}
 internal static void DrawAi(Rect rect){if(InterfaceMenu.Button(new Rect(rect.x,rect.y,126,30),"返回 AI 列表")){CloseAi();return;}InterfaceMenu.Label(new Rect(rect.x+140,rect.y,rect.width-140,30),aiTargets.Count>1?"批量 AI Buff · 已选 "+aiTargets.Count+" 名":"目标 AI · "+(aiTarget?.Profile?.Nickname??aiProfile),InterfaceMenu.Accent,16);float top=rect.y+42;if(aiTargets.Count>1){InterfaceMenu.Label(new Rect(rect.x,top,rect.width-194,30),"正在查看 "+(aiView+1)+"／"+aiTargets.Count+" · "+(aiTarget?.Profile?.Nickname??aiProfile),null,13);if(InterfaceMenu.Button(new Rect(rect.xMax-184,top,88,30),"上一名"))CycleAiView(-1);if(InterfaceMenu.Button(new Rect(rect.xMax-88,top,88,30),"下一名"))CycleAiView(1);top+=40;InterfaceMenu.Label(new Rect(rect.x,top,rect.width,30),"添加作用于全部所选 AI；当前状态与移除只针对正在查看的 AI。",InterfaceMenu.Accent,12);top+=38;}Draw(new Rect(rect.x,top,rect.width,Math.Max(100,rect.yMax-top)));}
 static IntPtr worldPointer,healthPointer;static string profile="",query="",presetQuery="",notice="",duration="300",strength="1",selectedPreset="BuffsPropital";
 static int page,mode,common,part=2;static bool loaded;static float nextScan,nextError;
 static Vector2 currentScroll,addScroll,detailScroll;
 static readonly EBodyPart[] parts={EBodyPart.Common,EBodyPart.Head,EBodyPart.Chest,EBodyPart.Stomach,EBodyPart.LeftArm,EBodyPart.RightArm,EBodyPart.LeftLeg,EBodyPart.RightLeg};
 internal static void Initialize(Plugin plugin){var zero=new Il2CppSystem.Nullable<float>(0f);if(!zero.HasValue||zero.Value!=0f)throw new InvalidOperationException("Buff zero-value marshalling check failed");plugin.Log.LogInfo("Character buffs ready: native float nullable arguments verified; effect snapshots and native preset operations");}
 static string Part(EBodyPart p)=>p switch {EBodyPart.Head=>"头部",EBodyPart.Chest=>"胸部",EBodyPart.Stomach=>"腹部",EBodyPart.LeftArm=>"左臂",EBodyPart.RightArm=>"右臂",EBodyPart.LeftLeg=>"左腿",EBodyPart.RightLeg=>"右腿",_=>"全身"};
 static string State(Effect effect)=>effect.State switch {EEffectState.Added=>"准备中",EEffectState.Started=>"生效中",EEffectState.Residued=>"残留中",EEffectState.Paused=>"已暂停",EEffectState.Removed=>"已结束",_=>"等待中"};
 static ActiveHealthController? Current(){try{var player=SelectedPlayer(Singleton<GameWorld>.Instance);return RaidClock.Active&&player?.HealthController?.IsAlive==true?player.ActiveHealthController:null;}catch{return null;}}
 static ActiveHealthController Target(){var health=Current()??throw new ArgumentException("需要进入战局且角色存活。");var world=Singleton<GameWorld>.Instance;var player=SelectedPlayer(world);
  if(world==null||player==null||!BuffRules.SameTarget(worldPointer.ToInt64(),healthPointer.ToInt64(),profile,world.Pointer.ToInt64(),health.Pointer.ToInt64(),player.ProfileId))throw new ArgumentException("当前角色或战局已改变，请刷新后操作。");return health;}
 static Effect[] Effects(ActiveHealthController health){var list=health.Effects;var result=new List<Effect>();if(list!=null)for(int i=0;i<list.Count;i++){var e=list[i];if(e!=null&&!e.ForceRemoved&&e.State!=EEffectState.Removed)result.Add(e);}return result.ToArray();}
 static EffectsSettings? Settings(ActiveHealthController health)=>Effect.EffectsSettings??Singleton<GlobalConfiguration>.Instance?.Health?.Effects;
 static string PresetName(string key)=>presetNames.TryGetValue(key,out var name)?name:key;
 static void LoadNames(){if(loaded)return;loaded=true;try{
  using var items=JsonDocument.Parse(File.ReadAllText(Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","SPT_Data","database","templates","items.json")));
  using var names=JsonDocument.Parse(File.ReadAllText(Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","esp5-names.json")));
  foreach(var item in items.RootElement.EnumerateObject())if(item.Value.TryGetProperty("_props",out var props)&&props.TryGetProperty("StimulatorBuffs",out var buff)&&buff.ValueKind==JsonValueKind.String){string key=buff.GetString()??"";if(key.Length==0||presetNames.ContainsKey(key))continue;string name=names.RootElement.TryGetProperty(item.Name+" ShortName",out var shortName)?shortName.GetString()??"":names.RootElement.TryGetProperty(item.Name,out var fullName)?fullName.GetString()??"":"";if(name.Length==0)name=item.Value.TryGetProperty("_name",out var raw)?raw.GetString()??key:key;presetNames[key]=name;}
 }catch(Exception e){Warn(e);}}
 static void ClearSnapshot(){rows.Clear();presets.Clear();added=new();expanded.Clear();worldPointer=healthPointer=IntPtr.Zero;profile="";nextScan=0;notice="";}
 internal static void Reset(){CloseAi();additions.Clear();trackingWorld=IntPtr.Zero;}
 internal static void Tick(GameWorld? world,Player? player){try{
  if(world==null||player==null||!RaidClock.Active||player.ActiveHealthController==null){if(worldPointer!=IntPtr.Zero||AiOpen||additions.Count>0)Reset();return;}
  if(AiOpen&&world.Pointer!=aiWorld){Reset();return;}if(InterfaceMenu.BuffPage&&AiOpen)CloseAi();player=SelectedPlayer(world);if(AiOpen&&(player?.HealthController?.IsAlive!=true||player.ActiveHealthController==null)&&Time.unscaledTime>=nextAiView){nextAiView=Time.unscaledTime+.25f;CycleAiView(1);player=SelectedPlayer(world);}if(player?.HealthController?.IsAlive!=true||player.ActiveHealthController==null){ClearSnapshot();return;}
  var health=player.ActiveHealthController;if(worldPointer!=world.Pointer||healthPointer!=health.Pointer||profile!=player.ProfileId)Bind(world,player);
  if(!(InterfaceMenu.BuffPage||InterfaceMenu.AiPage&&AiOpen)||Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.25f;Scan(health);
 }catch(Exception e){notice="读取状态失败："+e.Message;Warn(e);}}
 static void Scan(ActiveHealthController health){
  LoadNames();rows.Clear();var effects=Effects(health);var alive=effects.ToDictionary(e=>e.Pointer.ToInt64(),e=>e.Id);foreach(var key in added.Keys.ToArray())if(!alive.TryGetValue(key,out var id)||added[key]!=id)added.Remove(key);expanded.IntersectWith(alive.Keys);
  foreach(var effect in effects){try{
   string type=effect.Type?.Name??"Effect",name=BuffRules.Name(type);var details=new List<string>();var stim=effect.TryCast<Stimulator>();
   if(stim!=null){name=PresetName(stim.Name??"");if(string.IsNullOrEmpty(name))name="针剂／食品效果";var buffs=stim._buffs;
    if(buffs!=null)for(int i=0;i<buffs.Length;i++){var b=buffs[i];var settings=b?.Settings;if(b==null||settings==null)continue;float elapsed=stim._curTime;float end=b.WholeTime>0?b.WholeTime:settings.Delay+settings.Duration;
     string state=BuffRules.SubState(b.Active,b.Processed,elapsed,settings.Delay,end);
     float remaining=state=="待生效"?Math.Max(0,settings.Delay-elapsed):BuffRules.SubRemaining(elapsed,end);
     details.Add(BuffName(settings.BuffType)+(string.IsNullOrEmpty(settings.SkillName)?"":" · "+SkillName(settings.SkillName))+" · "+b.Value.ToString("+0.###;-0.###;0",CultureInfo.InvariantCulture)+" · "+state+(state=="待生效"?" "+BuffRules.Time(remaining)+"后":state=="生效中"?" "+BuffRules.Time(remaining):""));
    }
   }
   rows.Add(new(effect.Pointer.ToInt64(),effect.Id,type,name,Part(effect.BodyPart),State(effect),BuffRules.Time(effect.TimeLeft),effect.CurrentStrength,details.ToArray(),BuffRules.CanRemove(type)));
  }catch(Exception e){Warn(e);}}
  var available=Settings(health)?.Stimulator?.Buffs;presets.Clear();if(available!=null)foreach(var pair in available)presets.Add(new(pair.Key,PresetName(pair.Key)));
  presets.Sort((a,b)=>string.Compare(a.Name,b.Name,StringComparison.Ordinal));
 }
 static string SkillName(string value)=>value switch {"Strength"=>"力量","Endurance"=>"耐力","StressResistance"=>"抗压","Vitality"=>"活力","Health"=>"健康","Metabolism"=>"代谢","Immunity"=>"免疫","Perception"=>"感知","Attention"=>"专注",_=>value};
 static string BuffName(EStimulatorBuffType type)=>type switch {EStimulatorBuffType.HealthRate=>"生命恢复／秒",EStimulatorBuffType.EnergyRate=>"能量变化／秒",EStimulatorBuffType.HydrationRate=>"水分变化／秒",EStimulatorBuffType.SkillRate=>"技能等级",EStimulatorBuffType.MaxStamina=>"最大体力",EStimulatorBuffType.StaminaRate=>"体力恢复",EStimulatorBuffType.DamageModifier=>"伤害倍率",EStimulatorBuffType.WeightLimit=>"负重上限",EStimulatorBuffType.Pain=>"疼痛抑制",EStimulatorBuffType.HandsTremor=>"手部震颤",EStimulatorBuffType.RemoveNegativeEffects=>"清除负面状态",EStimulatorBuffType.RemoveAllBloodLosses=>"清除出血",EStimulatorBuffType.RemoveAllBuffs=>"清除全部增益",EStimulatorBuffType.Antidote=>"解毒",EStimulatorBuffType.BodyTemperature=>"体温变化",EStimulatorBuffType.LightBleeding=>"轻出血",EStimulatorBuffType.HeavyBleeding=>"重出血",EStimulatorBuffType.Fracture=>"骨折",EStimulatorBuffType.Contusion=>"挫伤",EStimulatorBuffType.UnknownToxin=>"未知毒素",EStimulatorBuffType.LethalToxin=>"致命毒素",_=>type.ToString()};
 static Effect? Add<T>(ActiveHealthController health,EBodyPart body,float seconds,float amount) where T:Effect,new(){var states=health.BodyState;if(states==null)throw new ArgumentException("角色效果部位尚未就绪。");if(body==EBodyPart.Common){body=EBodyPart.Head;if(!states.ContainsKey(body))body=EBodyPart.Chest;}if(body==EBodyPart.Common||!states.ContainsKey(body))throw new ArgumentException("该角色没有可用的效果作用部位。");return health.AddEffect<T>(body,new Il2CppSystem.Nullable<float>(0f),new Il2CppSystem.Nullable<float>(seconds),new Il2CppSystem.Nullable<float>(0f),new Il2CppSystem.Nullable<float>(amount),null);}
 static void Remember(ActiveHealthController health,HashSet<long> before,Dictionary<long,int> tracked){foreach(var effect in Effects(health))if(!before.Contains(effect.Pointer.ToInt64()))tracked[effect.Pointer.ToInt64()]=effect.Id;}
 static void ApplyTargets(string label,Func<ActiveHealthController,Dictionary<long,int>,string> apply){if(!Batch){var health=Target();string result=apply(health,added);Scan(health);notice=result;return;}var current=Singleton<GameWorld>.Instance;if(!RaidClock.Active||current?.Pointer!=aiWorld)throw new ArgumentException("战局已改变，请重新选择 AI。");var resultBatch=BuffBatch.Run(aiTargets,entry=>entry.Actor.Name,entry=>{var actor=Resolve(entry);apply(actor.ActiveHealthController,Tracking(current,actor));});var visible=Current();if(visible!=null)Scan(visible);batchNotice=resultBatch.Summary(label);if(resultBatch.Failures.Length>0){string first=resultBatch.Failures[0];batchNotice+="\n"+(first.Length>90?first[..90]+"…":first);foreach(var failure in resultBatch.Failures)Plugin.Self.Log.LogWarning("AI batch Buff: "+failure);}notice=batchNotice;FeatureToasts.Show(resultBatch.Summary(label));}
 static void AddCommon(){var option=BuffRules.Common[common];var body=option.WholeBody?EBodyPart.Common:parts[part];if(!option.WholeBody&&body==EBodyPart.Common)throw new ArgumentException("请选择具体作用部位。");if(option.Type=="Fracture"&&body is not (EBodyPart.LeftArm or EBodyPart.RightArm or EBodyPart.LeftLeg or EBodyPart.RightLeg))throw new ArgumentException("骨折请选四肢部位。");float seconds=BuffRules.Number(duration,1,float.MaxValue,"持续秒数"),amount=BuffRules.Number(strength,.01f,float.MaxValue,"强度倍率");ApplyTargets(option.Name,(health,tracked)=>ApplyCommon(health,tracked,option,body,seconds,amount));}
 static string ApplyCommon(ActiveHealthController health,Dictionary<long,int> tracked,BuffOption option,EBodyPart body,float seconds,float amount){var before=new HashSet<long>(Effects(health).Select(e=>e.Pointer.ToInt64()));Effect? result=null;try{
  result=option.Type switch {
   "PainKiller"=>Add<ActiveHealthController.PainKiller>(health,body,seconds,amount),"PainSuppressed"=>Add<ActiveHealthController.PainSuppressed>(health,body,seconds,amount),"LightBleedingSuppressed"=>Add<ActiveHealthController.LightBleedingSuppressed>(health,body,seconds,amount),"HeavyBleedingSuppressed"=>Add<ActiveHealthController.HeavyBleedingSuppressed>(health,body,seconds,amount),"FractureSuppressed"=>Add<ActiveHealthController.FractureSuppressed>(health,body,seconds,amount),"HealthBoost"=>Add<ActiveHealthController.HealthBoost>(health,body,seconds,amount),"Pain"=>Add<ActiveHealthController.Pain>(health,body,seconds,amount),"Tremor"=>Add<ActiveHealthController.Tremor>(health,body,seconds,amount),"LightBleeding"=>Add<ActiveHealthController.LightBleeding>(health,body,seconds,amount),"HeavyBleeding"=>Add<ActiveHealthController.HeavyBleeding>(health,body,seconds,amount),"Fracture"=>Add<ActiveHealthController.Fracture>(health,body,seconds,amount),"Contusion"=>Add<ActiveHealthController.Contusion>(health,body,seconds,amount),"Intoxication"=>Add<ActiveHealthController.Intoxication>(health,body,seconds,amount),_=>throw new ArgumentException("该状态尚不支持添加。")};
 }finally{Remember(health,before,tracked);}if(result==null)throw new ArgumentException("游戏未返回该效果，请检查部位与当前状态。");return "已添加／刷新："+option.Name+" · 持续 "+BuffRules.Time(result.WorkStateTime)+" · 强度 "+result.Strength.ToString("G6")+(result.Strength!=amount||result.WorkStateTime!=seconds?"（已调整）":"");}
 static void AddPreset(){string preset=selectedPreset;ApplyTargets(PresetName(preset),(health,tracked)=>ApplyPreset(health,tracked,preset));}
 static string ApplyPreset(ActiveHealthController health,Dictionary<long,int> tracked,string preset){var settings=Settings(health)?.Stimulator?.Buffs;if(settings==null||!settings.ContainsKey(preset))throw new ArgumentException("所选预设已不可用，请重新选择。");var before=new HashSet<long>(Effects(health).Select(e=>e.Pointer.ToInt64()));try{health.DoExternalBuff(preset,0);}finally{Remember(health,before,tracked);}if(!Effects(health).Any(e=>e.TryCast<Stimulator>()?.Name==preset))throw new ArgumentException("已提交应用请求，但未发现对应效果，请刷新检查。");return "已应用："+PresetName(preset)+"。子效果按原有延迟和概率生效。";}
 static void Remove(Row row){var health=Target();var effect=Effects(health).FirstOrDefault(e=>e.Pointer.ToInt64()==row.Pointer&&e.Id==row.Id);if(effect==null)throw new ArgumentException("该效果已结束，请刷新列表。");if(!BuffRules.CanRemove(effect.Type?.Name??""))throw new ArgumentException("基础生理进程不作为 Buff 移除。");effect.ForceRemove();added.Remove(row.Pointer);Scan(health);notice="已移除："+row.Name+"。由伤势或环境持续触发的状态可能再次出现。";}
 static void ClearAdded(){var health=Target();int removed=0;foreach(var effect in Effects(health))if(added.TryGetValue(effect.Pointer.ToInt64(),out var expectedId)&&expectedId==effect.Id&&BuffRules.CanRemove(effect.Type?.Name??"")){effect.ForceRemove();removed++;}added.Clear();Scan(health);notice=$"已移除本次手动新增的 {removed} 个效果。";}
 static void Action(Action action){try{action();}catch(Exception e){notice=e.Message;Warn(e);}}
 internal static void Draw(Rect rect){
  float x=rect.x,y=rect.y,w=rect.width;InterfaceMenu.Label(new Rect(x,y,w,27),AiOpen&&InterfaceMenu.AiPage?"AI Buff":"人物 Buff",InterfaceMenu.Text,18);y+=35;
  if(InterfaceMenu.Button(new Rect(x,y,130,30),"当前状态",page==0))page=0;if(InterfaceMenu.Button(new Rect(x+138,y,130,30),"添加 Buff",page==1))page=1;
  var old=GUI.enabled;GUI.enabled=old&&Current()!=null;if(InterfaceMenu.Button(new Rect(x+w-112,y,112,30),"刷新"))Action(()=>Scan(Target()));GUI.enabled=old;y+=40;
  InterfaceMenu.Paragraph(new Rect(x,y,w,38),notice.Length>0?notice:Batch&&batchNotice.Length>0?batchNotice:Current()==null?"进入战局且角色存活时，可查看、添加和移除当前角色的效果。":$"当前效果 {rows.Count} 项 · 本次手动新增 {added.Count} 项 · 每 0.25 秒刷新",InterfaceMenu.Accent,12);y+=46;
  if(page==0)DrawCurrent(new Rect(x,y,w,Math.Max(80,rect.yMax-y)));else DrawAdd(new Rect(x,y,w,Math.Max(80,rect.yMax-y)));
 }
 static void DrawCurrent(Rect rect){float x=rect.x,y=rect.y,w=rect.width;query=InterfaceMenu.Field(new Rect(x,y,w-180,30),query);var old=GUI.enabled;GUI.enabled=old&&Current()!=null&&added.Count>0;if(InterfaceMenu.Button(new Rect(x+w-170,y,170,30),"移除本次新增效果"))Action(ClearAdded);GUI.enabled=old;y+=40;
  var visible=rows.Where(row=>BuffRules.Matches(row.Name+" "+row.Type+" "+row.Part+" "+string.Join(" ",row.Details),query)).ToArray();
  float height=visible.Sum(row=>72f+(expanded.Contains(row.Pointer)?Math.Max(1,row.Details.Length)*23+10:0));var viewport=new Rect(x,y,w,Math.Max(40,rect.yMax-y));currentScroll=GUI.BeginScrollView(viewport,currentScroll,new Rect(0,0,w-20,Math.Max(viewport.height-1,height)));float top=0;
  foreach(var row in visible){bool open=expanded.Contains(row.Pointer);float h=64+(open?Math.Max(1,row.Details.Length)*23+10:0);var card=new Rect(0,top,w-24,h);InterfaceMenu.Surface(card,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.04f),9);
   InterfaceMenu.Label(new Rect(12,top+6,card.width-115,25),row.Name+" · "+row.Part+(added.ContainsKey(row.Pointer)?" · 手动新增":""),InterfaceMenu.Text,14);
   InterfaceMenu.Label(new Rect(12,top+34,card.width-122,24),row.State+" · 剩余 "+row.Remaining+" · 强度 "+row.Strength.ToString("0.###",CultureInfo.InvariantCulture),Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.3f),12);
   if(row.Details.Length>0&&InterfaceMenu.Button(new Rect(card.width-114,top+34,102,24),open?"收起明细":"子效果 "+row.Details.Length)){if(open)expanded.Remove(row.Pointer);else expanded.Add(row.Pointer);}
   old=GUI.enabled;GUI.enabled=old&&Current()!=null&&row.Removable;if(InterfaceMenu.Button(new Rect(card.width-100,top+6,88,25),row.Type=="Stimulator"?"移除整组":"移除"))Action(()=>Remove(row));GUI.enabled=old;
   if(open)for(int i=0;i<row.Details.Length;i++)InterfaceMenu.Label(new Rect(18,top+68+i*23,card.width-30,23),row.Details[i],InterfaceMenu.Text,12);
   top+=h+8;
  }
  if(visible.Length==0)InterfaceMenu.Label(new Rect(12,12,w-44,30),query.Length>0?"没有匹配的效果。":"当前没有可显示的效果。",InterfaceMenu.Text,13);GUI.EndScrollView();
 }
 static void DrawAdd(Rect rect){float x=rect.x,y=rect.y,w=rect.width;
  if(InterfaceMenu.Button(new Rect(x,y,(w-8)/2,30),"常用状态",mode==0))mode=0;if(InterfaceMenu.Button(new Rect(x+(w+8)/2,y,(w-8)/2,30),"针剂／食品效果组合",mode==1))mode=1;y+=40;
  var area=new Rect(x,y,w,rect.yMax-y);if(mode==0)DrawCommon(area);else DrawPresets(area);
 }
 static void DrawCommon(Rect rect){float x=rect.x,y=rect.y,w=rect.width;
  float height=Math.Min(150,Math.Max(92,rect.height*.4f));addScroll=GUI.BeginScrollView(new Rect(x,y,w,height),addScroll,new Rect(0,0,w-20,MathF.Ceiling(BuffRules.Common.Length/3f)*37));float cell=(w-30)/3;
  for(int i=0;i<BuffRules.Common.Length;i++)if(InterfaceMenu.Button(new Rect(i%3*(cell+5),i/3*37,cell,31),BuffRules.Common[i].Name,common==i))common=i;GUI.EndScrollView();y+=height+10;
  var option=BuffRules.Common[common];InterfaceMenu.Paragraph(new Rect(x,y,w,35),option.Description,InterfaceMenu.Text,12);y+=39;
  if(InterfaceMenu.Button(new Rect(x,y,Math.Min(210,w*.35f),30),"作用部位："+(option.WholeBody?"全身":Part(parts[part])))){if(!option.WholeBody){part=(part+1)%parts.Length;if(part==0)part=1;}}
  InterfaceMenu.Label(new Rect(x+w*.38f,y,65,28),"持续秒数",InterfaceMenu.Text,12);duration=InterfaceMenu.Field(new Rect(x+w*.38f+65,y,Math.Min(110,w*.2f),28),duration);y+=38;
  InterfaceMenu.Label(new Rect(x,y,80,28),"强度倍率",InterfaceMenu.Text,12);strength=InterfaceMenu.Field(new Rect(x+82,y,95,28),strength);
  var old=GUI.enabled;GUI.enabled=old&&CanApply;if(InterfaceMenu.Button(new Rect(x+w-160,y,160,30),Batch?"添加到所选 AI":"添加／刷新状态"))Action(AddCommon);GUI.enabled=old;y+=38;
  InterfaceMenu.Paragraph(new Rect(x,y,w,42),"1 为默认强度。",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.3f),12);
 }
 static void DrawPresets(Rect rect){float x=rect.x,y=rect.y,w=rect.width;float left=Math.Max(190,w*.4f),right=w-left-14;presetQuery=InterfaceMenu.Field(new Rect(x,y,left,28),presetQuery);y+=37;
  var matches=presets.Where(p=>BuffRules.Matches(p.Name+" "+p.Key,presetQuery)).ToArray();addScroll=GUI.BeginScrollView(new Rect(x,y,left,Math.Max(60,rect.yMax-y)),addScroll,new Rect(0,0,left-18,matches.Length*46));
  for(int i=0;i<matches.Length;i++)if(InterfaceMenu.Button(new Rect(0,i*46,left-22,40),matches[i].Name,selectedPreset==matches[i].Key)){selectedPreset=matches[i].Key;detailScroll=Vector2.zero;}GUI.EndScrollView();
  float rx=x+left+14;InterfaceMenu.Label(new Rect(rx,rect.y,right,28),PresetName(selectedPreset),InterfaceMenu.Text,15);
  var health=Current();var all=health!=null?Settings(health)?.Stimulator?.Buffs:null;var buffs=all!=null&&all.ContainsKey(selectedPreset)?all[selectedPreset]:null;
  float textY=rect.y+36,textHeight=Math.Max(60,rect.height-126);detailScroll=GUI.BeginScrollView(new Rect(rx,textY,right,textHeight),detailScroll,new Rect(0,0,right-18,(buffs?.Length??1)*66));
  if(buffs!=null)for(int i=0;i<buffs.Length;i++){var b=buffs[i];if(b==null)continue;InterfaceMenu.Paragraph(new Rect(0,i*66,right-22,60),BuffName(b.BuffType)+(string.IsNullOrEmpty(b.SkillName)?"":" · "+SkillName(b.SkillName))+"  "+b.Value.ToString("+0.###;-0.###;0",CultureInfo.InvariantCulture)+"\n延迟 "+(b.Delay<=0?"无":BuffRules.Time(b.Delay))+" · 持续 "+(b.Duration<=0?"即时":BuffRules.Time(b.Duration))+" · 概率 "+b.Chance.ToString("P0",CultureInfo.InvariantCulture),InterfaceMenu.Text,12);}else InterfaceMenu.Label(new Rect(0,0,right-22,40),"进入战局后载入可用预设。",InterfaceMenu.Text,12);GUI.EndScrollView();
  var old=GUI.enabled;GUI.enabled=old&&CanApply&&buffs!=null;if(InterfaceMenu.Button(new Rect(rx,rect.yMax-79,right,32),Batch?"应用到所选 AI":"应用效果组合"))Action(AddPreset);GUI.enabled=old;
  InterfaceMenu.Paragraph(new Rect(rx,rect.yMax-40,right,40),"使用原有时序、强度和触发概率，包含其副作用；不消耗物品。",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.3f),11);
 }
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+15;Plugin.Self.Error(e);}
}
