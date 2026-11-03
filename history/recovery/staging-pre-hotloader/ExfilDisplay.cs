using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using CommonAssets.Scripts.Game;
using EFT;
using EFT.Interactive;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ExfilDisplay {
 static ConfigEntry<bool> Enabled=null!,ShowSpecial=null!,Force=null!,CrossFaction=null!;
 static ConfigEntry<KeyCode> ForceKey=null!,CrossKey=null!;
 static ConfigEntry<int> Lines=null!;
 static readonly List<Row> rows=new();
 static readonly Dictionary<IntPtr,Original> originals=new();
 static readonly HashSet<IntPtr> seenPresent=new();
 static float nextScan,nextError;
 static IntPtr worldPointer;
 static bool lastForce,lastCross,hooksReady;
 static readonly HashSet<IntPtr> permissionOverrides=new();
 static string notice="";
 static bool layoutDirty=true;
 static GUIStyle? panelStyle;
 static int layoutWidth,layoutHeight,layoutFont,pageIndex,layoutLimit;
 static string nearestCaption="";
 static readonly List<(string Text,Color Color)> headers=new(),captions=new();
 static readonly List<float> headerHeights=new(),captionHeights=new();
 static List<PanelPages.Page> pages=new();
 static float headerHeight;
 static int transitRevision=-1;
 sealed record Original(ExfiltrationPoint Point,Il2CppStringArray Entries,EExfiltrationStatus Status,bool ChangedStatus,bool AddedScavId,string ProfileId,bool CrossFaction);
 sealed record Row(ExfiltrationPoint Point,string Name,string Kind,string State,bool Available,float Distance,Vector3 Position,bool Forced);
 internal static void Initialize(Plugin p){
  Enabled=CnConfig.Bind(p.Config,"14. Exfiltration","Enabled",true);
  ShowSpecial=CnConfig.Bind(p.Config,"14. Exfiltration","Show conditional exits",true);
  Lines=CnConfig.Bind(p.Config,"14. Exfiltration","Maximum exits",8,new ConfigDescription("",new AcceptableValueRange<int>(3,20)));
  Force=CnConfig.Bind(p.Config,"14. Exfiltration","Force unassigned exits",false);
  ForceKey=CnConfig.Bind(p.Config,"14. Exfiltration","Force toggle key",KeyCode.F10);
  CrossFaction=CnConfig.Bind(p.Config,"14. Exfiltration","Cross faction exits",false);
  CrossKey=CnConfig.Bind(p.Config,"14. Exfiltration","Cross faction toggle key",KeyCode.F11);
  try{
   foreach(var type in new[]{typeof(ExfiltrationPoint),typeof(ScavExfiltrationPoint),typeof(SharedExfiltrationPoint)}){
    var method=type.GetMethods().Single(m=>m.DeclaringType==type&&m.Name=="InfiltrationMatch");p.Patches.Patch(method,postfix:new HarmonyMethod(typeof(ExfilDisplay),nameof(MatchOverride)));
   }
   foreach(var method in typeof(ExfiltrationController).GetMethods().Where(m=>m.DeclaringType==typeof(ExfiltrationController)&&m.Name=="EligiblePoints")){
    var prefix=method.GetParameters()[0].ParameterType==typeof(Profile)?nameof(EligibleProfile):nameof(EligibleId);
    p.Patches.Patch(method,postfix:new HarmonyMethod(typeof(ExfilDisplay),prefix));
   }
   hooksReady=true;p.Log.LogInfo("Exfiltration permissions ready: local-only cross-faction and unassigned-exit hooks");
  }catch(Exception e){hooksReady=false;p.Log.LogError("Exfiltration permission hooks unavailable: "+e);}
 }
 internal static void Keys(){if(Hotkeys.Pressed(ForceKey.Value)){Force.Value=!Force.Value;nextScan=0;}if(Hotkeys.Pressed(CrossKey.Value)){CrossFaction.Value=!CrossFaction.Value;nextScan=0;}}
 internal static void Reset(){rows.Clear();originals.Clear();seenPresent.Clear();permissionOverrides.Clear();nextScan=0;worldPointer=IntPtr.Zero;notice="";lastForce=lastCross=false;layoutDirty=true;pageIndex=0;}
 static void MatchOverride(ExfiltrationPoint __instance,Player __0,ref bool __result){
  if(hooksReady&&(Force.Value||CrossFaction.Value)&&__0!=null&&__0.IsYourPlayer&&permissionOverrides.Contains(__instance.Pointer))__result=true;
 }
 static void EligibleProfile(Profile __0,ref Il2CppReferenceArray<ExfiltrationPoint> __result){var player=Singleton<GameWorld>.Instance?.MainPlayer;if(player!=null&&__0!=null&&__0.Id.ToString()==player.ProfileId)MergeEligible(ref __result);}
 static void EligibleId(string __0,ref Il2CppReferenceArray<ExfiltrationPoint> __result){var player=Singleton<GameWorld>.Instance?.MainPlayer;if(player!=null&&__0==player.ProfileId)MergeEligible(ref __result);}
 static void MergeEligible(ref Il2CppReferenceArray<ExfiltrationPoint> result){
  if(!hooksReady||(!Force.Value&&!CrossFaction.Value))return;
  var list=new List<ExfiltrationPoint>();var seen=new HashSet<IntPtr>();
  if(result!=null)foreach(var p in result)if(p!=null&&seen.Add(p.Pointer))list.Add(p);
  foreach(var original in originals.Values)if(original.Point!=null&&permissionOverrides.Contains(original.Point.Pointer)&&seen.Add(original.Point.Pointer))list.Add(original.Point);
  result=new Il2CppReferenceArray<ExfiltrationPoint>(list.ToArray());
 }
 static bool Money(ExfiltrationPoint point){
  var requirement=point.TransferItemRequirement;if(requirement==null)return false;
  return requirement.Id=="5449016a4bdc2d6f028b456f"||requirement.Id=="5696686a4bdc2da3298b456a"||requirement.Id=="569668774bdc2da2298b4568";
 }
 static bool Train(ExfiltrationPoint point){var reqs=point.Requirements;if(reqs!=null)foreach(var r in reqs)if(r!=null&&r.Requirement==ERequirementState.Train)return true;return false;}
 static bool Lever(ExfiltrationPoint point){
  if(point.Switch!=null)return true;
  var reqs=point.Requirements;if(reqs!=null)foreach(var r in reqs)if(r!=null&&(r.Requirement==ERequirementState.WorldEvent||r.Requirement==ERequirementState.Reference))return true;
  return false;
 }
 static void Restore(){
  foreach(var original in originals.Values){
   try{
    var point=original.Point;if(point==null)continue;
    point.EligibleEntryPoints=original.Entries;
    if(original.AddedScavId){var scav=point.TryCast<ScavExfiltrationPoint>();if(scav!=null&&scav.EligibleIds!=null)scav.EligibleIds.Remove(original.ProfileId);}
    // Do not interrupt an extraction countdown already started by normal gameplay.
    if(original.ChangedStatus&&point.Status==EExfiltrationStatus.RegularMode)point.ExternalSetStatus(original.Status,"SPT5 restore exit");
   }catch(Exception e){Warn(e);}
  }
  originals.Clear();permissionOverrides.Clear();
 }
 static void UnlockPoint(ExfiltrationPoint point,Player player,bool assigned){
  if(!hooksReady||point.TryCast<EFT.Interactive.SecretExfiltrations.SecretExfiltrationPoint>()!=null)return;
  var scav=point.TryCast<ScavExfiltrationPoint>();bool playerScav=player.Profile?.Side==EPlayerSide.Savage;
  bool cross=CrossFaction.Value&&(playerScav?scav==null:scav!=null);
  bool matchingFaction=playerScav?scav!=null:scav==null;
  bool force=Force.Value&&(cross||matchingFaction)&&!Money(point)&&!Train(point);
  if(!cross&&!force)return;
  if(assigned&&point.Status!=EExfiltrationStatus.NotPresent&&!cross)return;
  if(originals.ContainsKey(point.Pointer))return;
  var entries=point.EligibleEntryPoints;
  string entry=player.Profile?.Info?.EntryPoint??"";
  bool changed=force&&point.Status==EExfiltrationStatus.NotPresent;
  bool addScav=scav!=null&&scav.EligibleIds!=null&&!scav.EligibleIds.Contains(player.ProfileId);
  originals[point.Pointer]=new(point,entries,point.Status,changed,addScav,player.ProfileId,cross);
  permissionOverrides.Add(point.Pointer);
  if(addScav)scav!.EligibleIds!.Add(player.ProfileId);
  var expanded=new List<string>();if(entries!=null)foreach(var e in entries)expanded.Add(e);
  if(!string.IsNullOrEmpty(entry)&&!expanded.Contains(entry))expanded.Add(entry);point.EligibleEntryPoints=new Il2CppStringArray(expanded.ToArray());
  if(changed){point.ExternalSetStatus(EExfiltrationStatus.RegularMode,"SPT5 force unassigned exit");point.Enable();}
 }
 internal static void Tick(GameWorld? world,Player? player){
  if(world==null||player==null){if(worldPointer!=IntPtr.Zero)Reset();return;}
  if(worldPointer!=world.Pointer){Reset();worldPointer=world.Pointer;}
  if(Time.unscaledTime<nextScan&&lastForce==Force.Value&&lastCross==CrossFaction.Value)return;nextScan=Time.unscaledTime+1f;
  try{
   if((lastForce!=Force.Value||lastCross!=CrossFaction.Value)&&originals.Count>0)Restore();lastForce=Force.Value;lastCross=CrossFaction.Value;
   rows.Clear();layoutDirty=true;if(!Enabled.Value&&!Force.Value&&!CrossFaction.Value)return;
   var controller=ExfiltrationController.Instance;if(controller==null){notice="撤离点正在加载";return;}
   var assigned=new HashSet<IntPtr>();var eligible=controller.EligiblePoints(player.Profile);
   if(eligible!=null)foreach(var point in eligible)if(point!=null)assigned.Add(point.Pointer);
   var points=new Dictionary<IntPtr,ExfiltrationPoint>();
   var normal=controller.ExfiltrationPoints;if(normal!=null)foreach(var point in normal)if(point!=null)points[point.Pointer]=point;
   var scavPoints=controller.ScavExfiltrationPoints;if(scavPoints!=null)foreach(var point in scavPoints)if(point!=null)points[point.Pointer]=point;
   if(eligible!=null)foreach(var point in eligible)if(point!=null)points[point.Pointer]=point;
   bool banned=controller.BannedPlayers!=null&&controller.BannedPlayers.Contains(player.RaidId);notice="";
   foreach(var point in points.Values){
    try{
     bool isAssigned=assigned.Contains(point.Pointer);
     if(Force.Value||CrossFaction.Value)UnlockPoint(point,player,isAssigned);
     bool forced=originals.ContainsKey(point.Pointer);
     if(forced)isAssigned=point.InfiltrationMatch(player);
     string key=point.Settings?.Name??point.name;
     string name=LocalizationExtensions.Localized(key,EStringCase.None);if(string.IsNullOrWhiteSpace(name))name=key;
     bool car=Money(point),lever=Lever(point);string kind=ExfilPolicy.Kind(name+" "+key,car,lever);
     if(!ShowSpecial.Value&&!isAssigned&&kind!="普通")continue;
     bool met=true;var unmet=new List<string>();var requirements=point.Requirements;
     if(requirements!=null)foreach(var req in requirements){
      if(req==null||req.Met(player,point))continue;met=false;
      string tip=req.GetLocalizedTip(player.ProfileId);if(!string.IsNullOrWhiteSpace(tip))unmet.Add(tip.Replace('\n',' '));
     }
     bool queued=point.QueuedPlayers!=null&&point.QueuedPlayers.Contains(player.RaidId);
     var state=ExfilPolicy.Evaluate(point.Status.ToString(),isAssigned,met,banned,car,queued);
     string text=ExfilPolicy.Evaluate(point.Status.ToString(),true,met,banned,car,queued).Text;
     if(point.Status!=EExfiltrationStatus.NotPresent&&point.Status!=EExfiltrationStatus.Hidden)seenPresent.Add(point.Pointer);
     if(car&&point.Status==EExfiltrationStatus.NotPresent)text=seenPresent.Contains(point.Pointer)?"已离开":"未出现／已离开";
     if(kind=="绿烟")text=point.Status==EExfiltrationStatus.NotPresent?"未开启":(point.Status==EExfiltrationStatus.RegularMode?"已开启 · ":"")+text;
     if(lever&&point.Switch!=null)text+=" · 开关"+(point.Switch.DoorState==EDoorState.Open?"已开启":"未开启");
     if(unmet.Count>0)text+=" · "+string.Join(" / ",unmet.Take(2));
     if(car&&point.Status!=EExfiltrationStatus.NotPresent&&!met){
      var payment=point.TransferItemRequirement;int amount;
      if(payment!=null)text+=payment.CanTransferItems(player,out amount)?" · 可付款":" · 未付款／资金不足";
     }
     if(forced)text+=originals[point.Pointer].CrossFaction?" · 跨阵营":" · 强制";
     if(!isAssigned)text+=" · 未分配";
     var position=point.ExtendedCollider!=null?point.ExtendedCollider.bounds.center:point.transform.position;
     rows.Add(new(point,name,kind,text,state.Available,Vector3.Distance(player.Position,position),position,forced));
    }catch(Exception e){Warn(e);}
   }
   rows.Sort((a,b)=>a.Distance.CompareTo(b.Distance));
  }catch(Exception e){notice="撤离状态读取失败";Warn(e);}
 }
 internal static void Draw(Camera camera,Player player,GUIStyle style){
  bool transit=TransitDisplay.Enabled.Value&&TransitDisplay.Rows.Count>0;
  if(!Enabled.Value&&!transit)return;
  var nearest=Enabled.Value?rows.FirstOrDefault(r=>r.Available):null;
  string heading=!Enabled.Value?"":nearest!=null?$"最近：{nearest.Name} · {nearest.Distance:F0}米 · {Direction(camera,nearest.Position-player.Position)}":"最近："+(string.IsNullOrEmpty(notice)?"无可用撤离点":notice);
  int width=(int)Math.Min(680,Screen.width-24);float line=Math.Max(21,style.fontSize+5);
  if(panelStyle==null){panelStyle=new GUIStyle(style);panelStyle.alignment=TextAnchor.UpperLeft;panelStyle.wordWrap=true;panelStyle.richText=false;}
  if(layoutDirty||transitRevision!=TransitDisplay.Revision||layoutWidth!=width||layoutHeight!=Screen.height||layoutFont!=style.fontSize||layoutLimit!=Lines.Value||nearestCaption!=heading){
   transitRevision=TransitDisplay.Revision;
   layoutDirty=false;layoutWidth=width;layoutHeight=Screen.height;layoutFont=style.fontSize;layoutLimit=Lines.Value;nearestCaption=heading;panelStyle.fontSize=style.fontSize;
   headers.Clear();captions.Clear();headerHeights.Clear();captionHeights.Clear();headerHeight=0;
   if(!string.IsNullOrEmpty(heading))headers.Add((heading,nearest!=null?Color.green:Color.yellow));
   headers.Add(($"强制：{CnConfig.OnOff(Force.Value)} · 跨阵营：{CnConfig.OnOff(CrossFaction.Value)}",Color.cyan));
   if(Enabled.Value)foreach(var row in rows.OrderBy(r=>r.Available?0:r.Kind!="普通"?1:2).ThenBy(r=>r.Distance))captions.Add(($"[{row.Kind}] {row.Name} {row.Distance:F0}米 · {row.State}",row.Available?Color.green:new Color(1,0.8f,0.3f)));
   if(transit){var closest=TransitDisplay.Rows.FirstOrDefault(r=>r.Available);if(closest.Available)headers.Add(($"转移：{closest.Destination} · {closest.Distance:F0}米 · {Direction(camera,closest.Position-player.Position)}",Color.cyan));foreach(var row in TransitDisplay.Rows)captions.Add(($"[转移] {row.Destination} {row.Distance:F0}米 · {row.State}",row.Available?Color.cyan:Color.gray));}
   foreach(var text in headers){float height=Math.Max(line,panelStyle.CalcHeight(new GUIContent(text.Text),width-16)+4);headerHeights.Add(height);headerHeight+=height;}
   foreach(var text in captions)captionHeights.Add(Math.Max(line,panelStyle.CalcHeight(new GUIContent(text.Text),width-16)+4));
   pages=PanelPages.Build(captionHeights,Lines.Value,Math.Max(line,Screen.height-122-headerHeight-line-12));
  }
  if(Hotkeys.Pressed(KeyCode.PageDown))pageIndex++;
  if(Hotkeys.Pressed(KeyCode.PageUp))pageIndex--;
  pageIndex=Math.Clamp(pageIndex,0,pages.Count-1);var page=pages[pageIndex];
  float panelHeight=headerHeight+line+page.Height+12,x=Screen.width-width-12,y=Math.Max(110,Screen.height-panelHeight-12);
  var oldColor=GUI.color;
  try{
   GUI.color=new Color(0,0,0,0.65f);GUI.DrawTexture(new Rect(x,y,width,panelHeight),Texture2D.whiteTexture);float top=y+6;
   for(int i=0;i<headers.Count;i++){GUI.color=headers[i].Color;GUI.Label(new Rect(x+8,top,width-16,headerHeights[i]),headers[i].Text,panelStyle);top+=headerHeights[i];}
   GUI.color=Color.cyan;GUI.Label(new Rect(x+8,top,width-16,line),$"撤离／转移 {pageIndex+1}/{pages.Count}",panelStyle);top+=line;
   for(int i=page.Start;i<page.Start+page.Count;i++){GUI.color=captions[i].Color;float height=Math.Min(captionHeights[i],Screen.height-top-18);GUI.Label(new Rect(x+8,top,width-16,height),captions[i].Text,panelStyle);top+=captionHeights[i];}
   if(nearest!=null){var point=camera.WorldToScreenPoint(nearest.Position);if(point.z>0&&point.x>=0&&point.x<=Screen.width&&point.y>=0&&point.y<=Screen.height){GUI.color=Color.green;GUI.Label(new Rect(point.x-180,Screen.height-point.y-22,360,28),$"撤离 → {nearest.Name} [{nearest.Distance:F0}米]",style);}}
  }finally{GUI.color=oldColor;}
 }
 static string Direction(Camera camera,Vector3 delta){
  float angle=Vector3.SignedAngle(new Vector3(camera.transform.forward.x,0,camera.transform.forward.z),new Vector3(delta.x,0,delta.z),Vector3.up);
  if(Math.Abs(angle)<22.5f)return "前方";if(Math.Abs(angle)>157.5f)return "后方";
  if(angle>0)return angle<67.5f?"右前方":angle<112.5f?"右侧":"右后方";
  return angle>-67.5f?"左前方":angle>-112.5f?"左侧":"左后方";
 }
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+10;Plugin.Self.Error(e);}
}
