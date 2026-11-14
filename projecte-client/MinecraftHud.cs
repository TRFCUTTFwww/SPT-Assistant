using System.Text.Json;
using System.Text.RegularExpressions;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using Local.ModHost;
using UnityEngine;
namespace ProjectE.Client;
internal sealed class MinecraftHud {
 readonly MinecraftHudArt art;readonly ModuleContext context;readonly List<long> thresholds=[0];
 readonly List<QuickSlotView> slots=[],nativeViews=[];
 InventoryScreenQuickAccessPanel? panel;Player? player;float nextScan,nextStats,nextDiagnostics;
 float health,food,water,lastHurt=-100;int level;float experience;
 internal MinecraftHud(ModuleContext context,ProjectResources resources){
  this.context=context;art=new(context,resources);
  string path=Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","SPT_Data","database","globals.json");
  using var globals=JsonDocument.Parse(File.ReadAllText(path));long total=0;foreach(var entry in globals.RootElement.GetProperty("config").GetProperty("exp").GetProperty("level").GetProperty("exp_table").EnumerateArray()){total+=entry.GetProperty("exp").GetInt64();thresholds.Add(total);}
 }
 internal void Tick(){
  var game=Singleton<AbstractGame>.Instance;player=game?.InRaid==true&&game.TryCast<HideoutGame>()==null?Singleton<GameWorld>.Instance?.MainPlayer:null;
  if(player?.IsYourPlayer!=true||player.HealthController?.IsAlive!=true){panel=null;slots.Clear();nativeViews.Clear();return;}
  var current=CommonUI.Instance?.EftBattleUIScreen?.QuickAccessPanel;
  if(current?.Pointer!=panel?.Pointer||Time.unscaledTime>=nextScan){
   panel=current;nextScan=Time.unscaledTime+1;nativeViews.Clear();
   if(panel!=null){
    // Use the actual rendered views, including fixed weapons, G/V and 0.
    // Nested subweapon views share a binding; prefer its active native view.
    var views=panel.GetComponentsInChildren<QuickSlotView>(true);
    nativeViews.AddRange(views.Where(v=>v!=null).OrderBy(v=>v.transform.position.x));
   }
  }
  // Values, active subweapon views and selection follow the native UI each frame.
  slots.Clear();foreach(var view in nativeViews)
   if(view!=null&&view.gameObject.activeInHierarchy&&!slots.Any(v=>v.BoundIndex==view.BoundIndex))slots.Add(view);
  if(Time.unscaledTime<nextStats)return;nextStats=Time.unscaledTime+.1f;
  var h=player.ActiveHealthController;if(h==null)return;var hp=h.GetBodyPartHealth(EBodyPart.Common,false);float currentHealth=hp.Maximum>0?Math.Clamp(hp.Current/hp.Maximum,0,1):0;if(currentHealth<health-.0001f)lastHurt=Time.unscaledTime;health=currentHealth;food=h.Energy.Maximum>0?Math.Clamp(h.Energy.Current/h.Energy.Maximum,0,1):0;
  water=h.Hydration.Maximum>0?Math.Clamp(h.Hydration.Current/h.Hydration.Maximum,0,1):0;
  long xp=player.Profile.Info.Experience;level=Math.Max(1,player.Profile.Info.Level);experience=level<thresholds.Count-1?EmcRules.Progress(xp,thresholds[level],thresholds[level+1]):1;
  if(Time.unscaledTime>=nextDiagnostics){nextDiagnostics=Time.unscaledTime+5;WriteDiagnostics();}
 }
 void WriteDiagnostics(){
  try{
   var held=player?.HandsController?.Item;
   File.WriteAllText(Path.Combine(context.DataDirectory,"hud-status.json"),JsonSerializer.Serialize(new{
    utc=DateTime.UtcNow,source="CommonUI.EftBattleUIScreen.QuickAccessPanel",nativePanel=panel!=null,
    alpha=panel?._animation?._canvasGroup?.alpha??0,level,experience,
    slots=slots.Select(v=>new{binding=v.BoundIndex.ToString(),key=v.HotKey?.text??"",template=v.Item?.StringTemplateId??"",selected=v.Item!=null&&v.Item.Id.ToString()==held?.Id.ToString()})
   }));
  }catch(Exception error){context.Log(LogLevel.Info,"HUD diagnostics: "+error.Message);}
 }
 internal void Draw(float scale){
  if(Event.current.type!=EventType.Repaint||player?.IsYourPlayer!=true||player.HealthController?.IsAlive!=true||player.IsInventoryOpened||Cursor.visible)return;
  // Integer screen-pixel scale preserves the original pixel grid and spacing.
  float pixel=Math.Max(1,Mathf.RoundToInt(2*scale));float width=slots.Count>0?slots.Count*20+2:182;
  var oldMatrix=GUI.matrix;var oldColor=GUI.color;int oldDepth=GUI.depth;GUI.color=Color.white;GUI.depth=100;
  GUI.matrix=Matrix4x4.TRS(new(Mathf.Round((Screen.width-width*pixel)/2),Screen.height-22*pixel,0),Quaternion.identity,new(pixel,pixel,1));
  try{
   float stats=(width-182)/2;int hp=(int)Math.Ceiling(health*20),hunger=(int)Math.Ceiling(food*20);
   for(int i=0;i<10;i++){
    art.Symbol(stats+i*8,-17+HudMotion.Offset(health,i,0,Time.unscaledTime,Time.unscaledTime-lastHurt<.45f),false,hp-i*2);
    art.Symbol(stats+182-i*8-9,-17+HudMotion.Offset(food,i,1,Time.unscaledTime),true,hunger-i*2);
    art.Hydration(stats+182-i*8-9,-30+HudMotion.Offset(water,i,2,Time.unscaledTime),(int)Math.Ceiling(water*20)-i*2);
   }
   art.Experience(stats,-7,experience);art.Level(width/2,-13,level);
   if(panel==null||!panel.gameObject.activeInHierarchy||panel.IsDisabled)return;
   float alpha=panel._animation?._canvasGroup?.alpha??1;if(alpha<=.001f)return;
   GUI.color=new(1,1,1,alpha);art.Hotbar(slots.Count);
   var held=player.HandsController?.Item;
   for(int i=0;i<slots.Count;i++){
    var view=slots[i];if(view==null)continue;var item=view.Item;
    if(item!=null&&item.Id.ToString()==held?.Id.ToString())art.Selected(i);
    var image=view.ItemView?.MainImage;var sprite=image?.sprite;
    if(item!=null&&image?.enabled==true&&image.gameObject.activeInHierarchy&&sprite!=null){
     var tint=image.color;GUI.color=new(tint.r,tint.g,tint.b,tint.a*alpha);MinecraftHudArt.Sprite(new(i*20+3,3,16,16),sprite);GUI.color=new(1,1,1,alpha);
    }
    string key=Regex.Replace(view.HotKey?.text??"","<[^>]*>","").Trim();
    if(key.Length>0)art.FittedText(i*20+2,1,18,key,new(1,1,1,alpha));
    var resource=view.ItemView?.TryCast<QuickSlotItemView>()?._resourceValue??view.ItemView?.BottomPanel?.ItemValue;
    string count=item!=null&&resource?.enabled==true&&resource.gameObject.activeInHierarchy?resource.text:"";
    if(count.Length==0&&item?.StackObjectsCount>1)count=item.StackObjectsCount.ToString();
    if(count.Length>0){count=Regex.Replace(count,"<[^>]*>","");art.FittedText(i*20+1,13,19,count,new(1,1,1,alpha),true);}
   }
  }finally{GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.depth=oldDepth;}
 }
 internal void Clear(){panel=null;player=null;slots.Clear();nativeViews.Clear();}
}
