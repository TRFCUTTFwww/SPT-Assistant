using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using EFT.Interactive;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class CorpseDisplay {
 internal static ConfigEntry<bool> Enabled=null!,Contents=null!;
 internal static ConfigEntry<float> Range=null!;
 static Corpse[] corpses=Array.Empty<Corpse>();
 static float nextScan,nextError;
 static long focused;
 static long cachedBody;
 static float nextDetails;
 static string cachedLabel="";static Color[] cachedColors=Array.Empty<Color>();
 internal readonly record struct Marker(Corpse Body,Vector3 Position,string Label,Color[] Colors);
 internal static readonly List<Marker> Markers=new();
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"12. Corpse ESP","Enabled",true,"Show corpse markers with character ESP (F6), independently of the ground-loot distance.");
  Contents=CnConfig.Bind(p.Config,"12. Corpse ESP","Show contents",true,"Show corpse equipment and nested backpack/pocket items when looking at the corpse marker. Requires item ESP (F7).");
  Range=CnConfig.Bind(p.Config,"12. Corpse ESP","Maximum distance",500f,new ConfigDescription("Maximum corpse marker distance in meters",new AcceptableValueRange<float>(5,2000)));
 }
 internal static void Reset(){corpses=Array.Empty<Corpse>();Markers.Clear();nextScan=nextDetails=0;focused=cachedBody=0;cachedLabel="";cachedColors=Array.Empty<Color>();}
 internal static void Focus(long key)=>focused=key;
 internal static Vector3 Position(Corpse body)=>body.TrackableTransform!=null?body.TrackableTransform.position:body.transform.position;
 internal static void Scan(GameWorld world,Vector3 origin,IReadOnlyDictionary<string,string> names,bool people,bool loot){
  Markers.Clear();if(!Enabled.Value||!people)return;
  if(Time.unscaledTime>=nextScan){
   nextScan=Time.unscaledTime+2;var found=new List<Corpse>();var all=world.LootItems;
   if(all!=null)for(int i=0;i<all.Count;i++){var body=all.GetByIndex(i)?.TryCast<Corpse>();if(body!=null)found.Add(body);}
   corpses=found.ToArray();
  }
  foreach(var body in corpses){
   if(body==null)continue;
   try{
    var position=Position(body);float distance=Vector3.Distance(position,origin);if(distance>Range.Value)continue;
    string name=names.TryGetValue(body.PlayerProfileID??"",out var nickname)?nickname:body.Side.ToString();
    bool details=Contents.Value&&loot&&distance<=LootDisplay.ContentRange.Value&&body.Pointer.ToInt64()==focused;
    string label="尸体 · "+name+$" [{distance:F0}米]";Color[] colors={new Color(0.75f,0.75f,0.85f)};
    if(details){
     if(cachedBody!=focused||Time.unscaledTime>=nextDetails){cachedBody=focused;nextDetails=Time.unscaledTime+1;cachedLabel=LootDisplay.CorpseLabel("尸体 · "+name,body.ItemOwner?.RootItem,distance,true,out cachedColors);}
     label=cachedLabel;colors=cachedColors;
    }
    Markers.Add(new(body,position,label,colors));
   }catch(Exception e){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10;Plugin.Self.Error(e);}}
  }
 }
}
