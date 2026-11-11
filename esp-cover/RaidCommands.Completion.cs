using System;
using System.Collections.Generic;
using System.Linq;
using AssistantChat;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
namespace LocalSpt5Esp;
internal static partial class RaidCommands {
 internal static ChatSuggestion[] Complete(string prefix,ChatPeer[] peers){
  if(!ChatCommandSyntax.IsCommand(prefix))return Array.Empty<ChatSuggestion>();
  string[] words;try{words=ChatCommandSyntax.Tokens(prefix[1..],true);}catch{return Array.Empty<ChatSuggestion>();}
  int start=ChatCommandSyntax.CompletionStart(prefix);bool trailing=start==prefix.Length;
  if(words.Length==0||words.Length==1&&!trailing){string head=words.FirstOrDefault()??"";return Usage.Keys.Where(k=>k.StartsWith(head,StringComparison.OrdinalIgnoreCase)).OrderBy(k=>k).Select(k=>new ChatSuggestion("/"+k,"/"+k,Usage[k])).ToArray();}
  string cmd=words[0].ToLowerInvariant(),token=trailing?"":words[^1];var args=words.Skip(1).ToArray();int at=trailing?args.Length:args.Length-1;
  if(token.StartsWith('{'))return OptionSuggestions(prefix,start,cmd,args,token);
  var named=ChatNamedArguments.Complete(prefix,start,cmd,args,token);if(ChatNamedArguments.Assignment(token)||args.Take(Math.Max(0,args.Length-(trailing?0:1))).Any(ChatNamedArguments.Assignment))return named;
  if(!ChatCommandHints.HasArgument(cmd,args,at))return Array.Empty<ChatSuggestion>();
  string Arg(int n)=>n<args.Length?args[n]:"";
  var details=new Dictionary<string,(string Name,string Detail)>(StringComparer.OrdinalIgnoreCase);
  var candidates=new List<string>();void Add(params string[] values)=>candidates.AddRange(values);
  void Peers()=>candidates.AddRange(peers.SelectMany(p=>new[]{p.Name,p.Profile}));
  void Entities(){Add("@s","@a","@p","@e[type=ai]","@e[type=boss]","@selected");try{var w=Singleton<GameWorld>.Instance;if(w!=null&&RaidClock.Active)foreach(var p in All(w)){foreach(string key in new[]{Name(p),Type(p)+":"+Name(p),"uuid:"+Id(p)}){candidates.Add(key);details[key]=(Name(p),Type(p)+" · "+Role(p)+" · "+UnityEngine.Vector3.Distance(w.MainPlayer.Position,p.Position).ToString("F0")+" 米");}}}catch{}}
  void Places(){Add("~","^","btr");details["~"]=("相对坐标","相对命令执行位置；依次填写 x y z");details["^"]=("镜头局部坐标","相对镜头方向；依次填写 x y z");foreach(var m in markers){string key="marker:"+m.Key;candidates.Add(key);details[key]=(m.Key,"本局标记 · "+m.Value.ToString("F1"));}try{if(RaidClock.Active){var origin=Singleton<GameWorld>.Instance.MainPlayer.Position;foreach(var d in TeleportControl.ChatDestinations()){candidates.Add(d.Key);details[d.Key]=(d.Name,ChatPlaceHints.Kind(d.Key)+" · "+d.Detail+" · "+UnityEngine.Vector3.Distance(origin,d.Position).ToString("F0")+" 米");}}}catch{}}
  ChatSuggestion[]? itemCandidates=null;
  void Items(bool ammo=false){var catalog=ItemSpawner.CommandItems;itemCandidates=catalog.Search(token,ammo).Take(64).Select(p=>new ChatSuggestion(prefix[..start]+ChatCommandSyntax.Quote(catalog.Insert(p)),p.Title,(p.Name==p.Title?"":p.Name+"  ·  ")+p.Id)).ToArray();if(cmd=="give"&&!ammo){try{var presets=CommandPresets().Where(p=>("preset:"+p.Name).Contains(token,StringComparison.OrdinalIgnoreCase)||p.Name.Contains(token,StringComparison.OrdinalIgnoreCase)).Select(p=>new ChatSuggestion(prefix[..start]+ChatCommandSyntax.Quote("preset:"+p.Name),p.Name,"物品预设 · "+ItemSpawner.TemplateName(p.Template)+" · preset:"+p.Name));itemCandidates=presets.Concat(itemCandidates).ToArray();}catch{ /* A broken preset file must not hide ordinary item suggestions. */ } } }
  void Slots()=>candidates.AddRange(Enum.GetNames<EquipmentSlot>());
  switch(cmd){
   case "help":candidates.AddRange(Usage.Keys);break;
   case "msg":case "tell":case "w":if(at==0)Peers();break;
   case "identity":if(at==0)Add("verify");else if(at==1)Peers();break;
   case "chat":Add("reconnect");break;case "permission":case "list":Add(cmd=="list"?"entities":"list");break;
   case "time":if(at==0)Add("set","add","query");else if(Arg(0)=="query")Add("daytime");else if(Arg(0)=="set")Add("day","noon","night","midnight");break;
   case "weather":if(at==0)Add("clear","rain","thunder");break;
   case "gamerule":if(at==0)candidates.AddRange(Rules().Keys);else Add("true","false");break;
   case "raid":if(at==0)Add("time","extract");else if(Arg(0)=="time"&&at==1)Add("query","set","add");else if(Arg(0)=="extract")Add("@s");break;
   case "airdrop":Add("summon");break;
   case "btr":if(at==0)Add("status","route","stop");break;
   case "edit":Add("on","off");break;
   case "marker":if(at==0)Add("set","remove","list");else if(at==1)candidates.AddRange(markers.Keys);else if(Arg(0)=="set")Add("~","^");break;
   case "navigate":if(at==0){Places();Add("stop");}else Add("~","^");break;
   case "tp":case "teleport":Entities();Places();if(at>0)Add("facing");break;
   case "paste":Add("~","^");break;
   case "inspect":if(at==0)Entities();else Add("health","equipment","effects","position");break;
   case "position":case "kill":case "remove":case "copy":case "rename":if(at==0)Entities();break;
   case "heal":if(at==0)Entities();else candidates.AddRange(Enum.GetNames<EBodyPart>());break;
   case "damage":if(at==0)Entities();else if(at==2)candidates.AddRange(Enum.GetNames<EDamageType>());else if(at==3)candidates.AddRange(Enum.GetNames<EBodyPart>().Where(v=>v!="Common"));break;
   case "effect":if(at==0)Add("give","clear");else if(at==1)Entities();else if(at==2)candidates.AddRange(BuffRules.Common.Select(b=>b.Type));else if(at==5)candidates.AddRange(Enum.GetNames<EBodyPart>());break;
   case "summon":if(at==0)candidates.AddRange(AiSpawnCatalog.Options.Where(p=>p.Enabled).Select(p=>p.Role));else if(at<4)Add("~","^");break;
   case "tag":if(at==0)Entities();else if(at==1)Add("add","remove","list");else candidates.AddRange(tags.Values.SelectMany(t=>t));break;
   case "ai":if(at==0)Add("pause","resume","follow","look","pose","relation","anchor","remove","configure");else if(at==1)Entities();else switch(Arg(0)){case "follow":case "look":Add("@s");break;case "pose":Add("standing","crouched","prone","original");break;case "relation":Add("neutral","hostile","default");break;case "anchor":Add("on","off");break;}break;
   case "give":case "clear":if(at==0)Entities();else if(at==1)Items();break;
   case "repair":case "reload":if(at==0)Entities();else if(at==1)Slots();else if(cmd=="reload")Items(true);break;
   case "item":if(at==0)Add("replace","modify","inspect");else if(at==1)Add("entity");else if(at==2)Entities();else if(at==3)Slots();else if(at==4){if(Arg(0)=="replace")Add("with");else Add("durability","maxDurability","medicalResource","foodResource","keyRemaining","poisonUses","resource","repairKitResource","toggledOn","foundInRaid");}else if(at==5){if(Arg(0)=="replace")Items();else if(Arg(4) is "toggledOn" or "foundInRaid")Add("true","false");}break;
   case "loot":if(at==0)Add("spawn","give");else if(Arg(0)=="give"){if(at==1)Entities();else Add("loadout:");}else if(at==4)Items();else if(at<4)Add("~","^");break;
   case "attribute":if(at==0)Entities();else if(at==1)Add("invincible","damageMultiplier");else if(at==2)Add("base");else if(at==3)Add("get","set");else if(Arg(1)=="invincible")Add("true","false");break;
   case "data":if(at==0)Add("get","modify");else if(at==1)Add("entity");else if(at==2)Entities();else if(at==3){if(Arg(0)=="get")Add("health","equipment","effects","position");else Add("name","invincible","damageMultiplier");}else if(at==4)Add("set");else if(at==5)Add("value");else if(Arg(3)=="invincible")Add("true","false");break;
   case "experience":case "xp":if(at==0)Add("query");else if(at==1)Add("@s");else Add("points","levels");break;
   case "spreadplayers":if(at==4)Entities();break;
  }
  if(cmd=="give"&&at>=2)candidates.Add("edit");
  return (itemCandidates??candidates.Distinct(StringComparer.OrdinalIgnoreCase).Where(c=>c.StartsWith(token,StringComparison.OrdinalIgnoreCase)||details.TryGetValue(c,out var d)&&(d.Name+" "+d.Detail).Contains(token,StringComparison.OrdinalIgnoreCase)).OrderBy(c=>c).Take(64).Select(c=>details.TryGetValue(c,out var d)?new ChatSuggestion(prefix[..start]+ChatCommandSyntax.Quote(c),d.Name,d.Detail+" · "+c):new ChatSuggestion(prefix[..start]+ChatCommandSyntax.Quote(c),c,ChatPlaceHints.Description(c))).ToArray()).Concat(named).ToArray();
 }
}
