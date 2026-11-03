using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class CorpseCleanup {
 static readonly HashSet<IntPtr> selected=new();static IntPtr world;static ConfigEntry<KeyCode> selectedKey=null!,allKey=null!;static string query="",notice="";static Vector2 scroll;
 internal static void Initialize(){selectedKey=FeatureConfig.Bind("48. 导演与画面","Clear selected corpses key","清除所选尸体键",KeyCode.None);allKey=FeatureConfig.Bind("48. 导演与画面","Clear all corpses key","清除全图尸体键",KeyCode.None);}
 static Corpse[] Read(GameWorld current){var result=new List<Corpse>();var list=current.LootItems;if(list!=null)for(int i=0;i<list.Count;i++){var corpse=list.GetByIndex(i)?.TryCast<Corpse>();if(corpse!=null)result.Add(corpse);}return result.ToArray();}
 internal static void Tick(GameWorld? current){if(current?.Pointer!=world){selected.Clear();world=current?.Pointer??IntPtr.Zero;}if(current==null||InterfaceMenu.InputBlocked)return;if(allKey.Value!=KeyCode.None&&Input.GetKeyDown(allKey.Value))Clear(true);else if(selectedKey.Value!=KeyCode.None&&Input.GetKeyDown(selectedKey.Value))Clear(false);}
 static void Clear(bool all){try{var current=Singleton<GameWorld>.Instance??throw new ArgumentException("请进入战局。");var corpses=Read(current).Where(c=>all||selected.Contains(c.Pointer)).ToArray();int removed=0;foreach(var corpse in corpses){bool alive=false;var players=current.AllAlivePlayersList;if(players!=null)for(int i=0;i<players.Count;i++)if(players[i]?.HealthController?.IsAlive==true&&players[i].ProfileId==corpse.PlayerProfileID){alive=true;break;}if(alive)continue;current.DestroyLoot(corpse.Cast<IKillable>());selected.Remove(corpse.Pointer);removed++;}CorpseDisplay.Reset();notice=$"已清除 {removed} 具尸体（包括其剩余物品）。";FeatureToasts.Show(notice);}catch(Exception e){notice=e.Message;}}
 internal static void Draw(Rect rect){var current=Singleton<GameWorld>.Instance;var bodies=current==null?Array.Empty<Corpse>():Read(current);float x=rect.x,y=rect.y,w=rect.width,half=(w-12)/2;InterfaceMenu.Label(new Rect(x,y,w,30),"尸体清理",InterfaceMenu.Accent,21);y+=44;query=InterfaceMenu.Field(new Rect(x,y,w,32),query);y+=44;var matches=bodies.Where(c=>(c.PlayerProfileID+" "+BossLabels.Cached(c.PlayerProfileID??"",c.Side.ToString())).Contains(query,StringComparison.OrdinalIgnoreCase)).ToArray();if(InterfaceMenu.Button(new Rect(x,y,half,32),"勾选当前结果"))foreach(var body in matches)selected.Add(body.Pointer);if(InterfaceMenu.Button(new Rect(x+half+12,y,half,32),"取消全部勾选"))selected.Clear();y+=44;var view=new Rect(x,y,w,Math.Max(70,rect.yMax-y-118));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,w-20,matches.Length*46));for(int i=0;i<matches.Length;i++){var body=matches[i];float distance=current?.MainPlayer!=null?Vector3.Distance(current.MainPlayer.Position,CorpseDisplay.Position(body)):0;string name=BossLabels.Cached(body.PlayerProfileID??"",body.Side.ToString());if(InterfaceMenu.Button(new Rect(4,i*46,w-30,36),(selected.Contains(body.Pointer)?"✓ ":"□ ")+name+$" · {distance:F0}米 · "+body.PlayerProfileID,selected.Contains(body.Pointer))){if(!selected.Remove(body.Pointer))selected.Add(body.Pointer);}}GUI.EndScrollView();y=rect.yMax-108;
  if(InterfaceMenu.Button(new Rect(x,y,half,36),"清除所选尸体"))Clear(false);if(InterfaceMenu.Button(new Rect(x+half+12,y,half,36),"清除全图尸体"))Clear(true);InterfaceMenu.Paragraph(new Rect(x,y+48,w,58),notice.Length>0?notice:"只清除尸体及其剩余物品；不清除存活角色或普通地面物资。热键可在热键页面设置。",InterfaceMenu.Accent,12);}
}
