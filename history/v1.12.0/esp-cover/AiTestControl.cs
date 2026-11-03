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
 static int roleGroup;static Vector2 roleScroll,panelScroll;static bool requestedBoss;
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
 static bool AllowBrain(BotOwner __instance)=>!Paused(__instance?.GetPlayer);
 static bool AllowTrigger(Player.FirearmController __instance,bool __0)=>!__0||!Paused(__instance?._player);
 internal static void Tick(GameWorld? current,Player? local){try{
  if(current==null||local==null){if(worldPointer!=IntPtr.Zero)Reset();return;}
  if(worldPointer!=current.Pointer){Reset();worldPointer=current.Pointer;world=current;player=local;notice="先选择准星地面或前方距离作为生成位置。";}
  world=current;player=local;
  if(Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+.5f;bots.Clear();var all=current.AllAlivePlayersList;
   if(all!=null)for(int i=0;i<all.Count;i++){var bot=all[i];if(bot==null||bot.IsYourPlayer||!bot.IsAI||bot.HealthController?.IsAlive!=true)continue;var owner=bot.AIData?.BotOwner;if(owner!=null)bots.Add(owner);}
   bots.Sort((a,b)=>Vector3.Distance(a.GetPlayer.Position,local.Position).CompareTo(Vector3.Distance(b.GetPlayer.Position,local.Position)));
  }
  foreach(var id in frozen.Keys.ToArray())if(!selection.Contains(id)||frozen[id].Bot?.GetPlayer?.HealthController?.IsAlive!=true)Restore(id);
  foreach(var bot in bots){var p=bot.GetPlayer;if(!selection.Contains(p.ProfileId))continue;
   if(!frozen.ContainsKey(p.ProfileId)){var mover=bot.Mover;frozen[p.ProfileId]=new Frozen{Bot=bot,Pause=mover?.Pause??false,PauseUntil=mover?._pauseEndTime??0};}
   bot.StopMove();if(bot.Mover!=null){bot.Mover.Pause=true;bot.Mover._pauseEndTime=float.MaxValue;}
   p.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);
  }
  PollSpawn();
 }catch(Exception e){if(generating!=null||activating!=null||data!=null){CancelSpawn(false);notice="AI 生成失败："+e.Message;}Warn(e);}}
 static void Restore(string id){if(!frozen.Remove(id,out var snapshot))return;try{var mover=snapshot.Bot?.Mover;if(mover!=null){mover._pauseEndTime=snapshot.PauseUntil;mover.Pause=snapshot.Pause&&snapshot.PauseUntil>Time.time;}}catch(Exception e){Warn(e);}}
 static void Resume(){selection.Clear();foreach(var id in frozen.Keys.ToArray())Restore(id);}
 internal static void Reset(){Resume();CancelSpawn(false);bots.Clear();created.Clear();world=null;player=null;spawnAt=null;worldPointer=IntPtr.Zero;nextScan=0;}
 static void CancelSpawn(bool user,bool stop=true){
  requestSerial++;
  try{if(stop){data?.StopSpawn();cancellation?.Cancel();}}catch(Exception e){Warn(e);}
  try{cancellation?.Dispose();}catch{}
  generating=null;activating=null;data=null;cancellation=null;callback=null;positions=null;spawner=null;zone=null;
  if(user)notice="已取消后续生成；已经出现的 AI 保留。";
 }
 static BotsController Controller()=>Singleton<AbstractGame>.Instance?.TryCast<IBotGame>()?.BotsController??throw new ArgumentException("当前战局尚未开放 AI 控制器。");
 static Vector3 Ground(Vector3 point){
  if(!NavMesh.SamplePosition(point,out var hit,2.5f,NavMesh.AllAreas)||Math.Abs(hit.position.y-point.y)>1.8f)throw new ArgumentException("所选位置没有可行走道路，请重新对准地面选点。");
  if(player==null||Vector3.Distance(hit.position,player.Position)<5)throw new ArgumentException("生成位置必须距离玩家至少 5 米。");return hit.position;
 }
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
  var holder=Object.FindObjectOfType<AICorePointHolder>()??throw new ArgumentException("当前地图尚未提供 AI 导航节点。");
  positions=new Il2CppSystem.Collections.Generic.List<ISpawnPoint>();
  for(int i=0;i<requested;i++){
   var offset=i==0?Vector3.zero:new Vector3(Mathf.Cos(i*2.4f),0,Mathf.Sin(i*2.4f))*2.2f;
   var point=Ground(target+offset);var core=AICorePointHolder.GetClosest(point)??throw new ArgumentException("生成位置附近没有 AI 导航节点。");
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
   activating=spawner!.ActivateBots(positions!,zone!,data,callback,new BotSpawnBossAndGroupParams(requestedBoss,requested>1),cancellation!.Token);
   notice="AI 资料已就绪，正在地图中激活…";
  }
  if(activating!=null&&activating.IsCompleted){
   string? error=activating.IsFaulted?activating.Exception?.ToString():activating.IsCanceled?"请求已取消":null;
   int made=created.Count;CancelSpawn(false,error!=null);notice=error!=null?"激活 AI 失败："+error:made>0?$"已生成 {made} 个测试 AI。":"激活流程已结束，尚未收到 AI 成功回调，请检查服务器与地图限制。";
  }
 }
 static void Action(Action action){try{action();}catch(Exception e){notice=e.Message;Warn(e);if(generating==null&&activating==null)CancelSpawn(false);}}
 static void Choice(Rect rect,string[] labels,ref int selected){float width=(rect.width-6*(labels.Length-1))/labels.Length;for(int i=0;i<labels.Length;i++)if(InterfaceMenu.Button(new Rect(rect.x+i*(width+6),rect.y,width,rect.height),(selected==i?"✓ ":"")+labels[i]))selected=i;}
 internal static void Draw(Rect rect){var height=Math.Max(rect.height-1,660);panelScroll=GUI.BeginScrollView(rect,panelScroll,new Rect(0,0,rect.width-20,height));DrawContent(new Rect(0,0,rect.width-20,height));GUI.EndScrollView();}
 static void DrawContent(Rect rect){
  float x=rect.x,w=rect.width,y=rect.y;InterfaceMenu.Label(new Rect(x,y,w,28),"AI 测试控制",InterfaceMenu.Text,18);y+=36;
  InterfaceMenu.Paragraph(new Rect(x,y,w,38),"手动生成与暂停 AI；暂停全部也会影响随后刷新的 AI。单个 AI 可单独恢复。",InterfaceMenu.Text,12);y+=44;
  int oldGroup=roleGroup;Choice(new Rect(x,y,w,30),AiSpawnCatalog.Groups,ref roleGroup);y+=36;
  if(oldGroup!=roleGroup){role=Array.FindIndex(AiSpawnCatalog.Options,o=>o.Group==AiSpawnCatalog.Groups[roleGroup]);roleScroll=Vector2.zero;}
  var choices=AiSpawnCatalog.Options.Select((option,index)=>(Option:option,Index:index)).Where(o=>o.Option.Group==AiSpawnCatalog.Groups[roleGroup]).ToArray();
  roleScroll=GUI.BeginScrollView(new Rect(x,y,w,100),roleScroll,new Rect(0,0,w-20,MathF.Ceiling(choices.Length/3f)*35));float cell=(w-32)/3;for(int i=0;i<choices.Length;i++)if(InterfaceMenu.Button(new Rect(i%3*(cell+6),i/3*35,cell,30),choices[i].Option.Name,role==choices[i].Index))role=choices[i].Index;GUI.EndScrollView();y+=108;Choice(new Rect(x,y,w,30),difficultyNames,ref difficulty);y+=40;
  InterfaceMenu.Label(new Rect(x,y,58,28),"数量",InterfaceMenu.Text,12);count=InterfaceMenu.Field(new Rect(x+58,y,64,28),count);InterfaceMenu.Label(new Rect(x+140,y,100,28),"前方距离（米）",InterfaceMenu.Text,12);distance=InterfaceMenu.Field(new Rect(x+244,y,70,28),distance);
  if(InterfaceMenu.Button(new Rect(x+w-190,y,190,28),"预览受击方向（不损血）")){IncomingDamage.Preview();if(RaidClock.Active)InterfaceMenu.Close();}y+=37;
  float col=(w-18)/3;if(InterfaceMenu.Button(new Rect(x,y,col,32),"准星地面选点"))Action(()=>Pick(true));if(InterfaceMenu.Button(new Rect(x+col+9,y,col,32),"按前方距离选点"))Action(()=>Pick(false));
  bool busy=generating!=null||activating!=null;var old=GUI.enabled;GUI.enabled=old&&RaidClock.Active&&ready&&!busy&&spawnAt.HasValue;
  if(InterfaceMenu.Button(new Rect(x+2*(col+9),y,col,32),"生成 AI"))Action(Spawn);GUI.enabled=old;y+=40;
  if(busy){if(InterfaceMenu.Button(new Rect(x+w-110,y,110,30),"取消生成"))CancelSpawn(true);}
  string status=notice.Replace('\r',' ').Replace('\n',' ');if(status.Length>180)status=status[..180]+"…";InterfaceMenu.Paragraph(new Rect(x,y,w-(busy?120:0),42),status,InterfaceMenu.Accent,12);y+=49;
  old=GUI.enabled;GUI.enabled=old&&RaidClock.Active&&ready;
  float toggleWidth=(w-16)/3;
  if(InterfaceMenu.Button(new Rect(x,y,toggleWidth,30),Plugin.Self.God.Value?"玩家无敌：开":"玩家无敌：关"))Plugin.Self.God.Value=!Plugin.Self.God.Value;
  if(InterfaceMenu.Button(new Rect(x+toggleWidth+8,y,toggleWidth,30),Plugin.Self.AIGod.Value?"AI 无敌：开":"AI 无敌：关"))Plugin.Self.AIGod.Value=!Plugin.Self.AIGod.Value;
  if(InterfaceMenu.Button(new Rect(x+2*(toggleWidth+8),y,toggleWidth,30),Plugin.Self.IgnoreAI.Value?"AI 忽略玩家：开":"AI 忽略玩家：关"))Plugin.Self.IgnoreAI.Value=!Plugin.Self.IgnoreAI.Value;y+=38;
  if(InterfaceMenu.Button(new Rect(x,y,(w-10)/2,32),selection.All?"✓ 全局暂停已开启":"暂停全部 AI")){selection.PauseAll();notice="全部 AI 已暂停，包括随后生成的 AI。";}
  if(InterfaceMenu.Button(new Rect(x+(w+10)/2,y,(w-10)/2,32),"恢复全部 AI")){Resume();notice="全部 AI 已恢复。";}GUI.enabled=old;y+=42;
  InterfaceMenu.Label(new Rect(x,y,w,24),$"当前存活 AI：{bots.Count}    已暂停：{bots.Count(b=>selection.Contains(b.GetPlayer.ProfileId))}    本次生成：{created.Count}",InterfaceMenu.Text,12);y+=30;
  float height=Math.Max(60,rect.yMax-y);scroll=GUI.BeginScrollView(new Rect(x,y,w,height),scroll,new Rect(0,0,w-20,Math.Max(height-1,bots.Count*42)));
  for(int i=0;i<bots.Count;i++){try{var bot=bots[i];var p=bot.GetPlayer;bool paused=selection.Contains(p.ProfileId);var row=new Rect(0,i*42,w-24,36);InterfaceMenu.Surface(row,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.045f),7);
   var name=(p.Profile?.Nickname??"AI").Replace('\n',' ');if(name.Length>16)name=name[..16]+"…";
   InterfaceMenu.Label(new Rect(10,row.y,row.width-105,36),name+" · "+bot.Role+$" · {Vector3.Distance(p.Position,player!.Position):F0}米"+(created.Contains(p.ProfileId)?" · 测试":""),InterfaceMenu.Text,12);
   if(InterfaceMenu.Button(new Rect(row.width-88,row.y+3,80,30),paused?"恢复":"暂停"))selection.Set(p.ProfileId,!paused);
  }catch(Exception e){Warn(e);}}
  GUI.EndScrollView();
 }
 internal static void DrawMarker(Camera camera){if(spawnAt is not Vector3 position||!RaidClock.Active)return;var p=camera.WorldToScreenPoint(position+Vector3.up*.15f);if(p.z<=0||p.x<0||p.x>Screen.width||p.y<0||p.y>Screen.height)return;var a=new Vector2(p.x,Screen.height-p.y);var c=new Color(.45f,.9f,1,.9f);RaidLoot.Line(a+new Vector2(0,-9),a+new Vector2(9,0),c,2);RaidLoot.Line(a+new Vector2(9,0),a+new Vector2(0,9),c,2);RaidLoot.Line(a+new Vector2(0,9),a+new Vector2(-9,0),c,2);RaidLoot.Line(a+new Vector2(-9,0),a+new Vector2(0,-9),c,2);InterfaceMenu.Label(new Rect(a.x+14,a.y-12,190,24),"AI 测试生成点",c,12);}
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+15;Plugin.Self.Error(e);}
}
