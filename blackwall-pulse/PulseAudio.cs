using UnityEngine;
using UnityEngine.Networking;

namespace LocalBlackwallPulse;

internal static class PulseAudio {
 static readonly Dictionary<string,AudioClip> clips=new();
 static readonly Queue<(string Name,string File)> queue=new();
 static readonly List<(GameObject Object,float Until)> playing=new();
 static UnityWebRequest? loading;static string loadingName="";
 static float nextDeath;
 internal static void Initialize(){queue.Enqueue(("cast",Path.Combine(PulseRuntime.Home,"Assets","cast.wav")));queue.Enqueue(("death",Path.Combine(PulseRuntime.Home,"Assets","death.wav")));}
 internal static void Tick(){
  if(loading!=null&&loading.isDone){try{if(loading.result==UnityWebRequest.Result.Success){var clip=DownloadHandlerAudioClip.GetContent(loading);if(clip!=null)clips[loadingName]=clip;}else PulseRuntime.Log.LogWarning("Blackwall audio: "+loading.error);}finally{loading.Dispose();loading=null;}}
  if(loading==null&&queue.TryDequeue(out var item)&&File.Exists(item.File)){loadingName=item.Name;loading=UnityWebRequestMultimedia.GetAudioClip(new Uri(item.File).AbsoluteUri,AudioType.WAV);loading.timeout=15;loading.SendWebRequest();}
  foreach(var sound in playing.ToArray())if(Time.unscaledTime>=sound.Until||!PulseRuntime.InRaid()){if(sound.Object!=null)UnityEngine.Object.Destroy(sound.Object);playing.Remove(sound);}
 }
 static void Play(string key,Vector3 point,float volume,bool spatial){if(volume<=0||!clips.TryGetValue(key,out var clip)||playing.Count>=12)return;var root=new GameObject("Blackwall sound"){hideFlags=HideFlags.HideAndDontSave};root.transform.position=point;var source=root.AddComponent<AudioSource>();source.clip=clip;source.volume=volume;source.spatialBlend=spatial?1:0;source.minDistance=2;source.maxDistance=100;source.rolloffMode=AudioRolloffMode.Linear;source.Play();playing.Add((root,Time.unscaledTime+clip.length+.2f));}
 internal static void Cast(Vector3 point,float volume)=>Play("cast",point,volume,false);
 internal static void Death(Vector3 point,float volume){if(Time.unscaledTime<nextDeath)return;nextDeath=Time.unscaledTime+.1f;Play("death",point,volume,true);}
 internal static void Stop(){foreach(var item in playing)if(item.Object!=null)UnityEngine.Object.Destroy(item.Object);playing.Clear();}
 internal static void Shutdown(){loading?.Abort();loading?.Dispose();loading=null;queue.Clear();Stop();foreach(var clip in clips.Values)if(clip!=null)UnityEngine.Object.Destroy(clip);clips.Clear();}
}
