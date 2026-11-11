using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class TeleportControl {
 internal static (string Key,string Name,Vector3 Position)[] ChatDestinations(){Tick(Singleton<GameWorld>.Instance);int prior=page;string priorQuery=query;var result=new List<(string,string,Vector3)>();try{query="";foreach(int category in new[]{0,1,2,5,8,9,11}){page=category;Refresh();result.AddRange(destinations.Select(d=>(d.Key,d.Name,d.Position)));}return result.DistinctBy(p=>p.Item1).ToArray();}finally{page=prior;query=priorQuery;nextList=0;}}
 internal static void ChatMove(Player actor,Vector3 point){var current=Singleton<GameWorld>.Instance??throw new ArgumentException("请进入战局。");Tick(current);if(actor.IsYourPlayer)actor.Teleport(point,true);else{new LiveActorReference(current,actor).Resolve();AiFreePlacement.Place(current,actor,point);}}
}
internal static partial class WorldEditor {
 internal static Player[] ChatSelected()=>SelectedActors();
 internal static void ChatEdit(bool value){if(enabled==null)throw new ArgumentException("编辑器未就绪。");enabled.Value=value;if(!value)Suspend();}
 internal static void ChatAction(string action,Player[]? actors=null,Vector3? destination=null){if(!Active)throw new ArgumentException("先 /edit on 开启编辑模式。");if(actors!=null){if(actors.Any(p=>!Editable(p)))throw new ArgumentException("只能编辑本机控制的普通 AI。");selectedActors.Clear();selectedActors.AddRange(actors.Select(p=>new LiveActorReference(Current(),p)));selectionKind=SelectionKind.Ai;TargetAt(0);}if(destination.HasValue)point=destination.Value;switch(action){case "copy":Copy();break;case "paste":Paste();break;case "remove":Delete();break;default:throw new ArgumentException("未知编辑命令。");}}
}
internal static partial class AirdropControl {internal static void ChatSummon(){if(!loaded)Load();Summon();}}
internal static partial class CharacterBuffs {internal static void ChatClear(Player p,string? type){var health=p.ActiveHealthController??throw new ArgumentException("生命控制器未就绪。");if(type!=null&&!BuffRules.Common.Any(b=>b.Type.Equals(type,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("未知效果类型。");foreach(var effect in Effects(health))if(BuffRules.CanRemove(effect.Type?.Name??"")&&(type==null||string.Equals(effect.Type?.Name,type,StringComparison.OrdinalIgnoreCase)))effect.ForceRemove();}}
internal static partial class SceneTemplates {internal static SceneTemplate ChatTemplate(string name){Load();var candidates=templates.Where(t=>t.Id==name||t.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();return candidates.Length==1?candidates[0]:throw new ArgumentException("模板不存在或名称不唯一。");}}
internal static partial class AiActorControl {internal static void ChatRelation(GameWorld w,Player[] targets,string kind){foreach(var p in targets){if(kind=="default"){relations.Actors.Remove((p.ProfileId,w.MainPlayer.ProfileId));relations.Actors.Remove((w.MainPlayer.ProfileId,p.ProfileId));continue;}var state=EditorState(p);ApplyBirthState(w,p,state.Invincible,kind=="neutral"?1:2,0);}}}
internal static partial class BtrControl {internal static void ChatStop(GameWorld w){var vehicle=w.BtrController?.BtrVehicle??throw new ArgumentException("BTR 未就绪。");pendingRoute.Clear();requestedStop="";vehicle.MovePauseOn(60);}}
internal static partial class GroundNavigation {static Vector3 chatGoal;internal static void ChatTarget(Vector3 point,string name){Clear();chatGoal=point;kind=Destination.Chat;targetName=name;worldPointer=Singleton<GameWorld>.Instance.Pointer;enabled.Value=true;message="正在寻找可行走路线…";}}
internal static partial class RaidEnd {internal static void ChatExtract(){mode=RaidEndMode.Survived;Arm();if(request==null)throw new ArgumentException(notice);Confirm();if(endingWorld==IntPtr.Zero)throw new ArgumentException(notice);}}
internal static class ChatEnvironment {
 internal static int Daytime(){var date=Singleton<GameWorld>.Instance?.GameDateTime??throw new ArgumentException("请进入战局。");return (int)((date.Calculate().TimeOfDay.TotalHours+18)%24*1000);}
 internal static void SetTime(int ticks){var date=Singleton<GameWorld>.Instance?.GameDateTime??throw new ArgumentException("请进入战局。");date.ResetForce(date.Calculate().Date.AddHours(((ticks%24000+24000)%24000/1000d+6)%24));}
}
