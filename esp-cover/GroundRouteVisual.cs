using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using Object=UnityEngine.Object;
namespace LocalSpt5Esp;

internal sealed class GroundRouteVisual(string name) {
 GameObject? visual;
 LineRenderer? line;
 MeshRenderer? arrowRenderer;
 Mesh? arrowMesh;
 Material? material;
 static Vector3 U(NVector p)=>new(p.X,p.Y,p.Z);
 internal void Hide(){if(visual!=null&&visual.activeSelf)visual.SetActive(false);}
 bool Visual(){if(visual!=null&&material!=null)return true;DestroyVisual();var shader=new[]{"Hidden/Internal-Colored","Unlit/Color","Sprites/Default"}.Select(Shader.Find).FirstOrDefault(s=>s!=null&&s.isSupported);if(shader==null){return false;}material=new Material(shader){hideFlags=HideFlags.HideAndDontSave,renderQueue=3100};material.color=new Color(.22f,1f,.74f,.88f);material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_ZWrite",0);material.SetInt("_ZTest",(int)CompareFunction.LessEqual);material.SetInt("_Cull",(int)CullMode.Off);
  visual=new GameObject(name){hideFlags=HideFlags.HideAndDontSave};visual.transform.position=Vector3.zero;var lineObject=new GameObject("Route Line"){hideFlags=HideFlags.HideAndDontSave};lineObject.transform.SetParent(visual.transform,false);line=lineObject.AddComponent<LineRenderer>();line.useWorldSpace=true;line.sharedMaterial=material;line.numCornerVertices=3;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;var arrowObject=new GameObject("Route Arrows"){hideFlags=HideFlags.HideAndDontSave};arrowObject.transform.SetParent(visual.transform,false);arrowRenderer=arrowObject.AddComponent<MeshRenderer>();arrowRenderer.sharedMaterial=material;arrowRenderer.shadowCastingMode=ShadowCastingMode.Off;arrowRenderer.receiveShadows=false;arrowMesh=new Mesh{hideFlags=HideFlags.HideAndDontSave};arrowMesh.MarkDynamic();arrowObject.AddComponent<MeshFilter>().sharedMesh=arrowMesh;return true;
 }
 internal bool Render(NVector[] points,float width,float spacing,Color color){if(points.Length<2||!Visual()){Hide();return false;}visual!.SetActive(true);var positions=points.Select(p=>U(p)+Vector3.up*.12f).ToArray();line!.startWidth=line.endWidth=width;line.positionCount=positions.Length;line.SetPositions(new Il2CppStructArray<Vector3>(positions));material!.color=color;line.startColor=line.endColor=Color.white;
  var vertices=new List<Vector3>();var triangles=new List<int>();foreach(var arrow in RouteGeometry.Arrows(points,spacing)){var center=U(arrow.Point)+Vector3.up*.14f;var direction=U(arrow.Direction);var side=Vector3.Cross(Vector3.up,direction);var tip=center+direction*.48f;var middle=center-direction*.07f;var left=center-direction*.38f+side*.34f;var right=center-direction*.38f-side*.34f;int index=vertices.Count;vertices.AddRange(new[]{tip,left,middle,tip,middle,right});triangles.AddRange(new[]{index,index+1,index+2,index+3,index+4,index+5});}arrowMesh!.Clear();arrowMesh.vertices=new Il2CppStructArray<Vector3>(vertices.ToArray());arrowMesh.triangles=new Il2CppStructArray<int>(triangles.ToArray());arrowMesh.RecalculateBounds();return true;
 }
 internal void DestroyVisual(){try{if(visual!=null)Object.Destroy(visual);if(arrowMesh!=null)Object.Destroy(arrowMesh);if(material!=null)Object.Destroy(material);}finally{visual=null;line=null;arrowRenderer=null;arrowMesh=null;material=null;}}
}
