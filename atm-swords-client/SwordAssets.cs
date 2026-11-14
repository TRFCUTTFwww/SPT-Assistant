using System.Globalization;
using UnityEngine;
using Local.ModHost;
using Object=UnityEngine.Object;
namespace AllTheModium.Client;
// The source sprites inherit Minecraft item/handheld. They contain no arm animation.
// Reuse the native gladius mesh coordinate system: X thickness, Y width, Z blade.
internal sealed class SwordAssets {
 internal readonly Mesh Mesh;
 internal readonly Texture2D Texture;
 internal readonly Material Material;
 internal SwordAssets(Sword sword,string directory,ModuleContext context){
  Mesh=new Mesh{name="ATM "+sword.Key};context.OnShutdown(()=>Object.Destroy(Mesh));
  var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
  float F(string s)=>float.Parse(s,CultureInfo.InvariantCulture);
  foreach(string line in File.ReadLines(Path.Combine(directory,sword.Key+"_sword.obj"))){
   string[] p=line.Split(' ',StringSplitOptions.RemoveEmptyEntries);
   if(p.Length==0)continue;
   if(p[0]=="v")vertices.Add(new(F(p[1]),F(p[2]),F(p[3])));
   else if(p[0]=="vt")uv.Add(new(F(p[1]),F(p[2])));
   else if(p[0]=="f"){
    int[] face=p.Skip(1).Select(s=>int.Parse(s.Split('/')[0],CultureInfo.InvariantCulture)-1).ToArray();
    for(int i=1;i<face.Length-1;i++){indices.Add(face[0]);indices.Add(face[i]);indices.Add(face[i+1]);}
   }
  }
  if(vertices.Count==0||uv.Count!=vertices.Count||indices.Count==0)throw new InvalidDataException("Invalid sword geometry: "+sword.Key);
  float min=vertices.Min(v=>v.x+v.y),max=vertices.Max(v=>v.x+v.y);
  // Start from measured gladius bounds, then shrink around the hand's local
  // origin. Keep the width center at the original grip offset.
  float scale=.831764f/(max-min);
  const float modelScale=.65f;
  for(int i=0;i<vertices.Count;i++){var v=vertices[i];vertices[i]=new(v.z*scale*1.41421356f*modelScale,(v.x-v.y)*scale*modelScale-.003738f,((v.x+v.y-min)*scale-.152696f)*modelScale);}
  Mesh.vertices=vertices.ToArray();Mesh.uv=uv.ToArray();Mesh.triangles=indices.ToArray();Mesh.RecalculateNormals();Mesh.RecalculateBounds();
  Texture=new Texture2D(2,2,TextureFormat.RGBA32,false);context.OnShutdown(()=>Object.Destroy(Texture));
  if(!ImageConversion.LoadImage(Texture,File.ReadAllBytes(Path.Combine(directory,sword.Key+"_sword.png")),false))throw new InvalidDataException("Invalid sword texture: "+sword.Key);
  Texture.filterMode=FilterMode.Point;Texture.wrapMode=TextureWrapMode.Clamp;Texture.name="ATM "+sword.Key;
  var shader=Shader.Find("Unlit/Texture")??Shader.Find("Sprites/Default")??throw new InvalidOperationException("Pixel sword shader unavailable");
  Material=new Material(shader){name="ATM "+sword.Key,mainTexture=Texture};context.OnShutdown(()=>Object.Destroy(Material));
 }
}
