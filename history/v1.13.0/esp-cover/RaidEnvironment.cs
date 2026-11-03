using System;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Weather;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class RaidEnvironment {
 static ConfigEntry<bool> overrideWeather=null!,overrideSpeed=null!,freeze=null!;
 static ConfigEntry<float> rain=null!,fog=null!,clouds=null!,speed=null!;
 sealed record WeatherSnapshot(bool Enabled,float Rain,float Fog,float Clouds);
 static WeatherSnapshot? originalWeather;
 static WeatherController? weatherOwner;
 static GameDateTime? clockOwner;
 static float originalFactor,nextError;
 static bool originalLocked;
 static string hour="12",status="";
 internal static void Initialize(){
  overrideWeather=FeatureConfig.Bind("31. 环境控制","Override weather","自定义天气",false);
  rain=FeatureConfig.Bind("31. 环境控制","Rain","降雨强度",0f,"",new AcceptableValueRange<float>(0,1));fog=FeatureConfig.Bind("31. 环境控制","Fog","雾浓度",0f,"",new AcceptableValueRange<float>(0,1));clouds=FeatureConfig.Bind("31. 环境控制","Clouds","云层密度",0f,"",new AcceptableValueRange<float>(-1,1));
  overrideSpeed=FeatureConfig.Bind("31. 环境控制","Override time speed","自定义游戏时间流速",false);speed=FeatureConfig.Bind("31. 环境控制","Time multiplier","相对原始时间流速倍数",1f,"",new AcceptableValueRange<float>(.1f,20));freeze=FeatureConfig.Bind("31. 环境控制","Freeze time","冻结昼夜时间",false);
 }
 internal static void Tick(){try{
  if(!RaidClock.Active){Reset();return;}
  var current=Singleton<GameWorld>.Instance.GameDateTime;
  if((overrideSpeed.Value||freeze.Value)&&current!=null){
   if(clockOwner==null||clockOwner.Pointer!=current.Pointer){RestoreClock();clockOwner=current;originalFactor=current.TimeFactor;originalLocked=current.Locked;}
   if(freeze.Value){if(!current.Locked)current.Lock();}else{if(!originalLocked&&current.Locked)current.Unlock();current.TimeFactor=originalFactor*speed.Value;}
  }else RestoreClock();
  var owner=WeatherController.Instance;
  if(overrideWeather.Value&&owner?.WeatherDebug!=null){if(weatherOwner==null||weatherOwner.Pointer!=owner.Pointer){RestoreWeather();weatherOwner=owner;originalWeather=new(owner.WeatherDebug.Enabled,owner.WeatherDebug.Rain,owner.WeatherDebug.Fog,owner.WeatherDebug.CloudDensity);}var debug=owner.WeatherDebug;debug.Enabled=true;debug.Rain=rain.Value;debug.Fog=fog.Value;debug.CloudDensity=clouds.Value;}
  else RestoreWeather();
 }catch(Exception e){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+20;Plugin.Self.Error(e);}}}
 static void RestoreClock(){if(clockOwner!=null){try{clockOwner.TimeFactor=originalFactor;if(originalLocked)clockOwner.Lock();else clockOwner.Unlock();}finally{clockOwner=null;}}}
 static void RestoreWeather(){if(weatherOwner!=null&&originalWeather!=null)try{weatherOwner.WeatherDebug.Rain=originalWeather.Rain;weatherOwner.WeatherDebug.Fog=originalWeather.Fog;weatherOwner.WeatherDebug.CloudDensity=originalWeather.Clouds;weatherOwner.WeatherDebug.Enabled=originalWeather.Enabled;}finally{weatherOwner=null;originalWeather=null;}}
 internal static void Reset(){try{RestoreClock();}catch(Exception e){Plugin.Self.Error(e);}try{RestoreWeather();}catch(Exception e){Plugin.Self.Error(e);}}
 internal static void Draw(Rect rect){
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width,28),"昼夜与天气",InterfaceMenu.Accent,17);
  InterfaceMenu.Label(new Rect(rect.x,rect.y+38,120,30),"游戏小时 0～24");hour=InterfaceMenu.Field(new Rect(rect.x+130,rect.y+38,90,30),hour);
  if(InterfaceMenu.Button(new Rect(rect.x+230,rect.y+38,110,30),"设置时刻"))try{if(!RaidClock.Active)throw new ArgumentException("请先进入战局。");float h=ItemSpawnRules.Number(hour,0,23.999f,"小时");var date=Singleton<GameWorld>.Instance.GameDateTime??throw new ArgumentException("时间未同步。");date.ResetForce(date.Calculate().Date.AddHours(h));status="游戏时间已更新";}catch(Exception e){status=e.Message;}
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y+84,160,32),freeze.Value?"昼夜已冻结":"冻结昼夜",freeze.Value))freeze.Value=!freeze.Value;
  if(InterfaceMenu.Button(new Rect(rect.x+170,rect.y+84,160,32),overrideSpeed.Value?"自定义流速开启":"启用自定义流速",overrideSpeed.Value))overrideSpeed.Value=!overrideSpeed.Value;
  InterfaceMenu.Label(new Rect(rect.x,rect.y+126,rect.width,26),"流速倍数 "+speed.Value.ToString("0.0"));speed.Value=GUI.HorizontalSlider(new Rect(rect.x,rect.y+160,Math.Min(440,rect.width),20),speed.Value,.1f,20);
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y+198,160,32),overrideWeather.Value?"自定义天气开启":"启用自定义天气",overrideWeather.Value))overrideWeather.Value=!overrideWeather.Value;
  float w=Math.Min(440,rect.width);InterfaceMenu.Label(new Rect(rect.x,rect.y+242,w,24),$"雨 {rain.Value:P0}    雾 {fog.Value:P0}    云 {clouds.Value:0.00}");
  rain.Value=GUI.HorizontalSlider(new Rect(rect.x,rect.y+275,w,20),rain.Value,0,1);fog.Value=GUI.HorizontalSlider(new Rect(rect.x,rect.y+308,w,20),fog.Value,0,1);clouds.Value=GUI.HorizontalSlider(new Rect(rect.x,rect.y+341,w,20),clouds.Value,-1,1);
  InterfaceMenu.Label(new Rect(rect.x,rect.y+370,rect.width,24),status,null,12);
 }
}
