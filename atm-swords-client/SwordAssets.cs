using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using Local.ModHost;
using Object=UnityEngine.Object;
namespace AllTheModium.Client;
// The source sprites inherit Minecraft item/handheld. They contain no arm animation.
// Reuse the native gladius mesh coordinate system: X thickness, Y width, Z blade.
internal sealed class SwordAssets {
 internal readonly Mesh Mesh;
 internal readonly Texture2D Texture;
 internal readonly Material Material;
 Vector3[] originalVertices=[];
 float appliedSize;
 Color32[] originalPixels=[];
 int[] vertexPixels=[];
 float appliedBrightness=-1,appliedOpacity=-1;
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
  if(Swords.IsUtility(sword.Id)){
   float width=vertices.Max(v=>v.x)-vertices.Min(v=>v.x),height=vertices.Max(v=>v.y)-vertices.Min(v=>v.y),center=(vertices.Max(v=>v.x)+vertices.Min(v=>v.x))*.5f,bottom=vertices.Min(v=>v.y);scale=.42f/Math.Max(width,height);
   for(int i=0;i<vertices.Count;i++){var v=vertices[i];vertices[i]=new(v.z*scale,(v.x-center)*scale,(v.y-bottom)*scale-.06f);}
  }else for(int i=0;i<vertices.Count;i++){var v=vertices[i];vertices[i]=new(v.z*scale*1.41421356f,(v.x-v.y)*scale-.003738f,(v.x+v.y-min)*scale-.152696f);}
  originalVertices=vertices.ToArray();
  Mesh.vertices=vertices.ToArray();Mesh.uv=uv.ToArray();Mesh.triangles=indices.ToArray();Mesh.RecalculateNormals();Mesh.RecalculateBounds();
  Texture=new Texture2D(2,2,TextureFormat.RGBA32,false);context.OnShutdown(()=>Object.Destroy(Texture));
  if(!ImageConversion.LoadImage(Texture,File.ReadAllBytes(Path.Combine(directory,sword.Key+"_sword.png")),false))throw new InvalidDataException("Invalid sword texture: "+sword.Key);
  Texture.filterMode=FilterMode.Point;Texture.wrapMode=TextureWrapMode.Clamp;Texture.name="ATM "+sword.Key;
  originalPixels=Texture.GetPixels32();
  // Every imported pixel prism has a constant UV/color. Use vertex color so
  // the fog-free material can write depth and cull back faces. Sprites/Default
  // hardcodes ZWrite Off/Cull Off, causing the back and internal pixel faces
  // to overlap the visible blade when it is raised across the camera.
  vertexPixels=uv.Select(v=>Math.Clamp((int)(v.y*Texture.height),0,Texture.height-1)*Texture.width+Math.Clamp((int)(v.x*Texture.width),0,Texture.width-1)).ToArray();
  var shader=Shader.Find("Hidden/Internal-Colored")??throw new InvalidOperationException("Depth-aware pixel sword shader unavailable");
  Material=new Material(shader){name="ATM "+sword.Key};context.OnShutdown(()=>Object.Destroy(Material));
  foreach(string property in new[]{"_SrcBlend","_DstBlend","_ZWrite","_ZTest","_Cull"})if(!Material.HasProperty(property))throw new InvalidOperationException("Sword shader lacks "+property);
  Material.color=Color.white;
  Material.SetInt("_ZTest",(int)CompareFunction.LessEqual);Material.SetInt("_Cull",(int)CullMode.Back);
 }
 internal void SetAppearance(float brightnessPercent,float opacityPercent){
  float brightness=SwordAppearanceRules.Brightness(brightnessPercent),opacity=SwordAppearanceRules.Opacity(opacityPercent);
  if(brightness==appliedBrightness&&opacity==appliedOpacity)return;
  var pixels=new Color32[originalPixels.Length];
  for(int i=0;i<pixels.Length;i++){
   var p=originalPixels[i];
   pixels[i]=p.a==0?new Color32(0,0,0,0):new Color32(SwordAppearanceRules.Scale(p.r,brightness),SwordAppearanceRules.Scale(p.g,brightness),SwordAppearanceRules.Scale(p.b,brightness),SwordAppearanceRules.Scale(p.a,opacity));
  }
  Texture.SetPixels32(pixels);Texture.Apply(false,false);
  bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
  var colors=new Color[vertexPixels.Length];
  for(int i=0;i<colors.Length;i++){var p=pixels[vertexPixels[i]];Color color=new(p.r/255f,p.g/255f,p.b/255f,p.a/255f);colors[i]=linear?color.linear:color;}
  Mesh.colors=colors;
  bool opaque=opacity>=1;
  Material.SetInt("_ZWrite",opaque?1:0);
  Material.SetInt("_SrcBlend",(int)(opaque?BlendMode.One:BlendMode.SrcAlpha));
  Material.SetInt("_DstBlend",(int)(opaque?BlendMode.Zero:BlendMode.OneMinusSrcAlpha));
  Material.renderQueue=opaque?2000:3000;
  Material.SetOverrideTag("RenderType",opaque?"Opaque":"Transparent");
  appliedBrightness=brightness;appliedOpacity=opacity;
 }
 internal void SetSize(float percent){
  float size=(float.IsFinite(percent)?Math.Clamp(percent,20,100):55)/100;if(appliedSize==size)return;appliedSize=size;
  var grip=new Vector3(0,-.003738f,0);
  Mesh.vertices=originalVertices.Select(v=>grip+(v-grip)*size).ToArray();Mesh.RecalculateBounds();
 }
}
