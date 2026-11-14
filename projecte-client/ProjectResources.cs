using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Local.ModHost;
using LocalBlackwallPulse;
using UnityEngine;
using Object=UnityEngine.Object;
namespace ProjectE.Client;
internal sealed class ProjectResources {
 readonly ModuleContext context;
 readonly Dictionary<string,Texture2D> textures=[];
 readonly Dictionary<string,AudioClip> clips=[];
 GameObject? root;AudioSource? source;
 internal ProjectResources(ModuleContext context){this.context=context;context.OnShutdown(Clear);}
 internal Texture2D Texture(string name){
  if(textures.TryGetValue(name,out var t))return t;
  t=new Texture2D(2,2,TextureFormat.RGBA32,false){name="ProjectE "+name,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
  if(!ImageConversion.LoadImage(t,File.ReadAllBytes(Path.Combine(context.ModuleDirectory,"Assets",name+".png")))){Object.Destroy(t);throw new IOException("Invalid texture: "+name);}
  textures.Add(name,t);return t;
 }
 internal void Play(string name,float volume){
  if(!clips.TryGetValue(name,out var clip)){
   var wave=WavePcm.Read(Path.Combine(context.ModuleDirectory,"Assets",name+".wav"));clip=AudioClip.Create("ProjectE "+name,wave.Samples.Length/wave.Channels,wave.Channels,wave.Rate,false);
   if(!clip.SetData(new Il2CppStructArray<float>(wave.Samples),0)){Object.Destroy(clip);throw new IOException("Audio upload failed");}clips.Add(name,clip);
  }
  if(root==null){root=new GameObject("ProjectE sound"){hideFlags=HideFlags.HideAndDontSave};Object.DontDestroyOnLoad(root);source=root.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;}
  source!.PlayOneShot(clip,Mathf.Clamp01(volume));
 }
 void Clear(){if(root!=null)Object.Destroy(root);foreach(var t in textures.Values)Object.Destroy(t);foreach(var c in clips.Values)Object.Destroy(c);textures.Clear();clips.Clear();source=null;root=null;}
}
