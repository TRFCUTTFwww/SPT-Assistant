using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.Interactive;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;

namespace LocalSpt5Esp;

internal static class EditorTargetHighlight
{
 const string Prefix="Assistant editor highlight";
 sealed record Layer(Renderer Source,Renderer Overlay);
 static readonly List<Layer> layers=new();
 static readonly Il2CppStructArray<RaycastHit> hits=new(512);
 static GameObject? target;static Material? material;
 internal static void Tick(GameWorld current,Ray ray,float range){
  var contact=CrosshairProbe.Read(current,current.MainPlayer,ray,range);
  if(contact.State==CrosshairState.Overflow){Clear();return;}
  float nearest=contact.State==CrosshairState.Empty?range:contact.Distance+.025f;
  GameObject? next=contact.Actor?.gameObject;
  int count=Physics.RaycastNonAlloc(ray,hits,range,~0,QueryTriggerInteraction.Ignore);
  if(count>=hits.Length){Clear();return;}
  for(int i=0;i<count;i++){var hit=hits[i];var c=hit.collider;if(c==null||hit.distance>nearest||CrosshairProbe.LocalCollider(current,current.MainPlayer,c))continue;
   var container=c.GetComponentInParent<LootableContainer>();var loot=c.GetComponentInParent<LootItem>();
   if(container!=null){next=ContainerScene.Root(container).gameObject;nearest=hit.distance;}else if(loot!=null){next=loot.gameObject;nearest=hit.distance;}
  }
  if(next!=target){Clear();target=next;if(target!=null)Build();}
  foreach(var layer in layers)if(layer.Overlay!=null)layer.Overlay.enabled=layer.Source!=null&&layer.Source.enabled&&layer.Source.gameObject.activeInHierarchy;
 }
 static void Build(){
  var shader=new[]{"Hidden/Internal-Colored","Sprites/Default","Unlit/Color"}.Select(Shader.Find).FirstOrDefault(s=>s!=null&&s.isSupported);if(shader==null)return;
  material=new Material(shader){hideFlags=HideFlags.HideAndDontSave,renderQueue=4100,color=new Color(1,.04f,.03f,.72f)};
  material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_ZWrite",0);material.SetInt("_ZTest",(int)CompareFunction.LessEqual);material.SetInt("_Cull",(int)CullMode.Off);
  foreach(var source in target!.GetComponentsInChildren<Renderer>(true)){
   if(source==null||source.name.StartsWith(Prefix,StringComparison.Ordinal)||source.name.StartsWith("SPT Assistant tint",StringComparison.Ordinal)||source.name.StartsWith("Blackwall",StringComparison.Ordinal))continue;
   var skin=source.TryCast<SkinnedMeshRenderer>();var mesh=skin?.sharedMesh??source.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null)continue;
   var node=new GameObject(Prefix){hideFlags=HideFlags.HideAndDontSave,layer=source.gameObject.layer};node.transform.SetParent(source.transform,false);
   Renderer overlay;if(skin!=null){var copy=node.AddComponent<SkinnedMeshRenderer>();copy.sharedMesh=mesh;copy.bones=skin.bones;copy.rootBone=skin.rootBone;copy.localBounds=skin.localBounds;for(int i=0;i<mesh.blendShapeCount;i++)copy.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i));overlay=copy;}else{node.AddComponent<MeshFilter>().sharedMesh=mesh;overlay=node.AddComponent<MeshRenderer>();}
   var materials=new Il2CppReferenceArray<Material>(mesh.subMeshCount);for(int i=0;i<materials.Length;i++)materials[i]=material;overlay.sharedMaterials=materials;overlay.shadowCastingMode=ShadowCastingMode.Off;overlay.receiveShadows=false;overlay.allowOcclusionWhenDynamic=false;layers.Add(new(source,overlay));
  }
 }
 internal static void Clear(){foreach(var layer in layers)if(layer.Overlay!=null){layer.Overlay.gameObject.SetActive(false);UnityEngine.Object.Destroy(layer.Overlay.gameObject);}layers.Clear();target=null;if(material!=null)UnityEngine.Object.Destroy(material);material=null;}
}
