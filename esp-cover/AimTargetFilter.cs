using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AimTargetFilter {
 static readonly Dictionary<CharacterKind,ConfigEntry<bool>> excluded=new();
 static ConfigEntry<bool> excludeUsec=null!,excludeBear=null!;
 static ConfigEntry<string> roleRules=null!,nameRules=null!,profileRules=null!;
 static AimTargetRules? rules;
 static readonly List<Actor> actors=new();
 static float nextScan;
 static Vector2 scroll;
 static string page="阵营类别",search="",name="";
 sealed record Actor(string Id,string Name,string Role,string Side,CharacterKind Kind,float Distance);
 static readonly Dictionary<string,string> roleNames=new(StringComparer.OrdinalIgnoreCase){["ravangeZryachiyEvent"]="复仇兹拉奇",["infectedAssault"]="感染 Scav",["infectedPmc"]="感染 PMC",["infectedCivil"]="感染平民",["infectedLaborant"]="感染研究员",["infectedTagilla"]="感染大锤 · 镰刀",["bossBully"]="Reshala",["bossKilla"]="Killa",["bossTagilla"]="Tagilla",["bossKojaniy"]="Shturman",["bossGluhar"]="Glukhar",["bossSanitar"]="Sanitar",["bossZryachiy"]="Zryachiy",["bossKnight"]="Knight",["followerBigPipe"]="Big Pipe",["followerBirdEye"]="Birdeye",["bossBoar"]="Kaban",["bossKolontay"]="Kollontay",["bossPartisan"]="Partisan",["sectantPriest"]="邪教祭司",["sectantWarrior"]="邪教战士",["assault"]="普通 Scav",["marksman"]="狙击 Scav",["pmcBot"]="掠夺者",["exUsec"]="游荡者",["pmcUSEC"]="USEC PMC",["pmcBEAR"]="BEAR PMC",["shooterBTR"]="BTR 射手",["gifter"]="圣诞老人",["civilian"]="平民"};
 internal static void Initialize(Plugin plugin){
  ConfigEntry<T> Bind<T>(string key,string label,T value){var entry=plugin.Config.Bind("25. Aim exclusions",key,value,new ConfigDescription("",null,new CnConfig.ConfigurationManagerAttributes{DispName=label,Category="25. 自瞄排除"}));entry.SettingChanged+=(_,_)=>rules=null;return entry;}
  foreach(var kind in Enum.GetValues<CharacterKind>())excluded[kind]=Bind("Exclude "+kind,"排除 "+RolePalette.Name(kind),kind==CharacterKind.Teammate);
  excludeUsec=Bind("Exclude USEC","排除 USEC PMC",false);excludeBear=Bind("Exclude BEAR","排除 BEAR PMC",false);
  roleRules=Bind("Excluded roles","排除角色类型","");nameRules=Bind("Excluded names","排除名称","");profileRules=Bind("Excluded profiles","排除个体 ID","");
 }
 internal static bool Allowed(Player player){
  rules??=new AimTargetRules(excluded.Where(pair=>pair.Value.Value).Select(pair=>pair.Key),excludeUsec.Value,excludeBear.Value,roleRules.Value,nameRules.Value,profileRules.Value);
  return !rules.Excludes(BodyTint.Kind(player),player.Profile?.Side.ToString()??"",player.Profile?.Info?.Settings?.Role.ToString()??"",player.Profile?.Nickname??"",player.ProfileId??"");
 }
 static bool Contains(ConfigEntry<string> entry,string token)=>AimTargetRules.Tokens(entry.Value).Contains(token);
 static void Toggle(ConfigEntry<string> entry,string token){if(string.IsNullOrWhiteSpace(token))return;var values=AimTargetRules.Tokens(entry.Value);if(!values.Remove(token))values.Add(token);entry.Value=AimTargetRules.Join(values);}
 internal static void Draw(Rect rect){
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width,28),"自瞄目标排除",InterfaceMenu.Accent,18);
  InterfaceMenu.Label(new Rect(rect.x,rect.y+30,rect.width,22),"勾选后不再锁定；暴力和静默共用排除规则。",null,12);
  var pages=new[]{"阵营类别","AI 角色类型","具体 AI","名称规则"};int index=0;
  foreach(var item in pages)if(InterfaceMenu.Button(new Rect(rect.x+index++*rect.width/4,rect.y+65,rect.width/4-6,32),item,page==item)){page=item;search="";scroll=Vector2.zero;}
  var body=new Rect(rect.x,rect.y+110,rect.width,rect.height-154);
  if(page=="阵营类别")Categories(body);else if(page=="AI 角色类型")Roles(body);else if(page=="具体 AI")Individuals(body);else Names(body);
  InterfaceMenu.Label(new Rect(rect.x,rect.yMax-30,rect.width,28),$"已排除：类别 {excluded.Count(pair=>pair.Value.Value)} · 角色 {AimTargetRules.Tokens(roleRules.Value).Count} · 名称 {AimTargetRules.Tokens(nameRules.Value).Count} · 个体 {AimTargetRules.Tokens(profileRules.Value).Count}",null,12);
 }
 static void Categories(Rect rect){
  float cell=(rect.width-16)/3;int index=0;
  foreach(var pair in excluded){var row=new Rect(rect.x+(index%3)*(cell+8),rect.y+(index/3)*48,cell,38);if(InterfaceMenu.Button(row,RolePalette.Name(pair.Key)+(pair.Value.Value?" · 已排除":" · 允许"),pair.Value.Value))pair.Value.Value=!pair.Value.Value;index++;}
  float y=rect.y+((index+2)/3)*48+16;
  InterfaceMenu.Label(new Rect(rect.x,y,rect.width,25),"PMC 细分（排除 PMC 类别时，两侧都会排除）",null,12);y+=35;
  foreach(var entry in new[]{excludeUsec,excludeBear}){if(InterfaceMenu.Button(new Rect(rect.x,y,cell,36),(entry==excludeUsec?"USEC":"BEAR")+(entry.Value?" · 已排除":" · 允许"),entry.Value))entry.Value=!entry.Value;rect.x+=cell+8;}
 }
 static void Roles(Rect rect){
  search=InterfaceMenu.Field(new Rect(rect.x,rect.y,rect.width,30),search);
  var roles=Enum.GetNames<WildSpawnType>().Select(role=>(Role:role,Name:roleNames.TryGetValue(role,out var label)?label:role)).Where(item=>search.Length==0||item.Name.Contains(search,StringComparison.OrdinalIgnoreCase)||item.Role.Contains(search,StringComparison.OrdinalIgnoreCase)).OrderBy(item=>item.Name,StringComparer.OrdinalIgnoreCase).ToList();
  var view=new Rect(rect.x,rect.y+42,rect.width,rect.height-42);scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,roles.Count*43));
  for(int i=0;i<roles.Count;i++){var role=roles[i];bool blocked=Contains(roleRules,role.Role);if(InterfaceMenu.Button(new Rect(0,i*43,view.width-24,36),role.Name+"  /  "+role.Role+(blocked?" · 已排除":""),blocked))Toggle(roleRules,role.Role);}
  GUI.EndScrollView();
 }
 static void Scan(){
  if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+.5f;actors.Clear();
  var world=Singleton<GameWorld>.Instance;var local=world?.MainPlayer;var all=world?.AllAlivePlayersList;if(local==null||all==null)return;
  for(int i=0;i<all.Count;i++){var actor=all[i];if(actor==null||actor.IsYourPlayer||!(actor.IsAI||actor.AIData?.BotOwner!=null)||actor.HealthController==null||!actor.HealthController.IsAlive)continue;
   actors.Add(new(actor.ProfileId??"",actor.Profile?.Nickname??"AI",actor.Profile?.Info?.Settings?.Role.ToString()??"",actor.Profile?.Side.ToString()??"",BodyTint.Kind(actor),Vector3.Distance(local.Position,actor.Position)));
  }actors.Sort((a,b)=>a.Distance.CompareTo(b.Distance));
 }
 static void Individuals(Rect rect){
  Scan();search=InterfaceMenu.Field(new Rect(rect.x,rect.y,rect.width-160,30),search);
  if(InterfaceMenu.Button(new Rect(rect.xMax-150,rect.y,150,30),"清空个体排除"))profileRules.Value="";
  var visible=actors.Where(actor=>search.Length==0||actor.Name.Contains(search,StringComparison.OrdinalIgnoreCase)||actor.Role.Contains(search,StringComparison.OrdinalIgnoreCase)||actor.Id.Contains(search,StringComparison.OrdinalIgnoreCase)).ToList();
  var view=new Rect(rect.x,rect.y+42,rect.width,rect.height-42);scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,Math.Max(40,visible.Count*76)));
  if(visible.Count==0)InterfaceMenu.Label(new Rect(0,0,view.width-24,40),"进入战局后可选择已生成的 AI；也可在“名称规则”中提前设置。");
  for(int i=0;i<visible.Count;i++){var actor=visible[i];float y=i*76,width=view.width-24;bool individual=Contains(profileRules,actor.Id),named=Contains(nameRules,actor.Name);
   InterfaceMenu.Label(new Rect(0,y,width-310,30),$"{actor.Name} · {RolePalette.Name(actor.Kind)} · {actor.Distance:F0}米",InterfaceMenu.Accent);
   InterfaceMenu.Label(new Rect(0,y+32,width-310,25),$"{actor.Role} · {actor.Side} · ID {actor.Id}",null,11);
   if(InterfaceMenu.Button(new Rect(width-300,y+10,142,34),individual?"此 AI · 已排除":"排除此 AI",individual))Toggle(profileRules,actor.Id);
   if(InterfaceMenu.Button(new Rect(width-150,y+10,150,34),named?"同名 AI · 已排除":"排除此名称",named))Toggle(nameRules,actor.Name);
  }GUI.EndScrollView();
 }
 static void Names(Rect rect){
  name=InterfaceMenu.Field(new Rect(rect.x,rect.y,rect.width-160,30),name);
  if(InterfaceMenu.Button(new Rect(rect.xMax-150,rect.y,150,30),"添加排除名称")&&!string.IsNullOrWhiteSpace(name)){var values=AimTargetRules.Tokens(nameRules.Value);values.UnionWith(AimTargetRules.Tokens(name));nameRules.Value=AimTargetRules.Join(values);name="";}
  InterfaceMenu.Label(new Rect(rect.x,rect.y+40,rect.width,25),"按完整名称匹配并保存；同名 AI 都会排除。个体 ID 通常只对应当前生成的 AI。",null,12);
  var valuesList=AimTargetRules.Tokens(nameRules.Value).OrderBy(value=>value,StringComparer.OrdinalIgnoreCase).ToList();var view=new Rect(rect.x,rect.y+80,rect.width,rect.height-80);
  scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,valuesList.Count*43));
  for(int i=0;i<valuesList.Count;i++){var value=valuesList[i];InterfaceMenu.Label(new Rect(0,i*43,view.width-130,34),value);if(InterfaceMenu.Button(new Rect(view.width-120,i*43,96,34),"取消排除"))Toggle(nameRules,value);}
  GUI.EndScrollView();
 }
}
