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
  clips.TryGetValue(name,out var clip);
  if(!ProjectAudioRules.ValidClip(clip!=null,clip==null?0:clip.samples,clip==null?0:clip.channels,clip==null?0:clip.frequency,clip!=null&&clip.loadState==AudioDataLoadState.Loaded)){
   if(clip!=null)Object.Destroy(clip);
   clips.Remove(name);
   var wave=WavePcm.Read(Path.Combine(context.ModuleDirectory,"Assets",name+".wav"));clip=AudioClip.Create("ProjectE "+name,wave.Samples.Length/wave.Channels,wave.Channels,wave.Rate,false);
   // One-shot playback releases its native clip reference when the voice ends.
   // Keep module-owned clips through raid UnloadUnusedAssets, and still recover
   // if a game/device lifecycle explicitly destroys an object in our cache.
   clip.hideFlags=HideFlags.HideAndDontSave;
   if(!clip.SetData(new Il2CppStructArray<float>(wave.Samples),0)){Object.Destroy(clip);throw new IOException("Audio upload failed");}clips[name]=clip;
  }
  EnsureAudioSource();
  source!.PlayOneShot(clip,Mathf.Clamp01(volume));
 }
 void EnsureAudioSource(){
  if(root==null){root=new GameObject("ProjectE sound"){hideFlags=HideFlags.HideAndDontSave};Object.DontDestroyOnLoad(root);source=null;}
  if(source==null)source=root.GetComponent<AudioSource>()??root.AddComponent<AudioSource>();
  if(!root.activeSelf)root.SetActive(true);
  // Native game audio teardown/focus changes can disable surviving sources.
  // Restore only our owned feedback source on every actual playback request.
  source.enabled=true;source.playOnAwake=false;source.mute=false;source.volume=1;source.pitch=1;
  source.spatialBlend=0;source.spatialize=false;source.dopplerLevel=0;source.priority=16;
  source.ignoreListenerPause=true;source.bypassReverbZones=true;
 }
 void Clear(){if(root!=null)Object.Destroy(root);foreach(var t in textures.Values)Object.Destroy(t);foreach(var c in clips.Values)Object.Destroy(c);textures.Clear();clips.Clear();source=null;root=null;}
}
