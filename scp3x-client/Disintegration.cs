using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using EFT.InventoryLogic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace Scp3X.Client;
public sealed partial class DisruptorModule {
 sealed record PendingDeath(IntPtr World,string Profile,Vector3 Point,float Started,HashSet<string> Items);
 sealed class Dissolve {
  internal required PendingDeath Death;
  internal required Corpse Body;
  internal required GameObject Root;
  internal required List<Surface> Surfaces;
  internal required (Renderer Renderer,bool Enabled)[] Originals;
  internal float Started,Next;
 }
 sealed record Surface(Mesh Mesh,Transform Transform,Vector3[] Vertices,int[][] Triangles,float[][] Thresholds);
 sealed class BeamState {internal required LineRenderer Outer,Core;internal required GameObject Root;internal required IntPtr Shot;internal float Started;internal Vector3 End;}
 readonly List<PendingDeath> deaths=[];
 readonly List<Dissolve> dissolving=[];
 readonly HashSet<string> deadProfiles=[];
 readonly List<BeamState> beams=[];
 Material? energy;
 Material Energy(){if(energy!=null)return energy;var shader=Shader.Find("Hidden/Internal-Colored")??throw new InvalidOperationException("3-X energy shader unavailable");energy=new Material(shader){hideFlags=HideFlags.HideAndDontSave};energy.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);energy.SetInt("_DstBlend",(int)BlendMode.One);energy.SetInt("_ZTest",(int)CompareFunction.LessEqual);energy.SetInt("_ZWrite",0);energy.SetInt("_Cull",(int)CullMode.Off);return energy;}
 void Beam(Shot shot,Vector3 origin,Vector3 direction){
  if(beams.Count>=24){Object.Destroy(beams[0].Root);beams.RemoveAt(0);}
  var root=new GameObject("3-X Energy Beam"){hideFlags=HideFlags.HideAndDontSave};
  LineRenderer Line(string name,float width,Color color){var go=new GameObject(name){hideFlags=HideFlags.HideAndDontSave};go.transform.SetParent(root.transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=Energy();line.positionCount=2;line.useWorldSpace=true;line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;line.SetPosition(0,origin);line.SetPosition(1,origin+direction.normalized*DisruptorRules.BeamRange);line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;return line;}
  var outer=Line("Ion sheath",disintegration?.12f:.035f,new(.12f,1,.65f,.6f));var core=Line("White core",disintegration?.035f:.01f,new(.7f,1,.9f,1));beams.Add(new(){Outer=outer,Core=core,Root=root,Shot=shot.Pointer,Started=Time.unscaledTime,End=origin+direction.normalized*DisruptorRules.BeamRange});
 }
 void CollisionBeam(Shot shot){if(shot.HitCollider==null)return;foreach(var b in beams)if(b.Shot==shot.Pointer){b.End=shot.HitPoint;b.Outer.SetPosition(1,b.End);b.Core.SetPosition(1,b.End);}}
 void QueueDeath(Player victim,Vector3 point){
  var current=Singleton<GameWorld>.Instance;if(current==null||string.IsNullOrWhiteSpace(victim.ProfileId)||!deadProfiles.Add(victim.ProfileId))return;
  var ids=new HashSet<string>();if(victim.Profile?.Inventory?.Equipment is {} equipment)foreach(var item in equipment.GetAllItems())if(item!=null)ids.Add(item.Id.ToString());
  var held=victim.HandsController?.GetItem();if(held!=null)ids.Add(held.Id.ToString());
  deaths.Add(new(current.Pointer,victim.ProfileId,point,Time.unscaledTime,ids));kills++;
 }
 Corpse? FindBody(GameWorld current,string profile){var loot=current.LootItems;if(loot==null)return null;for(int i=0;i<loot.Count;i++){var corpse=loot.GetByIndex(i)?.TryCast<Corpse>();if(corpse!=null&&corpse.PlayerProfileID==profile)return corpse;}return null;}
 static bool Alive(GameWorld current,string profile){var all=current.AllAlivePlayersList;if(all==null)return false;for(int i=0;i<all.Count;i++){var p=all[i];if(p!=null&&p.ProfileId==profile&&p.HealthController?.IsAlive==true)return true;}return false;}
 void BeginDissolve(PendingDeath death,Corpse body){
  var originals=body.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&(r.TryCast<SkinnedMeshRenderer>()!=null||r.TryCast<MeshRenderer>()!=null)).ToArray();
  var root=new GameObject("3-X Disintegrating "+death.Profile){hideFlags=HideFlags.HideAndDontSave};root.transform.position=body.transform.position;var surfaces=new List<Surface>();
  try{
   foreach(var source in originals){
    Mesh? mesh=null;var skinned=source.TryCast<SkinnedMeshRenderer>();if(skinned!=null&&skinned.sharedMesh!=null){mesh=new Mesh{indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.HideAndDontSave};skinned.BakeMesh(mesh,false);}
    else{var original=source.GetComponent<MeshFilter>()?.sharedMesh;if(original!=null&&original.isReadable)mesh=Object.Instantiate(original);}
    if(mesh==null||mesh.vertexCount==0){if(mesh!=null)Object.Destroy(mesh);continue;}
    var go=new GameObject("Atomizing "+source.name){hideFlags=HideFlags.HideAndDontSave,layer=source.gameObject.layer};go.transform.SetParent(root.transform,false);go.transform.position=source.transform.position;go.transform.rotation=source.transform.rotation;go.transform.localScale=source.transform.lossyScale;
    go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=source.sharedMaterials;renderer.shadowCastingMode=ShadowCastingMode.Off;
    Vector3[] vertices=mesh.vertices;int[][] triangles=new int[mesh.subMeshCount][];float[][] thresholds=new float[mesh.subMeshCount][];
    var bounds=renderer.bounds;float height=Math.Max(.15f,bounds.size.y);
    for(int s=0;s<mesh.subMeshCount;s++){triangles[s]=mesh.GetTriangles(s);thresholds[s]=new float[triangles[s].Length/3];for(int i=0;i<thresholds[s].Length;i++){var center=(vertices[triangles[s][i*3]]+vertices[triangles[s][i*3+1]]+vertices[triangles[s][i*3+2]])/3;Vector3 world=go.transform.TransformPoint(center);float y=(world.y-bounds.min.y)/height;float noise=Mathf.Repeat(Mathf.Sin(Vector3.Dot(world,new Vector3(12.9898f,78.233f,37.719f)))*43758.5453f,1);thresholds[s][i]=Math.Clamp(y*.7f+noise*.3f,0,1);}}
    mesh.MarkDynamic();surfaces.Add(new(mesh,go.transform,vertices,triangles,thresholds));
   }
   if(surfaces.Count==0)throw new InvalidOperationException("Corpse geometry is not readable; refusing an invisible instant removal");
   var originalState=originals.Select(r=>(r,r.enabled)).ToArray();foreach(var r in originals)r.enabled=false;
   var ps=root.AddComponent<ParticleSystem>();var main=ps.main;main.loop=false;main.startLifetime=1.1f;main.startSpeed=.5f;main.startSize=.018f;main.startColor=new Color(.18f,1,.65f,.9f);main.maxParticles=1200;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=-.18f;
   var emission=ps.emission;emission.enabled=false;var pr=ps.GetComponent<ParticleSystemRenderer>();pr.sharedMaterial=Energy();pr.renderMode=ParticleSystemRenderMode.Billboard;ps.Play();
   dissolving.Add(new(){Death=death,Body=body,Root=root,Surfaces=surfaces,Originals=originalState,Started=Time.unscaledTime});
  }catch{foreach(var surface in surfaces)Object.Destroy(surface.Mesh);Object.Destroy(root);throw;}
 }
 void TickEffects(GameWorld? current){
  for(int i=beams.Count-1;i>=0;i--){var beam=beams[i];float t=(Time.unscaledTime-beam.Started)/.45f;if(t>=1||beam.Root==null){if(beam.Root!=null)Object.Destroy(beam.Root);beams.RemoveAt(i);continue;}var c=new Color(.12f,1,.65f,(1-t)*.6f);beam.Outer.startColor=beam.Outer.endColor=c;beam.Core.startColor=beam.Core.endColor=new Color(.7f,1,.9f,1-t);}
  if(current==null)return;
  for(int i=deaths.Count-1;i>=0;i--){var death=deaths[i];if(death.World!=current.Pointer||Alive(current,death.Profile)){deaths.RemoveAt(i);continue;}var body=FindBody(current,death.Profile);if(body!=null&&dissolving.Count<8){deaths.RemoveAt(i);try{BeginDissolve(death,body);}catch(Exception e){Error(e);}}else if(Time.unscaledTime-death.Started>12){deaths.RemoveAt(i);Error(new InvalidOperationException("3-X corpse not created: "+death.Profile));}}
  for(int i=dissolving.Count-1;i>=0;i--){var d=dissolving[i];if(d.Death.World!=current.Pointer||d.Body==null||Alive(current,d.Death.Profile)){Release(d,true);dissolving.RemoveAt(i);continue;}
   float progress=DisruptorRules.DissolveProgress(Time.unscaledTime-d.Started);
   if(progress>=1){try{RemoveVictimLoot(current,d);erased++;Release(d,false);}catch(Exception e){Error(e);Release(d,true);}dissolving.RemoveAt(i);continue;}
   if(Time.unscaledTime<d.Next)continue;d.Next=Time.unscaledTime+.05f;d.Root.transform.position+=Vector3.up*.018f;
   var ps=d.Root.GetComponent<ParticleSystem>();int emitted=0;
   foreach(var surface in d.Surfaces)for(int s=0;s<surface.Triangles.Length;s++){
    var source=surface.Triangles[s];var result=new List<int>(source.Length);for(int t=0;t<surface.Thresholds[s].Length;t++){float threshold=surface.Thresholds[s][t];if(threshold>progress){result.Add(source[t*3]);result.Add(source[t*3+1]);result.Add(source[t*3+2]);}else if(threshold>progress-.025f&&emitted<35){var point=surface.Vertices[source[t*3]];var emit=new ParticleSystem.EmitParams();emit.position=surface.Transform.TransformPoint(point);emit.velocity=new Vector3(UnityEngine.Random.Range(-.3f,.3f),UnityEngine.Random.Range(.3f,1.1f),UnityEngine.Random.Range(-.3f,.3f));ps.Emit(emit,1);emitted++;}}surface.Mesh.SetTriangles(result.ToArray(),s,false);
   }
  }
 }
 static void RemoveVictimLoot(GameWorld current,Dissolve d){
  if(d.Body==null||!DisruptorRules.MayEraseCorpse(d.Death.World.ToInt64(),current.Pointer.ToInt64(),d.Death.Profile,d.Body.PlayerProfileID,Alive(current,d.Death.Profile)))throw new InvalidOperationException("3-X corpse identity changed");
  var roots=new List<LootItem>();var list=current.LootItems;if(list!=null)for(int i=0;i<list.Count;i++){var loot=list.GetByIndex(i);if(loot!=null&&loot.Pointer!=d.Body.Pointer&&loot.TryCast<Corpse>()==null&&loot.Item!=null&&d.Death.Items.Contains(loot.Item.Id.ToString()))roots.Add(loot);}
  foreach(var root in roots)if(root!=null)current.DestroyLoot(root.Cast<IKillable>());
  current.DestroyLoot(d.Body.Cast<IKillable>());
 }
 static void Release(Dissolve d,bool restore){if(restore)foreach(var source in d.Originals)if(source.Renderer!=null)source.Renderer.enabled=source.Enabled;foreach(var s in d.Surfaces)if(s.Mesh!=null)Object.Destroy(s.Mesh);if(d.Root!=null)Object.Destroy(d.Root);}
 void ClearEffects(){foreach(var d in dissolving)Release(d,true);dissolving.Clear();deaths.Clear();deadProfiles.Clear();foreach(var b in beams)if(b.Root!=null)Object.Destroy(b.Root);beams.Clear();if(energy!=null)Object.Destroy(energy);energy=null;}
}
