using System.Text.Json;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Local.ModHost;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace Scp3X.Client;
internal sealed class DisruptorAssets:IDisposable {
 internal readonly JsonDocument Document;
 internal JsonElement Data=>Document.RootElement;
 internal readonly Dictionary<int,Mesh> Meshes=[];
 internal readonly Dictionary<int,Material> Materials=[];
 readonly Dictionary<string,Texture2D> textures=[];
 readonly Dictionary<string,AudioClip> audio=[];
 internal readonly Dictionary<string,float[]> Animations=[];
 internal readonly ItemIcon Icon;
 internal readonly Sprite Sprite;
 GameObject? soundRoot;AudioSource? sound;bool disposed;
 internal readonly string Directory;
 internal DisruptorAssets(ModuleContext context){
  Directory=Path.Combine(context.ModuleDirectory,"Assets");Document=JsonDocument.Parse(File.ReadAllText(Path.Combine(Directory,"weapon.json")));context.OnShutdown(Dispose);
  foreach(var m in Data.GetProperty("meshes").EnumerateObject())Meshes.Add(int.Parse(m.Name),ReadMesh(Path.Combine(Directory,m.Value.GetProperty("file").GetString()!)));
  foreach(var a in Data.GetProperty("animations").EnumerateObject()){byte[] bytes=File.ReadAllBytes(Path.Combine(Directory,a.Value.GetProperty("file").GetString()!));float[] values=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,values,0,bytes.Length);Animations.Add(a.Name,values);}
  foreach(var m in Data.GetProperty("materials").EnumerateObject()){
   var shader=Shader.Find("Standard")??Shader.Find("Legacy Shaders/Diffuse")??throw new InvalidOperationException("3-X textured shader unavailable");var mat=new Material(shader){name=m.Value.GetProperty("name").GetString(),hideFlags=HideFlags.HideAndDontSave};
   foreach(var p in m.Value.GetProperty("textures").EnumerateObject()){
    var tex=Texture(p.Value.GetString()!);string prop=p.Name switch{"_BaseColorMap"=>"_MainTex","_NormalMap"=>"_BumpMap","_MaskMap"=>"_MetallicGlossMap",_=>""};if(prop.Length>0&&mat.HasProperty(prop))mat.SetTexture(prop,tex);
    if(prop=="_BumpMap")mat.EnableKeyword("_NORMALMAP");if(prop=="_MetallicGlossMap")mat.EnableKeyword("_METALLICGLOSSMAP");
   }
   Materials.Add(int.Parse(m.Name),mat);
  }
  var icon=Texture("icon.png");Sprite=Sprite.Create(icon,new Rect(0,0,icon.width,icon.height),new Vector2(.5f,.5f),100);Icon=new ItemIcon(DisruptorRules.Weapon.GetHashCode(),false){_sprite=Sprite};
 }
 internal Texture2D Texture(string file){if(textures.TryGetValue(file,out var found))return found;var t=new Texture2D(2,2,TextureFormat.RGBA32,true){name="3-X "+file,hideFlags=HideFlags.HideAndDontSave};if(!ImageConversion.LoadImage(t,File.ReadAllBytes(Path.Combine(Directory,file)),false)){Object.Destroy(t);throw new IOException("3-X texture: "+file);}textures.Add(file,t);return t;}
 static Mesh ReadMesh(string path){
  using var r=new BinaryReader(File.OpenRead(path));if(new string(r.ReadChars(4))!="3XM1")throw new InvalidDataException(path);int count=r.ReadInt32(),subs=r.ReadInt32(),bind=r.ReadInt32();
  var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];var skin=new BoneWeight[count];
  for(int i=0;i<count;i++){vertices[i]=new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());normals[i]=new(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());uv[i]=new(r.ReadSingle(),r.ReadSingle());skin[i]=new BoneWeight{boneIndex0=r.ReadInt32(),boneIndex1=r.ReadInt32(),boneIndex2=r.ReadInt32(),boneIndex3=r.ReadInt32(),weight0=r.ReadSingle(),weight1=r.ReadSingle(),weight2=r.ReadSingle(),weight3=r.ReadSingle()};}
  var mesh=new Mesh{name="3-X "+Path.GetFileNameWithoutExtension(path),hideFlags=HideFlags.HideAndDontSave,indexFormat=IndexFormat.UInt32};mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.subMeshCount=subs;
  for(int s=0;s<subs;s++){int n=r.ReadInt32();int[] indices=new int[n];for(int i=0;i<n;i++)indices[i]=r.ReadInt32();mesh.SetTriangles(indices,s);}
  var poses=new Matrix4x4[bind];for(int i=0;i<bind;i++){var m=new Matrix4x4();for(int y=0;y<4;y++)for(int x=0;x<4;x++)m[y,x]=r.ReadSingle();poses[i]=m;}
  if(bind>0){mesh.bindposes=poses;mesh.boneWeights=skin;}mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
 }
 internal void Play(string name,float volume=.65f){
  if(!File.Exists(Path.Combine(Directory,name+".wav")))return;
  if(!audio.TryGetValue(name,out var clip)||clip==null||clip.samples<=0){using var r=new BinaryReader(File.OpenRead(Path.Combine(Directory,name+".wav")));if(new string(r.ReadChars(4))!="RIFF")throw new InvalidDataException(name);r.ReadInt32();r.ReadChars(4);int channels=0,rate=0,bits=0,format=0;byte[]? samples=null;
   while(r.BaseStream.Position+8<=r.BaseStream.Length){string id=new(r.ReadChars(4));int size=r.ReadInt32();long next=r.BaseStream.Position+size+(size&1);if(id=="fmt "){format=r.ReadUInt16();channels=r.ReadUInt16();rate=r.ReadInt32();r.ReadInt32();r.ReadInt16();bits=r.ReadInt16();}else if(id=="data")samples=r.ReadBytes(size);r.BaseStream.Position=next;}
   if(samples==null||channels<=0||rate<=0||!(format==1&&bits==16||format==3&&bits==32))throw new InvalidDataException("Unsupported 3-X audio "+name);float[] values=new float[samples.Length/(bits/8)];for(int i=0;i<values.Length;i++)values[i]=bits==16?BitConverter.ToInt16(samples,i*2)/32768f:BitConverter.ToSingle(samples,i*4);
   clip=AudioClip.Create(name,values.Length/channels,channels,rate,false);clip.hideFlags=HideFlags.HideAndDontSave;if(!clip.SetData(new Il2CppStructArray<float>(values),0))throw new IOException("3-X audio upload");audio[name]=clip;
  }
  if(soundRoot==null){soundRoot=new GameObject("3-X Audio"){hideFlags=HideFlags.HideAndDontSave};Object.DontDestroyOnLoad(soundRoot);}sound??=soundRoot.GetComponent<AudioSource>()??soundRoot.AddComponent<AudioSource>();sound.enabled=true;sound.mute=false;sound.spatialBlend=0;sound.playOnAwake=false;sound.volume=1;sound.pitch=1;sound.ignoreListenerPause=false;sound.PlayOneShot(clip,volume);
 }
 public void Dispose(){if(disposed)return;disposed=true;if(soundRoot!=null)Object.Destroy(soundRoot);if(Sprite!=null)Object.Destroy(Sprite);foreach(var o in Meshes.Values)Object.Destroy(o);foreach(var o in Materials.Values)Object.Destroy(o);foreach(var o in textures.Values)Object.Destroy(o);foreach(var o in audio.Values)if(o!=null)Object.Destroy(o);Meshes.Clear();Materials.Clear();textures.Clear();audio.Clear();Document.Dispose();}
}
internal sealed class DisruptorRig:IDisposable {
 internal readonly GameObject Root;
 readonly DisruptorAssets assets;readonly Transform[] nodes;string animation="idle_pose";float started,speed=1;
 internal DisruptorRig(DisruptorAssets assets,Transform parent,int layer){
  this.assets=assets;Root=new GameObject("3-X Original Rig"){hideFlags=HideFlags.HideAndDontSave,layer=layer};Root.transform.SetParent(parent,false);
  var descriptions=assets.Data.GetProperty("nodes").EnumerateArray().ToArray();nodes=new Transform[descriptions.Length];
  for(int i=0;i<nodes.Length;i++){var d=descriptions[i];var go=new GameObject(d.GetProperty("name").GetString()){layer=layer,hideFlags=HideFlags.HideAndDontSave};var t=go.transform;nodes[i]=t;int p=d.GetProperty("parent").GetInt32();t.SetParent(p<0?Root.transform:nodes[p],false);t.localPosition=V(d.GetProperty("position"));t.localRotation=Q(d.GetProperty("rotation"));t.localScale=V(d.GetProperty("scale"));}
  foreach(var r in assets.Data.GetProperty("renderers").EnumerateArray()){var skin=nodes[r.GetProperty("node").GetInt32()].gameObject.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=assets.Meshes[r.GetProperty("mesh").GetInt32()];skin.sharedMaterials=r.GetProperty("materials").EnumerateArray().Select(v=>assets.Materials[v.GetInt32()]).ToArray();skin.bones=r.GetProperty("bones").EnumerateArray().Select(v=>nodes[v.GetInt32()]).ToArray();skin.rootBone=nodes[r.GetProperty("rootBone").GetInt32()];skin.updateWhenOffscreen=true;skin.shadowCastingMode=ShadowCastingMode.Off;skin.receiveShadows=false;skin.localBounds=new Bounds(Vector3.zero,Vector3.one*100);}
  Play("idle_pose");
 }
 static Vector3 V(JsonElement a){var v=a.EnumerateArray().Select(v=>v.GetSingle()).ToArray();return new(v[0],v[1],v[2]);}
 static Quaternion Q(JsonElement a){var v=a.EnumerateArray().Select(v=>v.GetSingle()).ToArray();return new(v[0],v[1],v[2],v[3]);}
 internal void Play(string name,float duration=0){if(!assets.Animations.ContainsKey(name))return;animation=name;started=Time.unscaledTime;speed=duration>0?assets.Data.GetProperty("animations").GetProperty(name).GetProperty("duration").GetSingle()/duration:1;Sample(name,0);}
 internal void Tick(){float elapsed=(Time.unscaledTime-started)*speed;var a=assets.Data.GetProperty("animations").GetProperty(animation);float length=a.GetProperty("duration").GetSingle();if(elapsed>length&&animation!="idle_pose"){Play("idle_pose");return;}Sample(animation,Math.Min(elapsed,length));}
 void Sample(string name,float seconds){var a=assets.Data.GetProperty("animations").GetProperty(name);float frame=seconds*a.GetProperty("sampleRate").GetSingle();int max=a.GetProperty("frames").GetInt32()-1,i=Math.Clamp((int)frame,0,max),j=Math.Min(i+1,max);float blend=frame-i;var values=assets.Animations[name];
  foreach(var track in a.GetProperty("tracks").EnumerateArray()){var node=nodes[track.GetProperty("node").GetInt32()];int offset=track.GetProperty("offset").GetInt32(),size=track.GetProperty("size").GetInt32(),attribute=track.GetProperty("attribute").GetInt32();int l=offset+i*size,r=offset+j*size;
   if(attribute==2){node.localRotation=Quaternion.Slerp(new(values[l],values[l+1],values[l+2],values[l+3]),new(values[r],values[r+1],values[r+2],values[r+3]),blend);}else{var v=Vector3.Lerp(new(values[l],values[l+1],values[l+2]),new(values[r],values[r+1],values[r+2]),blend);if(attribute==1)node.localPosition=v;else if(attribute==3)node.localScale=v;else if(attribute==4)node.localRotation=Quaternion.Euler(v);}
  }
 }
 public void Dispose(){if(Root!=null)Object.Destroy(Root);}
}
