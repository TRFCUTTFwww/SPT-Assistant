using BepInEx.Configuration;
using EFT;
using EFT.CameraControl;
using HarmonyLib;
using UnityEngine;
using NativeNightVision=BSG.CameraEffects.NightVision;
namespace LocalSpt5Esp;
internal static partial class NightVisionAssist {
 internal static ConfigEntry<bool> Enabled=null!;
 static ConfigEntry<KeyCode> ToggleKey=null!;
 static Channel full=null!,scope=null!;
 static EffectLease? fullLease,scopeLease;
 static IntPtr scopeSight;
 static bool writing;
 static float nextError;
 static string notice="";
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"16. Night vision","Enabled",false);
  ToggleKey=CnConfig.Bind(p.Config,"16. Night vision","Night vision toggle",KeyCode.N);
  if(ToggleKey.Value==p.IgnoreAIKey.Value)ToggleKey.Value=KeyCode.N;
  full=new Channel(false,Enabled);scope=new Channel(true);
  NightVisionLighting.Initialize(p);
  Nw11Vision.Initialize(p);
  p.Patches.Patch(AccessTools.Method(typeof(OpticComponentUpdater),"LateUpdate"),postfix:new HarmonyMethod(typeof(NightVisionAssist),nameof(AfterOpticUpdate)));
  p.Patches.Patch(AccessTools.Method(typeof(NativeNightVision),"OnPreCull"),prefix:new HarmonyMethod(typeof(NightVisionAssist),nameof(BeforeNightRender)));
  p.Patches.Patch(AccessTools.Method(typeof(NativeNightVision),nameof(NativeNightVision.StartSwitch)),prefix:new HarmonyMethod(typeof(NightVisionAssist),nameof(NativeSwitch)));
 }
 internal static void Keys(){if(Hotkeys.Pressed(ToggleKey.Value))Enabled.Value=!Enabled.Value;}
 internal static string Status=>"夜视："+CnConfig.OnOff(Enabled.Value);
 static void AfterOpticUpdate(){Tick(Comfort.Common.Singleton<GameWorld>.Instance?.MainPlayer);}
 static void NativeSwitch(NativeNightVision __instance,bool __0){if(writing)return;Find(__instance)?.NativeOn(__0);}
 static EffectLease? Find(NativeNightVision value)=>fullLease?.Target.Pointer==value.Pointer?fullLease:scopeLease?.Target.Pointer==value.Pointer?scopeLease:null;
 static void BeforeNightRender(NativeNightVision __instance){
  if(writing)return;
  try{var lease=Find(__instance);if(lease!=null)lease.Apply(lease==fullLease?full:scope);}catch(Exception e){Failed(e);}
 }
 internal static void Tick(Player? player){
  try{
   NightVisionLighting.Frame();Nw11Vision.Frame();
   if(player?.IsYourPlayer!=true||player.HealthController?.IsAlive!=true){Reset();return;}
   var manager=CameraManager.Instance;
   NativeNightVision? main=manager?.NightVision;
   if(main==null){var camera=manager?.Camera??Camera.main;main=camera?.GetComponent<NativeNightVision>();}
   Update(ref fullLease,full.Enabled.Value?main:null,full);
   var optics=manager?.OpticCameraManager;
   var cameraOptic=optics?.Camera;
   var sight=optics?.CurrentOpticSight;
   bool active=scope.Enabled.Value&&NightVisionRules.ScopeActive(true,true,player.ProceduralWeaponAnimation?.IsAiming==true,sight!=null,cameraOptic!=null&&cameraOptic.isActiveAndEnabled&&!cameraOptic.orthographic,DirectorStage.FreeActive);
   IntPtr key=active?sight!.Pointer:IntPtr.Zero;
   if(scopeSight!=key){Release(ref scopeLease);scopeSight=key;}
   var optic=active?(optics?.NightVision??cameraOptic?.GetComponent<NativeNightVision>()):null;
   if(optic!=null&&main?.Pointer==optic.Pointer)optic=null;
   Update(ref scopeLease,optic,scope);
   Nw11Vision.Active(fullLease?.Profile?.Clarity==true||scopeLease?.Profile?.Clarity==true);
   NightVisionLighting.Active((fullLease!=null&&full.BlockLights.Value)||(scopeLease!=null&&scope.BlockLights.Value));
  }catch(Exception e){Failed(e);}
 }
 static void Update(ref EffectLease? lease,NativeNightVision? target,Channel channel){
  if(target==null){Release(ref lease);return;}
  if(lease?.Target.Pointer!=target.Pointer){Release(ref lease);lease=new(target);}
  lease.Apply(channel);
 }
 static void Release(ref EffectLease? lease){var old=lease;lease=null;old?.Restore();}
 internal static bool SuppressLights(Camera camera){
  var manager=CameraManager.Instance;
  return fullLease!=null&&full.BlockLights.Value&&full.Enabled.Value&&manager?.Camera?.Pointer==camera.Pointer
   ||scopeLease!=null&&scope.BlockLights.Value&&scope.Enabled.Value&&manager?.OpticCameraManager?.Camera?.Pointer==camera.Pointer;
 }
 internal static NightVisionProfile? RenderProfile(Camera? camera){
  if(camera==null)return null;var manager=CameraManager.Instance;
  if(fullLease?.Profile?.Clarity==true&&manager?.Camera?.Pointer==camera.Pointer)return fullLease.Profile;
  if(scopeLease?.Profile?.Clarity==true&&manager?.OpticCameraManager?.Camera?.Pointer==camera.Pointer)return scopeLease.Profile;
  return null;
 }
 static void Failed(Exception error){
  notice="夜视设置未能应用："+error.Message;
  try{Reset();}catch(Exception cleanup){Plugin.Self.Log.LogWarning(cleanup);}
  if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10;Plugin.Self.Error(error);}
 }
 internal static void Reset(){
  try{Release(ref fullLease);}finally{try{Release(ref scopeLease);}finally{scopeSight=IntPtr.Zero;NightVisionLighting.Reset();Nw11Vision.Reset();}}
 }
 internal static void Shutdown(){try{Reset();}finally{NightVisionLighting.Shutdown();}}
 sealed record View(Color Color,float Intensity,float Noise,float Scale,float Ambient,Texture? Mask,float Size,bool On,bool Enabled);
 sealed class EffectLease(NativeNightVision target){
  internal NativeNightVision Target=>target;
  internal NightVisionProfile? Profile{get;private set;}
  View original=Capture(target);
  View? last;
  static View Capture(NativeNightVision n)=>new(n.Color,n.Intensity,n.NoiseIntensity,n.NoiseScale,n.ambientFactor,n.Mask,n.MaskSize,n.On,n.enabled);
  static T Merge<T>(T baseline,T current,T applied)=>EqualityComparer<T>.Default.Equals(current,applied)?baseline:current;
  void Observe(View now){if(last is not {} l)return;original=new(Merge(original.Color,now.Color,l.Color),Merge(original.Intensity,now.Intensity,l.Intensity),Merge(original.Noise,now.Noise,l.Noise),Merge(original.Scale,now.Scale,l.Scale),Merge(original.Ambient,now.Ambient,l.Ambient),Merge(original.Mask,now.Mask,l.Mask),Merge(original.Size,now.Size,l.Size),Merge(original.On,now.On,l.On),Merge(original.Enabled,now.Enabled,l.Enabled));}
  internal void NativeOn(bool on)=>original=original with{On=on};
  internal NightVisionProfile Original(){
   var c=(Color32)original.Color;
   var mask=original.Mask==null?NightVisionMask.None:original.Mask.Pointer==target.AnvisMaskTexture?.Pointer?NightVisionMask.Anvis:original.Mask.Pointer==target.BinocularMaskTexture?.Pointer?NightVisionMask.Binocular:NightVisionMask.OldMonocular;
   return new(c.r,c.g,c.b,c.a,original.Intensity,original.Noise,original.Scale,original.Ambient,mask,original.Size);
  }
  internal void Apply(Channel channel){
   if(target==null)return;
   var now=Capture(target);Observe(now);
   var profile=channel.Read();Profile=profile;
   writing=true;
   try{
    if(profile==null){
     target.Color=original.Color;target.Intensity=original.Intensity;target.NoiseIntensity=original.Noise;target.NoiseScale=original.Scale;
     target.ambientFactor=original.Ambient;target.Mask=original.Mask;target.MaskSize=original.Size;
    }else{
     target.Color=new Color32(profile.R,profile.G,profile.B,profile.A);target.Intensity=profile.Intensity;
     target.NoiseIntensity=profile.NoiseIntensity;target.NoiseScale=profile.NoiseScale;target.ambientFactor=profile.Ambient;
     if(profile.Mask==NightVisionMask.None)target.Mask=null;
     else{var mask=profile.Mask switch{NightVisionMask.Anvis=>target.AnvisMaskTexture,NightVisionMask.Binocular=>target.BinocularMaskTexture,_=>target.OldMonocularMaskTexture};if(target.Mask?.Pointer!=mask?.Pointer)target.SetMask((EFT.InventoryLogic.NightVisionComponent.EMask)(int)profile.Mask);}
     target.MaskSize=profile.MaskSize;
    }
    if(profile?.Clarity!=true&&!target.enabled)target.enabled=true;
    bool native=profile?.Clarity!=true;if(target.On!=native)target.On=native;
    var next=Capture(target);
    if(now!=next)target.ApplySettings();
    last=Capture(target);
   }finally{writing=false;}
  }
  internal void Restore(){
   if(target==null)return;
   Observe(Capture(target));writing=true;
   try{
    target.Color=original.Color;target.Intensity=original.Intensity;target.NoiseIntensity=original.Noise;target.NoiseScale=original.Scale;
    target.ambientFactor=original.Ambient;target.Mask=original.Mask;target.MaskSize=original.Size;
    target.On=original.On;target.ApplySettings();target.enabled=original.Enabled;
   }finally{writing=false;}
  }
 }
}
