using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using Local.ModHost;
using UnityEngine;
namespace ProjectE.Client;

public sealed partial class ProjectModule {
 GamePlayerOwner? gameOwner;float nextToolUse;TerminalInventory? terminal;
 ConfigEntry<KeyCode> chargeKey=null!,skillKey=null!,modeKey=null!;
 ConfigEntry<bool> totemEnabled=null!;
 ConfigEntry<float> totemProtectionSeconds=null!;
 void InitializeTools(Harmony harmony){
  chargeKey=settingsFile!.Bind("Weapons","Charge key",KeyCode.R,"手持 PE 武器：充能；Shift + 此键降级。");
  skillKey=settingsFile.Bind("Weapons","Ability key",KeyCode.G,"手持 PE 武器：发动范围技能（取代此帧原生按键动作）。");
  modeKey=settingsFile.Bind("Weapons","Mode key",KeyCode.M,"红物质剑/拳剑切换仅敌对 AI / 全部 AI，不攻击玩家。");
  totemEnabled=settingsFile.Bind("Totem","Enabled",true,"随身携带不死图腾可自动消耗一枚抵挡致命伤；口袋或弹挂内可绑定快捷栏，不需要按键使用。");
  totemProtectionSeconds=settingsFile.Bind("Totem","触发后免伤秒数",10f,"不死图腾成功消耗后，无视后续伤害的实际秒数。默认 10，支持小数；0 关闭额外免伤期。修改用于下一次触发，保留再生和火焰抵抗效果。");
  harmony.Patch(AccessTools.Method(typeof(GamePlayerOwner),nameof(GamePlayerOwner.TranslateCommand)),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(ToolCommands)){priority=Priority.First});
  harmony.Patch(AccessTools.Method(typeof(Player.KnifeController),nameof(Player.KnifeController.MakeAlternativeKick)),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(ToolRightClick)){priority=Priority.First});
  harmony.Patch(AccessTools.Method(typeof(Player.KnifeController),nameof(Player.KnifeController.MakeKnifeKick)),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(ToolLeftClick)){priority=Priority.First});
  InitializeMatter(harmony);InitializeTotem(harmony);InitializeArtifacts(harmony);terminal=new(context!,harmony,Say);
  context!.OnShutdown(()=>{gameOwner=null;});
 }
 static Player? RaidPlayer(){var game=Singleton<AbstractGame>.Instance;var player=Singleton<GameWorld>.Instance?.MainPlayer;return game?.InRaid==true&&game.TryCast<HideoutGame>()==null&&player?.IsYourPlayer==true&&player.Side!=EPlayerSide.Savage?player:null;}
 internal static Player? CurrentRaidPlayer=>RaidPlayer();
 static bool Playing(Player? player)=>player?.HealthController?.IsAlive==true&&!player.IsInventoryOpened&&Application.isFocused&&!Cursor.visible&&Time.timeScale>0&&active?.table?.Open!=true;
 static bool OwnHands(Player.KnifeController hands)=>active?.context?.IsActive==true&&RaidPlayer() is {} player&&hands._player?.Pointer==player.Pointer;
 static bool ToolCommands(GamePlayerOwner __instance,ref InputNode.ETranslateResult __result){
  var a=active;if(a?.context?.IsActive!=true||__instance.Player?.IsYourPlayer!=true)return true;a.gameOwner=__instance;if(a.terminal?.Opening==true)return true;
  var p=RaidPlayer();if(!Playing(p))return true;string id=p!.HandsController?.GetItem()?.StringTemplateId??"";
  bool use=Gadgets.Handheld(id)&&(Input.GetMouseButton(0)||Input.GetMouseButton(1));
  bool weapon=MatterRules.MaxCharge(id)>0&&(Input.GetKeyDown(a.chargeKey.Value)||Input.GetKeyDown(a.skillKey.Value)||Input.GetKeyDown(a.modeKey.Value));
  if(!use&&!weapon)return true;__result=InputNode.ETranslateResult.BlockAll;return false;
 }
 static bool ToolRightClick(Player.KnifeController __instance){
  if(!OwnHands(__instance)||Gadgets.Find(__instance.GetItem()?.StringTemplateId??"")==null)return true;
  if(Playing(RaidPlayer()))active!.UseTool(__instance.GetItem().StringTemplateId);return false;
 }
 static bool ToolLeftClick(Player.KnifeController __instance)=>!OwnHands(__instance)||Gadgets.Find(__instance.GetItem()?.StringTemplateId??"")==null;
 void Say(string message){context!.Log(LogLevel.Info,message);}
 void UseTool(string id){
  if(Time.unscaledTime<nextToolUse)return;nextToolUse=Time.unscaledTime+.35f;
  if(id==Gadgets.Tablet)table!.SetOpen(true);
  else if(id==Gadgets.Terminal)terminal!.Open(gameOwner);
 }
 void TickTools(){
  var p=RaidPlayer();terminal?.Tick();TickTotem(p);TickArtifacts(p);
  if(!Playing(p))return;string id=p!.HandsController?.GetItem()?.StringTemplateId??"";
  if(Gadgets.Handheld(id)&&Input.GetMouseButtonDown(1))UseTool(id);
  TickMatter(p);
 }
 void DrawTools(){DrawTotem();}
}
