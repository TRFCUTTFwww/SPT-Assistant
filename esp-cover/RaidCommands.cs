using System;
using AssistantChat;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class RaidCommands {
 internal static readonly Dictionary<string,string> Usage=new(StringComparer.OrdinalIgnoreCase){
 ["help"]="[命令]",["list"]="[entities]",["msg"]="<玩家> <消息>",["tell"]="<玩家> <消息>",["w"]="<玩家> <消息>",["reply"]="<消息>",["r"]="<消息>",["me"]="<动作>",["clearhistory"]="",["identity"]="[verify <玩家或档案 ID> <指纹>]",["chat"]="reconnect",["permission"]="list",
 ["tp"]="[来源] <目标|x y z> [facing <目标|x y z>]",["teleport"]="[来源] <目标|x y z>",["position"]="[目标]",["back"]="",["spreadplayers"]="<x> <z> <最小间距> <范围> <目标>",
 ["inspect"]="<目标> [health|equipment|effects|position]",["kill"]="<目标>",["damage"]="<目标> <数值> [伤害类型] [部位]",["heal"]="[目标] [部位]",["effect"]="give <目标> <效果> [秒] [强度] [部位] | clear <目标> [效果]",
 ["summon"]="<AI 角色|template:场景名> [x y z] [数量] [难度0-3]",["tag"]="<目标> add|remove|list [标签]",["rename"]="<AI> <名称>",["ai"]="pause|resume|follow|look|pose|relation|anchor|remove <AI> [参数]",
 ["edit"]="on|off",["copy"]="[AI|@selected]",["paste"]="[x y z]",["remove"]="<AI|@selected>",["data"]="get entity <目标> [字段] | modify entity <AI> <字段> set value <值>",
 ["give"]="<玩家或AI> <item:模板ID或唯一名称> [数量]",["clear"]="<目标> [item:模板ID] [最大数量]",["item"]="replace entity <目标> <装备槽> with <物品> [数量] | modify entity <目标> <装备槽> <参数> <值>",["loot"]="spawn <x y z> <物品> [数量] | give <目标> loadout:<场景名>",["repair"]="<目标> [装备槽]",["reload"]="<目标> [装备槽] [item:弹药模板ID]",
 ["time"]="set day|noon|night|midnight|<ticks> | add <ticks> | query daytime",["weather"]="clear|rain|thunder [持续秒数]",["gamerule"]="[规则] [值]",["raid"]="time query|set|add [时长] | extract @s",["airdrop"]="summon",["btr"]="status|route <站点ID>|stop",["marker"]="set <名称> [x y z] | remove <名称> | list",["navigate"]="<目标标签|x y z>|stop",
 ["experience"]="query <玩家> [points|levels]",["xp"]="query <玩家> [points|levels]",["attribute"]="<目标> <属性> base get|set [数值]"};
 static readonly Dictionary<string,HashSet<string>> tags=new();static readonly Dictionary<string,Vector3> markers=new(StringComparer.OrdinalIgnoreCase);static Vector3? back;static IntPtr world;static string raid=Guid.NewGuid().ToString("N");
 internal static void Reset(){tags.Clear();markers.Clear();back=null;world=IntPtr.Zero;raid=Guid.NewGuid().ToString("N");}
 static GameWorld World(){var current=Singleton<GameWorld>.Instance;if(!RaidClock.Active||current?.MainPlayer?.HealthController?.IsAlive!=true)throw new ArgumentException("需要存活的本地角色和当前单人战局。");if(world!=current.Pointer){Reset();world=current.Pointer;}if(AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name?.StartsWith("Fika",StringComparison.OrdinalIgnoreCase)==true)||All(current).Any(p=>!p.IsYourPlayer&&!FikaTeams.IsAi(p)))throw new ArgumentException("当前命令执行端仅适配单人战局；不能直接修改远程玩家或联机世界。");return current;}
 static Player[] All(GameWorld current){var result=new List<Player>();var list=current.AllAlivePlayersList;if(list!=null)for(int i=0;i<list.Count;i++){var p=list[i];if(p?.HealthController?.IsAlive==true)result.Add(p);}return result.ToArray();}
 static string Name(Player p)=>p.Profile?.Nickname??p.ProfileId;
 static string Role(Player p)=>p.Profile?.Info?.Settings?.Role.ToString()??"";
 static string Type(Player p){if(!FikaTeams.IsAi(p))return "player";string r=Role(p).ToLowerInvariant();if(r.StartsWith("boss")||r=="sectantpriest")return "boss";if(r.StartsWith("follower"))return "follower";if(r.Contains("infected"))return "zombie";if(r.Contains("sectant"))return "cultist";if(r=="pmcbot")return "raider";if(r.StartsWith("exusec"))return "rogue";if(r.Contains("pmc"))return "pmc";return "scav";}
 static string Id(Player p){string seed=FikaTeams.IsAi(p)?raid+":"+p.ProfileId:p.ProfileId;byte[] hash=SHA256.HashData(Encoding.UTF8.GetBytes(seed));return new Guid(hash.AsSpan(0,16)).ToString();}
 static bool IsType(Player p,string type)=>type.ToLowerInvariant() switch{"ai"=>FikaTeams.IsAi(p),"usec"=>p.Profile?.Info?.Side==EPlayerSide.Usec,"bear"=>p.Profile?.Info?.Side==EPlayerSide.Bear,_=>Type(p).Equals(type,StringComparison.OrdinalIgnoreCase)};
 static Player[] Resolve(GameWorld current,string selector){var all=All(current);IEnumerable<Player> result=all;bool multi=selector.StartsWith('@');if(selector=="@selected"){var selectedEditor=WorldEditor.ChatSelected();if(selectedEditor.Length==0)throw new ArgumentException("编辑器没有所选 AI。");return selectedEditor;}
  if(multi){int bracket=selector.IndexOf('[');string root=bracket<0?selector:selector[..bracket];result=root switch{"@s"=>all.Where(p=>p.IsYourPlayer),"@a"=>all.Where(p=>!FikaTeams.IsAi(p)),"@p"=>all.Where(p=>!p.IsYourPlayer&&!FikaTeams.IsAi(p)).OrderBy(p=>(p.Position-current.MainPlayer.Position).sqrMagnitude).Take(1),"@e"=>all,_=>throw new ArgumentException("未知选择器："+root)};
   string sort="";int limit=root=="@p"?1:256;if(bracket>=0){if(!selector.EndsWith(']'))throw new ArgumentException("选择器没有闭合。");var seen=new HashSet<string>();foreach(string filter in selector[(bracket+1)..^1].Split(',',StringSplitOptions.RemoveEmptyEntries)){var pair=filter.Split('=',2);if(pair.Length!=2||!seen.Add(pair[0]))throw new ArgumentException("选择器筛选项无效或重复。");string key=pair[0],value=pair[1];switch(key){case "type":result=result.Where(p=>IsType(p,value));break;case "name":result=result.Where(p=>Name(p).Equals(value,StringComparison.OrdinalIgnoreCase));break;case "tag":result=result.Where(p=>tags.TryGetValue(Id(p),out var t)&&t.Contains(value));break;case "distance":var range=value.Split("..",StringSplitOptions.None);double min=range.Length==1?ChatCommandSyntax.Number(value,0,100000):range[0].Length==0?0:ChatCommandSyntax.Number(range[0],0,100000),max=range.Length==1?min:range[1].Length==0?100000:ChatCommandSyntax.Number(range[1],0,100000);if(range.Length>2||min>max)throw new ArgumentException("距离范围无效。");result=result.Where(p=>Vector3.Distance(p.Position,current.MainPlayer.Position)>=min&&Vector3.Distance(p.Position,current.MainPlayer.Position)<=max);break;case "sort":if(value is not ("nearest" or "furthest" or "arbitrary"))throw new ArgumentException("sort 支持 nearest、furthest、arbitrary。");sort=value;break;case "limit":limit=(int)ChatCommandSyntax.Number(value,1,256);break;default:throw new ArgumentException("不支持筛选项："+key);}}}if(sort=="nearest")result=result.OrderBy(p=>(p.Position-current.MainPlayer.Position).sqrMagnitude);if(sort=="furthest")result=result.OrderByDescending(p=>(p.Position-current.MainPlayer.Position).sqrMagnitude);result=result.Take(limit);
  }else{int colon=selector.IndexOf(':');if(colon>=0){string kind=selector[..colon],name=selector[(colon+1)..];result=kind.Equals("uuid",StringComparison.OrdinalIgnoreCase)?all.Where(p=>Id(p).StartsWith(name,StringComparison.OrdinalIgnoreCase)):all.Where(p=>IsType(p,kind)&&Name(p).Equals(name,StringComparison.OrdinalIgnoreCase));}else result=all.Where(p=>Name(p).Equals(selector,StringComparison.OrdinalIgnoreCase)||p.ProfileId==selector);}
  var selected=result.ToArray();if(selected.Length==0)throw new ArgumentException("没有匹配的对象："+selector);if(!multi&&selected.Length>1)throw new ArgumentException("对象重名，请指定类型或 UUID：\n"+string.Join("\n",selected.Select(p=>Type(p)+":"+Name(p)+" uuid:"+Id(p))));return selected;
 }
 static Player One(GameWorld w,string selector){var selected=Resolve(w,selector);return selected.Length==1?selected[0]:throw new ArgumentException("此处只允许一个目标。");}
 static Player[] Ai(GameWorld w,string selector){var targets=Resolve(w,selector);if(targets.Any(p=>!FikaTeams.IsAi(p)||p.AIData?.BotOwner==null||Role(p)=="shooterBTR"))throw new ArgumentException("此命令只允许本机控制的普通 AI。");return targets;}
 static Vector3 Coordinates(GameWorld w,string[] values,Vector3? origin=null){if(values.Length!=3)throw new ArgumentException("需要 x y z 三个坐标。");bool local=values.Any(v=>v.StartsWith('^'));if(local&&values.Any(v=>!v.StartsWith('^')))throw new ArgumentException("^ 局部坐标不能与绝对或 ~ 坐标混用。");var anchor=origin??w.MainPlayer.Position;float Axis(string value,float basis)=>value.StartsWith('~')?basis+(value.Length==1?0:(float)ChatCommandSyntax.Number(value[1..],-100000,100000)):(float)ChatCommandSyntax.Number(value,-100000,100000);if(local){float V(string s)=>s.Length==1?0:(float)ChatCommandSyntax.Number(s[1..],-100000,100000);var camera=EFT.CameraControl.CameraManager.Instance?.Camera??throw new ArgumentException("镜头未就绪。");return anchor-camera.transform.right*V(values[0])+camera.transform.up*V(values[1])+camera.transform.forward*V(values[2]);}return new(Axis(values[0],anchor.x),Axis(values[1],anchor.y),Axis(values[2],anchor.z));}
 static Vector3 Destination(GameWorld w,string value){if(value.StartsWith("marker:",StringComparison.OrdinalIgnoreCase)){if(markers.TryGetValue(value[7..],out var m))return m;throw new ArgumentException("没有该标记。");}if(value.StartsWith("quest:",StringComparison.OrdinalIgnoreCase)&&QuestLocations.TryNavigationTarget(value[6..],out var q,out _))return q;if(value.StartsWith("valuable:",StringComparison.OrdinalIgnoreCase))value="room:"+value[9..];if(value.StartsWith("airdrop:",StringComparison.OrdinalIgnoreCase))value="drop:"+value[8..];if((value.Contains(':')||value.Equals("btr",StringComparison.OrdinalIgnoreCase))&&new[]{"room:","saved:","exit:","drop:","interaction:","btr"}.Any(p=>value.StartsWith(p,StringComparison.OrdinalIgnoreCase))){int colon=value.IndexOf(':');string kind=colon<0?value:value[..colon],key=colon<0?"":value[(colon+1)..];var candidates=TeleportControl.ChatDestinations().Where(d=>d.Key.Equals(value,StringComparison.OrdinalIgnoreCase)||d.Key.StartsWith(kind+":",StringComparison.OrdinalIgnoreCase)&&d.Name.Equals(key,StringComparison.OrdinalIgnoreCase)).ToArray();if(candidates.Length!=1)throw new ArgumentException("地点不存在或重名，请 Tab 补全完整标签。");return candidates[0].Position;}return One(w,value).Position;}
 internal static string Help(string? name=null)=>string.Join("\n",Usage.Where(p=>name==null||p.Key.Equals(name,StringComparison.OrdinalIgnoreCase)).Select(p=>"/"+p.Key+(p.Value.Length>0?" "+p.Value:"")))+"\n单人本地命令；不执行远端消息。名称重名时使用 type:name 或 uuid:前缀。";
 internal static void Execute(string name,string[] a){ChatCommandSyntax.ValidateArguments(name,a);var w=World();switch(name){
 case "list":foreach(var p in All(w))RaidChat.Output(Type(p)+":"+Name(p)+" uuid:"+Id(p));return;
 case "position":foreach(var p in Resolve(w,a.Length==0?"@s":a[0]))RaidChat.Output(Name(p)+" "+p.Position.ToString("F2")+" uuid:"+Id(p));return;
 case "tp":case "teleport":Teleport(w,a);return;
 case "back":if(back==null)throw new ArgumentException("本局没有传送前位置。");var old=w.MainPlayer.Position;TeleportControl.ChatMove(w.MainPlayer,back.Value);back=old;return;
 case "inspect":Inspect(w,a);return;
 case "kill":Require(a,1);foreach(var p in Ai(w,a[0])){p.ActiveHealthController.Kill(EDamageType.Bullet);if(p.HealthController.IsAlive)throw new ArgumentException("AI 仍存活，请检查无敌设置。");}break;
 case "damage":Damage(w,a);return;
 case "heal":Heal(w,a);return;
 case "effect":Effect(w,a);return;
 case "summon":Summon(w,a);return;
 case "tag":Tag(w,a);return;
 case "rename":Require(a,2);if(a[1].Length is <1 or >64)throw new ArgumentException("名字须为 1～64 字符。");foreach(var p in Ai(w,a[0]))p.Profile.Info.Nickname=a[1];break;
 case "ai":ControlAi(w,a);return;
 case "edit":Require(a,1);if(a[0] is not ("on" or "off"))throw new ArgumentException("/edit on|off");WorldEditor.ChatEdit(a[0]=="on");break;
 case "copy":WorldEditor.ChatAction("copy",a.Length==0?null:Ai(w,a[0]));break;
 case "paste":WorldEditor.ChatAction("paste",destination:a.Length==0?w.MainPlayer.Position:Coordinates(w,a));break;
 case "remove":Require(a,1);if(a[0]=="@selected")WorldEditor.ChatAction("remove");else AiActorControl.Delete(Ai(w,a[0]));break;
 case "time":TimeCommand(a);return;
 case "weather":Weather(a);return;
 case "raid":Raid(w,a);return;
 case "gamerule":GameRule(a);return;
 case "marker":Marker(w,a);return;
 case "navigate":if(a.Length==1&&a[0]=="stop")GroundNavigation.Clear();else{var dest=a.Length==1?Destination(w,a[0]):Coordinates(w,a);GroundNavigation.ChatTarget(dest,string.Join(" ",a));}break;
 case "btr":Require(a,1);if(a[0]=="status"){RaidChat.Output(JsonSerializer.Serialize(BtrControl.Diagnostics()));return;}if(a[0]=="route"&&a.Length==2)BtrControl.RequestDestination(w,a[1]);else if(a[0]=="stop")BtrControl.ChatStop(w);else throw new ArgumentException("/btr status|route <站点ID>|stop");break;
 case "airdrop":if(a.Length!=1||a[0]!="summon")throw new ArgumentException("/airdrop summon · 当前版本在本机位置召唤。");AirdropControl.ChatSummon();break;
 case "spreadplayers":Spread(w,a);return;
 case "give":case "clear":case "item":case "loot":case "repair":case "reload":Inventory(w,name,a);return;
 case "data":Data(w,a);return;
 case "attribute":Attribute(w,a);return;
 case "experience":case "xp":Experience(w,a);return;
 default:throw new ArgumentException("未知命令 /"+name+"；输入 /help 查看帮助。");}RaidChat.Output("已执行 /"+name+"。");}
 static void Require(string[] a,int min){if(a.Length<min)throw new ArgumentException("参数不足；/help <命令> 查看用法。");}
}
