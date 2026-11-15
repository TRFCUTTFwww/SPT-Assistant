using System.Reflection;
using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using Local.ModHost;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
using GameTask=Il2CppSystem.Threading.Tasks.Task<UnityEngine.GameObject>;
namespace Scp3X.Client;
public sealed partial class DisruptorModule:IHotModule {
 static DisruptorModule? active;
 ModuleContext? context;DisruptorAssets? assets;DisruptorRig? rig;CatalogSync? catalog;bool nativeRigValidated;
 readonly Dictionary<IntPtr,ModelSwap> models=[];
 readonly List<(Item Item,GameTask Task,float Started)> pendingModels=[];
 readonly Dictionary<IntPtr,ItemView> icons=[];
 readonly List<(BallisticsCalculator Calculator,Ammo Ammo,Item Weapon,Vector3 Origin,Vector3 Direction,int FireIndex,int Raid,float Due)> extraShots=[];
 [ThreadStatic] static string? creating;
 bool driving,extra,disintegration=true,wasReloading,wasAiming,discoverIcons=true;
 Player.FirearmController? charge;IntPtr hands,world;
 float due,nextFire,nextScan,nextStatus;int shots,kills,erased,errors,modelsApplied;string lastError="";
 public int ApiVersion=>1;
 public string Version=>"1.0.2";
 public bool CanReload=>charge==null&&deaths.Count==0&&dissolving.Count==0&&extraShots.Count==0&&catalog?.Busy!=true;
 public void Initialize(ModuleContext context){
  this.context=context;context.OnShutdown(Cleanup);active=this;assets=new(context);catalog=new(context);
  var probeRoot=new GameObject("3-X rig validation"){hideFlags=HideFlags.HideAndDontSave};probeRoot.SetActive(false);try{using var probe=new DisruptorRig(assets,probeRoot.transform,0);foreach(string animation in assets.Animations.Keys){probe.Play(animation);probe.Tick();}nativeRigValidated=true;}finally{Object.Destroy(probeRoot);}
  var h=new Harmony(context.PatchId);
  h.Patch(AccessTools.Method(typeof(Player.FirearmController),nameof(Player.FirearmController.SetTriggerPressed),[typeof(bool)]),prefix:new HarmonyMethod(typeof(DisruptorModule),nameof(Trigger)));
  h.Patch(AccessTools.Method(typeof(BallisticsCalculator),nameof(BallisticsCalculator.CreateShot)),postfix:new HarmonyMethod(typeof(DisruptorModule),nameof(ShotCreated)));
  h.Patch(AccessTools.Method(typeof(Shot),nameof(Shot.HandleCollision)),postfix:new HarmonyMethod(typeof(DisruptorModule),nameof(Collision)));
  h.Patch(typeof(Player).GetMethods().Single(m=>m.DeclaringType==typeof(Player)&&m.Name=="OnBeenKilledByAggressor"),postfix:new HarmonyMethod(typeof(DisruptorModule),nameof(Killed)));
  h.Patch(AccessTools.Method(typeof(Player.FirearmController),"PlayFireBulletSound"),prefix:new HarmonyMethod(typeof(DisruptorModule),nameof(AllowNativeSound)));
  foreach(var method in typeof(ObjectsFactory).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Where(m=>!m.IsGenericMethod&&m.GetParameters().FirstOrDefault()?.ParameterType==typeof(Item))){
   if(method.ReturnType==typeof(GameObject)&&method.Name is "CreateItem" or "CreateItemUsablePrefab" or "CreateCleanLootPrefab" or "CreateLootPrefab")h.Patch(method,prefix:new HarmonyMethod(typeof(DisruptorModule),nameof(Creating)),postfix:new HarmonyMethod(typeof(DisruptorModule),nameof(Created)),finalizer:new HarmonyMethod(typeof(DisruptorModule),nameof(CreationEnded)));
   else if(method.ReturnType==typeof(GameTask)&&method.Name is "CreateItemAsync" or "CreateItemUsablePrefabAsync" or "CreateCleanLootPrefabAsync")h.Patch(method,prefix:new HarmonyMethod(typeof(DisruptorModule),nameof(Creating)),postfix:new HarmonyMethod(typeof(DisruptorModule),nameof(CreatedAsync)),finalizer:new HarmonyMethod(typeof(DisruptorModule),nameof(CreationEnded)));
  }
  h.Patch(AccessTools.Method(typeof(ObjectsFactory),nameof(ObjectsFactory.PopOrCreate)),postfix:new HarmonyMethod(typeof(DisruptorModule),nameof(Popped)));
  h.Patch(AccessTools.Method(typeof(ItemView),nameof(ItemView.IconChangedHandler)),prefix:new HarmonyMethod(typeof(DisruptorModule),nameof(IconBefore)),finalizer:new HarmonyMethod(typeof(DisruptorModule),nameof(IconAfter)));
  Status();context.Log(LogLevel.Info,"3-X ready: source skinned meshes, eleven source animation clips, fusion ammunition and confirmed-kill corpse disintegration. In-game appearance requires verification.");
 }
 static Player? Local(){var p=Singleton<GameWorld>.Instance?.MainPlayer;return p?.IsYourPlayer==true&&p.HealthController?.IsAlive==true?p:null;}
 static bool Own(Player.FirearmController? c)=>c!=null&&DisruptorRules.IsWeapon(c.GetItem()?.StringTemplateId)&&c._player?.Pointer==Local()?.Pointer;
 static bool Trigger(Player.FirearmController __instance,bool __0){
  var a=active;if(a?.context?.IsActive!=true||!Own(__instance)||a.driving||!__0)return true;
  try{
   var p=Local();var w=__instance.GetItem()?.TryCast<Weapon>();
   if(!DisruptorRules.CanFire(p?.IsYourPlayer==true,p?.HealthController?.IsAlive==true,Singleton<GameWorld>.Instance!=null,w!=null&&__instance.CanPressTrigger(),Cursor.visible||Time.timeScale<=0||a.charge!=null||Time.unscaledTime<a.nextFire,Own(__instance)))return false;
   if(w==null)return false;
   if(w.GetCurrentMagazineCount()+w.ChamberAmmoCount<=0)return true;
   a.charge=__instance;a.due=Time.unscaledTime+(a.disintegration?DisruptorRules.SingleChargeSeconds:DisruptorRules.BurstChargeSeconds);a.rig?.Play(a.disintegration?"shoot_single":"shoot_rapid");a.assets!.Play(a.disintegration?"3x_single_shot":"3x_rapid_shot");return false;
  }catch(Exception e){a.Error(e);return false;}
 }
 static bool AllowNativeSound(Player.FirearmController __instance)=>active?.context?.IsActive!=true||!DisruptorRules.IsWeapon(__instance.GetItem()?.StringTemplateId);
 static void ShotCreated(BallisticsCalculator __instance,Ammo __0,Vector3 __1,Vector3 __2,int __3,int __5,Item __6,Shot __result){
  var a=active;if(a?.context?.IsActive!=true||__result==null||__6==null||!DisruptorRules.IsWeapon(__6.StringTemplateId)||__5!=Local()?.RaidId)return;
  try{
   __result.Damage=a.disintegration?800:250;a.shots++;a.Beam(__result,__1,__2);if(a.extra){if(a.extraShots.Count==0)a.StartRecovery();return;}
   if(a.disintegration)a.StartRecovery();
   if(!a.disintegration){for(int i=1;i<=2;i++)a.extraShots.Add((__instance,__0,__6,__1,Quaternion.AngleAxis((i==1?-1:1)*.55f,Vector3.up)*__2,__3,__5,Time.unscaledTime+i*.5f));}
  }catch(Exception e){a.Error(e);}
 }
 void StartRecovery(){nextFire=Time.unscaledTime+DisruptorRules.RecoverySeconds;rig?.Play(disintegration?"break_single":"break_rapid",DisruptorRules.RecoverySeconds);var weapon=Local()?.HandsController?.GetItem()?.TryCast<Weapon>();bool last=weapon!=null&&weapon.GetCurrentMagazineCount()+weapon.ChamberAmmoCount<=1;assets?.Play((disintegration?"3x_single_action":"3x_rapid_action")+(last?"_last":""));}
 static void Collision(Shot __instance){var a=active;if(a?.context?.IsActive!=true||!DisruptorRules.IsWeapon(__instance?.Weapon?.StringTemplateId))return;try{a.CollisionBeam(__instance!);}catch(Exception e){a.Error(e);}}
 static void Killed(Player __instance,IPlayer __0,DamageInfo __1,EBodyPart __2){
  var a=active;var local=Local();if(a?.context?.IsActive!=true||local==null||__1==null||!DisruptorRules.ConfirmedKill(__1.Weapon?.StringTemplateId,__instance.HealthController?.IsAlive!=false,__instance.IsYourPlayer,__0?.ProfileId,local.ProfileId))return;
  try{a.QueueDeath(__instance,__1.HitPoint);}catch(Exception e){a.Error(e);}
 }
 static void Creating(Item __0,out string? __state){__state=creating;creating=__0?.StringTemplateId;}
 static void CreationEnded(string? __state){creating=__state;}
 static void Created(Item __0,GameObject __result)=>active?.TryModel(__0?.StringTemplateId,__result);
 static void Popped(GameObject __result)=>active?.TryModel(creating,__result);
 static void CreatedAsync(Item __0,GameTask __result){var a=active;if(a?.context?.IsActive!=true||__0==null||__result==null||__0.StringTemplateId is not (DisruptorRules.Weapon or DisruptorRules.DonorWeapon))return;if(__result.IsCompleted){if(!__result.IsFaulted&&!__result.IsCanceled)a.TryModel(__0.StringTemplateId,__result.Result);}else if(!a.pendingModels.Any(p=>p.Task.Pointer==__result.Pointer))a.pendingModels.Add((__0,__result,Time.unscaledTime));}
 sealed class ModelSwap {
  internal required GameObject Root,Visual;
  internal required (Renderer Renderer,bool Enabled)[] Originals;
  internal bool FirstPerson;
 }
 void TryModel(string? id,GameObject? root){if(context?.IsActive!=true||root==null||id is not (DisruptorRules.Weapon or DisruptorRules.DonorWeapon))return;try{ApplyModel(id,root);}catch(Exception e){Error(e);}}
 void ApplyModel(string id,GameObject root){
  if(models.TryGetValue(root.Pointer,out var existing)){if(id==DisruptorRules.Weapon)return;RestoreModel(existing);models.Remove(root.Pointer);return;}
  if(id!=DisruptorRules.Weapon)return;
  var renderers=root.GetComponentsInChildren<Renderer>(true).Where(r=>r.TryCast<MeshRenderer>()!=null||r.TryCast<SkinnedMeshRenderer>()!=null).Where(r=>!r.name.Contains("hands",StringComparison.OrdinalIgnoreCase)&&!r.name.Contains("arms",StringComparison.OrdinalIgnoreCase)).ToArray();if(renderers.Length==0)return;
  var anchor=renderers.FirstOrDefault(r=>r.GetComponent<MeshFilter>()?.sharedMesh!=null)??renderers[0];
  var visual=new GameObject("3-X World Model"){hideFlags=HideFlags.HideAndDontSave,layer=anchor.gameObject.layer};visual.transform.SetParent(anchor.transform,false);
  var f=visual.AddComponent<MeshFilter>();f.sharedMesh=assets!.Meshes[assets.Data.GetProperty("worldMesh").GetInt32()];var renderer=visual.AddComponent<MeshRenderer>();renderer.sharedMaterials=assets.Data.GetProperty("worldMaterials").EnumerateArray().Select(v=>assets.Materials[v.GetInt32()]).ToArray();
  var native=anchor.GetComponent<MeshFilter>()?.sharedMesh;if(native!=null){var source=f.sharedMesh.bounds;var target=native.bounds;Vector3 Axis(Vector3 size)=>size.x>size.y&&size.x>size.z?Vector3.right:size.y>size.z?Vector3.up:Vector3.forward;visual.transform.localRotation=Quaternion.FromToRotation(Axis(source.size),Axis(target.size));float scale=Math.Max(target.size.x,Math.Max(target.size.y,target.size.z))/Math.Max(source.size.x,Math.Max(source.size.y,source.size.z));visual.transform.localScale=Vector3.one*scale;visual.transform.localPosition=target.center-visual.transform.localRotation*(source.center*scale);}
  models[root.Pointer]=new(){Root=root,Visual=visual,Originals=renderers.Select(r=>(r,r.enabled)).ToArray()};foreach(var r in renderers)r.enabled=false;modelsApplied++;
 }
 static void RestoreModel(ModelSwap m){foreach(var r in m.Originals)if(r.Renderer!=null)r.Renderer.enabled=r.Enabled;if(m.Visual!=null)Object.Destroy(m.Visual);}
 static void IconBefore(ItemView __instance,out ItemIcon? __state){__state=null;var a=active;if(a?.context?.IsActive!=true||!DisruptorRules.IsWeapon(__instance.Item?.StringTemplateId)||__instance._itemIcon==null)return;__state=__instance._itemIcon;__instance._itemIcon=a.assets!.Icon;a.icons[__instance.Pointer]=__instance;}
 static void IconAfter(ItemView __instance,ItemIcon? __state){if(__state!=null)__instance._itemIcon=__state;}
 public void Tick(){if(context?.IsActive!=true)return;try{
  catalog?.Tick(charge!=null||deaths.Count>0||dissolving.Count>0);
  var current=Singleton<GameWorld>.Instance;var p=Local();var pointer=current?.Pointer??IntPtr.Zero;if(pointer!=world){ResetRaid();world=pointer;}
  var controller=p?.HandsController?.TryCast<Player.FirearmController>();bool equipped=Own(controller);var handPointer=equipped?controller!.Pointer:IntPtr.Zero;
  if(handPointer!=hands){rig?.Dispose();rig=null;charge=null;hands=handPointer;wasReloading=wasAiming=false;if(equipped){var camera=EFT.CameraControl.CameraManager.Instance?.Camera;if(camera!=null){rig=new(assets!,camera.transform,controller!.ControllerGameObject.layer);rig.Play("draw");assets!.Play("3x_draw");}TryModel(DisruptorRules.Weapon,controller!.ControllerGameObject);}}
  foreach(var m in models.Values){bool first=equipped&&m.Root?.Pointer==controller!.ControllerGameObject?.Pointer;m.FirstPerson=first;if(m.Visual!=null)m.Visual.SetActive(!first);foreach(var r in m.Originals)if(r.Renderer!=null)r.Renderer.enabled=false;}
  if(equipped){
   if(!Cursor.visible&&Time.timeScale>0&&Input.GetKeyDown(KeyCode.Mouse2)&&charge==null&&extraShots.Count==0&&Time.unscaledTime>=nextFire){disintegration=!disintegration;assets!.Play(disintegration?"3x_select_single":"3x_select_triple");}
   bool reloading=controller!.IsInReloadOperation();if(reloading&&!wasReloading){rig?.Play("reload");assets!.Play("3x_reload");}wasReloading=reloading;
   if(controller.IsAiming!=wasAiming){wasAiming=controller.IsAiming;if(charge==null&&!reloading){rig?.Play(wasAiming?"ads_in":"ads_out");assets!.Play(wasAiming?"3x_ads_in":"3x_ads_out");}}
   if(!Cursor.visible&&Input.GetKeyDown(KeyCode.L)&&charge==null&&!reloading){rig?.Play("inspect");assets!.Play("3x_inspect0_2");}
  }
  if(charge!=null){if(!equipped||charge.Pointer!=controller!.Pointer||Cursor.visible||Time.timeScale<=0){charge=null;}else if(Time.unscaledTime>=due){var firing=charge;charge=null;if(firing.CanPressTrigger()){nextFire=Time.unscaledTime+DisruptorRules.RecoverySeconds;driving=true;try{firing.SetTriggerPressed(true);firing.SetTriggerPressed(false);}finally{driving=false;}}}}
  for(int i=extraShots.Count-1;i>=0;i--){var shot=extraShots[i];if(Time.unscaledTime<shot.Due)continue;extraShots.RemoveAt(i);if(current==null||p==null||shot.Raid!=p.RaidId)continue;try{extra=true;var created=shot.Calculator.CreateShot(shot.Ammo,shot.Origin,shot.Direction,shot.FireIndex,Vector3.zero,shot.Raid,shot.Weapon,1,0);shot.Calculator.Shoot(created);}finally{extra=false;}}
  TickEffects(current);
  if(Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+.5f;
   if(discoverIcons){discoverIcons=false;foreach(var view in Resources.FindObjectsOfTypeAll<ItemView>())if(view!=null&&!view.IsKilled&&DisruptorRules.IsWeapon(view.Item?.StringTemplateId)){icons[view.Pointer]=view;if(view.gameObject.activeInHierarchy)view.IconChangedHandler();}}
   foreach(var key in icons.Where(p=>p.Value==null||p.Value.IsKilled).Select(p=>p.Key).ToArray())icons.Remove(key);
   foreach(var view in icons.Values)if(view.gameObject.activeInHierarchy&&DisruptorRules.IsWeapon(view.Item?.StringTemplateId)&&view.MainImage?.sprite?.Pointer!=assets!.Sprite.Pointer)view.IconChangedHandler();
   foreach(var key in models.Where(p=>p.Value.Root==null).Select(p=>p.Key).ToArray()){if(models[key].Visual!=null)Object.Destroy(models[key].Visual);models.Remove(key);}
  }
  for(int i=pendingModels.Count-1;i>=0;i--){var pending=pendingModels[i];if(!pending.Task.IsCompleted){if(Time.unscaledTime-pending.Started>30)pendingModels.RemoveAt(i);continue;}pendingModels.RemoveAt(i);if(!pending.Task.IsFaulted&&!pending.Task.IsCanceled)TryModel(pending.Item.StringTemplateId,pending.Task.Result);}
  if(Time.unscaledTime>=nextStatus){nextStatus=Time.unscaledTime+5;Status();}
 }catch(Exception e){Error(e);}}
 public void LateTick(){rig?.Tick();}
 public void Draw(){if(rig==null||Cursor.visible)return;var style=new GUIStyle(GUI.skin.label){fontSize=14,alignment=TextAnchor.MiddleCenter};style.normal.textColor=new Color(.3f,1,.7f);string state=charge!=null?"蓄能中…":Time.unscaledTime<nextFire?"冷却 "+(nextFire-Time.unscaledTime).ToString("0.0")+"s":"就绪";GUI.Label(new Rect(Screen.width*.5f-180,Screen.height-120,360,26),"3-X · "+(disintegration?"分解":"连束")+" · "+state+" · 中键切换",style);}
 public void Shutdown()=>Cleanup();
 void Error(Exception e){errors++;lastError=e.GetBaseException().Message;if(errors<=8||errors%100==0)context?.Log(LogLevel.Warning,"3-X: "+e);}
 void Status(){if(context==null)return;File.WriteAllText(Path.Combine(context.DataDirectory,"status.json"),JsonSerializer.Serialize(new{version=Version,utc=DateTime.UtcNow,pid=Environment.ProcessId,ready=assets!=null,nativeRigValidated,shots,kills,erased,errors,lastError,modelsApplied,pendingDeaths=deaths.Count,dissolving=dissolving.Count,sourceAnimations=assets?.Animations.Count,disintegration,visualVerification="user-required"}));}
 void ResetRaid(){charge=null;extraShots.Clear();rig?.Dispose();rig=null;hands=IntPtr.Zero;ClearEffects();}
 void Cleanup(){if(active==this)active=null;ResetRaid();foreach(var m in models.Values)RestoreModel(m);models.Clear();pendingModels.Clear();foreach(var view in icons.Values)try{if(view!=null&&!view.IsKilled&&view.gameObject.activeInHierarchy)view.IconChangedHandler();}catch{}icons.Clear();assets?.Dispose();assets=null;catalog=null;context=null;creating=null;}
}
