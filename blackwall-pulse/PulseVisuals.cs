using EFT;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
namespace LocalBlackwallPulse;
internal static class PulseVisuals {
 sealed class Victim {
  internal Player Actor=null!;internal float Start;internal bool Hand;internal Light? Glow;
  internal Material? Material;internal readonly List<Renderer> Overlays=new();internal readonly List<Transform> Panels=new();internal Material? PanelsMaterial,BeamMaterial;
 }
 static readonly List<Victim> victims=new();static GameObject? root;static Shader? shader;
 static Texture2D? palette,atlas,flare;static readonly List<Mesh> atlasFrames=new();static Shader? patternShader;static Color red=new(1,.015f,.01f,1);static Camera.CameraCallback? callback;static bool released;
 internal static void Initialize(){
  string atlasPath=Path.Combine(PulseRuntime.Home,"Assets","blackwall.png");if(File.Exists(atlasPath)){atlas=new Texture2D(2,2,TextureFormat.RGBA32,false);if(ImageConversion.LoadImage(atlas,File.ReadAllBytes(atlasPath))){
   // The exported color atlas is opaque, including its black background. Rebuild
   // coverage before using a premultiplied sprite shader; retain its source RGB.
   var pixels=atlas.GetPixels();for(int i=0;i<pixels.Length;i++){var c=pixels[i];float intensity=Mathf.Max(c.r,Mathf.Max(c.g,c.b));pixels[i]=intensity>.00001f?new Color(c.r/intensity,c.g/intensity,c.b/intensity,c.a*intensity):Color.clear;}atlas.SetPixels(pixels);atlas.Apply();atlas.wrapMode=TextureWrapMode.Clamp;
   // Sprites/Default does not transform UVs with _MainTex_ST. Crop in mesh UVs
   // instead of relying on mainTextureScale/Offset, with a half-texel inset.
   for(int row=0;row<3;row++)for(int col=0;col<2;col++){float u0=col*.5f+.5f/atlas.width,u1=(col+1)*.5f-.5f/atlas.width,v0=1-(row+1)*.25f+.5f/atlas.height,v1=1-row*.25f-.5f/atlas.height;var mesh=new Mesh{name="Blackwall atlas frame "+(row*2+col),hideFlags=HideFlags.HideAndDontSave};mesh.vertices=new Il2CppStructArray<Vector3>(new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0)});mesh.uv=new Il2CppStructArray<Vector2>(new[]{new Vector2(u0,v0),new Vector2(u1,v0),new Vector2(u0,v1),new Vector2(u1,v1)});mesh.triangles=new Il2CppStructArray<int>(new[]{0,2,1,2,3,1});mesh.RecalculateBounds();atlasFrames.Add(mesh);}
  }}
  string flarePath=Path.Combine(PulseRuntime.Home,"Assets","flare.png");if(File.Exists(flarePath)){flare=new Texture2D(2,2,TextureFormat.RGBA32,false);if(ImageConversion.LoadImage(flare,File.ReadAllBytes(flarePath))){var pixels=flare.GetPixels();for(int i=0;i<pixels.Length;i++){var c=pixels[i];pixels[i]=new Color(1,1,1,Mathf.Max(c.r,Mathf.Max(c.g,c.b))*c.a);}flare.SetPixels(pixels);flare.Apply();flare.wrapMode=TextureWrapMode.Clamp;}}
  string path=Path.Combine(PulseRuntime.Home,"Assets","glow.png");
  if(File.Exists(path)){palette=new Texture2D(2,2,TextureFormat.RGBA32,false);if(ImageConversion.LoadImage(palette,File.ReadAllBytes(path))){var color=palette.GetPixelBilinear(.7f,.5f);if(color.r>.1f)red=new Color(1,Math.Clamp(color.g/color.r,0,.15f),Math.Clamp(color.b/color.r,0,.1f),1);}}
  callback=DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(BeforeRender));Camera.onPreCull+=callback;
 }
 static void BeforeRender(Camera camera){if(camera!=null&&EFT.CameraControl.CameraManager.Instance?.Camera?.Pointer==camera.Pointer)PulseRuntime.Late(true);}
 static bool Resources(){if(root!=null)return true;shader=Shader.Find("Hidden/Internal-Colored");if(shader==null||!shader.isSupported)return false;root=new GameObject("Blackwall red glow"){hideFlags=HideFlags.HideAndDontSave};return true;}
 static Material GlowMaterial(){var m=new Material(shader!){hideFlags=HideFlags.HideAndDontSave};m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.One);m.SetInt("_Cull",(int)CullMode.Back);m.SetInt("_ZWrite",0);m.SetInt("_ZTest",(int)CompareFunction.LessEqual);m.color=new Color(red.r,red.g,red.b,.12f);m.renderQueue=3100;return m;}
 static Material BodyMaterial(){patternShader??=Shader.Find("Sprites/Default");if(patternShader==null||!patternShader.isSupported||atlas==null)return GlowMaterial();var m=new Material(patternShader){hideFlags=HideFlags.HideAndDontSave};m.mainTexture=atlas;m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetInt("_Cull",(int)CullMode.Off);m.SetInt("_ZWrite",0);m.SetInt("_ZTest",(int)CompareFunction.LessEqual);m.color=new Color(1,.015f,.045f,.45f);m.renderQueue=3100;return m;}
 internal static void Begin(Player owner,Player[] targets){if(!Resources())return;released=false;foreach(var old in victims.Where(v=>v.Hand||v.Actor==null||targets.Any(t=>t.Pointer==v.Actor.Pointer)).ToArray()){Destroy(old);victims.Remove(old);}while(victims.Count>24){Destroy(victims[0]);victims.RemoveAt(0);}Add(owner,true);foreach(var target in targets)Add(target,false);}
 static void Add(Player actor,bool hand){
  var v=new Victim{Actor=actor,Start=Time.unscaledTime,Hand=hand,Material=hand?GlowMaterial():BodyMaterial()};victims.Add(v);
  var renderers=hand?ArmAnimation.CastRenderers:actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r!=null&&!r.name.StartsWith("Blackwall",StringComparison.Ordinal)).Select(r=>(Renderer)r).ToArray();
  foreach(var source in renderers){
   if(v.Overlays.Count>=16)break;
   if(source==null||!source.enabled||source.forceRenderingOff||!source.gameObject.activeInHierarchy||source.name.Contains("SPT Assistant",StringComparison.OrdinalIgnoreCase))continue;
   var skin=source.TryCast<SkinnedMeshRenderer>();var shared=skin!=null?skin.sharedMesh:source.GetComponent<MeshFilter>()?.sharedMesh;if(shared==null)continue;
   var node=new GameObject("Blackwall red light layer"){hideFlags=HideFlags.HideAndDontSave};node.layer=source.gameObject.layer;node.transform.SetParent(source.transform,false);
   Renderer overlay;
   if(skin!=null){var skinned=node.AddComponent<SkinnedMeshRenderer>();skinned.sharedMesh=shared;skinned.bones=skin.bones;skinned.rootBone=skin.rootBone;skinned.localBounds=skin.localBounds;skinned.quality=skin.quality;overlay=skinned;}
   else{node.AddComponent<MeshFilter>().sharedMesh=shared;overlay=node.AddComponent<MeshRenderer>();}
   overlay.sharedMaterials=new Il2CppReferenceArray<Material>(Enumerable.Repeat(v.Material!,shared.subMeshCount).ToArray());overlay.shadowCastingMode=ShadowCastingMode.Off;overlay.receiveShadows=false;overlay.lightProbeUsage=LightProbeUsage.Off;overlay.reflectionProbeUsage=ReflectionProbeUsage.Off;v.Overlays.Add(overlay);
  }
  if(!hand&&atlas!=null){
   v.PanelsMaterial=BodyMaterial();
   v.BeamMaterial=BodyMaterial();v.BeamMaterial.mainTexture=flare!=null?flare:atlas;
   for(int i=0;i<10;i++){var panel=GameObject.CreatePrimitive(PrimitiveType.Quad);panel.name="Blackwall glitch panel";panel.hideFlags=HideFlags.HideAndDontSave;panel.layer=actor.gameObject.layer;panel.transform.SetParent(root!.transform,false);var collider=panel.GetComponent<Collider>();if(collider!=null)UnityEngine.Object.Destroy(collider);if(i>=2&&atlasFrames.Count>0)panel.GetComponent<MeshFilter>().sharedMesh=atlasFrames[(i-2)%atlasFrames.Count];var renderer=panel.GetComponent<Renderer>();renderer.sharedMaterial=i<2?v.BeamMaterial:v.PanelsMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;v.Panels.Add(panel.transform);}
  }
  if(victims.Count(x=>x.Glow!=null)<9){var node=new GameObject("Blackwall red light"){hideFlags=HideFlags.HideAndDontSave};node.transform.SetParent(root!.transform,false);var light=node.AddComponent<Light>();light.type=LightType.Point;light.color=red;light.range=1.4f;light.intensity=0;light.shadows=LightShadows.None;v.Glow=light;}
 }
 internal static void Release(){released=true;}
 internal static void Tick(bool inRaid,bool enabled,float age){if(!inRaid||!enabled){Clear();return;}foreach(var v in victims.ToArray()){float elapsed=Time.unscaledTime-v.Start;if(v.Actor==null||elapsed>(v.Hand?PulseRules.CastDuration:PulseRules.VictimDuration)){Destroy(v);victims.Remove(v);}}}
 internal static void Late(){
  foreach(var v in victims){if(v.Actor==null)continue;float elapsed=Time.unscaledTime-v.Start;float fade=v.Hand?PulseRules.Weight(elapsed):1-Math.Clamp((elapsed-2.5f)/1.4f,0,1);float pulse=.7f+.3f*Mathf.Sin(elapsed*15);float strength=(released?1:.6f)*fade;
   var center=v.Hand?ArmAnimation.CastPalm?.position??v.Actor.Position+Vector3.up:v.Actor.PlayerBones?.Ribcage?.Original?.position??v.Actor.Position+Vector3.up;
   if(v.Material!=null){if(v.Hand)v.Material.color=new Color(red.r,red.g,red.b,.10f*pulse*strength);else{v.Material.mainTextureOffset=new Vector2(Time.unscaledTime*.67f,Time.unscaledTime*1.31f);v.Material.color=new Color(.8f+Mathf.Sin(Time.unscaledTime*51)*.2f,.005f,.015f,(.24f+Mathf.Abs(Mathf.Sin(Time.unscaledTime*38))*.28f)*fade);}}
   if(v.Glow!=null){v.Glow.transform.position=center;v.Glow.intensity=(v.Hand?1.2f:2f)*pulse*strength;}
   if(v.BeamMaterial!=null)v.BeamMaterial.color=new Color(1,.015f,.005f,(released?.8f:.25f)*fade);
   if(v.PanelsMaterial!=null){float flash=.45f+.55f*Mathf.Abs(Mathf.Sin(elapsed*31));v.PanelsMaterial.color=new Color(1,.04f,.04f,(released?.55f:.15f)*flash*fade);var view=EFT.CameraControl.CameraManager.Instance?.Camera;for(int i=0;i<v.Panels.Count;i++){var panel=v.Panels[i];if(panel==null)continue;float phase=Mathf.Floor(elapsed*4+i*.37f)*2.1f+i*2.1f;bool beam=i<2;if(!beam&&atlasFrames.Count>0){var filter=panel.GetComponent<MeshFilter>();var mesh=atlasFrames[((int)Mathf.Floor(elapsed*8)+i)%atlasFrames.Count];if(filter.sharedMesh!=mesh)filter.sharedMesh=mesh;}panel.position=center+(beam?Vector3.up*(2.5f+Mathf.Max(0,1-elapsed/.4f)*4f):new Vector3(Mathf.Sin(phase)*.3f,Mathf.Cos(phase*1.3f)*.4f,Mathf.Sin(phase*.7f)*.25f));panel.localScale=beam?new Vector3(.16f+i*.1f,5,1):new Vector3(.15f+.1f*Mathf.Abs(Mathf.Sin(phase)),.3f+.35f*Mathf.Abs(Mathf.Cos(phase)),1);if(view!=null)panel.rotation=Quaternion.LookRotation(view.transform.forward,Vector3.up);}}
  }
 }
 internal static void DrawFilter(float age){if(Event.current.type!=EventType.Repaint||age<0)return;var color=GUI.color;int depth=GUI.depth;try{GUI.depth=-50;GUI.color=new Color(red.r,red.g,red.b,.045f*PulseRules.Weight(age));GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);}finally{GUI.color=color;GUI.depth=depth;}}
 static void Destroy(Victim v){foreach(var r in v.Overlays)if(r!=null)UnityEngine.Object.Destroy(r.gameObject);if(v.Glow!=null)UnityEngine.Object.Destroy(v.Glow.gameObject);if(v.Material!=null)UnityEngine.Object.Destroy(v.Material);foreach(var panel in v.Panels)if(panel!=null)UnityEngine.Object.Destroy(panel.gameObject);if(v.PanelsMaterial!=null)UnityEngine.Object.Destroy(v.PanelsMaterial);if(v.BeamMaterial!=null)UnityEngine.Object.Destroy(v.BeamMaterial);}
 internal static void Clear(){foreach(var v in victims)Destroy(v);victims.Clear();}
 internal static object Diagnostics()=>new{victims=victims.Count,overlays=victims.Sum(v=>v.Overlays.Count),arcs=0,lights=victims.Count(v=>v.Glow!=null),panels=victims.Sum(v=>v.Panels.Count),mode="alpha-masked atlas frames and descending glow / Unity approximation",atlasUvFrames=atlasFrames.Count,texture=atlas!=null,originalPalette=palette!=null};
 internal static void Shutdown(){Clear();if(callback!=null)Camera.onPreCull-=callback;callback=null;if(root!=null)UnityEngine.Object.Destroy(root);foreach(var mesh in atlasFrames)if(mesh!=null)UnityEngine.Object.Destroy(mesh);atlasFrames.Clear();if(palette!=null)UnityEngine.Object.Destroy(palette);if(atlas!=null)UnityEngine.Object.Destroy(atlas);if(flare!=null)UnityEngine.Object.Destroy(flare);root=null;palette=atlas=flare=null;shader=patternShader=null;}
}
