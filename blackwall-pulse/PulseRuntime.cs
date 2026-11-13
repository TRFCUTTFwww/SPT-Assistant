using System.Text.Json;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using UnityEngine;

namespace LocalBlackwallPulse;

internal static class PulseRuntime {
 internal static string Status{get;private set;}="等待进入战局";
 internal static bool Busy=>cast.Active;
 internal static Func<long,bool>? Protection;
 internal static string Home="";
 internal static ManualLogSource Log=null!;
 static readonly CastLatch cast=new();
 static readonly List<Target> targets=new();
 sealed record Target(Player Actor,string Id,IntPtr World,float Delay);
 static Player? owner;static IntPtr world,hands;
 static bool preview,assistant,blocked,enabled=true,walls,effects=true,filter;
 static float range=60,cone=70,volume=.65f,lastAssistant,nextError;
 static int limit=8,variant,casts,kills,lastTick=-1;static bool released;static float nextState;static long frames;
 internal static float Age=>cast.Active?Time.unscaledTime-cast.Started:-1;
 internal static void Initialize(string home,ManualLogSource log){Home=home;Log=log;ArmAnimation.Load(Path.Combine(home,"Assets","station-arms.glb"));PulseVisuals.Initialize();PulseAudio.Initialize();}
 internal static void Configure(bool on,float reach,float angle,int count,bool wall,bool fx,bool screen,float sound){enabled=on;range=float.IsFinite(reach)?Math.Clamp(reach,1,300):60;cone=float.IsFinite(angle)?Math.Clamp(angle,1,180):70;limit=Math.Clamp(count,1,24);walls=wall;effects=fx;filter=screen;volume=float.IsFinite(sound)?Math.Clamp(sound,0,1):.65f;}
 internal static void AssistantInput(bool value){assistant=true;lastAssistant=Time.unscaledTime;blocked=value;}
 internal static void Disconnect(){assistant=false;blocked=false;Protection=null;Cancel();}
 internal static bool InRaid(){var game=Singleton<AbstractGame>.Instance;return game!=null&&game.InRaid&&game.TryCast<HideoutGame>()==null;}
 static bool Allowed(Player? local)=>enabled&&InRaid()&&local?.IsYourPlayer==true&&local.HealthController?.IsAlive==true&&Application.isFocused&&!Cursor.visible&&Time.timeScale>0&&(!assistant||!blocked);
 internal static bool Cast(bool harmless=false){
  try{
   var current=Singleton<GameWorld>.Instance;var local=current?.MainPlayer;
   if(!Allowed(local)||current==null||local==null){Status="请进入战局并关闭菜单后施放";return false;}
   if(cast.Active){Status="正在收手";return false;}
   var camera=EFT.CameraControl.CameraManager.Instance?.Camera;if(camera==null){Status="玩家镜头尚未就绪";return false;}
   if(!ArmAnimation.Ready){Status="缺少车站动作资源，请重新安装资源包";return false;}
   var firearm=local.HandsController?.TryCast<Player.FirearmController>();
   if(!local.StateIsSuitableForHandInput||firearm!=null&&(firearm.IsInInteractionStrictCheck()||firearm.InCanNotBeInterruptedOperation()||!firearm.CanPressTrigger())){Status="请等待换弹、用药或手部动作完成";return false;}
   if(!ArmAnimation.Prepare(local,camera,variant++%4)){Status="没有找到玩家右手骨架，请查看 blackwall-state.json";WriteState();return false;}
   targets.Clear();owner=local;world=current.Pointer;hands=local.HandsController?.Pointer??IntPtr.Zero;preview=harmless;released=false;
   if(!harmless){
    var candidates=new List<(Player Actor,float Distance)>();var all=current.AllAlivePlayersList;
    if(all!=null)for(int i=0;i<all.Count;i++){
     var actor=all[i];if(actor==null||actor.Pointer==local.Pointer||actor.IsYourPlayer||!(actor.IsAI||actor.AIData?.BotOwner!=null)||actor.HealthController?.IsAlive!=true)continue;
     var point=actor.PlayerBones?.Ribcage?.Original?.position??actor.Position+Vector3.up;var delta=point-camera.transform.position;float distance=delta.magnitude;
     if(!PulseRules.Target(distance,Vector3.Dot(delta.normalized,camera.transform.forward),range,cone,true,true,false,true,walls))continue;
     int mask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider");
     if(!walls&&(mask==0||Physics.Raycast(camera.transform.position,delta.normalized,Math.Max(0,distance-.2f),mask,QueryTriggerInteraction.Ignore)))continue;
     if(Protection?.Invoke(actor.Pointer.ToInt64())==true)continue;
     candidates.Add((actor,distance));
    }
    foreach(var item in candidates.OrderBy(t=>t.Distance).Take(limit))targets.Add(new(item.Actor,item.Actor.ProfileId,world,targets.Count%3*.035f));
   }
   firearm?.SetTriggerPressed(false);firearm?.SetAim(false);killsBefore=kills;
   cast.Begin(Time.unscaledTime);casts++;Status=harmless?"动作与特效预览（不造成伤害）":$"黑墙脉冲：锁定 {targets.Count} 个 AI";
   if(effects)PulseVisuals.Begin(local,targets.Select(t=>t.Actor).ToArray());WriteState();return true;
  }catch(Exception error){Fail(error);return false;}
 }
 internal static void Tick(){
  try{
   if(lastTick==Time.frameCount)return;lastTick=Time.frameCount;frames++;
   if(assistant&&Time.unscaledTime-lastAssistant>3){assistant=false;Protection=null;blocked=false;}
#if !SPT_ASSISTANT
   var p=RuntimeHost.Settings;if(p==null)return;
   if(!assistant){Configure(p.Enabled.Value,p.Range.Value,p.Cone.Value,p.Limit.Value,p.ThroughWalls.Value,p.Effects.Value,p.Filter.Value,p.Volume.Value);if(p.Key.Value!=KeyCode.None&&Input.GetKeyDown(p.Key.Value))Cast();}
#endif
   PulseAudio.Tick();
   if(cast.Active){
    var current=Singleton<GameWorld>.Instance;
    var firearm=owner?.HandsController?.TryCast<Player.FirearmController>();
    if(!Allowed(owner)||current?.Pointer!=world||(owner?.HandsController?.Pointer??IntPtr.Zero)!=hands||firearm?.IsInInteractionStrictCheck()==true){Cancel();return;}
    if(cast.Step(Time.unscaledTime)){released=true;PulseVisuals.Release();}
    if(released&&!preview){foreach(var target in targets.ToArray())if(Age>=PulseRules.ReleaseAt+target.Delay){targets.Remove(target);Hit(current!,target);}}
    if(!cast.Active)Finish();
   }
   PulseVisuals.Tick(InRaid(),effects,Age);
   if(Time.unscaledTime>=nextState){nextState=Time.unscaledTime+2;WriteState();}
  }catch(Exception error){Fail(error);}
 }
 static void Hit(GameWorld current,Target entry){
  var target=entry.Actor;var local=owner;
  if(local==null||current.Pointer!=entry.World||target==null||target.ProfileId!=entry.Id||target.IsYourPlayer||target.Pointer==local.Pointer||!(target.IsAI||target.AIData?.BotOwner!=null)||target.ActiveHealthController is not {} health||!health.IsAlive||Protection?.Invoke(target.Pointer.ToInt64())==true)return;
  bool present=false;for(int i=0;i<current.AllAlivePlayersList.Count;i++)if(current.AllAlivePlayersList[i]?.Pointer==target.Pointer){present=true;break;}if(!present)return;
  float total=health.GetBodyPartHealth(EBodyPart.Common,false).Current;if(!float.IsFinite(total)||total<=0)return;
  float damage=Math.Max(10000,Math.Min(total,1000000)*10);var point=target.PlayerBones?.Ribcage?.Original?.position??target.Position+Vector3.up;
  var info=new DamageInfo{DamageType=EDamageType.Bullet,Damage=damage,Player=new PlayerBridge(local).Cast<IObserverToPlayerBridge>(),MasterOrigin=local.Position,HitPoint=point,Direction=(point-local.Position).normalized,SourceId="blackwall-pulse"};
  target.LastAggressor=local;health.ApplyDamage(EBodyPart.Head,damage,info);
  if(health.IsAlive&&Protection?.Invoke(target.Pointer.ToInt64())!=true)health.Kill(EDamageType.Bullet);
  if(!health.IsAlive){
   // Burnout/short-circuit files contain impact audio. Submit them only on
   // confirmed kills, never empty casts or harmless animation previews.
   if(kills==killsBefore)PulseAudio.Impact(volume);
   kills++;PulseAudio.Death(point,volume);
  }
 }
 internal static bool Owns(Player.FirearmController controller)=>cast.Active&&owner?.HandsController?.Pointer==controller.Pointer;
 internal static void Late(bool beforeRender=false){try{if(cast.Active&&(!Allowed(owner)||Singleton<GameWorld>.Instance?.Pointer!=world)){Cancel();return;}ArmAnimation.Apply(owner,Age,beforeRender);PulseVisuals.Late();}catch(Exception error){Fail(error);}}
 internal static void Draw(){try{if(filter&&cast.Active&&effects)PulseVisuals.DrawFilter(Age);}catch(Exception error){Fail(error);}}
 static void Finish(){ArmAnimation.Reset();owner=null;targets.Clear();released=false;Status=preview?"预览完成":$"施放完成，本次已击杀 {kills-killsBefore} 个 AI";killsBefore=kills;WriteState();}
 static int killsBefore;
 static void Cancel(){cast.Cancel();ArmAnimation.Reset();PulseVisuals.Clear();PulseAudio.Stop();targets.Clear();owner=null;released=false;Status="施放已取消";killsBefore=kills;WriteState();}
 static void Fail(Exception error){Cancel();Status="黑墙脉冲错误："+error.Message;if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10;Log.LogWarning(error);}WriteState();}
 internal static string Diagnostics()=>JsonSerializer.Serialize(new{version="0.2.10",status=Status,busy=cast.Active,age=Age,frames,casts,kills,assistant,blocked,enabled,range,cone,limit,walls,effects,filter,arm=ArmAnimation.Diagnostics(),rig=ArmAnimation.InspectRig(Singleton<GameWorld>.Instance?.MainPlayer),visuals=PulseVisuals.Diagnostics(),audio=PulseAudio.Diagnostics(),utc=DateTime.UtcNow});
 internal static bool SoundPreview()=>PulseAudio.Preview(volume);
 static void WriteState(){try{File.WriteAllText(Path.Combine(Home,"blackwall-state.json"),Diagnostics());}catch{}}
 internal static void Shutdown(){Cancel();ArmAnimation.Shutdown();PulseVisuals.Shutdown();PulseAudio.Shutdown();Protection=null;}
}
