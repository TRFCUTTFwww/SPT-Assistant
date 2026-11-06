using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.CameraControl;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using UiColor=UnityEngine.Color;
namespace LocalSpt5Esp;
internal static class BodyTint {
 internal static ConfigEntry<bool> Enabled=null!,ThroughWalls=null!;static ConfigEntry<bool> scopeOnly=null!,visibilityLabels=null!,splitDepth=null!;static ConfigEntry<float> opacity=null!,hiddenOpacity=null!;
 static readonly ConfigEntry<string>[] colors=new ConfigEntry<string>[Enum.GetValues<CharacterKind>().Length];static readonly string?[] paletteCodes=new string?[Enum.GetValues<CharacterKind>().Length];static readonly UiColor[] palette=new UiColor[Enum.GetValues<CharacterKind>().Length];
 static readonly Material?[] visible=new Material?[Enum.GetValues<CharacterKind>().Length+1],hidden=new Material?[Enum.GetValues<CharacterKind>().Length+1];
 sealed record Mesh(SkinnedMeshRenderer Renderer,int Parts,int Color);
 sealed record View(Camera Camera,CommandBuffer Commands){internal int Revision=-1;}
 sealed record Layer(SkinnedMeshRenderer Source,SkinnedMeshRenderer Visible,SkinnedMeshRenderer Hidden,int Color,IntPtr Mesh);
 static readonly Dictionary<IntPtr,Layer> layers=new();static readonly HashSet<IntPtr> ownedRenderers=new();
 static readonly Dictionary<IntPtr,View> views=new();static readonly List<Mesh> meshes=new();static readonly Dictionary<IntPtr,bool> friends=new();static readonly Dictionary<IntPtr,CharacterKind> kinds=new();static readonly Dictionary<IntPtr,(SkinnedMeshRenderer[] Renderers,float Expires)> rendererCache=new();
 static readonly Dictionary<IntPtr,(string Id,float Time,string Label)> visibility=new();static float nextScan,nextShader,nextColors,nextError,nextStatus;static int revision,layerRevision=-1;static bool active;static int environmentMask;static string lastState="";
 internal static readonly UiColor TeammateColor=new(55/255f,230/255f,121/255f);
 internal static void Initialize(Plugin plugin){const string section="7. Character colors";Enabled=CnConfig.Bind(plugin.Config,section,"Enabled",true);ThroughWalls=CnConfig.Bind(plugin.Config,section,"Through walls",true);opacity=CnConfig.Bind(plugin.Config,section,"Opacity",.65f,new ConfigDescription("",new AcceptableValueRange<float>(.15f,1)));foreach(var kind in Enum.GetValues<CharacterKind>())colors[(int)kind]=CnConfig.Bind(plugin.Config,section,kind+" color",RolePalette.Hex(kind));
  scopeOnly=FeatureConfig.Bind(section,"Only inside optical scope","仅瞄准镜内人物上色",false,"仅在原生光学瞄具摄像机中绘制上色，镜外保留原始人物材质。红点、机瞄不启用此模式。");splitDepth=FeatureConfig.Bind(section,"Separate visible and occluded colors","区分直视与遮挡颜色",true,"直视像素使用明亮阵营色，被掩体遮挡的像素使用暗色与较低透明度；根据每个像素的场景深度区分。");hiddenOpacity=FeatureConfig.Bind(section,"Occluded tint opacity","遮挡部分上色透明度",.3f,"",new AcceptableValueRange<float>(.08f,.8f));visibilityLabels=FeatureConfig.Bind(section,"Visibility state labels","人物标签显示直视与遮挡状态",true,"对头、胸、腹三处采样环境视线，显示直视／遮挡／部分遮挡。文字状态是采样判断，上色使用实际像素深度。");
 }
 internal static void BeginScan(){kinds.Clear();friends.Clear();}
 internal static bool Teammate(Player actor){if(!friends.TryGetValue(actor.Pointer,out bool value))friends[actor.Pointer]=value=AiTeamControl.IsTeammate(actor);return value;}
 internal static CharacterKind Kind(Player actor){if(!kinds.TryGetValue(actor.Pointer,out var kind))kinds[actor.Pointer]=kind=RolePalette.Classify(actor.Profile?.Info?.Settings?.Role.ToString()??"",actor.Profile?.Side==EPlayerSide.Savage);return kind;}
 internal static string KindName(Player actor)=>RolePalette.Name(Kind(actor));
 static UiColor ForKind(CharacterKind kind){int index=(int)kind;string code=colors[index]?.Value??RolePalette.Hex(kind);if(paletteCodes[index]==code)return palette[index];string hex=code.Trim().TrimStart('#');if(hex.Length!=6||!uint.TryParse(hex,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint rgb))rgb=uint.Parse(RolePalette.Hex(kind)[1..],NumberStyles.HexNumber,CultureInfo.InvariantCulture);paletteCodes[index]=code;return palette[index]=new UiColor(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f);}
 internal static UiColor Color(Player actor)=>Teammate(actor)?TeammateColor:ForKind(Kind(actor));
 static bool Materials(){if(visible.All(m=>m!=null)&&hidden.All(m=>m!=null))return true;if(Time.unscaledTime<nextShader)return false;nextShader=Time.unscaledTime+15;var shader=new[]{"Hidden/Internal-Colored","Unlit/Color","Sprites/Default"}.Select(Shader.Find).FirstOrDefault(s=>s!=null&&s.isSupported);if(shader==null)return false;for(int i=0;i<visible.Length;i++){visible[i]??=Make(shader,CompareFunction.LessEqual);hidden[i]??=Make(shader,CompareFunction.Greater);}return true;}
 static Material Make(Shader shader,CompareFunction depth){var material=new Material(shader){hideFlags=HideFlags.HideAndDontSave,renderQueue=depth==CompareFunction.Greater?4000:4001};material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_Cull",(int)CullMode.Off);material.SetInt("_ZWrite",0);material.SetInt("_ZTest",(int)depth);return material;}
 static void Colors(){if(Time.unscaledTime<nextColors)return;nextColors=Time.unscaledTime+.15f;for(int i=0;i<visible.Length;i++){var color=i==visible.Length-1?TeammateColor:ForKind((CharacterKind)i);visible[i]!.color=new(color.r,color.g,color.b,opacity.Value);var dark=splitDepth.Value?UiColor.Lerp(color,UiColor.black,.52f):color;hidden[i]!.color=new(dark.r,dark.g,dark.b,splitDepth.Value?hiddenOpacity.Value:opacity.Value);}}
 internal static void Update(IReadOnlyList<Player> players,Player local,bool showPeople,float range){if(!Enabled.Value||!showPeople||!RaidClock.Active){Clear();return;}active=true;if(!Materials())return;Colors();if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.75f;meshes.Clear();var current=new HashSet<IntPtr>();foreach(var actor in players){if(actor==null||actor.IsYourPlayer||actor.HealthController?.IsAlive!=true||Vector3.Distance(local.Position,actor.Position)>range)continue;current.Add(actor.Pointer);int color=Teammate(actor)?visible.Length-1:(int)Kind(actor);if(!rendererCache.TryGetValue(actor.Pointer,out var cached)||Time.unscaledTime>=cached.Expires){cached=(actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r!=null&&!ownedRenderers.Contains(r.Pointer)).ToArray(),Time.unscaledTime+3);rendererCache[actor.Pointer]=cached;}foreach(var renderer in cached.Renderers)if(renderer!=null&&!ownedRenderers.Contains(renderer.Pointer)&&renderer.enabled&&renderer.gameObject.activeInHierarchy&&renderer.sharedMesh!=null)meshes.Add(new(renderer,renderer.sharedMesh.subMeshCount,color));}foreach(var key in rendererCache.Keys.Where(k=>!current.Contains(k)).ToArray()){rendererCache.Remove(key);visibility.Remove(key);}revision++;}
 // General tints are native scene renderers, so they enter the game's scene color
 // before SSAA/DLSS/FSR and post-processing. Only optics use camera command buffers.
 internal static void Tick(){try{
  if(!active||!Enabled.Value||!Plugin.Self.People.Value||!RaidClock.Active){DropViews();DropLayers();return;}
  if(!Materials()){DropViews();DropLayers();return;}Colors();
  if(!scopeOnly.Value){DropViews();NormalView();Status(null);return;}
  DropLayers();
  var manager=CameraManager.Instance;var optic=manager?.OpticCameraManager;
  Camera? scope=Singleton<GameWorld>.Instance?.MainPlayer?.ProceduralWeaponAnimation?.IsAiming==true&&optic?.CurrentOpticSight!=null?optic.Camera:null;
  if(scope!=null&&!scope.isActiveAndEnabled)scope=null;
  foreach(var key in views.Keys.Where(k=>k!=scope?.Pointer).ToArray())RemoveView(key);
  if(scope!=null)SyncView(scope);Status(scope);
 }catch(Exception error){DropViews();DropLayers();if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10;Plugin.Self.Log.LogWarning("Character tint rendering: "+error);}}}
 static float nextLayers;
 static void NormalView(){
  if(Time.unscaledTime<nextLayers)return;nextLayers=Time.unscaledTime+.05f;
  if(layerRevision!=revision){
   var current=new HashSet<IntPtr>();int budget=16;bool pending=false;
   foreach(var entry in meshes){var source=entry.Renderer;if(source==null||source.sharedMesh==null)continue;current.Add(source.Pointer);
    if(layers.TryGetValue(source.Pointer,out var layer)&&(layer.Color!=entry.Color||layer.Mesh!=source.sharedMesh.Pointer||layer.Visible==null||layer.Hidden==null)){RemoveLayer(source.Pointer);layer=null;}
    if(layer==null){if(budget--<=0){pending=true;continue;}SkinnedMeshRenderer? shown=null;try{shown=MakeLayer(source,visible[entry.Color]!,entry.Parts,"visible");var occluded=MakeLayer(source,hidden[entry.Color]!,entry.Parts,"occluded");layers[source.Pointer]=new(source,shown,occluded,entry.Color,source.sharedMesh.Pointer);}catch{DestroyLayer(shown);throw;}}
   }
   foreach(var key in layers.Keys.Where(k=>!current.Contains(k)).ToArray())RemoveLayer(key);if(!pending)layerRevision=revision;
  }
  foreach(var layer in layers.Values){bool enabled=layer.Source!=null&&layer.Source.enabled&&layer.Source.gameObject.activeInHierarchy;
   if(layer.Visible!=null&&layer.Visible.enabled!=enabled)layer.Visible.enabled=enabled;
   bool occluded=enabled&&ThroughWalls.Value;if(layer.Hidden!=null&&layer.Hidden.enabled!=occluded)layer.Hidden.enabled=occluded;
  }
 }
 static SkinnedMeshRenderer MakeLayer(SkinnedMeshRenderer source,Material material,int parts,string pass){
  var node=new GameObject("SPT Assistant tint "+pass){hideFlags=HideFlags.HideAndDontSave,layer=source.gameObject.layer};
  try{node.transform.SetParent(source.transform,false);var renderer=node.AddComponent<SkinnedMeshRenderer>();renderer.enabled=false;
   renderer.sharedMesh=source.sharedMesh;renderer.bones=source.bones;renderer.rootBone=source.rootBone;renderer.localBounds=source.localBounds;renderer.quality=source.quality;
   renderer.updateWhenOffscreen=false;renderer.allowOcclusionWhenDynamic=false;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
   var materials=new Il2CppReferenceArray<Material>(parts);for(int part=0;part<parts;part++)materials[part]=material;renderer.sharedMaterials=materials;
   for(int shape=0;shape<source.sharedMesh.blendShapeCount;shape++)renderer.SetBlendShapeWeight(shape,source.GetBlendShapeWeight(shape));
   ownedRenderers.Add(renderer.Pointer);return renderer;
  }catch{node.SetActive(false);UnityEngine.Object.Destroy(node);throw;}
 }
 static void DestroyLayer(SkinnedMeshRenderer? renderer){if(renderer==null)return;ownedRenderers.Remove(renderer.Pointer);renderer.enabled=false;renderer.gameObject.SetActive(false);UnityEngine.Object.Destroy(renderer.gameObject);}
 static void RemoveLayer(IntPtr key){if(!layers.Remove(key,out var layer))return;DestroyLayer(layer.Visible);DestroyLayer(layer.Hidden);}
 static void DropLayers(){foreach(var key in layers.Keys.ToArray())RemoveLayer(key);layerRevision=-1;}
 static void Status(Camera? scope){if(Time.unscaledTime<nextStatus)return;nextStatus=Time.unscaledTime+5;
  string state=$"enabled={Enabled.Value}, scopeOnly={scopeOnly.Value}, path={(scopeOnly.Value?"optic commands":"native scene layers")}, optic={scope?.name??"none"}, meshes={meshes.Count}, layers={layers.Count}, buffers={views.Count}";
  if(state!=lastState){lastState=state;Plugin.Self.Log.LogInfo("Character tint rendering: "+state);}
 }
 static void SyncView(Camera camera){if(!views.TryGetValue(camera.Pointer,out var view)){var commands=new CommandBuffer{name="SPT Assistant character visibility tints"};camera.AddCommandBuffer(CameraEvent.BeforeImageEffects,commands);views[camera.Pointer]=view=new(camera,commands);}int state=revision*2+(ThroughWalls.Value?1:0);if(view.Revision==state)return;view.Revision=state;view.Commands.Clear();foreach(var entry in meshes){if(entry.Renderer==null)continue;for(int part=0;part<entry.Parts;part++){if(ThroughWalls.Value)view.Commands.DrawRenderer(entry.Renderer,hidden[entry.Color]!,part,0);view.Commands.DrawRenderer(entry.Renderer,visible[entry.Color]!,part,0);}}}
 static void RemoveView(IntPtr key){if(!views.Remove(key,out var view))return;try{if(view.Camera!=null)view.Camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects,view.Commands);}catch{}view.Commands.Dispose();}
 static void DropViews(){foreach(var key in views.Keys.ToArray())RemoveView(key);}
 internal static string VisibilityTag(Player actor,Camera camera){if(!visibilityLabels.Value||!RaidClock.Active)return "";if(visibility.TryGetValue(actor.Pointer,out var cached)&&cached.Id==actor.ProfileId&&Time.unscaledTime<cached.Time)return cached.Label;try{if(environmentMask==0)environmentMask=LayerMask.GetMask("HighPolyCollider","Terrain","LowPolyCollider");if(environmentMask==0)return "";var origin=camera.transform.position;var bones=actor.PlayerBones;var points=new[]{bones?.Head?.Original?.position??actor.Position+Vector3.up*1.6f,bones?.Spine3?.Original?.position??actor.Position+Vector3.up*1.05f,bones?.Pelvis?.Original?.position??actor.Position+Vector3.up*.65f};int clear=0;foreach(var point in points){var delta=point-origin;float distance=delta.magnitude;if(distance<.1f||!Physics.Raycast(origin,delta/Math.Max(.001f,distance),Math.Max(0,distance-.08f),environmentMask,QueryTriggerInteraction.Ignore))clear++;}string tag=clear==3?" · 直视":clear==0?" · 遮挡":" · 部分遮挡";visibility[actor.Pointer]=(actor.ProfileId,Time.unscaledTime+.25f,tag);return tag;}catch{return "";}}
 internal static void Clear(){active=false;DropViews();DropLayers();meshes.Clear();rendererCache.Clear();kinds.Clear();friends.Clear();visibility.Clear();nextScan=0;}
 internal static void Dispose(){Clear();for(int i=0;i<visible.Length;i++){if(visible[i]!=null)UnityEngine.Object.Destroy(visible[i]);if(hidden[i]!=null)UnityEngine.Object.Destroy(hidden[i]);visible[i]=hidden[i]=null;}}
}
