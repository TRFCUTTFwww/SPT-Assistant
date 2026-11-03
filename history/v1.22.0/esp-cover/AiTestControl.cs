using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.Game.Spawning;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.AI;
using NativeTask=Il2CppSystem.Threading.Tasks.Task;
using Object=UnityEngine.Object;
namespace LocalSpt5Esp;
internal static class AiTestControl {
 static readonly HashSet<string> checkedBots=new();
 internal static void ForgetActor(string id){selection.Set(id,false);Restore(id);checkedBots.Remove(id);nextScan=0;}
 static int roleGroup;static Vector2 roleScroll;static bool requestedBoss;
 static readonly string[] difficultyNames={"简单","普通","困难","极难"};
 static int role,difficulty=1;static string count="1",distance="15",notice="进入战局后选择生成位置。";
 static Vector2 scroll;static IntPtr worldPointer;
 static bool ready;static float nextScan,nextError,requestTime;
 static GameWorld? world;static Player? player;static Vector3? spawnAt;
 static readonly AiPauseSelection selection=new();
 static readonly List<BotOwner> bots=new();
 sealed class Frozen {internal BotOwner Bot=null!;internal bool Pause;internal float PauseUntil;}
 static readonly Dictionary<string,Frozen> frozen=new();
 static Il2CppSystem.Threading.Tasks.Task<BotCreationData>? generating;
 static NativeTask? activating;static BotCreationData? data;static BotSpawner? spawner;static BotZone? zone;
 static Il2CppSystem.Collections.Generic.List<ISpawnPoint>? positions;
 static Il2CppSystem.Threading.CancellationTokenSource? cancellation;
 static Il2CppSystem.Action<BotOwner>? callback;
 static HashSet<string> created=new();static int requested,requestSerial;
 internal static void Initialize(Plugin plugin){
  try{
   plugin.Patches.Patch(typeof(BotOwner).GetMethod("UpdateManual",Type.EmptyTypes),prefix:new HarmonyMethod(typeof(AiTestControl),nameof(AllowBrain)));
   plugin.Patches.Patch(typeof(Player.FirearmController).GetMethod("SetTriggerPressed",new[]{typeof(bool)}),prefix:new HarmonyMethod(typeof(AiTestControl),nameof(AllowTrigger)));
   ready=true;plugin.Log.LogInfo("AI test control ready: native profile generation / activation + reversible pause");
  }catch(Exception e){plugin.Log.LogError("AI test hooks failed: "+e);}
 }
 static bool Paused(Player? bot){try{return ready&&worldPointer!=IntPtr.Zero&&bot!=null&&bot.IsAI&&!bot.IsYourPlayer&&selection.Contains(bot.ProfileId);}catch{return false;}}
 static bool AllowBrain(BotOwner __instance)=>!AiEquipmentRemoval.Blocks(__instance?.GetPlayer)&&(!selection.Any||!Paused(__instance?.GetPlayer));
 static bool AllowTrigger(Player.FirearmController __instance,bool __0)=>!__0||!AiEquipmentRemoval.Blocks(__instance?._player)&&(!selection.Any||!Paused(__instance?._player));
 internal static void Tick(GameWorld? current,Player? local){try{
  if(current==null||local==null){if(worldPointer!=IntPtr.Zero)Reset();return;}
  if(worldPointer!=current.Pointer){Reset();worldPointer=current.Pointer;world=current;player=local;notice="先选择准星地面或前方距离作为生成位置。";}
  world=current;player=local;
  PollSpawn();
  if(!ListVisible&&!selection.Any&&frozen.Count==0)return;
  if(Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+.5f;bots.Clear();var all=current.AllAlivePlayersList;
   if(all!=null)for(int i=0;i<all.Count;i++){var bot=all[i];if(bot==null||bot.IsYourPlayer||!bot.IsAI||bot.HealthController?.IsAlive!=true)continue;var owner=bot.AIData?.BotOwner;if(owner!=null)bots.Add(owner);}
   if(ListVisible){var origin=local.Position;var ordered=bots.Select(bot=>(Bot:bot,Distance:(bot.GetPlayer.Position-origin).sqrMagnitude)).OrderBy(entry=>entry.Distance).Select(entry=>entry.Bot).ToArray();bots.Clear();bots.AddRange(ordered);}
  }
  if(frozen.Count>0)foreach(var id in frozen.Keys.ToArray())if(!selection.Contains(id)||frozen[id].Bot?.GetPlayer?.HealthController?.IsAlive!=true)Restore(id);
  if(selection.Any)foreach(var bot in bots){var p=bot.GetPlayer;if(!selection.Contains(p.ProfileId))continue;
   if(!frozen.ContainsKey(p.ProfileId)){var mover=bot.Mover;frozen[p.ProfileId]=new Frozen{Bot=bot,Pause=mover?.Pause??false,PauseUntil=mover?._pauseEndTime??0};}
   bot.StopMove();if(bot.Mover!=null){bot.Mover.Pause=true;bot.Mover._pauseEndTime=float.MaxValue;}
   p.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);
  }
 }catch(Exception e){if(generating!=null||activating!=null||data!=null){CancelSpawn(false);notice="AI 生成失败："+e.Message;}Warn(e);}}
 static void Restore(string id){if(!frozen.Remove(id,out var snapshot))return;try{var mover=snapshot.Bot?.Mover;if(mover!=null){mover._pauseEndTime=snapshot.PauseUntil;mover.Pause=snapshot.Pause&&snapshot.PauseUntil>Time.time;}}catch(Exception e){Warn(e);}}
 static void Resume(){selection.Clear();foreach(var id in frozen.Keys.ToArray())Restore(id);}
 internal static void Reset(){AiEquipmentEditor.Close();AiBackpackEditor.Close();CharacterBuffs.CloseAi();AiActorControl.Close();checkedBots.Clear();Resume();CancelSpawn(false);bots.Clear();created.Clear();world=null;player=null;spawnAt=null;worldPointer=IntPtr.Zero;nextScan=0;}
 static void CancelSpawn(bool user,bool stop=true){
  requestSerial++;
  try{if(stop){data?.StopSpawn();cancellation?.Cancel();}}catch(Exception e){Warn(e);}
  try{cancellation?.Dispose();}catch{}
  RaidSpawnControl.ForgetManual(data);generating=null;activating=null;data=null;cancellation=null;callback=null;positions=null;spawner=null;zone=null;
  if(user)notice="已取消后续生成；已经出现的 AI 保留。";
 }
 static BotsController Controller()=>Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController??throw new ArgumentException("当前战局尚未开放 AI 控制器。");
 static Vector3 Ground(Vector3 point){
  if(!NavMesh.SamplePosition(point,out var hit,2.5f,NavMesh.AllAreas)||Math.Abs(hit.position.y-point.y)>1.8f)throw new ArgumentException("所选位置没有可行走道路，请重新对准地面选点。");
  if(player==null||Vector3.Distance(hit.position,player.Position)<5)throw new ArgumentException("生成位置必须距离玩家至少 5 米。");return hit.position;
 }
 internal static bool TrySpawnPoint(out Vector3 point){point=spawnAt??default;return RaidClock.Active&&spawnAt.HasValue&&Singleton<GameWorld>.Instance?.Pointer==worldPointer;}
 static void Pick(bool sight){
  if(!RaidClock.Active||player==null)throw new ArgumentException("请进入战局后选点。");var camera=Camera.main??throw new ArgumentException("当前相机未就绪。");Vector3 target;
  if(sight){if(!Physics.Raycast(camera.transform.position+camera.transform.forward*.4f,camera.transform.forward,out var hit,200,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||hit.normal.y<.35f)throw new ArgumentException("请将准星对准 200 米以内的地面后重试。");target=hit.point;}
  else{float range=AiSpawnRules.Distance(distance);target=player.Position+Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized*range;
   if(Physics.Raycast(target+Vector3.up*2,Vector3.down,out var hit,4,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))target=hit.point;}
  spawnAt=Ground(target);notice=$"生成位置已选：距玩家 {Vector3.Distance(spawnAt.Value,player.Position):F1} 米。";
 }
 static void Spawn(){
  if(!ready||!RaidClock.Active||world==null||player==null)throw new ArgumentException("请在 AI 控制器就绪的战局中使用。");
  if(generating!=null||activating!=null)throw new ArgumentException("已有生成请求，请等待或取消。");
  var target=Ground(spawnAt??throw new ArgumentException("请先选择生成位置。"));requested=AiSpawnRules.Count(count);
  var option=AiSpawnCatalog.Options[role];var type=Enum.Parse<WildSpawnType>(option.Role);var side=Enum.Parse<EPlayerSide>(option.Side);requestedBoss=option.Boss;
  var controller=Controller();spawner=controller.GetSpawner()??throw new ArgumentException("AI 生成器尚未就绪。");zone=spawner.GetClosestZone(target,out _)??throw new ArgumentException("该位置附近没有 AI 区域。");
  if(!zone.HaveFreeSpace(requested))throw new ArgumentException("当前 AI 区域没有足够空位；该区域原生容量为 "+zone.MaxPersons+"，请减少数量或更换位置。");
  var holder=Object.FindObjectOfType<AICorePointHolder>()??throw new ArgumentException("当前地图尚未提供 AI 导航节点。");
  positions=new Il2CppSystem.Collections.Generic.List<ISpawnPoint>();
  for(int i=0;i<requested;i++){
   var offset=i==0?Vector3.zero:new Vector3(Mathf.Cos(i*2.4f),0,Mathf.Sin(i*2.4f))*2.2f;
   var point=Ground(target+offset);if(!AiLimitCompatibility.HasNavigation(controller.CoversData,point))throw new ArgumentException("该位置缺少完整 AI 导航数据，请换到原生 AI 活动区域后选点。");var core=AICorePointHolder.GetClosest(point)??throw new ArgumentException("生成位置附近没有 AI 导航节点。");
   var spawn=new SpawnPoint{Id="local-test-"+Guid.NewGuid().ToString("N"),Name="辅助模组 AI 测试",Position=point,Rotation=Quaternion.identity,BotZone=zone,CorePointId=core.Id,Sides=(EPlayerSideMask)(-1),Categories=(ESpawnCategoryMask)(-1)};
   positions.Add(spawn.Cast<ISpawnPoint>());
  }
  var parameters=new BotSpawnParams{TriggerType=SpawnTriggerType.none,ShallBeGroup=new ShallBeGroupParams(requested>1,requestedBoss,requested),Id_spawn="local-ai-test-"+Guid.NewGuid().ToString("N")};
  var profile=new GetProfileDataParams(side,type,(BotDifficulty)difficulty,0,parameters,true);
  created.Clear();requestTime=Time.unscaledTime;cancellation=new Il2CppSystem.Threading.CancellationTokenSource();
  generating=BotCreationData.Create(profile.Cast<IGetProfileData>(),spawner._botCreator,requested,spawner.Cast<ITokenGetter>());
  notice=$"正在从服务器准备 {requested} 个 {option.Name}（{difficultyNames[difficulty]}）…";
 }
 static void PollSpawn(){
  if(generating==null&&activating==null)return;
  if(Time.unscaledTime-requestTime>90){CancelSpawn(false);notice="AI 生成超时，已取消后续生成，请检查服务器日志。";return;}
  if(generating!=null&&generating.IsCompleted){
   if(generating.IsFaulted||generating.IsCanceled){string error=generating.Exception?.ToString()??"请求已取消";CancelSpawn(false);notice="准备 AI 失败："+error;return;}
   data=generating.Result;generating=null;if(data==null||data.Count==0){CancelSpawn(false);notice="服务器没有返回可生成的 AI。";return;}
   var expectedWorld=worldPointer;int expectedRequest=requestSerial;
   callback=DelegateSupport.ConvertDelegate<Il2CppSystem.Action<BotOwner>>(new Action<BotOwner>(bot=>{try{if(worldPointer==IntPtr.Zero||worldPointer!=expectedWorld||requestSerial!=expectedRequest||bot?.GetPlayer==null)return;created.Add(bot.GetPlayer.ProfileId);nextScan=0;notice=$"AI 已生成 {created.Count} / {requested}。";}catch(Exception e){Warn(e);}}));
   RaidSpawnControl.AllowManual(data);activating=spawner!.ActivateBots(positions!,zone!,data,callback,new BotSpawnBossAndGroupParams(requestedBoss,requested>1),cancellation!.Token);
   notice="AI 资料已就绪，正在地图中激活…";
  }
  if(activating!=null&&activating.IsCompleted){
   string? error=activating.IsFaulted?activating.Exception?.ToString():activating.IsCanceled?"请求已取消":null;
   int made=created.Count;CancelSpawn(false,error!=null);notice=error!=null?"激活 AI 失败："+error:made>0?$"已生成 {made} 个测试 AI。":"激活流程已结束，尚未收到 AI 成功回调，请检查服务器与地图限制。";
  }
 }
 static void Action(Action action){try{action();}catch(Exception e){notice=e.Message;Warn(e);if(generating==null&&activating==null)CancelSpawn(false);}}
 static void Choice(Rect rect,string[] labels,ref int selected){float width=(rect.width-6*(labels.Length-1))/labels.Length;for(int i=0;i<labels.Length;i++)if(InterfaceMenu.Button(new Rect(rect.x+i*(width+6),rect.y,width,rect.height),(selected==i?"✓ ":"")+labels[i]))selected=i;}
 static int mainPage;static string listFilter="";static AiTypeFilter listType;static bool listTypesOpen;static Vector2 typeScroll,spawnScroll,settingsScroll;
 static bool ListVisible=>InterfaceMenu.AiPage&&mainPage==0&&!AiActorControl.Open&&!AiBackpackEditor.Open&&!CharacterBuffs.AiOpen&&!AiEquipmentEditor.Open;
 internal static void Draw(Rect rect){if(AiEquipmentEditor.Open){AiEquipmentEditor.Draw(rect);return;}if(AiActorControl.Open){AiActorControl.Draw(rect);return;}if(AiBackpackEditor.Open){AiBackpackEditor.Draw(rect);return;}if(CharacterBuffs.AiOpen){CharacterBuffs.DrawAi(rect);return;}float x=rect.x,y=rect.y,w=rect.width;InterfaceMenu.Label(new Rect(x,y,w,32),"AI 管理",InterfaceMenu.Accent,21);y+=46;Choice(new Rect(x,y,w,36),new[]{"AI 列表","添加 AI","全局设置"},ref mainPage);y+=52;var body=new Rect(x,y,w,Math.Max(100,rect.yMax-y));if(mainPage==0)DrawList(body);else if(mainPage==1)DrawSpawn(body);else DrawGlobal(body);}
 static void DrawList(Rect rect){float x=rect.x,y=rect.y,w=rect.width;string previous=listFilter;listFilter=InterfaceMenu.Field(new Rect(x,y,w-302,32),listFilter);if(previous!=listFilter)scroll=Vector2.zero;if(InterfaceMenu.Button(new Rect(x+w-290,y,164,32),"类型："+AiTypeRules.Name(listType)))listTypesOpen=!listTypesOpen;if(InterfaceMenu.Button(new Rect(x+w-114,y,114,32),"刷新列表"))nextScan=0;y+=44;if(listTypesOpen){var oldType=listType;float height=Math.Min(AiTypePicker.Height(w),Math.Max(80,rect.height-340));AiTypePicker.Options(new Rect(x,y,w,height),ref listType,ref listTypesOpen,ref typeScroll);y+=height+12;if(oldType!=listType)scroll=Vector2.zero;}var visible=bots.Where(bot=>{try{return AiTypeRules.Matches(listType,bot.Role.ToString(),bot.GetPlayer.Profile?.Side==EPlayerSide.Savage)&&((bot.GetPlayer.Profile?.Nickname??"")+" "+bot.Role+" "+(bot.BotsGroup?.Id.ToString()??"")).Contains(listFilter,StringComparison.OrdinalIgnoreCase);}catch{return false;}}).ToArray();var picked=bots.Select(bot=>bot.GetPlayer).Where(p=>p!=null&&checkedBots.Contains(p.ProfileId)).ToArray();InterfaceMenu.Label(new Rect(x,y,w-250,28),"显示 "+visible.Length+"／"+bots.Count+" · 已选 "+picked.Length+(picked.Count(p=>!visible.Any(bot=>bot.GetPlayer.ProfileId==p.ProfileId)) is int hidden&&hidden>0?"（隐藏 "+hidden+"）":""),null,13);if(InterfaceMenu.Button(new Rect(x+w-238,y,110,28),"选择当前结果"))foreach(var bot in visible)checkedBots.Add(bot.GetPlayer.ProfileId);if(InterfaceMenu.Button(new Rect(x+w-116,y,116,28),"清空选择"))checkedBots.Clear();y+=40;
  var view=new Rect(x,y,w,Math.Max(90,rect.yMax-y-146));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,w-20,Math.Max(view.height-1,visible.Length*124)));int first=Math.Max(0,(int)(scroll.y/124)),end=Math.Min(visible.Length,first+(int)Math.Ceiling(view.height/124)+2);for(int i=first;i<end;i++)try{var bot=visible[i];var actor=bot.GetPlayer;string name=actor.Profile?.Nickname??"AI";float top=i*124;var row=new Rect(0,top,w-26,112);bool selected=checkedBots.Contains(actor.ProfileId);InterfaceMenu.Surface(row,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Accent,selected ? .10f : .035f),9);if(InterfaceMenu.Button(new Rect(10,top+10,32,30),selected?"✓":"",selected)){if(!checkedBots.Remove(actor.ProfileId))checkedBots.Add(actor.ProfileId);}InterfaceMenu.Label(new Rect(54,top+6,row.width-70,30),name+" · "+bot.Role+" · "+Vector3.Distance(actor.Position,player!.Position).ToString("F0")+" 米",null,14);InterfaceMenu.Label(new Rect(54,top+37,row.width-210,24),AiActorControl.Team(bot)+(selection.Contains(actor.ProfileId)?" · 已暂停":""),InterfaceMenu.Accent,12);if(InterfaceMenu.Button(new Rect(row.width-132,top+37,120,26),"选择同队")){var group=bot.BotsGroup;if(group!=null)foreach(var mate in bots)if(mate.BotsGroup?.Pointer==group.Pointer)checkedBots.Add(mate.GetPlayer.ProfileId);}float col=(row.width-40)/3;if(InterfaceMenu.Button(new Rect(10,top+74,col,30),"查看／修改"))AiActorControl.Select(world!,new[]{actor});if(InterfaceMenu.Button(new Rect(20+col,top+74,col,30),"背包物品"))AiBackpackEditor.Select(world!,actor);if(InterfaceMenu.Button(new Rect(30+2*col,top+74,col,30),"Buff 管理"))CharacterBuffs.SelectAi(world!,actor);}catch(Exception e){Warn(e);}if(visible.Length==0)InterfaceMenu.Label(new Rect(12,14,w-48,34),RaidClock.Active?"没有符合条件的 AI":"进入战局后显示 AI",null,14);GUI.EndScrollView();
  float bottom=rect.yMax-130,half=(w-12)/2,third=(w-20)/3;bool old=GUI.enabled;GUI.enabled=old&&picked.Length>0;if(InterfaceMenu.Button(new Rect(x,bottom,third,36),"管理所选（"+picked.Length+"）"))AiActorControl.Select(world!,picked);if(InterfaceMenu.Button(new Rect(x+third+10,bottom,third,36),"所选 AI Buff"))Action(()=>CharacterBuffs.SelectAi(world!,picked));if(InterfaceMenu.Button(new Rect(x+2*(third+10),bottom,third,36),"删除所选"))Action(()=>{int removed=AiActorControl.Delete(picked);notice="已删除 "+removed+" 名 AI";FeatureToasts.Show(notice);nextScan=0;});bottom+=46;if(InterfaceMenu.Button(new Rect(x,bottom,half,34),"暂停所选")){foreach(var actor in picked)selection.Set(actor.ProfileId,true);FeatureToasts.Show("所选 AI 已暂停");}if(InterfaceMenu.Button(new Rect(x+half+12,bottom,half,34),"恢复所选")){foreach(var actor in picked){selection.Set(actor.ProfileId,false);Restore(actor.ProfileId);}FeatureToasts.Show("所选 AI 已恢复");}GUI.enabled=old;InterfaceMenu.Label(new Rect(x,rect.yMax-34,w,28),notice,InterfaceMenu.Accent,12);
 }
 static void DrawSpawn(Rect rect){spawnScroll=GUI.BeginScrollView(rect,spawnScroll,new Rect(0,0,rect.width-20,700));float x=8,y=8,w=rect.width-36;InterfaceMenu.Label(new Rect(x,y,w,28),"选择角色",InterfaceMenu.Accent,17);y+=42;int oldGroup=roleGroup;Choice(new Rect(x,y,w,34),AiSpawnCatalog.Groups,ref roleGroup);y+=50;if(oldGroup!=roleGroup){role=Array.FindIndex(AiSpawnCatalog.Options,option=>option.Group==AiSpawnCatalog.Groups[roleGroup]);roleScroll=Vector2.zero;}var options=AiSpawnCatalog.Options.Select((option,index)=>(Option:option,Index:index)).Where(option=>option.Option.Group==AiSpawnCatalog.Groups[roleGroup]).ToArray();int columns=w>=740?3:2;float cell=(w-8*(columns-1))/columns;float height=Math.Max(52,MathF.Ceiling(options.Length/(float)columns)*46);roleScroll=GUI.BeginScrollView(new Rect(x,y,w,Math.Min(224,height)),roleScroll,new Rect(0,0,w-20,height));for(int i=0;i<options.Length;i++)if(InterfaceMenu.Button(new Rect(i%columns*(cell+8),i/columns*46,cell-8,38),options[i].Option.Name,role==options[i].Index))role=options[i].Index;GUI.EndScrollView();y+=Math.Min(224,height)+24;InterfaceMenu.Label(new Rect(x,y,w,27),"难度",null,14);y+=36;Choice(new Rect(x,y,w,34),difficultyNames,ref difficulty);y+=52;float half=(w-20)/2;InterfaceMenu.Label(new Rect(x,y,half,24),"数量",null,13);InterfaceMenu.Label(new Rect(x+half+20,y,half,24),"前方距离（米）",null,13);count=InterfaceMenu.Field(new Rect(x,y+30,half,34),count);distance=InterfaceMenu.Field(new Rect(x+half+20,y+30,half,34),distance);y+=84;float col=(w-20)/3;if(InterfaceMenu.Button(new Rect(x,y,col,38),"准星地面选点"))Action(()=>Pick(true));if(InterfaceMenu.Button(new Rect(x+col+10,y,col,38),"按距离选点"))Action(()=>Pick(false));bool busy=generating!=null||activating!=null,old=GUI.enabled;GUI.enabled=old&&RaidClock.Active&&ready&&!busy&&spawnAt.HasValue;if(InterfaceMenu.Button(new Rect(x+2*(col+10),y,col,38),"生成 AI"))Action(Spawn);GUI.enabled=old;y+=56;if(busy&&InterfaceMenu.Button(new Rect(x,y,180,34),"取消生成"))CancelSpawn(true);InterfaceMenu.Paragraph(new Rect(x,y+(busy?46:0),w,74),notice,InterfaceMenu.Accent,13);GUI.EndScrollView();}
 static void DrawGlobal(Rect rect){settingsScroll=GUI.BeginScrollView(rect,settingsScroll,new Rect(0,0,rect.width-20,520));float x=12,y=12,w=rect.width-44;InterfaceMenu.Label(new Rect(x,y,w,28),"生存与行为",InterfaceMenu.Accent,17);y+=46;if(InterfaceMenu.Button(new Rect(x,y,w,40),Plugin.Self.God.Value?"玩家无敌 · 开启":"玩家无敌 · 关闭",Plugin.Self.God.Value))Plugin.Self.God.Value=!Plugin.Self.God.Value;y+=54;if(InterfaceMenu.Button(new Rect(x,y,w,40),Plugin.Self.AIGod.Value?"全部 AI 无敌 · 开启":"全部 AI 无敌 · 关闭",Plugin.Self.AIGod.Value))Plugin.Self.AIGod.Value=!Plugin.Self.AIGod.Value;y+=54;if(InterfaceMenu.Button(new Rect(x,y,w,40),Plugin.Self.IgnoreAI.Value?"全部 AI 忽略玩家 · 开启":"全部 AI 忽略玩家 · 关闭",Plugin.Self.IgnoreAI.Value))Plugin.Self.IgnoreAI.Value=!Plugin.Self.IgnoreAI.Value;y+=74;float half=(w-12)/2;if(InterfaceMenu.Button(new Rect(x,y,half,40),"暂停全部 AI")){selection.PauseAll();FeatureToasts.Show("全部 AI 已暂停");}if(InterfaceMenu.Button(new Rect(x+half+12,y,half,40),"恢复全部 AI")){Resume();FeatureToasts.Show("全部 AI 已恢复");}y+=64;AiLimitCompatibility.Draw(new Rect(x,y,w,40));y+=60;if(InterfaceMenu.Button(new Rect(x,y,w,38),"预览受击方向")){IncomingDamage.Preview();if(RaidClock.Active)InterfaceMenu.Close();}GUI.EndScrollView();}
 internal static void SetPaused(IEnumerable<Player> actors,bool value){foreach(var actor in actors){selection.Set(actor.ProfileId,value);if(!value)Restore(actor.ProfileId);}nextScan=0;}
 internal static void DrawMarker(Camera camera){if(spawnAt is not Vector3 position||!RaidClock.Active)return;var p=camera.WorldToScreenPoint(position+Vector3.up*.15f);if(p.z<=0||p.x<0||p.x>Screen.width||p.y<0||p.y>Screen.height)return;var a=new Vector2(p.x,Screen.height-p.y);var c=new Color(.45f,.9f,1,.9f);RaidLoot.Line(a+new Vector2(0,-9),a+new Vector2(9,0),c,2);RaidLoot.Line(a+new Vector2(9,0),a+new Vector2(0,9),c,2);RaidLoot.Line(a+new Vector2(0,9),a+new Vector2(-9,0),c,2);RaidLoot.Line(a+new Vector2(-9,0),a+new Vector2(0,-9),c,2);InterfaceMenu.Label(new Rect(a.x+14,a.y-12,190,24),"AI 测试生成点",c,12);}
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+15;Plugin.Self.Error(e);}
}
