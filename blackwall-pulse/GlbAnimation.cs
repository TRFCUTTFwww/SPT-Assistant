using System.Numerics;
using System.Text.Json;

namespace LocalBlackwallPulse;

// Read the ordinary glTF 2 animation data. No REDengine runtime is required.
internal sealed class GlbAnimation {
 internal sealed record Bone(string Name,int Parent,Vector3 Position,Quaternion Rotation);
 internal sealed record Track(int Bone,string Path,float[] Times,float[] Values,int Width,bool Step);
 internal sealed record Clip(string Name,float Duration,Track[] Tracks);
 internal Bone[] Bones{get;}
 internal Clip[] Clips{get;}
 internal Dictionary<string,int> Names{get;}
 readonly byte[] data;readonly JsonElement json;readonly int binary;
 internal GlbAnimation(string path){
  data=File.ReadAllBytes(path);if(data.Length<28||BitConverter.ToUInt32(data,0)!=0x46546C67||BitConverter.ToUInt32(data,4)!=2||BitConverter.ToUInt32(data,8)!=data.Length)throw new InvalidDataException("Invalid GLB header");
  int length=checked((int)BitConverter.ToUInt32(data,12));if(BitConverter.ToUInt32(data,16)!=0x4E4F534A||length>data.Length-28)throw new InvalidDataException("Invalid GLB JSON");
  using var doc=JsonDocument.Parse(data.AsMemory(20,length));json=doc.RootElement.Clone();binary=28+length;
  if(BitConverter.ToUInt32(data,24+length)!=0x004E4942)throw new InvalidDataException("Missing GLB binary chunk");
  var nodes=json.GetProperty("nodes");var parents=Enumerable.Repeat(-1,nodes.GetArrayLength()).ToArray();
  for(int i=0;i<parents.Length;i++)if(nodes[i].TryGetProperty("children",out var children))foreach(var child in children.EnumerateArray()){int childIndex=child.GetInt32();if(childIndex<=i||childIndex>=parents.Length||parents[childIndex]!=-1)throw new InvalidDataException("Animation skeleton is not a parent-first tree");parents[childIndex]=i;}
  Bones=nodes.EnumerateArray().Select((n,i)=>new Bone(n.TryGetProperty("name",out var name)?name.GetString()??"":"",parents[i],V(n,"translation"),Q(n,"rotation"))).ToArray();
  Names= new(StringComparer.Ordinal);for(int i=0;i<Bones.Length;i++)Names.TryAdd(Bones[i].Name,i);
  Clips=json.GetProperty("animations").EnumerateArray().Where(a=>a.GetProperty("name").GetString()?.StartsWith("gesture_",StringComparison.Ordinal)==true).Select(a=>{
   var samplers=a.GetProperty("samplers");var tracks=a.GetProperty("channels").EnumerateArray().Select(c=>{
    var s=samplers[c.GetProperty("sampler").GetInt32()];string interpolation=s.TryGetProperty("interpolation",out var value)?value.GetString()??"LINEAR":"LINEAR";if(interpolation is not ("LINEAR" or "STEP"))throw new InvalidDataException("Unsupported interpolation: "+interpolation);
    var target=c.GetProperty("target");string kind=target.GetProperty("path").GetString()??"";int width=kind=="rotation"?4:3;
    var times=Read(s.GetProperty("input").GetInt32(),1);var output=Read(s.GetProperty("output").GetInt32(),width);
    if(times.Length==0||output.Length!=times.Length*width||times.Any(t=>!float.IsFinite(t)))throw new InvalidDataException("Invalid animation samples");
    for(int i=1;i<times.Length;i++)if(times[i]<=times[i-1])throw new InvalidDataException("Unsorted animation times");
    int bone=target.GetProperty("node").GetInt32();if(bone<0||bone>=Bones.Length)throw new InvalidDataException("Invalid animation bone");return new Track(bone,kind,times,output,width,interpolation=="STEP");
   }).ToArray();return new Clip(a.GetProperty("name").GetString()!,tracks.Max(t=>t.Times[^1]),tracks);
  }).ToArray();
  if(Clips.Length!=4)throw new InvalidDataException("Expected four station gestures");
 }
 static Vector3 V(JsonElement n,string key)=>n.TryGetProperty(key,out var a)?new(a[0].GetSingle(),a[1].GetSingle(),a[2].GetSingle()):Vector3.Zero;
 static Quaternion Q(JsonElement n,string key)=>n.TryGetProperty(key,out var a)?Quaternion.Normalize(new(a[0].GetSingle(),a[1].GetSingle(),a[2].GetSingle(),a[3].GetSingle())):Quaternion.Identity;
 float[] Read(int index,int width){
  var a=json.GetProperty("accessors")[index];if(a.GetProperty("componentType").GetInt32()!=5126||a.TryGetProperty("sparse",out _)||a.GetProperty("type").GetString()!=(width==1?"SCALAR":width==4?"VEC4":"VEC3"))throw new InvalidDataException("Unsupported animation accessor");
  var view=json.GetProperty("bufferViews")[a.GetProperty("bufferView").GetInt32()];if(view.GetProperty("buffer").GetInt32()!=0)throw new InvalidDataException("External animation buffer");
  int start=binary+(view.TryGetProperty("byteOffset",out var vo)?vo.GetInt32():0)+(a.TryGetProperty("byteOffset",out var ao)?ao.GetInt32():0),count=a.GetProperty("count").GetInt32(),stride=view.TryGetProperty("byteStride",out var st)?st.GetInt32():width*4;
  if(count<0||count>100000||start<binary||stride<width*4||(long)start+(long)Math.Max(0,count-1)*stride+width*4>data.Length)throw new InvalidDataException("Animation buffer bounds");
  var values=new float[count*width];for(int i=0;i<count;i++)for(int k=0;k<width;k++){float v=BitConverter.ToSingle(data,start+i*stride+k*4);if(!float.IsFinite(v))throw new InvalidDataException("Non-finite animation");values[i*width+k]=v;}return values;
 }
 internal void BindPose(Vector3[] positions,Quaternion[] rotations){for(int i=0;i<Bones.Length;i++){positions[i]=Bones[i].Position;rotations[i]=Bones[i].Rotation;}}
 internal void Sample(int clip,float time,Vector3[] positions,Quaternion[] rotations){
  for(int i=0;i<Bones.Length;i++){positions[i]=Bones[i].Position;rotations[i]=Bones[i].Rotation;}
  foreach(var t in Clips[clip%Clips.Length].Tracks){int found=Array.BinarySearch(t.Times,time);int right=Math.Clamp(found<0?~found:found,0,t.Times.Length-1);int left=found>=0||time>=t.Times[^1]?right:Math.Max(0,right-1);float blend=left==right||t.Step?0:Math.Clamp((time-t.Times[left])/(t.Times[right]-t.Times[left]),0,1);int x=left*t.Width,y=right*t.Width;
   if(t.Path=="rotation"){var a=new Quaternion(t.Values[x],t.Values[x+1],t.Values[x+2],t.Values[x+3]);var b=new Quaternion(t.Values[y],t.Values[y+1],t.Values[y+2],t.Values[y+3]);rotations[t.Bone]=Quaternion.Normalize(Quaternion.Slerp(a,b,blend));}
   else if(t.Path=="translation")positions[t.Bone]=Vector3.Lerp(new(t.Values[x],t.Values[x+1],t.Values[x+2]),new(t.Values[y],t.Values[y+1],t.Values[y+2]),blend);
  }
 }
}
