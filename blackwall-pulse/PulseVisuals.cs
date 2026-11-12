using EFT;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
namespace LocalBlackwallPulse;
internal static class PulseVisuals {
 sealed class Victim {
  internal Player Actor=null!;internal float Start;internal bool Hand;internal Light? Glow;
  internal Material? Material;internal readonly List<SkinnedMeshRenderer> Overlays=new();
 }
 static readonly List<Victim> victims=new();static GameObject? root;static Shader? shader;
 static Texture2D? palette;static Color red=new(1,.015f,.01f,1);static Camera.CameraCallback? callback;static bool released;
 internal static void Initialize(){
  string path=Path.Combine(PulseRuntime.Home,"Assets","glow.png");
  if(File.Exists(path)){palette=new Texture2D(2,2,TextureFormat.RGBA32,false);if(ImageConversion.LoadImage(palette,File.ReadAllBytes(path))){var color=palette.GetPixelBilinear(.7f,.5f);if(color.r>.1f)red=new Color(1,Math.Clamp(color.g/color.r,0,.15f),Math.Clamp(color.b/color.r,0,.1f),1);}}
  callback=DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(BeforeRender));Camera.onPreCull+=callback;
 }
 static void BeforeRender(Camera camera){if(camera!=null&&EFT.CameraControl.CameraManager.Instance?.Camera?.Pointer==camera.Pointer)PulseRuntime.Late();}
 static bool Resources(){if(root!=null)return true;shader=Shader.Find("Hidden/Internal-Colored");if(shader==null||!shader.isSupported)return false;root=new GameObject("Blackwall red glow"){hideFlags=HideFlags.HideAndDontSave};return true;}
 static Material GlowMaterial(){var m=new Material(shader!){hideFlags=HideFlags.HideAndDontSave};m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.One);m.SetInt("_Cull",(int)CullMode.Back);m.SetInt("_ZWrite",0);m.SetInt("_ZTest",(int)CompareFunction.LessEqual);m.color=new Color(red.r,red.g,red.b,.12f);m.renderQueue=3100;return m;}
 internal static void Begin(Player owner,Player[] targets){if(!Resources())return;released=false;foreach(var old in victims.Where(v=>v.Hand||v.Actor==null||targets.Any(t=>t.Pointer==v.Actor.Pointer)).ToArray()){Destroy(old);victims.Remove(old);}while(victims.Count>24){Destroy(victims[0]);victims.RemoveAt(0);}Add(owner,true);foreach(var target in targets)Add(target,false);}
 static void Add(Player actor,bool hand){
  var v=new Victim{Actor=actor,Start=Time.unscaledTime,Hand=hand,Material=GlowMaterial()};victims.Add(v);
  var renderers=hand?ArmAnimation.CastRenderers:actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r!=null&&!r.name.StartsWith("Blackwall",StringComparison.Ordinal)).ToArray();
  foreach(var source in renderers){
   if(v.Overlays.Count>=16)break;
   if(source==null||source.sharedMesh==null||!source.enabled||source.forceRenderingOff||!source.gameObject.activeInHierarchy||source.name.Contains("SPT Assistant",StringComparison.OrdinalIgnoreCase))continue;
   var node=new GameObject("Blackwall red light layer"){hideFlags=HideFlags.HideAndDontSave};node.layer=source.gameObject.layer;node.transform.SetParent(source.transform,false);
   var overlay=node.AddComponent<SkinnedMeshRenderer>();overlay.sharedMesh=source.sharedMesh;overlay.bones=source.bones;overlay.rootBone=source.rootBone;overlay.localBounds=source.localBounds;overlay.quality=source.quality;overlay.sharedMaterials=new Il2CppReferenceArray<Material>(Enumerable.Repeat(v.Material!,source.sharedMesh.subMeshCount).ToArray());overlay.shadowCastingMode=ShadowCastingMode.Off;overlay.receiveShadows=false;overlay.lightProbeUsage=LightProbeUsage.Off;overlay.reflectionProbeUsage=ReflectionProbeUsage.Off;v.Overlays.Add(overlay);
  }
  if(victims.Count(x=>x.Glow!=null)<6){var node=new GameObject("Blackwall red light"){hideFlags=HideFlags.HideAndDontSave};node.transform.SetParent(root!.transform,false);var light=node.AddComponent<Light>();light.type=LightType.Point;light.color=red;light.range=hand?1.4f:2.6f;light.intensity=0;light.shadows=LightShadows.None;v.Glow=light;}
 }
 internal static void Release(){released=true;}
 internal static void Tick(bool inRaid,bool enabled,float age){if(!inRaid||!enabled){Clear();return;}foreach(var v in victims.ToArray()){float elapsed=Time.unscaledTime-v.Start;if(v.Actor==null||elapsed>(v.Hand?PulseRules.CastDuration:PulseRules.VictimDuration)){Destroy(v);victims.Remove(v);}}}
 internal static void Late(){
  foreach(var v in victims){if(v.Actor==null)continue;float elapsed=Time.unscaledTime-v.Start;float fade=v.Hand?PulseRules.Weight(elapsed):1-Math.Clamp((elapsed-2.5f)/1.4f,0,1);float pulse=.7f+.3f*Mathf.Sin(elapsed*15);float strength=(released?1:.6f)*fade;
   var center=v.Hand?ArmAnimation.CastPalm?.position??v.Actor.Position+Vector3.up:v.Actor.PlayerBones?.Ribcage?.Original?.position??v.Actor.Position+Vector3.up;
   if(v.Material!=null)v.Material.color=new Color(red.r,red.g,red.b,(v.Hand?.10f:.16f)*pulse*strength);
   if(v.Glow!=null){v.Glow.transform.position=center;v.Glow.intensity=(v.Hand?1.2f:2f)*pulse*strength;}
  }
 }
 internal static void DrawFilter(float age){if(Event.current.type!=EventType.Repaint||age<0)return;var color=GUI.color;int depth=GUI.depth;try{GUI.depth=-50;GUI.color=new Color(red.r,red.g,red.b,.045f*PulseRules.Weight(age));GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);}finally{GUI.color=color;GUI.depth=depth;}}
 static void Destroy(Victim v){foreach(var r in v.Overlays)if(r!=null)UnityEngine.Object.Destroy(r.gameObject);if(v.Glow!=null)UnityEngine.Object.Destroy(v.Glow.gameObject);if(v.Material!=null)UnityEngine.Object.Destroy(v.Material);}
 internal static void Clear(){foreach(var v in victims)Destroy(v);victims.Clear();}
 internal static object Diagnostics()=>new{victims=victims.Count,overlays=victims.Sum(v=>v.Overlays.Count),arcs=0,lights=victims.Count(v=>v.Glow!=null),mode="original-palette red glow",originalPalette=palette!=null};
 internal static void Shutdown(){Clear();if(callback!=null)Camera.onPreCull-=callback;callback=null;if(root!=null)UnityEngine.Object.Destroy(root);if(palette!=null)UnityEngine.Object.Destroy(palette);root=null;palette=null;shader=null;}
}
