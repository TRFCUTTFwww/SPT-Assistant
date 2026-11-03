using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.Interactive;
using HarmonyLib;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LocalSpt5Esp;

internal class Plugin {
 readonly LocalSpt5Esp.HotReload.IHostServices host;
 internal Plugin(LocalSpt5Esp.HotReload.IHostServices host){this.host=host;}
 internal ConfigFile Config=>host.Config;
 internal BepInEx.Logging.ManualLogSource Log=>host.Log;
 internal static Plugin Self=null!;
 internal ConfigEntry<bool> People=null!,Loot=null!,God=null!,Containers=null!,ShowHealth=null!;
 internal ConfigEntry<bool> IgnoreAI=null!;
 internal ConfigEntry<float> PeopleRange=null!,LootRange=null!;
 internal ConfigEntry<int> MaxLoot=null!,FontSize=null!;
 internal ConfigEntry<KeyCode> PeopleKey=null!,LootKey=null!,GodKey=null!;
 internal ConfigEntry<KeyCode> IgnoreAIKey=null!;
 internal Harmony Patches=null!;
 public void Load(){
  Self=this;
  People=CnConfig.Bind(Config,"1. ESP","People",true,"Show living characters through walls. F6 toggles.");
  ShowHealth=CnConfig.Bind(Config,"1. ESP","Show health",true,"Show current / maximum total body health and a colored health bar above each character.");
  Loot=CnConfig.Bind(Config,"1. ESP","Items",true,"Show ground loot and corpses through walls. F7 toggles.");
  Containers=CnConfig.Bind(Config,"1. ESP","Containers",true,"Include searchable containers.");
  PeopleRange=CnConfig.Bind(Config,"1. ESP","People range",500f,new ConfigDescription("Meters",new AcceptableValueRange<float>(25,2000)));
  LootRange=CnConfig.Bind(Config,"1. ESP","Loot range",100f,new ConfigDescription("Maximum loot and container label distance in meters; applies to ground items, corpses and containers. Contents distance separately controls detailed container contents.",new AcceptableValueRange<float>(5,1000)));
  MaxLoot=CnConfig.Bind(Config,"1. ESP","Maximum loot labels",150,new ConfigDescription("Closest labels are shown first",new AcceptableValueRange<int>(10,500)));
  FontSize=CnConfig.Bind(Config,"1. ESP","Font size",14,new ConfigDescription("Label font size",new AcceptableValueRange<int>(10,24)));
  God=CnConfig.Bind(Config,"2. God mode","Enabled",true,"Block damage and death for the local player only. F8 toggles. This does not prevent the raid timer from ending.");
  PeopleKey=CnConfig.Bind(Config,"3. Keys","People toggle",KeyCode.F6);
  LootKey=CnConfig.Bind(Config,"3. Keys","Items toggle",KeyCode.F7);
  GodKey=CnConfig.Bind(Config,"3. Keys","God mode toggle",KeyCode.F8);
  IgnoreAI=CnConfig.Bind(Config,"4. AI ignore","Enabled",true,"AI ignores the local player: suppresses hostility, visual targeting and existing enemy memory. F9 toggles. Does not hide your model from human players.");
  IgnoreAIKey=CnConfig.Bind(Config,"3. Keys","AI ignore toggle",KeyCode.F9);
  Patches=new Harmony("local.spt5.esp");
  Patch("ApplyDamage",nameof(BlockDamage));
  Patch("Kill",nameof(BlockDeath));
  PatchAI(typeof(BotsGroup),"IsEnemy",nameof(IgnoreTarget));
  PatchAI(typeof(BotEnemiesController),"IsEnemy",nameof(IgnoreTarget));
  PatchAI(typeof(BotMemory),"AddEnemyInfo",nameof(SkipTarget));
  PatchAI(typeof(BotMemory),"SetUnderFire",nameof(SkipTarget));
  PatchAI(typeof(BotsGroup),"ReportAboutEnemy",nameof(SkipTarget));
  // The getter shares its native IL2CPP body with unrelated methods.
  // Decision and vision hooks below still suppress local-player targeting.
  PatchAI(typeof(EnemyInfo),"ShallKnowEnemy",nameof(IgnoreEnemyInfo));
  PatchAI(typeof(EnemyInfo),"CheckLookEnemy",nameof(SkipVision));
  LootDisplay.Initialize(this);BodyTint.Initialize(this);CombatAssist.Initialize(this);RecoilAssist.Initialize(this);HitDisplay.Initialize(this);CorpseDisplay.Initialize(this);RaidPopulation.Initialize(this);ExfilDisplay.Initialize(this);TransitDisplay.Initialize(this);BtrDisplay.Initialize(this);NightVisionAssist.Initialize(this);DoorAssist.Initialize(this);VitalsDisplay.Initialize(this);MovementAssist.Initialize(this);MagazineDisplay.Initialize(this);ArmorAnalysis.Initialize(this);AggroDisplay.Initialize(this);

  Log.LogInfo($"SPT5 ESP ready v1.7.0. Health numbers + bars: {ShowHealth.Value}. AI ignore: {IgnoreAI.Value}. {PeopleKey.Value}: people; {LootKey.Value}: items; {GodKey.Value}: god mode; {IgnoreAIKey.Value}: AI ignore. RightShift: main menu; F12: settings.");
 }
 void Patch(string target,string prefix){
  try {var methods=typeof(ActiveHealthController).GetMethods().Where(x=>x.DeclaringType==typeof(ActiveHealthController)&&x.Name==target).ToArray();
   if(methods.Length!=1)throw new InvalidOperationException("Expected one "+target+" method, found "+methods.Length);
   Patches.Patch(methods[0],prefix:new HarmonyMethod(typeof(Plugin),prefix));Log.LogInfo("Local player protection patched: "+methods[0]);
  }catch(Exception e){Log.LogError("Protection patch failed: "+target+" "+e);}
 }
 static bool Protected(ActiveHealthController h){try {return Self.God.Value&&h!=null&&h.Player!=null&&h.Player.IsYourPlayer;}catch{return false;}}
 static bool BlockDamage(ActiveHealthController __instance,ref float __result){if(!Protected(__instance))return true;__result=0;return false;}
 static bool BlockDeath(ActiveHealthController __instance)=>!Protected(__instance);
 void PatchAI(Type type,string method,string prefix){
  try{
   var target=type.GetMethod(method)??throw new MissingMethodException(type.FullName,method);
   Patches.Patch(target,prefix:new HarmonyMethod(typeof(Plugin),prefix));
   Log.LogInfo("AI ignore patched: "+type.Name+"."+method);
  }catch(Exception e){Log.LogError("AI ignore patch failed: "+type.Name+"."+method+" "+e);}
 }
 internal static bool Hidden(IPlayer player){
  try{
   if(!Self.IgnoreAI.Value||player==null)return false;
   var world=Singleton<GameWorld>.Instance;var local=world!=null?world.MainPlayer:null;
   return local!=null&&local.IsYourPlayer&&player.ProfileId==local.ProfileId;
  }catch{return false;}
 }
 static bool IgnoreTarget(IPlayer __0,ref bool __result){if(!Hidden(__0))return true;__result=false;return false;}
 static bool SkipTarget(IPlayer __0)=>!Hidden(__0);
 static bool IgnoreEnemyInfo(EnemyInfo __instance,ref bool __result){if(__instance==null||!Hidden(__instance.Person))return true;__result=false;return false;}
 static bool SkipVision(EnemyInfo __instance){
  if(__instance==null||!Hidden(__instance.Person))return true;
  __instance.SetVisible(false);__instance.SetCanShoot(false);return false;
 }
 internal void Error(Exception e)=>Log.LogWarning(e.ToString());
}
internal class Overlay {

 readonly List<Player> players=new();
 readonly List<Marker> items=new();
 readonly Dictionary<string,string> names=new();
 readonly Dictionary<IntPtr,string> actorLabels=new();
 readonly Dictionary<IntPtr,(float Current,float Maximum,float Updated)> healthCache=new();
 GameWorld? world;Player? me;Camera? camera;
 float nextScan,nextContainers,nextError,nextStatus;
 LootableContainer[] containers=Array.Empty<LootableContainer>();
 readonly Dictionary<IntPtr,(LootableContainer Container,Vector3 Position)> containerAnchors=new();
 int nextAnchor;
 float nextAnchorRetry;
 GUIStyle? style;int lastFont;bool inRaid;
 readonly LabelLayout lootLayout=new();
 bool wasIgnoringAI;
 readonly FocusSelection focus=new();
 long detailKey;int detailOffset;
 record Marker(Vector3 Position,string Name,Color Color,float Distance,Color[]? LineColors=null,long Key=0,int Kind=0);
 public void Update(){try{
  var s=Plugin.Self;
  CombatAssist.Keys();RecoilAssist.Keys();NightVisionAssist.Keys();ExfilDisplay.Keys();
  if(Hotkeys.Pressed(s.PeopleKey.Value))s.People.Value=!s.People.Value;
  if(Hotkeys.Pressed(s.LootKey.Value))s.Loot.Value=!s.Loot.Value;
  if(Hotkeys.Pressed(s.GodKey.Value)){s.God.Value=!s.God.Value;nextScan=0;}
  if(Hotkeys.Pressed(s.IgnoreAIKey.Value)){s.IgnoreAI.Value=!s.IgnoreAI.Value;nextScan=0;Plugin.Self.Log.LogInfo("AI ignore toggled: "+s.IgnoreAI.Value);}
  if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+0.5f;
  var w=Singleton<GameWorld>.Instance;
  var p=w!=null?w.MainPlayer:null;
  if(w==null||p==null||!p.IsYourPlayer){BodyTint.Clear();LootDisplay.Reset();CombatAssist.Reset();HitDisplay.Reset();RecoilAssist.Reset();NightVisionAssist.Reset();DoorAssist.Reset();CorpseDisplay.Reset();RaidPopulation.Reset();ExfilDisplay.Reset();TransitDisplay.Reset();VitalsDisplay.Reset();MovementAssist.Reset();MagazineDisplay.Reset();ArmorAnalysis.Reset();AggroDisplay.Reset();focus.Clear();world=null;me=null;players.Clear();actorLabels.Clear();items.Clear();healthCache.Clear();containers=Array.Empty<LootableContainer>();containerAnchors.Clear();nextAnchor=0;inRaid=false;wasIgnoringAI=false;return;}
  if(world==null||world.Pointer!=w.Pointer){BodyTint.Clear();LootDisplay.Reset();CombatAssist.Reset();HitDisplay.Reset();RecoilAssist.Reset();NightVisionAssist.Reset();DoorAssist.Reset();CorpseDisplay.Reset();RaidPopulation.Reset();ExfilDisplay.Reset();TransitDisplay.Reset();VitalsDisplay.Reset();MovementAssist.Reset();MagazineDisplay.Reset();ArmorAnalysis.Reset();AggroDisplay.Reset();focus.Clear();names.Clear();actorLabels.Clear();healthCache.Clear();containers=Array.Empty<LootableContainer>();containerAnchors.Clear();nextAnchor=0;nextContainers=0;wasIgnoringAI=false;}
  world=w;me=p;camera=Camera.main;DoorAssist.Tick(p);
  BodyTint.BeginScan();
  RaidPopulation.Update(w,p);
  if(s.IgnoreAI.Value){ClearLocalEnemyMemory(w,p);wasIgnoringAI=true;}
  else if(wasIgnoringAI){RestoreLocalEnemyMemory(w,p);wasIgnoringAI=false;}
  if(!inRaid){inRaid=true;Plugin.Self.Log.LogInfo("ESP raid active; local="+p.ProfileId+" god="+s.God.Value);}
  players.Clear();actorLabels.Clear();
  if(s.People.Value){var all=w.AllAlivePlayersList;if(all!=null)for(int i=0;i<all.Count;i++){var q=all[i];if(q!=null&&!q.IsYourPlayer&&q.HealthController!=null&&q.HealthController.IsAlive){players.Add(q);names[q.ProfileId]=BodyTint.KindName(q)+" · "+(q.Profile?.Nickname??q.ProfileId);actorLabels[q.Pointer]=names[q.ProfileId];}}}
  BodyTint.Update(players,p,s.People.Value,s.PeopleRange.Value);
  CorpseDisplay.Scan(w,p.Position,names,s.People.Value,s.Loot.Value);
  items.Clear();
  if(s.Loot.Value){
   var origin=p.Position;var range=s.LootRange.Value;var loot=w.LootItems;
   var nearby=new List<(Vector3 Position,EFT.InventoryLogic.Item Item,float Distance)>();
   if(loot!=null)for(int i=0;i<loot.Count;i++){try{var l=loot.GetByIndex(i);if(l==null||l.Item==null||l.TryCast<Corpse>()!=null)continue;var pos=l.transform.position;var d=Vector3.Distance(pos,origin);if(d>range)continue;
    nearby.Add((pos,l.Item,d));
   }catch(Exception e){Warn(e);}}
   foreach(var entry in nearby.OrderBy(x=>x.Distance).Take(s.MaxLoot.Value)){
    try{string label=LootDisplay.LooseLabel(entry.Item,entry.Distance,out var color);items.Add(new(entry.Position,label,color,entry.Distance));}catch(Exception e){Warn(e);}
   }
   if(s.Containers.Value){
    if(Time.unscaledTime>=nextContainers){nextContainers=Time.unscaledTime+30;containers=Object.FindObjectsOfType<LootableContainer>().ToArray();nextAnchor=0;}
    BuildContainers(origin,range);
   }
   items.Sort((a,b)=>a.Distance.CompareTo(b.Distance));if(items.Count>s.MaxLoot.Value)items.RemoveRange(s.MaxLoot.Value,items.Count-s.MaxLoot.Value);
  }
  if(Time.unscaledTime>=nextStatus){nextStatus=Time.unscaledTime+30;Plugin.Self.Log.LogInfo($"ESP status: people={players.Count} loot={items.Count} god={s.God.Value}; {CombatAssist.Diagnostics}");}
 }catch(Exception e){Warn(e);}}
 public void LateUpdate(){IndexContainers();RecoilAssist.Tick(me);NightVisionAssist.Tick(me);CombatAssist.Tick(world,me,camera);HitDisplay.Tick(world);ExfilDisplay.Tick(world,me);TransitDisplay.Tick(world,me);BtrDisplay.Tick(world);MovementAssist.Tick(me);}
 public void OnDestroy(){BodyTint.Dispose();LootDisplay.Reset();CombatAssist.Reset();HitDisplay.Reset();RecoilAssist.Reset();NightVisionAssist.Reset();DoorAssist.Reset();CorpseDisplay.Reset();RaidPopulation.Reset();VitalsDisplay.Reset();MovementAssist.Reset();MagazineDisplay.Reset();ArmorAnalysis.Reset();AggroDisplay.Reset();ExfilDisplay.Shutdown();TransitDisplay.Reset();BtrDisplay.Reset();focus.Clear();}
 void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Error(e);}
 public void OnGUI(){try{
  if(world==null||me==null||camera==null||Event.current.type!=EventType.Repaint)return;
  var s=Plugin.Self;
  if(style==null||lastFont!=s.FontSize.Value){lastFont=s.FontSize.Value;style=new GUIStyle(GUI.skin.label);style.fontSize=lastFont;style.alignment=TextAnchor.MiddleCenter;style.richText=false;}
  Text(new Rect(15,8,Screen.width-30,24),$"人物：{CnConfig.OnOff(s.People.Value)} · 物品：{CnConfig.OnOff(s.Loot.Value)} · 无敌：{CnConfig.OnOff(s.God.Value)} · 敌人忽略：{CnConfig.OnOff(s.IgnoreAI.Value)}",s.God.Value?Color.green:Color.white);
  Text(new Rect(15,32,Screen.width-30,24),CombatAssist.Status+"   "+RecoilAssist.Status+"   "+NightVisionAssist.Status,Color.cyan);
  if(s.People.Value)foreach(var p in players){try{
   if(p==null||p.HealthController==null||!p.HealthController.IsAlive)continue;
   var pos=p.Position;float distance=Vector3.Distance(pos,me.Position);if(distance>s.PeopleRange.Value)continue;
   var feet=camera.WorldToScreenPoint(pos);var head=camera.WorldToScreenPoint(pos+Vector3.up*1.8f);if(feet.z<=0||head.z<=0||head.x<0||head.x>Screen.width)continue;
   float h=Math.Abs(head.y-feet.y);float width=h*0.45f;float top=Screen.height-head.y;
   Color color=BodyTint.Color(p);
   Box(new Rect(head.x-width/2,top,width,h),color);
   (float Current,float Maximum,float Updated) health=default;
   bool hasHealth=s.ShowHealth.Value&&TryGetHealth(p,out health);
   Text(new Rect(head.x-180,top-(hasHealth?50:26),360,24),$"{(actorLabels.TryGetValue(p.Pointer,out var label)?label:"人物")} [{distance:F0}米]",color);
   if(hasHealth){
    float ratio=Math.Clamp(health.Current/health.Maximum,0f,1f);
    Color healthColor=ratio>0.66f?new Color(0.25f,1f,0.3f):ratio>0.33f?new Color(1f,0.85f,0.15f):new Color(1f,0.2f,0.15f);
    Text(new Rect(head.x-120,top-28,240,24),$"HP {Math.Ceiling(health.Current):F0} / {Math.Ceiling(health.Maximum):F0}",healthColor);
    float barWidth=Math.Clamp(width,60f,120f);
    GUI.color=new Color(0f,0f,0f,0.85f);GUI.DrawTexture(new Rect(head.x-barWidth/2-1,top-5,barWidth+2,5),Texture2D.whiteTexture);
    GUI.color=healthColor;GUI.DrawTexture(new Rect(head.x-barWidth/2,top-4,barWidth*ratio,3),Texture2D.whiteTexture);
   }
  }catch(Exception e){Warn(e);}}
  if(s.Loot.Value||s.People.Value)DrawLoot();
  HitDisplay.Draw();
  RaidPopulation.Draw(style);
  VitalsDisplay.Draw(me,style);
  MagazineDisplay.Draw(me,style);
  ArmorAnalysis.Draw(world,me,camera,style);
  AggroDisplay.Draw(world,me,style);
  TransitDisplay.Draw(camera,style);
  ExfilDisplay.Draw(camera,me,style);
 }catch(Exception e){Warn(e);}finally{GUI.color=Color.white;}}
 void DrawLoot(){
  float opacity=LootDisplay.Opacity.Value/100f;
  if(opacity<=0)return;
  lootLayout.Clear();int hidden=0;
  float lineHeight=lastFont+5;
  var oldAlignment=style!.alignment;
  var oldWrap=style.wordWrap;var oldClipping=style.clipping;
  try{
   style.alignment=TextAnchor.MiddleLeft;style.wordWrap=false;style.clipping=TextClipping.Clip;
   foreach(var m in items.Where(m=>m.Kind==0&&Plugin.Self.Loot.Value)){
    if(Vector3.Distance(m.Position,me!.Position)>Plugin.Self.LootRange.Value)continue;
    var screen=camera!.WorldToScreenPoint(m.Position);
    if(screen.z<=0||screen.x<0||screen.x>Screen.width||screen.y<0||screen.y>Screen.height)continue;
    var lines=m.Name.Split('\n');
    float width=80;
    foreach(var line in lines)width=Math.Max(width,style.CalcSize(new GUIContent(line)).x+16);
    width=Math.Min(width,Screen.width-12);
    float height=lines.Length*lineHeight+8;
    float anchorY=Screen.height-screen.y;
    if(!lootLayout.Place(screen.x,anchorY-10,width,height,Screen.width,Screen.height,out var placed)){hidden++;continue;}
    var rect=new Rect(placed.X,placed.Y,placed.Width,placed.Height);
    var anchor=new Vector2(screen.x,anchorY);
    var end=new Vector2(Math.Clamp(anchor.x,rect.xMin,rect.xMax),Math.Clamp(anchor.y,rect.yMin,rect.yMax));
    DrawLootLine(anchor,end,LootColor(m.Color,opacity));
    GUI.color=new Color(0,0,0,0.72f*opacity);GUI.DrawTexture(rect,Texture2D.whiteTexture);
    Box(rect,new Color(m.Color.r,m.Color.g,m.Color.b,0.55f*opacity));
    for(int i=0;i<lines.Length;i++)Text(new Rect(rect.x+8,rect.y+4+i*lineHeight,rect.width-16,lineHeight),lines[i],LootColor(m.LineColors!=null&&i<m.LineColors.Length?m.LineColors[i]:m.Color,opacity));
   }
  }finally{style.alignment=oldAlignment;style.wordWrap=oldWrap;style.clipping=oldClipping;}
  DrawFixedMarkers(opacity);
  if(hidden>0)Text(new Rect(15,Screen.height-30,600,24),$"隐藏标签：{hidden}",LootColor(Color.yellow,opacity));
 }
 void IndexContainers(){
  if(world==null||!Plugin.Self.Loot.Value||!Plugin.Self.Containers.Value)return;
  if(nextAnchor>=containers.Length){if(Time.unscaledTime<nextAnchorRetry)return;nextAnchor=0;nextAnchorRetry=Time.unscaledTime+2;}
  int budget=32;
  while(nextAnchor<containers.Length&&budget-->0){
   var container=containers[nextAnchor++];if(container==null||containerAnchors.ContainsKey(container.Pointer)||!container.IsInitialized||container.ItemOwner==null)continue;
   try{containerAnchors[container.Pointer]=(container,ContainerAnchor.Position(container));}catch(Exception e){Warn(e);}
  }
 }
 void BuildContainers(Vector3 origin,float range){
  var nearby=new List<(LootableContainer Container,Vector3 Position,float Distance)>();
  foreach(var cached in containerAnchors.Values){
   var container=cached.Container;var position=cached.Position;float distance=Vector3.Distance(position,origin);
   if(distance<=range)nearby.Add((container,position,distance));
  }
  foreach(var entry in nearby.OrderBy(e=>e.Distance).Take(LootDisplay.ContainerLimit.Value)){
   var container=entry.Container;
   try{
    if(container==null){containerAnchors.Remove(entry.Container.Pointer);continue;}
    var position=ContainerAnchor.Position(container);containerAnchors[container.Pointer]=(container,position);
    string label=LootDisplay.ContainerLabel(container,Vector3.Distance(position,origin),out var colors);
    items.Add(new(position,label,colors[0],Vector3.Distance(position,origin),colors,container.Pointer.ToInt64(),1));
   }catch(Exception e){Warn(e);}
  }
 }
 void DrawFixedMarkers(float opacity){
  var markers=new List<Marker>();
  if(Plugin.Self.Loot.Value)markers.AddRange(items.Where(m=>m.Kind==1));
  if(Plugin.Self.People.Value&&CorpseDisplay.Enabled.Value)foreach(var body in CorpseDisplay.Markers){
   if(body.Body==null)continue;
   var position=CorpseDisplay.Position(body.Body);float distance=Vector3.Distance(position,me!.Position);
   if(distance<=CorpseDisplay.Range.Value)markers.Add(new(position,body.Label,body.Colors[0],distance,body.Colors,body.Body.Pointer.ToInt64(),2));
  }
  var projected=new List<(Marker Marker,Vector2 Screen)>();
  var candidates=new List<FocusSelection.Candidate>();
  foreach(var marker in markers){
   if(marker.Kind==1&&Vector3.Distance(marker.Position,me!.Position)>Plugin.Self.LootRange.Value)continue;
   var point=camera!.WorldToScreenPoint(marker.Position);
   if(point.z<=0||point.x<0||point.x>Screen.width||point.y<0||point.y>Screen.height)continue;
   var screen=new Vector2(point.x,Screen.height-point.y);projected.Add((marker,screen));candidates.Add(new(marker.Key,screen.x,screen.y));
  }
  long selected=focus.Choose(candidates,Screen.width/2f,Screen.height/2f,Time.unscaledTime);
  CorpseDisplay.Focus(selected);
  var oldAlignment=style!.alignment;style.alignment=TextAnchor.MiddleLeft;
  try{
   var occupied=new List<Rect>();
   foreach(var entry in projected.OrderBy(e=>e.Marker.Key==selected?0:1).ThenBy(e=>e.Marker.Key)){
    var m=entry.Marker;var point=entry.Screen;var color=LootColor(m.Color,opacity);
    Box(new Rect(point.x-4,point.y-4,8,8),color);
    string title=m.Name.Split('\n')[0];
    
    float width=Math.Min(520,style.CalcSize(new GUIContent(title)).x+12);
    var rect=new Rect(point.x+10,point.y-lastFont/2f,width,lastFont+8);
    // Keep the anchor exact. Suppress crowded titles instead of moving their positions.
    if(rect.xMax>Screen.width||rect.y<110||rect.yMax>Screen.height-20||occupied.Any(r=>r.Overlaps(rect)))continue;
    occupied.Add(rect);GUI.color=new Color(0,0,0,0.6f*opacity);GUI.DrawTexture(rect,Texture2D.whiteTexture);Text(new Rect(rect.x+4,rect.y,rect.width-8,rect.height),title,color);
   }
   var focused=projected.FirstOrDefault(e=>e.Marker.Key==selected);
   if(focused.Marker!=null)DrawDetailPanel(focused.Marker,opacity);
  }finally{style.alignment=oldAlignment;}
 }
 void DrawDetailPanel(Marker marker,float opacity){
  var lines=marker.Name.Split('\n');float lineHeight=lastFont+6;
  float width=Math.Min(580,Screen.width-24),x=12,y=Math.Max(110,Screen.height*0.25f);
  int limit=Math.Max(1,(int)((Screen.height-y-35)/lineHeight)-1);
  if(detailKey!=marker.Key){detailKey=marker.Key;detailOffset=0;}
  int maxOffset=Math.Max(0,lines.Length-limit);
  bool ctrl=Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl);
  if(!ctrl&&Input.GetKeyDown(KeyCode.PageDown))detailOffset+=Math.Max(1,limit-1);
  if(!ctrl&&Input.GetKeyDown(KeyCode.PageUp))detailOffset-=Math.Max(1,limit-1);
  detailOffset=Math.Clamp(detailOffset,0,maxOffset);
  int count=Math.Min(lines.Length-detailOffset,limit);
  var rect=new Rect(x,y,width,(count+1)*lineHeight+12);
  GUI.color=new Color(0,0,0,0.8f*opacity);GUI.DrawTexture(rect,Texture2D.whiteTexture);Box(rect,LootColor(marker.Color,opacity));
  Text(new Rect(x+8,y+4,width-16,lineHeight),$"物品 {detailOffset+1}–{detailOffset+count}/{lines.Length}",LootColor(Color.white,opacity));
  for(int i=0;i<count;i++){
   int index=i+detailOffset;string text=lines[index];
   var color=marker.LineColors!=null&&index<marker.LineColors.Length?marker.LineColors[index]:marker.Color;
   Text(new Rect(x+8,y+4+(i+1)*lineHeight,width-16,lineHeight),text,LootColor(color,opacity));
  }
 }
 static void DrawLootLine(Vector2 from,Vector2 to,Color color){
  var delta=to-from;float length=delta.magnitude;
  GUI.color=color;GUI.DrawTexture(new Rect(from.x-2,from.y-2,4,4),Texture2D.whiteTexture);
  if(length<4)return;
  var matrix=GUI.matrix;
  try{GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,from);GUI.DrawTexture(new Rect(from.x,from.y,length,1),Texture2D.whiteTexture);}
  finally{GUI.matrix=matrix;}
 }
 static Color LootColor(Color color,float opacity)=>new(color.r,color.g,color.b,color.a*opacity);
 void ClearLocalEnemyMemory(GameWorld world,Player local){
  var all=world.AllAlivePlayersList;if(all==null)return;
  IPlayer target=local;
  for(int i=0;i<all.Count;i++){
   try{
    var bot=all[i];if(bot==null||bot.IsYourPlayer)continue;
    var owner=bot.AIData?.BotOwner;if(owner==null)continue;
    ForgetLocalTarget(owner,target);
    // Preserve group hostility. Deleting it makes invisibility persist after the toggle is off.
   }catch(Exception e){Warn(e);}
  }
 }
 void RestoreLocalEnemyMemory(GameWorld world,Player local){
  var all=world.AllAlivePlayersList;if(all==null)return;
  IPlayer target=local;int restored=0;
  for(int i=0;i<all.Count;i++){
   try{
    var bot=all[i];if(bot==null||bot.IsYourPlayer)continue;
    var owner=bot.AIData?.BotOwner;var group=owner?.BotsGroup;var memory=owner?.Memory;
    if(group==null||memory==null||!group.Enemies.ContainsKey(target))continue;
    var info=group.Enemies[target];
    // Rebuild only the local player's per-bot entry so suppressed vision/shoot flags do not persist.
    ForgetLocalTarget(owner!,target);
    memory.AddEnemyInfo(target,info,false);
    restored++;
   }catch(Exception e){Warn(e);}
  }
  Plugin.Self.Log.LogInfo("AI ignore disabled: restored local-player enemy memory for "+restored+" bot(s)");
 }
 static void ForgetLocalTarget(BotOwner owner,IPlayer target){
  var memory=owner.Memory;if(memory==null)return;
  var goal=memory._goalEnemy;
  if(goal!=null&&goal.Person!=null&&goal.Person.ProfileId==target.ProfileId){
   goal.SetVisible(false);goal.SetCanShoot(false);memory.GoalEnemy=null;
  }
  var last=memory.LastEnemy;
  if(last!=null&&last.Person!=null&&last.Person.ProfileId==target.ProfileId)memory.LastEnemy=null;
  var enemies=owner.EnemiesController;
  if(enemies!=null&&enemies.EnemyInfos.ContainsKey(target))enemies.Remove(target);
 }
 bool TryGetHealth(Player p,out (float Current,float Maximum,float Updated) health){
  health=default;
  try{
   if(healthCache.TryGetValue(p.Pointer,out health)&&Time.unscaledTime-health.Updated<0.1f)return health.Maximum>0;
   var value=p.HealthController.GetBodyPartHealth(EBodyPart.Common,false);
   if(!float.IsFinite(value.Current)||!float.IsFinite(value.Maximum)||value.Maximum<=0)return false;
   health=(Math.Clamp(value.Current,0f,value.Maximum),value.Maximum,Time.unscaledTime);
   healthCache[p.Pointer]=health;return true;
  }catch(Exception e){Warn(e);return false;}
 }
 void Text(Rect r,string text,Color c){GUI.color=new Color(0,0,0,c.a);GUI.Label(new Rect(r.x+1,r.y+1,r.width,r.height),text,style);GUI.color=c;GUI.Label(r,text,style);}
 static void Box(Rect r,Color c){GUI.color=c;var t=Texture2D.whiteTexture;GUI.DrawTexture(new Rect(r.x,r.y,r.width,1),t);GUI.DrawTexture(new Rect(r.x,r.y+r.height,r.width,1),t);GUI.DrawTexture(new Rect(r.x,r.y,1,r.height),t);GUI.DrawTexture(new Rect(r.x+r.width,r.y,1,r.height),t);}
}




