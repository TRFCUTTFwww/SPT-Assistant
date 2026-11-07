using System;
using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class DamagePreview {
 static ConfigEntry<bool> enabled=null!;static float nextRead,nextError,lastSeen;static IntPtr world;
 static string text="",detail="",actual="";static float actualUntil;static bool lethal;static Player? observed;static Vector3 observedDirection;static GUIStyle? label;
 internal static void Initialize(){enabled=FeatureConfig.Bind("40. 弹道与伤害预览","Damage preview","准星 AI 伤害预览",true,"显示当前弹药的伤害上限与命中部位；可能致死时标红。距离衰减、散布、碎片、护甲和随机穿透会影响实际结果。");}
 static void ClearPrediction(){text=detail="";observed=null;lethal=false;lastSeen=0;}
 internal static void Reset(){ClearPrediction();actual="";actualUntil=0;world=IntPtr.Zero;nextRead=0;CrosshairProbe.Reset();}
 internal static void RecordActual(string victim,string part,float damage){actual="实际扣血 "+damage.ToString("0.0")+" · "+part;actualUntil=Time.unscaledTime+4;}
 static void Read(GameWorld current,Player local,Camera camera){
  if(world!=current.Pointer){Reset();world=current.Pointer;}
  if(Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+.05f;
  var controller=local.HandsController?.TryCast<Player.FirearmController>();if(controller==null||controller._isInventoryOpened){ClearPrediction();return;}
  var ammo=MagazineDisplay.CurrentAmmo(local);if(ammo==null){ClearPrediction();return;}
  var hit=CrosshairProbe.Read(current,local,camera,2000);
  var target=hit.Actor;var body=hit.Body;var health=target?.ActiveHealthController;
  if(hit.State!=CrosshairState.Target||target==null||body==null||health==null){
   if(!PreviewDisplayRules.KeepGap(Time.unscaledTime,lastSeen,Vector3.Angle(camera.transform.forward,observedDirection),observed?.HealthController?.IsAlive==true,hit.State is CrosshairState.Blocked or CrosshairState.Overflow))ClearPrediction();
   return;
  }
  var partType=body.BodyPartType;float total=health.GetBodyPartHealth(EBodyPart.Common,false).Current,partHealth=health.GetBodyPartHealth(partType,false).Current;
  bool immune=Plugin.DamageBlocked(health);float upper=immune?0:DamagePreviewRules.Upper(ammo.Damage,AiActorControl.PreviewDamageFactor(target),total,health.DamageCoeff,health.DamageMultiplier);
  bool armor=target.TryGetArmorResistData(body,ammo.PenetrationPower,out var resistance);float chance=armor?resistance.GetPenetrationChance(ammo.PenetrationPower):1,scale=armor&&ArmorResistanceData.MAX_CHANCE>1?100:1;chance=Math.Clamp(chance/scale,0,1);
  lethal=!immune&&DamagePreviewRules.PotentiallyLethal(partType is EBodyPart.Head or EBodyPart.Chest,partHealth,total,upper);
  text=immune?"预计 0 · AI 无敌":"未减伤预计 "+upper.ToString("0.0")+" · "+Part(partType)+(lethal?" · 可能致死":"");
  detail=(ammo.ProjectileCount>1?"单弹丸 · ":"")+(armor?"穿透 "+(chance*100).ToString("0.#")+"% · ":"")+"部位生命 "+partHealth.ToString("0.#")+" · "+hit.Distance.ToString("F0")+" 米";
  observed=target;observedDirection=camera.transform.forward;lastSeen=Time.unscaledTime;
 }
 static string Part(EBodyPart part)=>part switch{EBodyPart.Head=>"头部",EBodyPart.Chest=>"胸部",EBodyPart.Stomach=>"腹部",EBodyPart.LeftArm=>"左臂",EBodyPart.RightArm=>"右臂",EBodyPart.LeftLeg=>"左腿",EBodyPart.RightLeg=>"右腿",_=>"全身"};
 internal static void Draw(GameWorld current,Player local,Camera camera,GUIStyle source){
  if(!enabled.Value||InterfaceMenu.Open||Cursor.visible){ClearPrediction();nextRead=0;return;}
  try{
   Read(current,local,camera);var display=PreviewDisplayRules.Compose(text,detail,actual,Time.unscaledTime<actualUntil);if(display.Text.Length==0)return;
   label??=new GUIStyle(source){alignment=TextAnchor.UpperCenter,wordWrap=false,richText=false};label.fontSize=14;
   using var windowOpacity=InterfaceMenu.BeginWindow("damagepreview");var rect=InterfaceMenu.Place("damagepreview",new Rect(Screen.width*.5f-240,Screen.height*.5f+22,480,48));var previous=GUI.color;
   try{string value=display.Text+"\n"+display.Detail;GUI.color=Color.black;GUI.Label(new Rect(rect.x+1,rect.y+1,rect.width,rect.height),I18n.Text(value),label);GUI.color=lethal&&text.Length>0?new Color(1,.2f,.2f):new Color(1,.92f,.63f);GUI.Label(rect,I18n.Text(value),label);}finally{GUI.color=previous;}
  }catch(Exception error){ClearPrediction();if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+20;Plugin.Self.Log.LogWarning("Damage preview: "+error.Message);}}
 }
}