using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ContainerScene {
 internal sealed record Definition(string Id,string Name,string Code);
 internal sealed class Reference {
  readonly IntPtr world,pointer;readonly string id;readonly LootableContainer target;
  internal Reference(GameWorld current,LootableContainer value){world=current.Pointer;pointer=value.Pointer;id=value.Id;target=value;}
  internal LootableContainer Resolve(){var current=Singleton<GameWorld>.Instance;if(!RaidClock.Active||current?.Pointer!=world||target==null||target.Pointer!=pointer||target.Id!=id||!target.gameObject.activeInHierarchy)throw new ArgumentException("容器已被删除或战局已改变。");return target;}
 }
 static readonly List<Definition> definitions=new();static readonly Dictionary<string,LootableContainer> prototypes=new(StringComparer.Ordinal);
 static readonly Dictionary<IntPtr,GameObject> created=new();static readonly HashSet<IntPtr> deleted=new();static LootableContainer[] scene=Array.Empty<LootableContainer>();static IntPtr world;static float nextScan;
 internal static IReadOnlyList<Definition> Catalog{get{Load();return definitions;}}
 static void Load(){if(definitions.Count>0)return;using var db=JsonDocument.Parse(File.ReadAllText(ItemCatalog.DatabasePath));foreach(var row in db.RootElement.EnumerateObject())if(row.Value.TryGetProperty("_parent",out var parent)&&parent.GetString()=="566965d44bdc2d814c8b4571")definitions.Add(new(row.Name,ItemSpawner.TemplateName(row.Name),row.Value.GetProperty("_name").GetString()??row.Name));definitions.Sort((a,b)=>string.Compare(a.Name,b.Name,StringComparison.CurrentCulture));}
 internal static string Template(LootableContainer c)=>c.ItemOwner?.RootItem?.TemplateId.ToString()??c.Template??"";
 static bool ModelUsable(LootableContainer c)=>c!=null&&!deleted.Contains(c.Pointer)&&c.GetComponentInParent<EFT.SynchronizableObjects.AirdropSynchronizableObject>()==null&&Root(c).GetComponentsInChildren<MeshRenderer>(true).Length>0;
 static bool Usable(LootableContainer c)=>c!=null&&!deleted.Contains(c.Pointer)&&c.gameObject.activeInHierarchy&&c.GetComponentInParent<EFT.SynchronizableObjects.AirdropSynchronizableObject>()==null;
 internal static LootableContainer[] Read(GameWorld current,bool force=false){if(world!=current.Pointer){Reset();world=current.Pointer;}if(!force&&Time.unscaledTime<nextScan)return scene;nextScan=Time.unscaledTime+1;scene=UnityEngine.Object.FindObjectsByType<LootableContainer>(FindObjectsSortMode.None).Where(Usable).ToArray();foreach(var c in UnityEngine.Resources.FindObjectsOfTypeAll<LootableContainer>()){if(!ModelUsable(c))continue;string tpl=Template(c);if(tpl.Length==24&&(!prototypes.TryGetValue(tpl,out var prior)||!ModelUsable(prior)))prototypes[tpl]=c;}return scene;}
 internal static LootableContainer? Prototype(GameWorld current,string template){Read(current);if(prototypes.TryGetValue(template,out var exact)&&ModelUsable(exact))return exact;Load();string code=definitions.FirstOrDefault(d=>d.Id==template)?.Code??"";string stem=Stem(code);return prototypes.Where(p=>ModelUsable(p.Value)&&Stem(definitions.FirstOrDefault(d=>d.Id==p.Key)?.Code??"")==stem&&stem.Length>0).Select(p=>p.Value).FirstOrDefault();}
 static string Stem(string code){foreach(string suffix in new[]{"_terminal_key","_labyrinth","_rich","_lift","_lock","_quest","_ev_01"})code=code.Replace(suffix,"",StringComparison.Ordinal);return code;}
 internal static Transform Root(LootableContainer c){if(created.TryGetValue(c.Pointer,out var wrapper)&&wrapper!=null)return wrapper.transform;var target=c.transform;for(var ancestor=target;ancestor!=null;ancestor=ancestor.parent)if(ancestor.name.StartsWith("Assistant placed container ",StringComparison.Ordinal)){created[c.Pointer]=ancestor.gameObject;return ancestor;}if(target.GetComponentsInChildren<Renderer>(true).Length>0)return target;for(int i=0;i<2&&target.parent!=null;i++){var parent=target.parent;if(parent.GetComponentsInChildren<WorldInteractiveObject>(true).Length!=1||parent.GetComponentsInChildren<Renderer>(true).Length==0)break;var size=Bounds(parent).size;if(size.x>6||size.y>6||size.z>6)break;target=parent;}return target;}
 internal static Bounds Bounds(Transform root){var renderers=root.GetComponentsInChildren<Renderer>(true);var bounds=new Bounds(root.position,Vector3.zero);bool first=true;foreach(var renderer in renderers){if(renderer==null)continue;if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}if(first)bounds=new Bounds(root.position,Vector3.one*.5f);return bounds;}
 internal static GameObject Visual(LootableContainer source){var origin=Root(source);var result=new GameObject("Assistant container preview");result.hideFlags=HideFlags.HideAndDontSave;result.transform.SetPositionAndRotation(origin.position,origin.rotation);foreach(var renderer in origin.GetComponentsInChildren<MeshRenderer>(true)){var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null)continue;var child=new GameObject(renderer.name);child.transform.SetParent(result.transform,false);child.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);child.transform.localScale=renderer.transform.lossyScale;child.AddComponent<MeshFilter>().sharedMesh=mesh;var copy=child.AddComponent<MeshRenderer>();copy.sharedMaterials=renderer.sharedMaterials;copy.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}return result;}
 internal static LootableContainer Create(GameWorld current,LootableContainer source,Item root,Vector3 position,Quaternion rotation,bool opened){
  var original=Root(source);var wrapper=new GameObject("Assistant placed container "+root.Id);GameObject? clone=null;bool active=original.gameObject.activeSelf;
  try{
   original.gameObject.SetActive(false);try{clone=UnityEngine.Object.Instantiate(original.gameObject,wrapper.transform);}finally{original.gameObject.SetActive(active);}
   clone.transform.localPosition=Vector3.zero;clone.transform.localRotation=Quaternion.identity;clone.transform.localScale=original.lossyScale;wrapper.transform.SetPositionAndRotation(position,rotation);
   var target=clone.GetComponentInChildren<LootableContainer>(true)??throw new ArgumentException("模型没有可交互容器。");
   string id=root.Id.ToString();target.Id=id;target.Template=root.TemplateId.ToString();target.NoInteractionsAllowed=false;target.Operatable=true;target._forceLocalInteraction=true;
   var owner=new ItemController(root,id,"assistant-world-editor",false,EOwnerType.Profile);target.ItemOwner=null;target.Init(owner);
   var key=owner.Cast<IItemOwner>();current.ItemOwners[key]=new GameWorld.ItemOwnerWorldData(target.transform,false,key);created[target.Pointer]=wrapper;
   foreach(var body in clone.GetComponentsInChildren<Rigidbody>(true)){if(!body.isKinematic){body.velocity=Vector3.zero;body.angularVelocity=Vector3.zero;}body.isKinematic=true;body.useGravity=false;}clone.SetActive(true);current.RegisterWorldInteractionObject(target);SetOpen(target,opened);nextScan=0;RaidLoot.Invalidate();return target;
  }catch{if(clone!=null){var container=clone.GetComponentInChildren<LootableContainer>(true);if(container?.ItemOwner is {} owner)current.ItemOwners.Remove(owner.Cast<IItemOwner>());if(container!=null)created.Remove(container.Pointer);}UnityEngine.Object.Destroy(wrapper);throw;}
 }
 internal static void Replace(GameWorld current,LootableContainer target,Item root){var old=target.ItemOwner??throw new ArgumentException("容器库存尚未就绪。");var owner=new ItemController(root,root.Id.ToString(),"assistant-world-editor",false,EOwnerType.Profile);try{target.Init(owner);var key=owner.Cast<IItemOwner>();current.ItemOwners[key]=new GameWorld.ItemOwnerWorldData(target.transform,false,key);current.ItemOwners.Remove(old.Cast<IItemOwner>());}catch{target.Init(old);throw;}nextScan=0;RaidLoot.Invalidate();}
 internal static void Remove(GameWorld current,LootableContainer target){if(target.InteractingPlayer!=null)throw new ArgumentException("容器正在使用中，请结束交互后重试。");if(target.ItemOwner is {} owner)current.ItemOwners.Remove(owner.Cast<IItemOwner>());deleted.Add(target.Pointer);Root(target).gameObject.SetActive(false);if(created.Remove(target.Pointer,out var wrapper))UnityEngine.Object.Destroy(wrapper);nextScan=0;RaidLoot.Invalidate();}
 internal static void SetOpen(LootableContainer target,bool value){if(target.InteractingPlayer!=null)throw new ArgumentException("容器正在使用中，请结束交互后重试。");target.CancelInteraction();if(value)target.Open();else target.ForceCloseLocalDoor(false);}
 internal static bool Open(LootableContainer target)=>target.DoorState==EDoorState.Open;
 internal static void Reset(){prototypes.Clear();created.Clear();deleted.Clear();scene=Array.Empty<LootableContainer>();world=IntPtr.Zero;nextScan=0;}
}
