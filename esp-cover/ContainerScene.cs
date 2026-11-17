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
 static readonly Dictionary<IntPtr,Transform> roots=new();static readonly Dictionary<IntPtr,bool> modelValidity=new();static bool prefabsScanned;static readonly Dictionary<string,LootableContainer> libraryPrototypes=new(StringComparer.Ordinal);
 static readonly Dictionary<IntPtr,GameObject> created=new();static readonly HashSet<IntPtr> deleted=new();static LootableContainer[] scene=Array.Empty<LootableContainer>();static IntPtr world;static float nextScan;
 internal static IReadOnlyList<Definition> Catalog{get{Load();return definitions;}}
 static void Load(){if(definitions.Count>0)return;using var db=JsonDocument.Parse(File.ReadAllText(ItemCatalog.DatabasePath));foreach(var row in db.RootElement.EnumerateObject())if(row.Value.TryGetProperty("_parent",out var parent)&&parent.GetString()=="566965d44bdc2d814c8b4571")definitions.Add(new(row.Name,row.Name=="578f87b7245977356274f2cd"?"抽屉柜（四抽屉）":ItemSpawner.TemplateName(row.Name),row.Value.GetProperty("_name").GetString()??row.Name));definitions.Sort((a,b)=>string.Compare(a.Name,b.Name,StringComparison.CurrentCulture));}
 internal static string Template(LootableContainer c)=>c.ItemOwner?.RootItem?.TemplateId.ToString()??c.Template??"";
 static bool ModelUsable(LootableContainer c){if(c==null||deleted.Contains(c.Pointer))return false;if(modelValidity.TryGetValue(c.Pointer,out bool valid))return valid;return modelValidity[c.Pointer]=c.GetComponentInParent<EFT.SynchronizableObjects.AirdropSynchronizableObject>()==null&&Root(c).GetComponentsInChildren<MeshRenderer>(true).Length>0;}
 static bool Usable(LootableContainer c)=>c!=null&&!deleted.Contains(c.Pointer)&&c.gameObject.activeInHierarchy&&c.GetComponentInParent<EFT.SynchronizableObjects.AirdropSynchronizableObject>()==null;
 static void Index(IEnumerable<LootableContainer> values){foreach(var c in values){if(!ModelUsable(c))continue;string tpl=Template(c);if(tpl.Length==24&&(!prototypes.TryGetValue(tpl,out var prior)||prior==null||deleted.Contains(prior.Pointer)))prototypes[tpl]=c;}}
 internal static LootableContainer[] Read(GameWorld current,bool force=false){if(world!=current.Pointer){Reset();world=current.Pointer;}if(!force&&Time.unscaledTime<nextScan)return scene;nextScan=Time.unscaledTime+2;var all=UnityEngine.Object.FindObjectsByType<LootableContainer>(FindObjectsSortMode.None).Where(Usable).ToArray();Index(all);if(!prefabsScanned){prefabsScanned=true;Index(UnityEngine.Resources.FindObjectsOfTypeAll<LootableContainer>());}scene=all.GroupBy(c=>Root(c).Pointer).Select(g=>g.First()).ToArray();return scene;}
 internal static LootableContainer? Prototype(GameWorld current,string template){Read(current);if(libraryPrototypes.TryGetValue(template,out var loaded)&&loaded!=null)return loaded;if(ContainerLibrary.Get(template) is {} prefab){var list=prefab.GetComponentsInChildren<LootableContainer>(true);foreach(var c in list)roots[c.Pointer]=prefab.transform;var first=list.FirstOrDefault();if(first!=null)libraryPrototypes[template]=first;return first;}if(prototypes.TryGetValue(template,out var exact)&&exact!=null&&!deleted.Contains(exact.Pointer))return exact;Load();string stem=Stem(definitions.FirstOrDefault(d=>d.Id==template)?.Code??"");return prototypes.Where(p=>p.Value!=null&&!deleted.Contains(p.Value.Pointer)&&Stem(definitions.FirstOrDefault(d=>d.Id==p.Key)?.Code??"")==stem&&stem.Length>0).Select(p=>p.Value).FirstOrDefault();}
 internal static LootableContainer[] Group(LootableContainer c)=>Root(c).GetComponentsInChildren<LootableContainer>(true).ToArray();
 static string Stem(string code){foreach(string suffix in new[]{"_terminal_key","_labyrinth","_rich","_lift","_lock","_quest","_ev_01"})code=code.Replace(suffix,"",StringComparison.Ordinal);return code;}
 internal static Transform Root(LootableContainer c){if(created.TryGetValue(c.Pointer,out var wrapper)&&wrapper!=null)return wrapper.transform;if(roots.TryGetValue(c.Pointer,out var cached)&&cached!=null)return cached;var target=c.transform;for(var ancestor=target;ancestor!=null;ancestor=ancestor.parent)if(ancestor.name.StartsWith("Assistant placed container ",StringComparison.Ordinal)){created[c.Pointer]=ancestor.gameObject;return ancestor;}if(target.parent!=null&&target.parent.name.StartsWith("card_file",StringComparison.OrdinalIgnoreCase)){target=target.parent;roots[c.Pointer]=target;return target;}if(target.GetComponentsInChildren<Renderer>(true).Length>0){roots[c.Pointer]=target;return target;}for(int i=0;i<2&&target.parent!=null;i++){var parent=target.parent;if(parent.GetComponentsInChildren<WorldInteractiveObject>(true).Length!=1||parent.GetComponentsInChildren<Renderer>(true).Length==0)break;var size=Bounds(parent).size;if(size.x>6||size.y>6||size.z>6)break;target=parent;}roots[c.Pointer]=target;return target;}
 internal static Bounds Bounds(Transform root){var renderers=root.GetComponentsInChildren<Renderer>(true);var bounds=new Bounds(root.position,Vector3.zero);bool first=true;foreach(var renderer in renderers){if(renderer==null)continue;if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}if(first)bounds=new Bounds(root.position,Vector3.one*.5f);return bounds;}
 static Quaternion BaseRotation(LootableContainer source){var root=Root(source);if(created.ContainsKey(source.Pointer))return Quaternion.identity;if(ContainerLibrary.Get(Template(source)) is {} prefab&&root.Pointer==prefab.transform.Pointer)return root.rotation;var e=root.eulerAngles;return Quaternion.Euler(e.x,0,e.z);}
 internal static GameObject Visual(LootableContainer source){var origin=Root(source);var result=new GameObject("Assistant container preview");result.hideFlags=HideFlags.HideAndDontSave;result.transform.SetPositionAndRotation(origin.position,origin.rotation*Quaternion.Inverse(BaseRotation(source)));foreach(var renderer in origin.GetComponentsInChildren<MeshRenderer>(true)){var mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null)continue;var child=new GameObject(renderer.name);child.transform.SetParent(result.transform,false);child.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);child.transform.localScale=renderer.transform.lossyScale;child.AddComponent<MeshFilter>().sharedMesh=mesh;var copy=child.AddComponent<MeshRenderer>();copy.sharedMaterials=renderer.sharedMaterials;copy.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}result.transform.rotation=Quaternion.identity;return result;}
 internal static LootableContainer Create(GameWorld current,LootableContainer source,IReadOnlyList<Item> inventories,Vector3 position,Quaternion rotation,bool opened){
  var original=Root(source);var wrapper=new GameObject("Assistant placed container "+inventories[0].Id);GameObject? clone=null;bool active=original.gameObject.activeSelf;var registered=new List<LootableContainer>();
  try{
   original.gameObject.SetActive(false);try{clone=UnityEngine.Object.Instantiate(original.gameObject,wrapper.transform);}finally{original.gameObject.SetActive(active);}
   clone.transform.localPosition=Vector3.zero;clone.transform.localRotation=BaseRotation(source);clone.transform.localScale=original.lossyScale;wrapper.transform.SetPositionAndRotation(position,rotation);
   var targets=clone.GetComponentsInChildren<LootableContainer>(true);if(targets.Length!=inventories.Count)throw new ArgumentException("模型容器数量与库存数量不匹配。");
   for(int i=0;i<targets.Length;i++){
    var target=targets[i];var root=inventories[i];string id=root.Id.ToString();target.Id=id;target.Template=root.TemplateId.ToString();target.NoInteractionsAllowed=false;target.Operatable=true;target._forceLocalInteraction=true;
    var owner=new ItemController(root,id,"assistant-world-editor",false,EOwnerType.Profile);target.ItemOwner=null;target.Init(owner);
    var key=owner.Cast<IItemOwner>();current.ItemOwners[key]=new GameWorld.ItemOwnerWorldData(target.transform,false,key);created[target.Pointer]=wrapper;registered.Add(target);
   }
   foreach(var body in clone.GetComponentsInChildren<Rigidbody>(true)){if(!body.isKinematic){body.velocity=Vector3.zero;body.angularVelocity=Vector3.zero;}body.isKinematic=true;body.useGravity=false;}clone.SetActive(true);
   // Native registration dereferences NetworkWorld in offline games.
   foreach(var target in targets){if(current.NetworkWorld!=null)current.RegisterWorldInteractionObject(target);SetOpen(target,opened);}
   nextScan=0;RaidLoot.Invalidate();return targets[0];
  }catch{foreach(var target in registered){if(target.ItemOwner is {} owner)current.ItemOwners.Remove(owner.Cast<IItemOwner>());created.Remove(target.Pointer);}UnityEngine.Object.Destroy(wrapper);throw;}
 }
 internal static void Replace(GameWorld current,LootableContainer target,Item root){var old=target.ItemOwner??throw new ArgumentException("容器库存尚未就绪。");var owner=new ItemController(root,root.Id.ToString(),"assistant-world-editor",false,EOwnerType.Profile);try{target.Init(owner);var key=owner.Cast<IItemOwner>();current.ItemOwners[key]=new GameWorld.ItemOwnerWorldData(target.transform,false,key);current.ItemOwners.Remove(old.Cast<IItemOwner>());}catch{target.Init(old);throw;}nextScan=0;RaidLoot.Invalidate();}
 internal static void Remove(GameWorld current,LootableContainer target){var root=Root(target);var group=Group(target);foreach(var member in group)if(member.InteractingPlayer!=null)throw new ArgumentException("容器正在使用中，请结束交互后重试。");bool placed=created.ContainsKey(target.Pointer);foreach(var c in group){if(c.ItemOwner is {} owner)current.ItemOwners.Remove(owner.Cast<IItemOwner>());deleted.Add(c.Pointer);created.Remove(c.Pointer);roots.Remove(c.Pointer);}root.gameObject.SetActive(false);if(placed)UnityEngine.Object.Destroy(root.gameObject);nextScan=0;RaidLoot.Invalidate();}
 internal static void SetOpen(LootableContainer target,bool value){if(target.InteractingPlayer!=null)throw new ArgumentException("容器正在使用中，请结束交互后重试。");target.CancelInteraction();if(value)target.Open();else target.ForceCloseLocalDoor(false);}
 internal static bool Open(LootableContainer target)=>target.DoorState==EDoorState.Open;
 internal static void Reset(){roots.Clear();modelValidity.Clear();libraryPrototypes.Clear();prefabsScanned=false;prototypes.Clear();created.Clear();deleted.Clear();scene=Array.Empty<LootableContainer>();world=IntPtr.Zero;nextScan=0;}
}
