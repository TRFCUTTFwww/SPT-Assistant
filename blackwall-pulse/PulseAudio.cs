using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Comfort.Common;
using EFT.UI;
using UnityEngine;
namespace LocalBlackwallPulse;
internal static class PulseAudio {
 const float OutputGain=3f;
 static readonly Dictionary<string,AudioClip> clips=new();
 // Wwise death event: two pairs of random sequences, plus a third random layer.
 static readonly string[][] deathGroups={new[]{"198549883","504056792","1039762929","402672814","354156372","33035252"},new[]{"934676726","507834237","546145831","681900649","209456523","245105235"},new[]{"295523828","192515260","332804055","888906332"},new[]{"71869785","1053829965","616156953","329011936"},new[]{"648806972","79544389","22212768","384243996","198417711","848620842"}};
 static readonly Dictionary<string,WavePcm> waves=new();
 sealed record Playing(AudioClip Clip,float Until);
 static readonly List<Playing> playing=new();static readonly System.Random random=new();static string[] lastDeath=Array.Empty<string>();
 static string error="";static bool loaded;static float nextDeath;static int casts,deaths,requested;
 internal static void Initialize(){loaded=false;error="";nextDeath=0;casts=deaths=requested=0;}
 internal static void Tick(){
  if(!loaded){loaded=true;foreach(string key in new[]{"cast","sparks","release","death"}.Concat(deathGroups.SelectMany(g=>g).Select(id=>"npc_"+id)))try{var wave=WavePcm.Read(Path.Combine(PulseRuntime.Home,"Assets",key+".wav"));if(key.StartsWith("npc_")){waves[key]=wave;continue;}clips[key]=CreateClip(key,wave);PulseRuntime.Log.LogInfo($"Blackwall PCM ready: {key}, {clips[key].length:F2}s, {wave.Channels}ch");}catch(Exception e){error=key+": "+e.Message;PulseRuntime.Log.LogWarning("Blackwall audio: "+error);}}
  foreach(var entry in playing.Where(p=>Time.unscaledTime>=p.Until).ToArray()){playing.Remove(entry);if(entry.Clip!=null)UnityEngine.Object.Destroy(entry.Clip);}
 }
 static AudioClip CreateClip(string name,WavePcm wave){wave=wave.WithGain(OutputGain);var clip=AudioClip.Create("Blackwall "+name,wave.Samples.Length/wave.Channels,wave.Channels,wave.Rate,false);if(clip==null||!clip.SetData(new Il2CppStructArray<float>(wave.Samples),0))throw new IOException("AudioClip.SetData failed");return clip;}
 static bool Play(string key,float volume){
  if(!clips.TryGetValue(key,out var clip)||clip==null){error=key+" clip is not loaded";return false;}return PlayClip(clip,volume);
 }
 static bool PlayClip(AudioClip clip,float volume){
  if(volume<=0)return false;
  try{if(!GUISounds.Exist(out var sounds)||sounds==null){error="game audio system unavailable";return false;}sounds.PlaySound(clip,false,true,volume);error="";return true;}
  catch(Exception e){error=clip.name+": "+e.Message;PulseRuntime.Log.LogWarning("Blackwall playback: "+error);return false;}
 }
 internal static void Impact(float volume){Tick();if(Play("cast",volume))casts++;Play("sparks",volume*.3f);Play("release",volume*.7f);}
 internal static bool Preview(float volume){int before=deaths;Death(Vector3.zero,volume);return deaths>before;}
 internal static void Death(Vector3 point,float volume){
  requested++;if(Time.unscaledTime<nextDeath)return;Tick();nextDeath=Time.unscaledTime+.25f;
  var selected=deathGroups.Select((g,i)=>{var options=g.Select(id=>"npc_"+id).Where(key=>lastDeath.Length<=i||lastDeath[i]!=key).ToArray();return options[random.Next(options.Length)];}).ToArray();lastDeath=selected;
  if(!selected.All(waves.ContainsKey)){if(Play("death",volume))deaths++;return;}
  try{var clip=CreateClip("mixed death",DeathMix.Mix(selected.Select(key=>waves[key]).ToArray()));if(PlayClip(clip,volume*.708f)){playing.Add(new(clip,Time.unscaledTime+clip.length+2));deaths++;}else UnityEngine.Object.Destroy(clip);}
  catch(Exception e){error="death mix: "+e.Message;PulseRuntime.Log.LogWarning("Blackwall audio: "+error);if(Play("death",volume))deaths++;}
 }
 internal static object Diagnostics()=>new{loaded=clips.Keys.Concat(waves.Keys).ToArray(),impactSequences=casts,deathRequests=requested,deathAccepted=deaths,deathVariants=deathGroups.Select(g=>g.Length).ToArray(),lastDeath,activeMixes=playing.Count,outputGain=OutputGain,lastError=error,route="mixed event via EFT UI sound system",listenerVolume=AudioListener.volume,listenerPaused=AudioListener.pause};
 internal static void Stop(){}
 internal static void Shutdown(){foreach(var entry in playing)if(entry.Clip!=null)UnityEngine.Object.Destroy(entry.Clip);playing.Clear();foreach(var clip in clips.Values)if(clip!=null)UnityEngine.Object.Destroy(clip);clips.Clear();waves.Clear();loaded=false;}
}
