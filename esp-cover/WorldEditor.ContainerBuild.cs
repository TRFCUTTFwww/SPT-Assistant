using System;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using EFT.Interactive;
using LocalSpt.WorldEditing;
using UnityEngine;
using Shortcut=BepInEx.Unity.IL2CPP.Configuration.KeyboardShortcut;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 internal static bool ContainerBuilding=>containerBuilding;
 static ConfigEntry<Shortcut>? containerBuildKey;
 static bool containerBuilding,containerValid,containerGrid=true;
 static string containerTemplate="",containerMode="server",containerInvalid="";
 static float containerYaw;
 static GameObject? containerGhost,containerImageObject;static Camera? containerCamera;static RenderTexture? containerImage;
 static string containerImageTemplate="";static IntPtr containerImageSource;
 static Material? containerGhostMaterial;static Vector3 containerPosition;
 static readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> buildHits=new(512);
 static void InitializeContainerBuild(){containerBuildKey=FeatureConfig.Bind("63. 场景编辑","Container placement shortcut","连续放置容器热键",new Shortcut(KeyCode.F7),"编辑模式下按一次进入、再按一次退出。右键放置，左键删除准星处容器；Q/E 旋转，Shift 精细旋转，滚轮旋转，X 网格吸附，Esc 取消。");}
 static void BeginContainerBuild(){
  if(containerTemplate.Length==0){createContainer=true;BeginCreate();return;}if(containerMode=="custom")LootOptions("custom").Validate();
  var source=ContainerScene.Prototype(Current(),containerTemplate)??throw new ArgumentException("当前地图尚未加载此类容器模型。");
  StopContainerBuild();ClearSelection();CloseDialog();altCursor=false;SyncCursor();
  containerGhost=ContainerScene.Visual(source);var shader=Shader.Find("Sprites/Default")??Shader.Find("Unlit/Color");if(shader!=null){containerGhostMaterial=new Material(shader);foreach(var renderer in containerGhost.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=containerGhostMaterial;}
  containerBuilding=true;shortcutFrame=Time.frameCount;Current().MainPlayer.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);
 }
 static void StopContainerBuild(){containerBuilding=false;containerValid=false;if(containerGhost!=null)UnityEngine.Object.Destroy(containerGhost);containerGhost=null;if(containerGhostMaterial!=null)UnityEngine.Object.Destroy(containerGhostMaterial);containerGhostMaterial=null;shortcutFrame=Time.frameCount;}
 static bool ContainerBuildInput(GameWorld current){
  if(containerBuildKey!=null&&(dialog is "" or "context")&&!moving&&!dragging&&!RaidChat.InputBlocked&&!FloatingStatus.Capturing&&!DirectorStage.FreeActive&&(!Cursor.visible||cursorOwned)&&ShortcutBindings.Take(containerBuildKey,true)){
   shortcutFrame=Time.frameCount;if(containerBuilding)StopContainerBuild();else BeginContainerBuild();return true;
  }
  if(!containerBuilding)return false;
  if(Cursor.visible||DirectorStage.FreeActive){StopContainerBuild();return true;}
  float step=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?1:15;
  if(Input.GetKeyDown(KeyCode.Q))containerYaw-=step;if(Input.GetKeyDown(KeyCode.E))containerYaw+=step;
  containerYaw=ContainerBuildRules.Yaw(containerYaw+Input.mouseScrollDelta.y*step);if(Input.GetKeyDown(KeyCode.X))containerGrid=!containerGrid;
  UpdateContainerGhost(current);
  if(Time.frameCount<=shortcutFrame||gate.Blocked)return true;
  if(Input.GetMouseButtonDown(0)){
   var ray=PointerRay(false);var target=ContainerAtRay(current,ray,20);
   if(target!=null){ContainerScene.Remove(current,target);containerStatus="已删除容器。";}else FeatureToasts.Show("准星处没有可删除的容器。");
  }
  if(Input.GetMouseButtonDown(1)){if(!containerValid)FeatureToasts.Show(containerInvalid);else QueueContainer(containerTemplate,containerPosition,Quaternion.Euler(0,containerYaw,0),false,LootOptions(containerMode),null,null);}
  return true;
 }
 static LootableContainer? ContainerAtRay(GameWorld current,Ray ray,float range){
  var surface=CrosshairProbe.Read(current,current.MainPlayer,ray,range);float nearest=surface.State==CrosshairState.Empty?range:surface.Distance+.025f;LootableContainer? result=null;
  foreach(var c in ContainerScene.Read(current))foreach(var collider in ContainerScene.Root(c).GetComponentsInChildren<Collider>(true))if(collider!=null&&collider.enabled&&collider.gameObject.activeInHierarchy&&collider.Raycast(ray,out var hit,nearest)){nearest=hit.distance;result=c;}
  return result;
 }
 static void UpdateContainerGhost(GameWorld current){
  containerValid=false;containerInvalid="请瞄准 20 米内的平坦地面。";if(containerGhost==null)return;
  var ray=PointerRay(false);int count=Physics.RaycastNonAlloc(ray,buildHits,20,~0,QueryTriggerInteraction.Ignore);float nearest=21;RaycastHit? contact=null;
  if(count<buildHits.Length)for(int i=0;i<count;i++){var hit=buildHits[i];if(hit.collider==null||CrosshairProbe.LocalCollider(current,current.MainPlayer,hit.collider)||hit.distance>=nearest)continue;nearest=hit.distance;contact=hit;}
  containerGhost.SetActive(contact!=null);
  if(contact is {} surface){
   var p=surface.point;if(containerGrid){p.x=ContainerBuildRules.Snap(p.x,.25f);p.z=ContainerBuildRules.Snap(p.z,.25f);}
   // Recheck the snapped point vertically: snapping may move the anchor off a ledge.
   if(Physics.Raycast(p+Vector3.up*.3f,Vector3.down,out var ground,.7f,~0,QueryTriggerInteraction.Ignore)){p.y=ground.point.y;containerValid=ground.normal.y>=.85f&&surface.normal.y>=.85f;}
   containerGhost.transform.SetPositionAndRotation(p,Quaternion.Euler(0,containerYaw,0));var bounds=ContainerScene.Bounds(containerGhost.transform);containerGhost.transform.position+=Vector3.up*(p.y-bounds.min.y+.015f);containerPosition=containerGhost.transform.position;
   bounds=ContainerScene.Bounds(containerGhost.transform);
   // AABB test is deliberately conservative for rotated furniture. Keep a small floor clearance.
   var overlaps=Physics.OverlapBox(bounds.center+Vector3.up*.035f,new Vector3(Math.Max(.02f,bounds.extents.x-.04f),Math.Max(.02f,bounds.extents.y-.06f),Math.Max(.02f,bounds.extents.z-.04f)),Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
   foreach(var collider in overlaps)if(collider!=null&&collider.Pointer!=surface.collider?.Pointer&&collider.bounds.max.y>p.y+.08f){containerValid=false;containerInvalid="空间不足：请避开墙体、人物或其他容器。";break;}
   if(!containerValid&&containerInvalid.StartsWith("请瞄准"))containerInvalid="落点不够平坦，或网格吸附后没有支撑面。";
  }
  if(containerGhostMaterial!=null)containerGhostMaterial.color=containerValid?new Color(.15f,.85f,.6f,.55f):new Color(1,.25f,.22f,.55f);
 }
 static void DrawContainerBuild(){
  DrawEditorCrosshair();float w=Math.Min(640,Screen.width-24);var box=new Rect((Screen.width-w)/2,Math.Max(12,Screen.height-160),w,144);InterfaceMenu.Panel(box);
  InterfaceMenu.Label(new(box.x+14,box.y+8,w-28,26),"连续放置 · "+ItemSpawner.TemplateName(containerTemplate),InterfaceMenu.Accent,16);
  InterfaceMenu.Label(new(box.x+14,box.y+38,w-28,24),containerValid?"可放置 · "+containerYaw.ToString("0")+"° · 网格 "+(containerGrid?"0.25m":"关闭"):containerInvalid,containerValid?new Color(.3f,1,.65f):new Color(1,.5f,.4f),12);
  InterfaceMenu.Paragraph(new(box.x+14,box.y+68,w-28,48),"右键放置 · 左键删除容器 · Q/E 或滚轮旋转 · Shift 精细旋转 · X 吸附\n"+ShortcutBindings.Label(containerBuildKey!.Value)+" / Esc 退出 · "+containerStatus,null,12);
 }
 static void ClearContainerPreview(){
  if(containerImageObject!=null)UnityEngine.Object.Destroy(containerImageObject);containerImageObject=null;
  if(containerCamera!=null)UnityEngine.Object.Destroy(containerCamera.gameObject);containerCamera=null;
  if(containerImage!=null){containerImage.Release();UnityEngine.Object.Destroy(containerImage);}containerImage=null;containerImageTemplate="";containerImageSource=IntPtr.Zero;
 }
 static void ContainerThumbnail(LootableContainer source,string template){
  if(containerImageTemplate==template&&containerImageSource==source.Pointer&&containerImage!=null)return;
  ClearContainerPreview();containerImageTemplate=template;containerImageSource=source.Pointer;
  containerImageObject=ContainerScene.Visual(source);containerImageObject.transform.SetPositionAndRotation(new Vector3(0,-10000,0),Quaternion.Euler(0,25,0));
  foreach(var t in containerImageObject.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
  containerImage=new RenderTexture(384,256,16);containerImage.Create();
  var go=new GameObject("Assistant container thumbnail camera");go.hideFlags=HideFlags.HideAndDontSave;containerCamera=go.AddComponent<Camera>();containerCamera.enabled=false;containerCamera.cullingMask=1<<31;containerCamera.clearFlags=CameraClearFlags.SolidColor;containerCamera.backgroundColor=new Color(.04f,.055f,.07f);containerCamera.targetTexture=containerImage;containerCamera.nearClipPlane=.01f;
  var b=ContainerScene.Bounds(containerImageObject.transform);float size=Math.Max(.2f,b.extents.magnitude);containerCamera.transform.position=b.center+new Vector3(1,.65f,1).normalized*size*3;containerCamera.transform.LookAt(b.center);containerCamera.farClipPlane=size*8;containerCamera.fieldOfView=40;
  var light=go.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.cullingMask=1<<31;go.transform.LookAt(b.center);
  // Only preview meshes are enabled for this explicit render.
  try{containerCamera.Render();}finally{containerImageObject.SetActive(false);light.enabled=false;}
 }
 static void DrawContainerCatalog(Rect rect){
  float x=rect.x,y=rect.y,w=rect.width;if(InterfaceMenu.Button(new(x,y,w,30),"← 返回 AI / 物品 / 容器分类")){createContainer=false;return;}y+=40;
  query=InterfaceMenu.Field(new(x,y,w,30),query);y+=40;
  var current=Current();var catalog=ContainerScene.Catalog.Where(d=>query.Length==0||(d.Name+d.Code+d.Id).Contains(query,StringComparison.OrdinalIgnoreCase)).ToArray();
  float listHeight=Math.Clamp((rect.height-300)*.55f,80,300);scroll=GUI.BeginScrollView(new(x,y,w,listHeight),scroll,new Rect(0,0,w-22,catalog.Length*40));
  try{for(int i=0;i<catalog.Length;i++){var entry=catalog[i];bool available=ContainerScene.Prototype(current,entry.Id)!=null;if(InterfaceMenu.Button(new(0,i*40,w-28,32),(available?"● ":"○ ")+entry.Name,entry.Id==containerTemplate)){containerTemplate=entry.Id;ClearContainerPreview();}}}finally{GUI.EndScrollView();}y+=listHeight+10;
  var source=ContainerScene.Prototype(current,containerTemplate);float previewHeight=Math.Max(68,Math.Min(128,rect.yMax-y-124)),previewWidth=Math.Min(192,w*.34f);
  if(source!=null){if(Event.current.type==EventType.Repaint)ContainerThumbnail(source,containerTemplate);if(containerImage!=null)GUI.DrawTexture(new(x,y,previewWidth,previewHeight),containerImage,ScaleMode.ScaleToFit);}
  InterfaceMenu.Paragraph(new(x+previewWidth+12,y,w-previewWidth-12,previewHeight),(containerTemplate.Length==0?"请选择容器。":ItemSpawner.TemplateName(containerTemplate)+"\n"+containerTemplate+"\n")+(source!=null?"当前地图模型可用；预览为实际网格。":"此容器的模型尚未随当前地图加载。列表保留此项以便查找，当前不可放置。"),null,12);y+=previewHeight+8;
  string[] modes={"server","empty","custom"};string[] names={"当前服务端配置","空容器","自定义物资"};
  int index=Array.IndexOf(modes,containerMode);if(InterfaceMenu.Button(new(x,y,w,30),"初始物资："+names[Math.Max(0,index)]))containerMode=modes[(index+1)%3];y+=38;
  bool old=GUI.enabled;GUI.enabled=old&&source!=null;
  if(InterfaceMenu.Button(new(x,y,w,34),"进入连续放置 · "+ShortcutBindings.Label(containerBuildKey!.Value)))Run(()=>{if(containerMode=="custom")LootOptions("custom").Validate();BeginContainerBuild();});
  GUI.enabled=old;y+=40;
  if(containerMode=="custom"&&InterfaceMenu.Button(new(x,y,w,30),"编辑自定义物资候选项"))Modal("container-loot");
 }
}
