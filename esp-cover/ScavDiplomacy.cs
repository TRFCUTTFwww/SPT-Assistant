using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using LocalMiscShared;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ScavDiplomacy {
 static readonly HashSet<IntPtr> seen=new();static readonly Dictionary<IntPtr,string> protectedOwners=new();static IntPtr world;static bool active;static float next;static string localId="";
 internal static bool Protect(BotOwner? owner,IPlayer? target){if(!active||!MiscPanel.Options.DiplomaticImmunity||owner==null||target==null||target.ProfileId!=localId)return false;return protectedOwners.TryGetValue(owner.Pointer,out var id)&&owner.GetPlayer?.ProfileId==id;}
 internal static bool Protect(BotsGroup group,IPlayer target){bool found=false;var members=group._members;if(members==null)return false;foreach(var member in members){if(member==null)continue;if(!Protect(member,target))return false;found=true;}return found;}
 internal static void Tick(){
  var current=Singleton<GameWorld>.Instance;var local=current?.MainPlayer;
  if(current==null||local==null||!RaidClock.Active){Reset();return;}
  if(world!=current.Pointer){seen.Clear();protectedOwners.Clear();active=false;world=current.Pointer;}
  bool enabled=MiscPanel.Options.DiplomaticImmunity&&local.Side==EPlayerSide.Savage;
  if(Time.unscaledTime<next&&enabled==active)return;next=Time.unscaledTime+.5f;
  if(!enabled&&!active){protectedOwners.Clear();return;}
  var players=current.AllAlivePlayersList;if(players==null)return;localId=local.ProfileId;protectedOwners.Clear();
  foreach(var actor in players){var owner=actor?.AIData?.BotOwner;var group=owner?.BotsGroup;if(owner==null||group==null)continue;
   if(enabled&&MiscSettings.Diplomacy(true,true,true,true,actor!=null&&!actor.IsYourPlayer,actor?.Side==EPlayerSide.Savage,actor?.Profile?.Info?.Settings?.Role.ToString()??"")){protectedOwners[owner.Pointer]=actor!.ProfileId;if(seen.Add(group.Pointer)){group.BotGroupHostility?.LastHostilityStatus?.Remove(local.RaidId);group.BotGroupHostility?.OwnedGroupCalcActionNextFrame();}AiActorControl.Forget(owner,local);}
   else if(active&&!enabled&&seen.Remove(group.Pointer)){group.BotGroupHostility?.LastHostilityStatus?.Remove(local.RaidId);group.BotGroupHostility?.OwnedGroupCalcActionNextFrame();}
  }
  active=enabled;
 }
 internal static void Reset(){var current=Singleton<GameWorld>.Instance;var local=current?.MainPlayer;if(local!=null&&current?.Pointer==world&&current.AllAlivePlayersList!=null)foreach(var actor in current.AllAlivePlayersList){var state=actor?.AIData?.BotOwner?.BotsGroup?.BotGroupHostility;if(state!=null&&seen.Contains(actor!.AIData.BotOwner.BotsGroup.Pointer)){state.LastHostilityStatus?.Remove(local.RaidId);state.OwnedGroupCalcActionNextFrame();}}seen.Clear();protectedOwners.Clear();localId="";active=false;world=IntPtr.Zero;next=0;}
}
