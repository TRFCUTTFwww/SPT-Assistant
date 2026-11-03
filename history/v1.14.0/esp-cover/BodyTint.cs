using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
namespace LocalSpt5Esp;
internal static class BodyTint {
 internal static ConfigEntry<bool> Enabled=null!,ThroughWalls=null!;
 static ConfigEntry<float> Alpha=null!;
 static readonly ConfigEntry<string>[] colors=new ConfigEntry<string>[Enum.GetValues<CharacterKind>().Length];
 static readonly string?[] paletteCodes=new string?[Enum.GetValues<CharacterKind>().Length];
 static readonly Color[] palette=new Color[Enum.GetValues<CharacterKind>().Length];
 static readonly Material?[] materials=new Material?[Enum.GetValues<CharacterKind>().Length];
 record Saved(SkinnedMeshRenderer Renderer,Il2CppReferenceArray<Material> Original,CharacterKind Kind);
 static readonly Dictionary<IntPtr,Saved> saved=new();
 static readonly Dictionary<IntPtr,CharacterKind> kinds=new();
 static readonly Dictionary<IntPtr,(SkinnedMeshRenderer[] Renderers,float Expires)> rendererCache=new();
 static float nextScan,nextShaderTry;
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"7. Character colors","Enabled",true);
  ThroughWalls=CnConfig.Bind(p.Config,"7. Character colors","Through walls",true);
  Alpha=CnConfig.Bind(p.Config,"7. Character colors","Opacity",0.65f,new ConfigDescription("",new AcceptableValueRange<float>(0.15f,1)));
  foreach(var kind in Enum.GetValues<CharacterKind>())colors[(int)kind]=CnConfig.Bind(p.Config,"7. Character colors",kind+" color",RolePalette.Hex(kind));
 }
 internal static void BeginScan()=>kinds.Clear();
 internal static CharacterKind Kind(Player player){
  if(kinds.TryGetValue(player.Pointer,out var kind))return kind;
  kind=RolePalette.Classify(player.Profile?.Info?.Settings?.Role.ToString()??"",player.Profile?.Side==EPlayerSide.Savage);kinds[player.Pointer]=kind;return kind;
 }
 internal static string KindName(Player player)=>RolePalette.Name(Kind(player));
 static Color ForKind(CharacterKind kind){
  int index=(int)kind;string code=colors[index]?.Value??RolePalette.Hex(kind);
  if(paletteCodes[index]==code)return palette[index];
  string hex=code.Trim().TrimStart('#');
  if(hex.Length!=6||!uint.TryParse(hex,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint rgb))rgb=uint.Parse(RolePalette.Hex(kind)[1..],NumberStyles.HexNumber,CultureInfo.InvariantCulture);
  paletteCodes[index]=code;return palette[index]=new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f);
 }
 internal static Color Color(Player player)=>ForKind(Kind(player));
 static bool Materials(){
  if(materials.All(m=>m!=null))return true;
  if(Time.unscaledTime<nextShaderTry)return false;nextShaderTry=Time.unscaledTime+15;
  Shader? shader=new[]{"Hidden/Internal-Colored","Unlit/Color","Sprites/Default"}.Select(Shader.Find).FirstOrDefault(s=>s!=null&&s.isSupported);
  if(shader==null){Plugin.Self.Log.LogWarning("No supported character tint shader is available");return false;}
  for(int i=0;i<materials.Length;i++){
   if(materials[i]!=null)continue;var m=new Material(shader);materials[i]=m;
   m.hideFlags=HideFlags.HideAndDontSave;m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);m.SetInt("_Cull",(int)CullMode.Off);m.SetInt("_ZWrite",0);m.renderQueue=4000;
  }
  Plugin.Self.Log.LogInfo("Character tint shader: "+shader.name+"; "+materials.Length+" role colors");return true;
 }
 internal static void Update(IReadOnlyList<Player> players,Player local,bool showPeople,float range){
  if(!Enabled.Value||!showPeople){Clear();return;}
  if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+1;
  if(!Materials())return;
  foreach(var kind in Enum.GetValues<CharacterKind>()){
   var material=materials[(int)kind]!;material.SetInt("_ZTest",(int)(ThroughWalls.Value?CompareFunction.Always:CompareFunction.LessEqual));
   var color=ForKind(kind);material.color=new Color(color.r,color.g,color.b,Alpha.Value);
  }
  var active=new HashSet<IntPtr>();
  var activePlayers=new HashSet<IntPtr>();
  foreach(var player in players){
   if(player==null||player.IsYourPlayer||player.HealthController==null||!player.HealthController.IsAlive||Vector3.Distance(local.Position,player.Position)>range)continue;
   var kind=Kind(player);var material=materials[(int)kind]!;
   activePlayers.Add(player.Pointer);
   if(!rendererCache.TryGetValue(player.Pointer,out var cached)||Time.unscaledTime>=cached.Expires){cached=(player.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToArray(),Time.unscaledTime+5);rendererCache[player.Pointer]=cached;}
   foreach(var renderer in cached.Renderers){
    if(renderer==null)continue;active.Add(renderer.Pointer);
    if(saved.TryGetValue(renderer.Pointer,out var state)&&state.Kind==kind)continue;
    var original=state?.Original??renderer.sharedMaterials;if(original==null||original.Length==0)continue;
    saved[renderer.Pointer]=new(renderer,original,kind);
    var replacement=new Il2CppReferenceArray<Material>(original.Length);for(int i=0;i<replacement.Length;i++)replacement[i]=material;renderer.sharedMaterials=replacement;
   }
  }
  foreach(var key in saved.Keys.Where(k=>!active.Contains(k)).ToArray()){Restore(saved[key]);saved.Remove(key);}
  foreach(var key in rendererCache.Keys.Where(k=>!activePlayers.Contains(k)).ToArray())rendererCache.Remove(key);
 }
 static void Restore(Saved entry){try{if(entry.Renderer!=null)entry.Renderer.sharedMaterials=entry.Original;}catch{}}
 internal static void Clear(){foreach(var entry in saved.Values)Restore(entry);saved.Clear();rendererCache.Clear();kinds.Clear();nextScan=0;}
 internal static void Dispose(){Clear();for(int i=0;i<materials.Length;i++){if(materials[i]!=null)UnityEngine.Object.Destroy(materials[i]);materials[i]=null;}}
}
