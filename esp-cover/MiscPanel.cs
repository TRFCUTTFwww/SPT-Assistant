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
 static bool commandRequest;
 static bool requested,syncSecure;static string revision="",status="";static Vector2 scroll;
 static string[] secureTemplates=Array.Empty<string>();static bool choosingSecure;
 static readonly MenuRowLayout layout=new();
 static readonly (string Key,string Name,string Help)[] rows={
  ("DeveloperTraderEnabled","启用开发者商人（需重启服务端）",""),
  ("FunItemsEnabled","启用趣味物品（需重启服务端）","控制 MC／ProjectE 趣味物品的售卖、兑换与能力，重启服务端后生效。"),
  ("GoodBrother","背叛者","Scav 出战时，攻击或击杀 Scav、BOSS 造成的 Fence 好感扣分改为等额加分。"),
  ("DiplomaticImmunity","外交豁免权","Scav 出战时，普通 Scav 与 BOSS（含保镖）将你视为中立，攻击或击杀后也不转为敌对。"),
  ("Connections","关系户","允许玩家 Scav 进入实验室、迷宫和破冰船。"),
  ("ScavNoCooldown","随叫随到 · Scav 无冷却","解除当前与后续玩家 Scav 出击冷却。"),
  ("FreeInsurance","人情世故","投保费用为 0。重新打开投保页面后刷新报价。"),
  ("GuaranteedInsurance","客户就是上帝","所有地图（含实验室、迷宫）已投保遗失装备 100% 回保。"),
  ("UnlimitedLightkeeperVisit","有朋自远方来","取消灯塔商人的访问时间限制。"),
  ("SniperBlind","看不见我","阻止边界狙击区锁定及狙击区伤害。"),
  ("ScavSecure","违禁品","为玩家 Scav 配备跨战局保留的独立安全箱；更换箱型前须清空。"),
  ("FreeFlea","诚信经营 · 免手续费","跳蚤市场上架手续费为 0。"),
  (nameof(MiscSettings.FreeTrade),"自由贸易","移除跳蚤市场同时在售的挂单数量上限。"),
  (nameof(MiscSettings.BatchFleaListings),"批量售出","移除跳蚤市场单笔上架的物品数量上限。"),
  ("InstantFleaSales","供不应求","玩家上架的跳蚤市场挂单立即全部售出，通过 Ragman 邮件结算货款与信誉。"),
  ("ClearanceTraders","清仓甩卖（商人）","商人在售商品的货币或交换物需求数量全部改为 1。"),
  ("ClearanceMarket","清仓甩卖（市场）","跳蚤市场 AI 挂单的货币或交换物需求数量全部改为 1。"),
  ("ExpressInsurance","加急件","新产生的保险无需等待回送，在服务端下一次保险检查时发信。"),
  ("UnlimitedTraderPurchases","供大于求（商人）","商人购买无上限"),
  ("UnlimitedMarketStock","供大于求（市场）","跳蚤市场非玩家报价存量为 2147483647，购买不消耗原始库存；关闭后恢复正常存量。"),
  ("OnlineTrading","线上交易","允许在战局中右键物品新增报价，包括背包、容器里的物品。"),
  (nameof(MiscSettings.Smuggler),"走私者","移除单人战局的物品带入上限和非 FIR 物品丢弃数量限制。")
 };
 internal static System.Collections.Generic.IEnumerable<(string Key,string Name)> CommandRows=>rows.Select(r=>(r.Key,r.Name));
 internal static void Command(string[] args){
  if(args.Length==1&&args[0]=="status"){foreach(var r in rows)RaidChat.Output(r.Key+" · "+r.Name+" = "+typeof(MiscSettings).GetProperty(r.Key)!.GetValue(Options));return;}
  var row=rows.FirstOrDefault(r=>r.Key.Equals(args[0],StringComparison.OrdinalIgnoreCase)||r.Name==args[0]);if(row.Key==null)throw new ArgumentException("未知杂项规则，请用 /misc 后按 Tab 选择。");
  if(args.Length==1||args[1]=="status"){RaidChat.Output(row.Name+" = "+typeof(MiscSettings).GetProperty(row.Key)!.GetValue(Options));return;}
  if(args.Length!=2||args[1] is not ("on" or "off"))throw new ArgumentException("/misc <规则> on|off|status");
  if(ItemSpawner.InRaid())throw new ArgumentException("请退出战局后修改杂项规则。");if(request!=null)throw new ArgumentException("上一项设置仍在保存。");
  async Task<string> Save(){using var first=JsonDocument.Parse(await CharacterDataPanel.Call("/local/gear/data","characterData",new(){{"action","miscStatus"}}));var state=CharacterDataPanel.Data(first.RootElement);return await CharacterDataPanel.Call("/local/gear/data","characterData",new(){{"action","miscSave"},{"revision",state.GetProperty("revision").GetString()},{"settings",new Dictionary<string,bool>{{row.Key,args[1]=="on"}}}});}
  request=Save();commandRequest=true;status="正在保存杂项规则…";
 }
 internal static string SearchTerms=>string.Join(" ",rows.Select(row=>row.Key+" "+row.Name+" "+row.Help));
 internal static void Initialize(Plugin plugin){
  MiscNativeProtection.Initialize(plugin);FleaFees.Initialize(plugin);FleaOfferSlots.Initialize(plugin);
  Smuggler.Initialize(plugin);
  plugin.Patches.Patch(AccessTools.Method(typeof(Profile.FenceTraderInfo),"AddStanding"),prefix:new HarmonyMethod(typeof(MiscPanel),nameof(Fence)));
  plugin.Patches.Patch(AccessTools.Method(typeof(SniperFiringZone),"Shoot"),prefix:new HarmonyMethod(typeof(MiscPanel),nameof(Sniper)));
  plugin.Patches.Patch(AccessTools.Method(typeof(SniperFiringZone),"IsPlayerCanBeDamaged"),prefix:new HarmonyMethod(typeof(MiscPanel),nameof(SniperEligible)));
  plugin.Patches.Patch(AccessTools.Method(typeof(EquipmentTab),"Show"),postfix:new HarmonyMethod(typeof(MiscPanel),nameof(EquipmentShown)));
 }
 static void Fence(Profile.FenceTraderInfo __instance,ref double __0,EFenceStandingSource __1){
  if(!Options.GoodBrother||!RaidClock.Active||__1 is not (EFenceStandingSource.ScavKill or EFenceStandingSource.BossKill))return;
  var local=Singleton<GameWorld>.Instance?.MainPlayer;if(local?.Side!=EPlayerSide.Savage||local.Profile?.FenceInfo?.Pointer!=__instance.Pointer)return;
  __0=MiscSettings.StandingDelta(true,true,__0);
 }
 internal static bool SniperProtected(IPlayer? player)=>Options.SniperBlind&&RaidClock.Active&&player!=null&&Singleton<GameWorld>.Instance?.MainPlayer is {} local&&player.ProfileId==local.ProfileId;
 static bool Sniper(IPlayer __0)=>!SniperProtected(__0);
 static bool SniperEligible(IPlayer __0,ref bool __result){if(!SniperProtected(__0))return true;__result=false;return false;}
 internal static bool SniperDamage(EFT.HealthSystem.ActiveHealthController health,EDamageType type)=>type==EDamageType.Sniper&&SniperProtected(health.Player);
 static void EquipmentShown(EquipmentTab __instance,InventoryEquipment __1){if(__1?.GetSlot(EquipmentSlot.SecuredContainer)?.ContainedItem!=null&&__instance._slotViews!=null&&__instance._slotViews.TryGetValue(EquipmentSlot.SecuredContainer,out var view)&&view!=null)view.gameObject.SetActive(true);}
 static void Begin(Dictionary<string,object?> payload){if(request!=null)return;request=CharacterDataPanel.Call("/local/gear/data","characterData",payload);status="处理中…";}
 internal static void Tick(){
  if(syncSecure){status=ScavEquipmentSync.Status;if(!ScavEquipmentSync.Busy)syncSecure=false;}
  if(reading?.IsCompleted==true){if(reading.IsCompletedSuccessfully)ApplyOptions(reading.Result);reading=null;ApplyScavCooldown();}
  if(reading==null&&Time.unscaledTime>=nextRead){nextRead=Time.unscaledTime+2;reading=Task.Run(()=>{try{var path=Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","user","mods","LocalGearMail","misc-settings.json");return File.Exists(path)?JsonSerializer.Deserialize<MiscSettings>(File.ReadAllText(path))??new():new();}catch{return Options;}});}
  if(request?.IsCompleted!=true)return;
  try{using var doc=JsonDocument.Parse(request.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0)status=root.TryGetProperty("errmsg",out var message)?message.GetString()??"操作失败":"操作失败";
   else{var data=CharacterDataPanel.Data(root);ApplyOptions(JsonSerializer.Deserialize<MiscSettings>(data.GetProperty("settings").GetRawText())!);reading=null;nextRead=Time.unscaledTime+2;revision=data.GetProperty("revision").GetString()!;if(data.TryGetProperty("secureTemplates",out var list))secureTemplates=list.EnumerateArray().Select(i=>i.GetString()!).ToArray();status="杂项规则已同步；地图与装备变更请重新登录角色。";if(data.TryGetProperty("scavSecureSync",out var template)&&template.ValueKind==JsonValueKind.String){ScavEquipmentSync.BeginSecure(template.GetString()!);syncSecure=true;status=ScavEquipmentSync.Status;}}
  }catch{status="杂项模块尚未就绪，请重载管理模块后刷新。";}if(commandRequest){RaidChat.Output(status);commandRequest=false;}request=null;ApplyScavCooldown();
 }
 static void ApplyOptions(MiscSettings options){
  bool offerLimitChanged=Options.FreeTrade!=options.FreeTrade;
  Options=options;
  if(offerLimitChanged)FleaOfferSlots.RefreshOpenMarket();
 }
 static void ApplyScavCooldown(){
  if(!Options.ScavNoCooldown)return;
  ScavEquipmentSync.ClearCooldown();
 }
 internal static void Reset(){request=null;reading=null;Options=new();requested=syncSecure=commandRequest=false;revision="";secureTemplates=Array.Empty<string>();choosingSecure=false;}
 internal static void Draw(Rect rect){
  if(!requested){requested=true;Begin(new(){{"action","miscStatus"}});}
  bool enabled=GUI.enabled;GUI.enabled=enabled&&request==null&&!ScavEquipmentSync.Busy;
  try{
   if(InterfaceMenu.Button(new(rect.x,rect.y,100,32),"刷新"))Begin(new(){{"action","miscStatus"}});
   var view=new Rect(rect.x,rect.y+42,rect.width,Math.Max(40,rect.height-98));float width=rect.width-24;bool narrow=width<600;float textWidth=narrow?width-32:width-152;
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
