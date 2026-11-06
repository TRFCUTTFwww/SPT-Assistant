using System;
using BepInEx.Configuration;
using EFT;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
namespace LocalSpt5Esp;

internal static class MuzzleLine {
 static ConfigEntry<bool> enabled=null!;static ConfigEntry<float> range=null!,width=null!;static ConfigEntry<string> color=null!;
 static readonly Il2CppStructArray<RaycastHit> hits=new(64);
 static GameObject? node;static LineRenderer? line;static Material? material;static float nextError;
 internal static void Initialize(){const string section="40. 弹道与伤害预览";
  enabled=FeatureConfig.Bind(section,"Muzzle aim line","枪口瞄准线",true,"从当前枪口沿武器实际朝向绘制，到第一个碰撞点截止。只显示方向，不预测重力下坠、散布或静默瞄准修正。");
  range=FeatureConfig.Bind(section,"Muzzle line distance","枪口线最大距离（米）",200f,"",new AcceptableValueRange<float>(1,1000));
  width=FeatureConfig.Bind(section,"Muzzle line width","枪口线宽度（米）",.012f,"",new AcceptableValueRange<float>(.002f,.05f));
  color=FeatureConfig.Bind(section,"Muzzle line color","枪口线颜色（十六进制）","#56E5FF");
 }
 internal static void Tick(GameWorld? world,Player? player){try{
  if(!enabled.Value||!RaidClock.Active||DirectorStage.HudHidden||DirectorStage.FreeActive||world==null||player?.HealthController?.IsAlive!=true||player.HandsController?.TryCast<Player.FirearmController>() is not {} weapon){Hide();return;}
  var start=weapon.FireportPosition;var direction=weapon.WeaponDirection;
  if(!Finite(start)||!Finite(direction)||direction.sqrMagnitude<.0001f){Hide();return;}
  direction.Normalize();float length=range.Value;if(!float.IsFinite(length)||length<=0){Hide();return;}
  var mask=EFTHardSettings.Instance!=null?(int)EFTHardSettings.Instance.GunshotMask:Physics.DefaultRaycastLayers;
  int count=Physics.RaycastNonAlloc(start,direction,hits,length,mask,QueryTriggerInteraction.Ignore);
  // A saturated query cannot guarantee the nearest hit; skip that frame.
  if(count>=hits.Length){Hide();return;}
  for(int i=0;i<count;i++){var hit=hits[i];var collider=hit.collider;if(collider==null||hit.distance<0||hit.distance>=length||collider.transform.IsChildOf(player.transform)||world.PlayersColliders?.TryGetValue(collider,out var owner)==true&&owner?.ProfileId==player.ProfileId)continue;length=hit.distance;}
  if(!Resources()){Hide();return;}
  line!.startWidth=line.endWidth=width.Value;line.startColor=line.endColor=ColorUtility.TryParseHtmlString(color.Value,out var chosen)?chosen:new Color(.34f,.9f,1);
  line.SetPosition(0,start);line.SetPosition(1,start+direction*length);line.enabled=true;
 }catch(Exception e){Hide();if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+20;Plugin.Self.Log.LogWarning("Muzzle aim line: "+e);}}}
 static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
 static bool Resources(){if(line!=null)return true;var shader=Shader.Find("Hidden/Internal-Colored");if(shader==null||!shader.isSupported)return false;
  material??=new Material(shader){hideFlags=HideFlags.HideAndDontSave};material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_Cull",(int)CullMode.Off);material.SetInt("_ZWrite",0);material.SetInt("_ZTest",(int)CompareFunction.LessEqual);
  node=new GameObject("SPT Assistant muzzle aim line"){hideFlags=HideFlags.HideAndDontSave};line=node.AddComponent<LineRenderer>();line.enabled=false;line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.lightProbeUsage=LightProbeUsage.Off;line.reflectionProbeUsage=ReflectionProbeUsage.Off;line.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;return true;
 }
 static void Hide(){if(line!=null)line.enabled=false;}
 internal static void Dispose(){Hide();if(node!=null)UnityEngine.Object.Destroy(node);if(material!=null)UnityEngine.Object.Destroy(material);node=null;line=null;material=null;}
}
