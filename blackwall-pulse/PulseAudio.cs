using EFT.UI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
namespace LocalBlackwallPulse;
internal static class PulseAudio {
 static readonly Dictionary<string,AudioClip> clips=new();static readonly List<(GameObject Object,float Until)> playing=new();
 static string error="";static bool loaded;static float nextDeath;static int casts,deaths,requested;
 internal static void Initialize(){loaded=false;error="";nextDeath=0;casts=deaths=requested=0;}
 internal static void Tick(){
  if(!loaded){loaded=true;foreach(string key in new[]{"cast","death"})try{var wave=WavePcm.Read(Path.Combine(PulseRuntime.Home,"Assets",key+".wav"));var clip=AudioClip.Create("Blackwall "+key,wave.Samples.Length/wave.Channels,wave.Channels,wave.Rate,false);if(clip==null||!clip.SetData(new Il2CppStructArray<float>(wave.Samples),0))throw new IOException("AudioClip.SetData failed");clips[key]=clip;PulseRuntime.Log.LogInfo($"Blackwall PCM ready: {key}, {clip.length:F2}s, {wave.Channels}ch");}catch(Exception e){error=key+": "+e.Message;PulseRuntime.Log.LogWarning("Blackwall audio: "+error);}}
  foreach(var sound in playing.ToArray())if(Time.unscaledTime>=sound.Until||!PulseRuntime.InRaid()){if(sound.Object!=null)UnityEngine.Object.Destroy(sound.Object);playing.Remove(sound);}
 }
 static bool Play(string key,Vector3 point,float volume){
  if(volume<=0)return false;if(!clips.TryGetValue(key,out var clip)||clip==null){error=key+" clip is not loaded";return false;}
  try{
   if(GUISounds.Exist(out var sounds)&&sounds!=null){sounds.PlaySound(clip,false,true,volume);return true;}
   if(playing.Count>=8)return false;var root=new GameObject("Blackwall sound"){hideFlags=HideFlags.HideAndDontSave};root.transform.position=point;
   var source=root.AddComponent<AudioSource>();source.playOnAwake=false;source.clip=clip;source.volume=volume;source.spatialBlend=0;source.priority=32;source.ignoreListenerPause=true;source.dopplerLevel=0;source.Play();playing.Add((root,Time.unscaledTime+clip.length+.2f));return true;
  }catch(Exception e){error=key+": "+e.Message;PulseRuntime.Log.LogWarning("Blackwall playback: "+error);return false;}
 }
 internal static void Cast(Vector3 point,float volume){if(Play("cast",point,volume))casts++;}
 internal static bool Preview(float volume){Tick();return Play("death",Vector3.zero,volume);}
 internal static void Death(Vector3 point,float volume){requested++;if(Time.unscaledTime<nextDeath)return;if(Play("death",point,volume)){nextDeath=Time.unscaledTime+.1f;deaths++;}}
 internal static object Diagnostics()=>new{loaded=clips.Keys.ToArray(),castPlays=casts,deathRequests=requested,deathPlays=deaths,lastError=error,route="EFT audible/2D",activeSources=playing.Count};
 internal static void Stop(){foreach(var item in playing)if(item.Object!=null)UnityEngine.Object.Destroy(item.Object);playing.Clear();}
 internal static void Shutdown(){Stop();foreach(var clip in clips.Values)if(clip!=null)UnityEngine.Object.Destroy(clip);clips.Clear();loaded=false;}
}
