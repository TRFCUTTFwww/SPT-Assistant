using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class AiActorControl {
 static readonly Dictionary<IntPtr,LiveActorReference> hiddenActors=new();static float nextHidden;
 internal static bool HiddenFromAi(IPlayer? target){
  if(target==null||!RaidClock.Active)return false;var current=Singleton<GameWorld>.Instance;
  if(target.TryCast<Player>() is {} player&&WorldEditor.HideMoving(player))return true;
  return current!=null&&hiddenActors.TryGetValue(target.Pointer,out var reference)&&reference.World==current.Pointer&&reference.Profile==target.ProfileId;
 }
 internal static void SetHiddenFromAi(GameWorld current,IEnumerable<Player> selected,bool value){
  var refs=selected.Select(player=>{var reference=new LiveActorReference(current,player);reference.Resolve();if(player.IsYourPlayer||player.AIData?.BotOwner==null)throw new ArgumentException("请选择 AI。");return reference;}).ToArray();
  foreach(var reference in refs)if(value)hiddenActors[reference.Pointer]=reference;else hiddenActors.Remove(reference.Pointer);
  nextHidden=0;TickHidden(current);FeatureToasts.Show(value?"所选 AI 已被其他 AI 无视。":"所选 AI 已恢复被其他 AI 识别。");
 }
 internal static void RefreshHidden(GameWorld current){nextHidden=0;TickHidden(current);}
 static void TickHidden(GameWorld? current){
  if(current==null||!RaidClock.Active){hiddenActors.Clear();return;}if(Time.unscaledTime<nextHidden)return;nextHidden=Time.unscaledTime+.25f;
  foreach(var pair in hiddenActors.ToArray())try{pair.Value.Resolve();}catch{hiddenActors.Remove(pair.Key);}
  var all=current.AllAlivePlayersList;if(all==null)return;
  for(int i=0;i<all.Count;i++){var target=all[i];if(target==null||!HiddenFromAi(target))continue;for(int j=0;j<all.Count;j++){var observer=all[j];if(observer?.Pointer==target.Pointer)continue;try{if(observer?.AIData?.BotOwner is {} owner)Forget(owner,target);}catch{}}}
 }
 static void DrawHiddenFromAi(Rect rect){bool all=targets.Count>0&&targets.All(p=>HiddenFromAi(p));if(InterfaceMenu.Button(rect,all?"被其他 AI 无视 · 开启":"被其他 AI 无视 · 关闭",all))Action(()=>SetHiddenFromAi(Singleton<GameWorld>.Instance,SelectedTargets(),!all));}
}
