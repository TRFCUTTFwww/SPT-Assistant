using EFT;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace LocalBlackwallPulse;

internal static class PulseVisuals {
 sealed class Victim {
  internal Player Actor=null!;internal float Start;internal bool Hand;
  internal readonly List<SkinnedMeshRenderer> Overlays=new();
  internal readonly List<LineRenderer> Lines=new();
 }
 static readonly List<Victim> victims=new();static GameObject? root;
 static Material? pattern,red,black;static Texture2D? atlas,noise;
 static Camera.CameraCallback? callback;static bool released;
 static Texture2D? Load(string name){string file=Path.Combine(PulseRuntime.Home,"Assets",name);if(!File.Exists(file))return null;var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(file))){UnityEngine.Object.Destroy(texture);return null;}texture.wrapMode=TextureWrapMode.Repeat;return texture;}
 internal static void Initialize(){atlas=Load("blackwall.png");noise=Load("noise.png");callback=DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(BeforeRender));Camera.onPreCull+=callback;}
 static void BeforeRender(Camera camera){if(camera!=null&&EFT.CameraControl.CameraManager.Instance?.Camera?.Pointer==camera.Pointer)PulseRuntime.Late();}
 static bool Resources(){
  if(root!=null)return true;
  var shader=Shader.Find("Hidden/Internal-Colored");if(shader==null||!shader.isSupported)return false;
  red=Material(shader,new Color(1,.012f,.035f,.9f));black=Material(shader,new Color(.008f,0,.006f,.9f));
  var textured=Shader.Find("Sprites/Default");if(textured!=null&&textured.isSupported){pattern=Material(textured,new Color(1,.015f,.045f,.45f));if(atlas!=null)pattern.mainTexture=atlas;}
  root=new GameObject("Blackwall Pulse effects"){hideFlags=HideFlags.HideAndDontSave};return true;
 }
 static Material Material(Shader shader,Color color){var m=new Material(shader){hideFlags=HideFlags.HideAndDontSave};m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetInt("_Cull",(int)CullMode.Off);m.SetInt("_ZWrite",0);m.SetInt("_ZTest",(int)CompareFunction.LessEqual);m.color=color;m.renderQueue=3100;return m;}
 internal static void Begin(Player owner,Player[] targets){if(!Resources())return;released=false;foreach(var old in victims.Where(v=>v.Hand||v.Actor==null||targets.Any(t=>t.Pointer==v.Actor.Pointer)).ToArray()){Destroy(old);victims.Remove(old);}while(victims.Count>24){Destroy(victims[0]);victims.RemoveAt(0);}Add(owner,true);foreach(var target in targets)Add(target,false);}
 static void Add(Player actor,bool hand){
  var v=new Victim{Actor=actor,Start=Time.unscaledTime,Hand=hand};victims.Add(v);
  var renderers=hand?actor.PlayerBody?.SkeletonHands?.GetComponentsInChildren<SkinnedMeshRenderer>(true):actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
  if(renderers!=null&&pattern!=null)foreach(var source in renderers){
   if(v.Overlays.Count>=16)break;
   if(source==null||source.sharedMesh==null||!source.enabled||!source.gameObject.activeInHierarchy||source.name.StartsWith("Blackwall",StringComparison.Ordinal)||source.name.Contains("SPT Assistant",StringComparison.OrdinalIgnoreCase)||victims.Any(x=>x.Overlays.Any(o=>o!=null&&o.Pointer==source.Pointer)))continue;
   var node=new GameObject("Blackwall body layer"){hideFlags=HideFlags.HideAndDontSave};node.layer=source.gameObject.layer;node.transform.SetParent(source.transform,false);
   var overlay=node.AddComponent<SkinnedMeshRenderer>();overlay.sharedMesh=source.sharedMesh;overlay.bones=source.bones;overlay.rootBone=source.rootBone;overlay.localBounds=source.localBounds;overlay.quality=source.quality;overlay.updateWhenOffscreen=false;
   overlay.sharedMaterials=new Il2CppReferenceArray<Material>(Enumerable.Repeat(pattern,source.sharedMesh.subMeshCount).ToArray());overlay.shadowCastingMode=ShadowCastingMode.Off;overlay.receiveShadows=false;overlay.lightProbeUsage=LightProbeUsage.Off;overlay.reflectionProbeUsage=ReflectionProbeUsage.Off;v.Overlays.Add(overlay);
  }
  for(int i=0;i<(hand?4:9);i++){var node=new GameObject("Blackwall arc"){hideFlags=HideFlags.HideAndDontSave};node.transform.SetParent(root!.transform,false);var line=node.AddComponent<LineRenderer>();line.sharedMaterial=i%3==0?black:red;line.positionCount=7;line.useWorldSpace=true;line.startWidth=line.endWidth=i%3==0?.045f:.012f;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;v.Lines.Add(line);}
 }
 internal static void Release(){released=true;}
 internal static void Tick(bool inRaid,bool enabled,float age){if(!inRaid||!enabled){Clear();return;}foreach(var v in victims.ToArray()){float elapsed=Time.unscaledTime-v.Start;if(v.Actor==null||elapsed>(v.Hand?PulseRules.CastDuration:PulseRules.VictimDuration)){Destroy(v);victims.Remove(v);}}if(pattern!=null){pattern.mainTextureOffset=new Vector2(Time.unscaledTime*.67f,Time.unscaledTime*1.31f);pattern.color=new Color(.8f+Mathf.Sin(Time.unscaledTime*51)*.2f,.005f,.015f,.24f+Mathf.Abs(Mathf.Sin(Time.unscaledTime*38))*.28f);}}
 internal static void Late(){
  foreach(var v in victims){if(v.Actor==null)continue;float elapsed=Time.unscaledTime-v.Start;float fade=1-Math.Clamp((elapsed-2.5f)/1.4f,0,1);int frame=Mathf.FloorToInt(Time.unscaledTime*22);
   var actor=v.Actor;var bones=actor.PlayerBones;var center=v.Hand?bones?.RightPalm?.position??actor.Position+Vector3.up:bones?.Ribcage?.Original?.position??actor.Position+Vector3.up;
   for(int i=0;i<v.Lines.Count;i++){
    var line=v.Lines[i];if(line==null)continue;float scale=v.Hand?.15f:.65f;float h=(i*.29f+frame*.017f)%1;
    var rotation=Quaternion.Euler(0,actor.Rotation.x,0);var start=center+rotation*new Vector3(Mathf.Sin(i*2.4f+frame)*scale,(h-.5f)*(v.Hand?.2f:1.3f),Mathf.Cos(i*1.8f+frame)*scale*.4f);
    for(int j=0;j<7;j++){float seed=i*3.77f+j*1.97f+frame;var offset=rotation*new Vector3(Mathf.Sin(seed*2.1f)*scale*.22f,(j-3)*scale*.2f,Mathf.Cos(seed*1.7f)*scale*.18f);line.SetPosition(j,start+offset);}
    line.startColor=line.endColor=i%3==0?new Color(.005f,0,.003f,fade):new Color(1,.01f,.03f,(released?.95f:.5f)*fade);
   }
  }
 }
 internal static void DrawFilter(float age){
  if(Event.current.type!=EventType.Repaint||age<0)return;var previous=GUI.color;float strength=PulseRules.Weight(age);int frame=Mathf.FloorToInt(Time.unscaledTime*24);
  int depth=GUI.depth;GUI.depth=-50;GUI.color=new Color(.12f,0,.015f,.13f*strength);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
  if(noise!=null){GUI.color=new Color(1,.015f,.04f,.07f*strength);GUI.DrawTextureWithTexCoords(new Rect(0,0,Screen.width,Screen.height),noise,new Rect(frame*.071f,frame*.037f,3,2));}
  for(int i=0;i<8;i++){float y=((i*.137f+frame*.173f)%1)*Screen.height;GUI.color=new Color(i%3==0?0:.9f,0,.015f,.12f*strength);GUI.DrawTexture(new Rect(0,y,Screen.width,1+(i+frame)%4),Texture2D.whiteTexture);}
  GUI.color=previous;GUI.depth=depth;
 }
 static void Destroy(Victim v){foreach(var r in v.Overlays)if(r!=null)UnityEngine.Object.Destroy(r.gameObject);foreach(var l in v.Lines)if(l!=null)UnityEngine.Object.Destroy(l.gameObject);}
 internal static void Clear(){foreach(var v in victims)Destroy(v);victims.Clear();}
 internal static object Diagnostics()=>new{victims=victims.Count,overlays=victims.Sum(v=>v.Overlays.Count),arcs=victims.Sum(v=>v.Lines.Count),texture=atlas!=null,noise=noise!=null};
 internal static void Shutdown(){Clear();if(callback!=null)Camera.onPreCull-=callback;callback=null;if(root!=null)UnityEngine.Object.Destroy(root);foreach(var m in new[]{pattern,red,black})if(m!=null)UnityEngine.Object.Destroy(m);if(atlas!=null)UnityEngine.Object.Destroy(atlas);if(noise!=null)UnityEngine.Object.Destroy(noise);root=null;pattern=red=black=null;atlas=noise=null;}
}
