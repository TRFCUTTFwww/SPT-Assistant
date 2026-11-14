using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
namespace ProjectE.Client;
public sealed partial class ProjectModule {
 sealed class MatterState {public int Charge{get;set;} public bool All{get;set;}}
 Dictionary<string,MatterState> matter=[];float nextAbility;bool abilityDamage;string matterProfile="";
 string MatterFile=>Path.Combine(context!.DataDirectory,"matter-"+matterProfile+".json");
 void InitializeMatter(Harmony harmony){harmony.Patch(AccessTools.Method(typeof(ActiveHealthController),nameof(ActiveHealthController.ApplyDamage),[typeof(EBodyPart),typeof(float),typeof(DamageInfo)]),prefix:new HarmonyMethod(typeof(ProjectModule),nameof(ChargedMelee)));}
 MatterState State(Player p,Item item){
  if(matterProfile!=p.ProfileId){matterProfile=p.ProfileId;matter=[];if(File.Exists(MatterFile))try{matter=JsonSerializer.Deserialize<Dictionary<string,MatterState>>(File.ReadAllText(MatterFile))??[];}catch(Exception e){context!.Log(Local.ModHost.LogLevel.Warning,"Charge state: "+e.Message);}}
  string id=item.Id.ToString();if(!matter.TryGetValue(id,out var state))matter[id]=state=new();state.Charge=Math.Clamp(state.Charge,0,MatterRules.MaxCharge(item.StringTemplateId));return state;
 }
 void SaveMatter(){string next=MatterFile+".next";File.WriteAllText(next,JsonSerializer.Serialize(matter));File.Move(next,MatterFile,true);}
 void TickMatter(Player p){
  var item=p.HandsController?.GetItem();if(item==null||Swords.Find(item.StringTemplateId)==null)return;var state=State(p,item);
  if(Input.GetKeyDown(chargeKey.Value)){bool down=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);state.Charge=MatterRules.Step(state.Charge,MatterRules.MaxCharge(item.StringTemplateId),down);SaveMatter();resources!.Play(down?"peuncharge":"pecharge",volume.Value);Say($"充能 {state.Charge}/{MatterRules.MaxCharge(item.StringTemplateId)} · 范围 {MatterRules.Radius(state.Charge):0.0} 米");}
  if(Input.GetKeyDown(modeKey.Value)&&Swords.Find(item.StringTemplateId)!.Key!="dm"){state.All=!state.All;SaveMatter();Say(state.All?"范围模式：全部 AI（不攻击玩家）":"范围模式：敌对 AI");}
  if(Input.GetKeyDown(skillKey.Value))UseAbility(p,item,state);
 }
 static void ChargedMelee(ActiveHealthController __instance,ref float __1,DamageInfo __2){
  var a=active;var p=RaidPlayer();if(a?.context?.IsActive!=true||a.abilityDamage||p==null||__1<=0||__2==null||__2.OverDamageFrom.HasValue||__2.DamageType!=EDamageType.Melee||__2.Player?.iPlayer?.ProfileId!=p.ProfileId)return;
  var target=__instance.Player;if(target==null||!target.IsAI||target.IsYourPlayer)return;
  var item=p.HandsController?.GetItem();if(item==null||Swords.Find(item.StringTemplateId)==null||__2.Weapon?.Id.ToString()!=item.Id.ToString())return;
  int charge=a.State(p,item).Charge;if(charge>0)__1+=MatterRules.MeleeBonus(charge)+1+charge;
 }
 void UseAbility(Player p,Item item,MatterState state){
  if(Time.unscaledTime<nextAbility)return;
  if(state.Charge==0){Say("先按充能键提升等级，再发动范围技能。");return;}
  nextAbility=Time.unscaledTime+1;var world=Singleton<GameWorld>.Instance;var actors=world?.AllAlivePlayersList;if(actors==null)return;
  var sword=Swords.Find(item.StringTemplateId)!;float radius=MatterRules.Radius(state.Charge);int hits=0;
  // Snapshot: native death removes entries from AllAlivePlayersList.
  foreach(var target in actors.ToArray()){
   var owner=target?.AIData?.BotOwner;bool friendly=owner?.BotsGroup?.IsEnemy(p)!=true;
   if(target==null||owner==null||!MatterRules.EligibleTarget(target.Pointer==p.Pointer,!target.IsAI,target.HealthController?.IsAlive==true,friendly,state.All&&sword.Key!="dm")||Vector3.Distance(target.Position,p.Position)>radius)continue;
   var health=target.ActiveHealthController;if(health==null)continue;
   float damage=sword.Key=="katar"?1000:sword.Damage+state.Charge;
   var info=new DamageInfo{DamageType=EDamageType.Melee,Damage=damage,Player=new PlayerBridge(p).Cast<IObserverToPlayerBridge>(),Weapon=item,MasterOrigin=p.Position,HitPoint=target.Position+Vector3.up,Direction=(target.Position-p.Position).normalized,SourceId="projecte-ability"};
   try{abilityDamage=true;target.LastAggressor=p;health.ApplyDamage(EBodyPart.Chest,damage,info);hits++;}finally{abilityDamage=false;}
  }
  resources!.Play("pecharge",volume.Value);Say($"{(sword.Key=="katar"?"死亡光环":"范围斩击")} · 命中 {hits} 个 AI · 半径 {radius:0.0} 米");
 }
 string MatterHint(string id){var p=RaidPlayer();var item=p?.HandsController?.GetItem();if(item==null||Swords.Find(id)==null)return "";var state=State(p!,item);return $"充能 {state.Charge}/{MatterRules.MaxCharge(id)} · {chargeKey.Value} 充能 / Shift 降级 · {skillKey.Value} 范围技能"+(Swords.Find(id)!.Key=="dm"?"":$" · {modeKey.Value} {(state.All?"全部 AI":"敌对 AI")}");}
}
