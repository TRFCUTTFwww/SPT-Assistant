using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace ProjectE.Client;
internal sealed class MinecraftHud {
 readonly McGui gui;readonly ProjectResources resources;readonly List<long> thresholds=[0];
 Item?[] bar=new Item?[9];float next;Player? player;float health,food,water;int level;float experience;
 internal MinecraftHud(ProjectResources resources){
  this.resources=resources;gui=new(resources);foreach(string kind in new[]{"heart","hunger","drop"})foreach(string state in new[]{"empty","half","full"})resources.Texture(kind+"_"+state);
  string path=Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","SPT_Data","database","globals.json");
  using var globals=JsonDocument.Parse(File.ReadAllText(path));long total=0;foreach(var entry in globals.RootElement.GetProperty("config").GetProperty("exp").GetProperty("level").GetProperty("exp_table").EnumerateArray()){total+=entry.GetProperty("exp").GetInt64();thresholds.Add(total);}
 }
 internal void Tick(){
  if(Time.unscaledTime<next)return;next=Time.unscaledTime+.1f;var game=Singleton<AbstractGame>.Instance;player=game?.InRaid==true&&game.TryCast<HideoutGame>()==null?Singleton<GameWorld>.Instance?.MainPlayer:null;if(player?.IsYourPlayer!=true||player.HealthController?.IsAlive!=true)return;
  var h=player.ActiveHealthController;if(h==null)return;var hp=h.GetBodyPartHealth(EBodyPart.Common,false);health=hp.Maximum>0?Math.Clamp(hp.Current/hp.Maximum,0,1):0;food=h.Energy.Maximum>0?Math.Clamp(h.Energy.Current/h.Energy.Maximum,0,1):0;water=h.Hydration.Maximum>0?Math.Clamp(h.Hydration.Current/h.Hydration.Maximum,0,1):0;
  long xp=player.Profile.Info.Experience;level=Math.Max(1,player.Profile.Info.Level);experience=level<thresholds.Count-1?EmcRules.Progress(xp,thresholds[level],thresholds[level+1]):1;
  var fast=player.Profile.Inventory.FastAccess;for(int i=0;i<9;i++)bar[i]=fast.GetBoundItem((EBoundItem)((int)EBoundItem.Item1+i));
 }
 internal void Draw(float scale){
  if(player?.IsYourPlayer!=true||player.HealthController?.IsAlive!=true||player.IsInventoryOpened||Cursor.visible)return;gui.InitializeFont();
  var old=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new((Screen.width-366*scale)/2,Screen.height-58*scale,0),Quaternion.identity,new(scale,scale,1));
  try{
   for(int i=0;i<10;i++){Symbol(new(2+i*17,-31,18,18),"heart",health*20-i*2);Symbol(new(195+i*17,-31,18,18),"hunger",food*20-i*2);Symbol(new(195+i*17,-51,18,18),"drop",water*20-i*2);}
   McGui.Fill(new(0,-9,366,8),Color.black);McGui.Fill(new(2,-7,362,4),new(.22f,.22f,.22f));McGui.Fill(new(2,-7,362*experience,4),new(.4f,.9f,.12f));gui.Label(new(145,-32,76,21),level.ToString(),new(.5f,1,.2f),18,true);
   McGui.Fill(new(-2,1,370,46),Color.black);var held=player.HandsController?.Item;
   for(int i=0;i<9;i++){var r=new Rect(i*40.5f,3,40,40);bool selected=bar[i]!=null&&bar[i]!.Id.ToString()==held?.Id.ToString();McGui.Slot(r,selected);if(bar[i]!=null){gui.Icon(new(r.x+4,r.y+3,32,32),bar[i],bar[i]!.StringTemplateId);if(bar[i]!.StackObjectsCount>1)gui.Label(new(r.x+15,r.y+22,24,16),bar[i]!.StackObjectsCount.ToString(),Color.white,11,true);}gui.Label(new(r.x+2,r.y,16,12),(i+1).ToString(),Color.white,10);}
  }finally{GUI.matrix=old;}
 }
 void Symbol(Rect r,string kind,float units)=>GUI.DrawTexture(r,resources.Texture(kind+"_"+(units>=2?"full":units>0?"half":"empty")));
 internal void Clear()=>gui.Clear();
}
