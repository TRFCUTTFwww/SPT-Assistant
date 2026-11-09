using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Counters;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using LocalMiscShared;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class MiscPanel {
 internal static MiscSettings Options=new();
 static Task<string>? request;static Task<MiscSettings>? reading;static float nextRead;
 static bool requested,syncSecure;static string revision="",status="";static Vector2 scroll;
 static string[] secureTemplates=Array.Empty<string>();static bool choosingSecure;
 static readonly MenuRowLayout layout=new();
 static readonly (string Key,string Name,string Help)[] rows={
  ("GoodBrother","“好”兄弟","Scav 出战时，攻击或击杀 Scav、BOSS 造成的 Fence 好感扣分改为等额加分。"),
  ("Connections","关系户","允许 Scav 选择实验室、迷宫和破冰船。地图自身的入场物品与开放条件仍需满足；下次准备战局生效。"),
  ("ScavNoCooldown","随叫随到 · Scav 无冷却","开启后立即解除当前 Scav 冷却，后续 Scav 出战无需等待。停用后，新生成的 Scav 恢复正常冷却。"),
  ("FreeInsurance","人情世故","投保费用为 0。重新打开投保页面后刷新报价。"),
  ("GuaranteedInsurance","客户就是上帝","所有地图的已投保遗失装备 100% 回保，包括实验室、迷宫；不模拟物品被拿走。"),
  ("SniperBlind","我没看到","狙击边界区域不再射击本地玩家，不影响地图内的普通 AI 狙击手。"),
  ("ScavSecure","违禁品","Scav 使用独立安全箱，默认 Alpha，可选择箱型；一键售卖、撤离、阵亡和下一名 Scav 保留箱子及其内容，与 PMC 完全分开。更换箱型前须清空。开启后自动同步当前 Scav，重新打开装备页面查看。"),
  ("FreeFlea","诚信经营","跳蚤市场上架手续费为 0；重新打开挂单窗口刷新费用。"),
  ("ExpressInsurance","加急件","新产生的保险无需等待回送；在服务端下一次保险检查时发信，不改变领取期限。")
 };
 internal static void Initialize(Plugin plugin){
  plugin.Patches.Patch(AccessTools.Method(typeof(Profile.FenceTraderInfo),"AddStanding"),prefix:new HarmonyMethod(typeof(MiscPanel),nameof(Fence)));
  plugin.Patches.Patch(AccessTools.Method(typeof(SniperFiringZone),"Shoot"),prefix:new HarmonyMethod(typeof(MiscPanel),nameof(Sniper)));
  plugin.Patches.Patch(AccessTools.Method(typeof(EquipmentTab),"Show"),postfix:new HarmonyMethod(typeof(MiscPanel),nameof(EquipmentShown)));
 }
 static void Fence(Profile.FenceTraderInfo __instance,ref double __0,EFenceStandingSource __1){
  if(!Options.GoodBrother||!RaidClock.Active||__1 is not (EFenceStandingSource.ScavKill or EFenceStandingSource.BossKill))return;
  var local=Singleton<GameWorld>.Instance?.MainPlayer;if(local?.Side!=EPlayerSide.Savage||local.Profile?.FenceInfo?.Pointer!=__instance.Pointer)return;
  __0=MiscSettings.StandingDelta(true,true,__0);
 }
 static bool Sniper(IPlayer __0)=>!(Options.SniperBlind&&RaidClock.Active&&__0?.IsYourPlayer==true);
 static void EquipmentShown(EquipmentTab __instance,InventoryEquipment __1){if(__1?.GetSlot(EquipmentSlot.SecuredContainer)?.ContainedItem!=null)__instance.GetSlotView(EquipmentSlot.SecuredContainer)?.gameObject.SetActive(true);}
 static void Begin(Dictionary<string,object?> payload){if(request!=null)return;request=CharacterDataPanel.Call("/local/gear/data","characterData",payload);status="处理中…";}
 internal static void Tick(){
  if(syncSecure){status=ScavEquipmentSync.Status;if(!ScavEquipmentSync.Busy)syncSecure=false;}
  if(reading?.IsCompleted==true){if(reading.IsCompletedSuccessfully)Options=reading.Result;reading=null;ApplyScavCooldown();}
  if(reading==null&&Time.unscaledTime>=nextRead){nextRead=Time.unscaledTime+2;reading=Task.Run(()=>{try{var path=Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","user","mods","LocalGearMail","misc-settings.json");return File.Exists(path)?JsonSerializer.Deserialize<MiscSettings>(File.ReadAllText(path))??new():new();}catch{return Options;}});}
  if(request?.IsCompleted!=true)return;
  try{using var doc=JsonDocument.Parse(request.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0)status=root.TryGetProperty("errmsg",out var message)?message.GetString()??"操作失败":"操作失败";
   else{var data=CharacterDataPanel.Data(root);Options=JsonSerializer.Deserialize<MiscSettings>(data.GetProperty("settings").GetRawText())!;revision=data.GetProperty("revision").GetString()!;if(data.TryGetProperty("secureTemplates",out var list))secureTemplates=list.EnumerateArray().Select(i=>i.GetString()!).ToArray();status="杂项规则已同步；地图与装备变更请重新登录角色。";if(data.TryGetProperty("scavSecureSync",out var template)&&template.ValueKind==JsonValueKind.String){ScavEquipmentSync.BeginSecure(template.GetString()!);syncSecure=true;status=ScavEquipmentSync.Status;}}
  }catch{status="杂项模块尚未就绪，请重载管理模块后刷新。";}request=null;ApplyScavCooldown();
 }
 static void ApplyScavCooldown(){
  if(!Options.ScavNoCooldown)return;
  var app=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>();
  var session=app?.GetClientBackEndSession()?.TryCast<ClientBackendSession>()??app?.Session?.TryCast<ClientBackendSession>();
  if(session?.ProfileOfPet?.Info is {} info)info.SavageLockTime=0;
 }
 internal static void Reset(){request=null;reading=null;Options=new();requested=syncSecure=false;revision="";secureTemplates=Array.Empty<string>();choosingSecure=false;}
 internal static void Draw(Rect rect){
  if(!requested){requested=true;Begin(new(){{"action","miscStatus"}});}
  bool enabled=GUI.enabled;GUI.enabled=enabled&&request==null&&!ScavEquipmentSync.Busy;
  try{
   if(InterfaceMenu.Button(new(rect.x,rect.y,100,32),"刷新"))Begin(new(){{"action","miscStatus"}});
   InterfaceMenu.Paragraph(new(rect.x+114,rect.y,rect.width-114,42),"特殊待遇，自选规则；所有开关默认关闭。",null,12);
   var view=new Rect(rect.x,rect.y+54,rect.width,Math.Max(40,rect.height-116));float width=rect.width-24;bool narrow=width<600;float textWidth=narrow?width-32:width-152;
   layout.Build(rows.Select(r=>Math.Max(110,InterfaceMenu.ParagraphHeight(r.Help,textWidth,12)+(narrow?112:65))+(r.Key=="ScavSecure"?44+(choosingSecure?secureTemplates.Length*42:0):0)),12);
   scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,rect.width-18,layout.Height));
   try{for(int i=0;i<rows.Length;i++){var row=rows[i];float y=layout.Tops[i],height=layout.Heights[i];InterfaceMenu.Surface(new(0,y,width,height),Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.05f),10);
    bool value=(bool)typeof(MiscSettings).GetProperty(row.Key)!.GetValue(Options)!;
    float extra=row.Key=="ScavSecure"?44+(choosingSecure?secureTemplates.Length*42:0):0;float baseHeight=height-extra;
    InterfaceMenu.Label(new(16,y+10,textWidth-76,30),row.Name,InterfaceMenu.Accent,18);InterfaceMenu.Label(new(16+textWidth-72,y+14,72,24),value?"已启用":"已停用",value?Color.green:InterfaceMenu.Text,12);InterfaceMenu.Paragraph(new(16,y+48,textWidth,baseHeight-55),row.Help,null,12);
    bool old=GUI.enabled;GUI.enabled=old&&!ItemSpawner.InRaid()&&revision.Length>0;
    if(InterfaceMenu.Button(new(narrow?16:width-112,narrow?y+baseHeight-46:y+18,narrow?width-32:96,32),value?"停用此项":"启用此项",value))Begin(new(){{"action","miscSave"},{"revision",revision},{"settings",new Dictionary<string,bool>{{row.Key,!value}}}});
    if(row.Key=="ScavSecure"){
     if(InterfaceMenu.WrappedButton(new(16,y+baseHeight,width-32,34),string.Format(I18n.Text("安全箱：{0} · 选择箱型"),ItemSpawner.TemplateName(Options.ScavSecureTemplate))))choosingSecure=!choosingSecure;
     if(choosingSecure)for(int j=0;j<secureTemplates.Length;j++){string template=secureTemplates[j];if(InterfaceMenu.WrappedButton(new(16,y+baseHeight+42+j*42,width-32,36),ItemSpawner.TemplateName(template),template==Options.ScavSecureTemplate)){Begin(new(){{"action","miscSave"},{"revision",revision},{"settings",new Dictionary<string,string>{{"ScavSecureTemplate",template}}}});choosingSecure=false;break;}}
    }
    GUI.enabled=old;
   }}finally{GUI.EndScrollView();}
  }finally{GUI.enabled=enabled;}
  InterfaceMenu.Paragraph(new(rect.x,rect.yMax-50,rect.width,50),ItemSpawner.InRaid()?"请退出战局后修改杂项规则。":status,null,12);
 }
}
