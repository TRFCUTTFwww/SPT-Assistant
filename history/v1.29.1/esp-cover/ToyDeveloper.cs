using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;
namespace LocalSpt5Esp;
internal enum ToyAction { SpawnAI,AddBuff,TeleportAI,SpawnItem,SetAction,AddRoutePoint,SelectAI,SetBodyHealth,GiveWeapon,LockPose,UnlockPose }
internal static class ToyDeveloper {
 internal const string Toy="66015072e9f84d5680039678";
 sealed record Command(IntPtr World,Vector3 Point,LiveActorReference? Target,ToyAction Action,SceneTemplate? Template,AiBirthPreset Birth,string Item,int Count);
 static ConfigEntry<bool> enabled=null!,noDamage=null!;static ConfigEntry<ToyAction> mode=null!;static ConfigEntry<KeyCode> nextKey=null!;static ConfigEntry<string> ammo=null!,item=null!,quantity=null!;static ConfigEntry<float> cooldown=null!,damage=null!,velocity=null!;
 static readonly Queue<Command> pending=new();static readonly ImpactGate gate=new();static LiveActorReference? picked;static Ammo? cachedAmmo;static string cachedId="",query="",notice="";static Vector2 scroll;static float nextError;
 static ConfigEntry<KeyCode> toggleKey=null!;
 static readonly string[] labels={"生成 AI（已选模板）","命中 AI 添加 Buff","选择／传送 AI","生成物品","命中 AI 设置动作","设置 AI 行动路线","选中命中的 AI","命中 AI 设置部位血量","命中 AI 装备预设枪械","命中 AI 锁定动作","命中 AI 解除锁定"};
 internal static void UseTemplate(){enabled.Value=true;mode.Value=ToyAction.SpawnAI;}
 internal static void Initialize(Plugin plugin){
  const string section="50. 玩具枪工具";
  enabled=FeatureConfig.Bind(section,"Developer toy gun","玩具枪开发者工具",false,"只作用于 20×1 毫米玩具枪；开启后取代旧弹着点生成操作。真实子弹碰撞才执行操作。");
  mode=FeatureConfig.Bind(section,"Developer toy action","玩具枪操作",ToyAction.SpawnAI);
  nextKey=FeatureConfig.Bind(section,"Developer toy next action key","玩具枪切换下一操作键",KeyCode.None);
  toggleKey=FeatureConfig.Bind(section,"Developer toy toggle key","玩具枪开发工具切换键",KeyCode.None);
  noDamage=FeatureConfig.Bind(section,"Developer toy no damage","开发者玩具枪不造成伤害",true);
  cooldown=FeatureConfig.Bind(section,"Developer toy interval","工具最短操作间隔（秒）",.3f,"每次开枪最多执行一次操作。",new AcceptableValueRange<float>(.05f,10));
  ammo=FeatureConfig.Bind(section,"Developer toy ammo","替换玩具枪弹药模板","");velocity=FeatureConfig.Bind(section,"Developer toy velocity","玩具枪弹速倍率",1f,"替换发射的弹丸，不改变枪械原有装填限制。",new AcceptableValueRange<float>(.05f,20));
  damage=FeatureConfig.Bind(section,"Developer toy damage","玩具枪伤害倍率",1f,"开启不造成伤害时此项不生效。",new AcceptableValueRange<float>(0,1000));
  item=FeatureConfig.Bind(section,"Developer toy item","玩具枪物品模板","");quantity=FeatureConfig.Bind(section,"Developer toy item quantity","玩具枪物品数量","1");
  plugin.Patches.Patch(typeof(Shot).GetMethod("HandleCollision"),prefix:new HarmonyMethod(typeof(ToyDeveloper),nameof(BeforeCollision)));plugin.Patches.Patch(typeof(BallisticsCalculator).GetMethod("CreateShot"),prefix:new HarmonyMethod(typeof(ToyDeveloper),nameof(BeforeShot)),postfix:new HarmonyMethod(typeof(ToyDeveloper),nameof(AfterShot)));
 }
 static bool Own(Item? weapon,int raid)=>enabled.Value&&weapon?.StringTemplateId==Toy&&Singleton<GameWorld>.Instance?.MainPlayer?.RaidId==raid;
 static void BeforeShot(ref Ammo __0,int __5,Item __6,ref float __7,int __8){if(!Own(__6,__5)||__8>0)return;try{if(ammo.Value.Length>0){if(cachedAmmo==null||cachedId!=ammo.Value){var factory=Singleton<ItemFactory>.Instance;cachedAmmo=factory?.CreateItem(factory.NextId.ToString(),ammo.Value,null)?.TryCast<Ammo>()??throw new ArgumentException("所选替换模板不是弹药。");cachedId=ammo.Value;}__0=cachedAmmo;}__7*=velocity.Value;}catch(Exception e){notice=e.Message;}}
 static void AfterShot(Shot __result){if(__result==null||!Own(__result.Weapon,__result.PlayerRaidId))return;__result.Damage=noDamage.Value?Math.Max(.01f,__result.Damage):__result.Damage*damage.Value;}
 static void BeforeCollision(Shot __instance){if(__instance!=null&&noDamage.Value&&Own(__instance.Weapon,__instance.PlayerRaidId)){__instance.Damage=0;__instance.ArmorDamage=0;}}
 internal static bool Uses(Item? item)=>enabled!=null&&enabled.Value&&item?.StringTemplateId==Toy;
 static Player? HitActor(GameWorld current,Shot shot){var collider=shot.HitCollider;if(collider==null)return null;var part=collider.GetComponent<BodyPartCollider>()??collider.GetComponent<BaseBallistic>()?.Get(shot.HitPoint)?.TryCast<BodyPartCollider>();var actor=part?.Player?.TryCast<Player>();if(actor==null&&current.PlayersColliders?.TryGetValue(collider,out var registered)==true)actor=registered.TryCast<Player>();actor??=collider.GetComponentInParent<Player>();return actor!=null&&!actor.IsYourPlayer&&(actor.IsAI||actor.AIData?.BotOwner!=null)&&actor.HealthController?.IsAlive==true?actor:null;}
 internal static bool Handle(Shot shot){if(!enabled.Value||shot==null||!Own(shot.Weapon,shot.PlayerRaidId))return false;if(shot.Parent!=null||shot.HitCollider==null)return true;try{var current=Singleton<GameWorld>.Instance!;if(!gate.Accept(current.Pointer.ToInt64(),shot.Weapon.Id,shot.FireIndex,Time.unscaledTime,cooldown.Value))return true;if(pending.Count>=16)throw new ArgumentException("玩具枪操作队列已满。");var target=HitActor(current,shot);var selected=SceneTemplates.Selected;var birth=selected?.Birth??AiSpawnPresets.ToolPayload(mode.Value);pending.Enqueue(new(current.Pointer,shot.HitPoint,target==null?null:new(current,target),mode.Value,selected,birth,item.Value,mode.Value==ToyAction.SpawnItem?ItemSpawnRules.Integer(quantity.Value,1,2000,"物品数量"):1));}catch(Exception e){notice=e.Message;}return true;}
 static Player Require(Command command)=>command.Target?.Resolve()??throw new ArgumentException("此操作需要击中存活 AI。");
 static void Pick(GameWorld current,Player actor){picked=new(current,actor);AiTestControl.CheckForDirector(new[]{actor});notice="已选中 AI："+actor.Profile.Nickname;}
 internal static void Tick(GameWorld? current){if(current==null){Reset();return;}if(!InterfaceMenu.InputBlocked&&toggleKey.Value!=KeyCode.None&&Input.GetKeyDown(toggleKey.Value))enabled.Value=!enabled.Value;if(!InterfaceMenu.InputBlocked&&nextKey.Value!=KeyCode.None&&Input.GetKeyDown(nextKey.Value)){mode.Value=(ToyAction)(((int)mode.Value+1)%labels.Length);FeatureToasts.Show("玩具枪："+labels[(int)mode.Value]);}if(pending.Count==0)return;var command=pending.Dequeue();if(command.World!=current.Pointer)return;
  try{switch(command.Action){
   case ToyAction.SpawnAI:if(command.Template!=null)SceneTemplates.Spawn(current,command.Point,command.Template);else ImpactAiSpawner.SpawnAt(current,command.Point,command.Birth);notice="已提交弹着点 AI 生成。";break;
   case ToyAction.AddBuff:var buffActor=Require(command);if(command.Birth.Buffs.Length==0&&(command.Birth.Effects?.Length??0)==0)throw new ArgumentException("请在出生参数或模板中选择 Buff。");foreach(var type in command.Birth.Buffs)CharacterBuffs.AddBirthBuff(current,buffActor,type,command.Birth.Part,command.Birth.Duration,command.Birth.Strength);if(command.Birth.Effects!=null)foreach(var effect in command.Birth.Effects)CharacterBuffs.AddBirthBuff(current,buffActor,effect.Type,Enum.Parse<EBodyPart>(effect.Part),effect.Seconds,effect.Strength);notice="已添加 Buff。";break;
   case ToyAction.TeleportAI:if(command.Target!=null){Pick(current,command.Target.Resolve());notice+="；下一枪打地面传送。";break;}var moving=picked!=null?new[]{picked.Resolve()}:AiTestControl.PoseSelection;if(moving.Length==0)throw new ArgumentException("先击中 AI 选中，或在列表中勾选 AI。");TeleportControl.DirectorMove(moving,command.Point);notice="已传送 AI。";break;
   case ToyAction.SpawnItem:SpawnItem(current,command);break;
   case ToyAction.SetAction:AiSceneDirector.Apply(current,Require(command),(command.Birth.Scene??AiSceneDirector.Snapshot()) with{Enabled=true});notice="已设置目标 AI 动作。";break;
   case ToyAction.AddRoutePoint:if(command.Target!=null){Pick(current,command.Target.Resolve());break;}if(picked!=null)AiTestControl.CheckForDirector(new[]{picked.Resolve()});AiSceneDirector.AddRoute(command.Point);AiSceneDirector.AppendToSelected(command.Point);notice="已添加路线点。";break;
   case ToyAction.SelectAI:Pick(current,Require(command));break;
   case ToyAction.SetBodyHealth:AiBodyHealth.Apply(Require(command),command.Birth.BodyHealth??throw new ArgumentException("请先设置逐部位血量。"));notice="已设置目标部位血量。";break;
   case ToyAction.GiveWeapon:AiSceneEquipment.Enqueue(current,Require(command),command.Birth.Weapon??AiSceneEquipment.BirthWeapon??throw new ArgumentException("请先选择玩家预设枪械。"));notice="已提交 AI 枪械装备。";break;
   case ToyAction.LockPose:ModelPoseLock.Request(Require(command));notice="已请求 AI 动作锁定。";break;
   case ToyAction.UnlockPose:ModelPoseLock.Release(Require(command));notice="已解除 AI 动作锁定。";break;
  }}catch(Exception e){notice=e.Message;if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+3;Plugin.Self.Log.LogWarning("Developer toy: "+e.Message);FeatureToasts.Show(notice);}}
 }
 static void SpawnItem(GameWorld current,Command command){if(string.IsNullOrWhiteSpace(command.Item))throw new ArgumentException("请选择物品模板。");var factory=Singleton<ItemFactory>.Instance;int left=command.Count,roots=0;while(left>0&&roots<32){var item=factory.CreateItem(factory.NextId.ToString(),command.Item,null);if(command.Count>32*Math.Max(1,item.StackMaxSize))throw new ArgumentException("单次最多生成 32 个物品堆叠，请减少数量。");int amount=Math.Min(left,Math.Max(1,item.StackMaxSize));item.StackObjectsCount=amount;item.SpawnedInSession=true;current.ThrowItem(item,current.MainPlayer.Cast<IPlayer>(),command.Point+Vector3.up*(.15f+roots*.03f),Quaternion.identity,Vector3.zero,Vector3.zero,true,true,0);left-=amount;roots++;}notice="已生成物品 ×"+(command.Count-left);}
 internal static void Reset(){pending.Clear();gate.Reset();picked=null;cachedAmmo=null;cachedId="";}
 internal static void Draw(Rect rect){scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-20,2000));float x=8,y=8,w=rect.width-36,half=(w-12)/2;InterfaceMenu.Label(new Rect(x,y,w,32),"玩具枪开发者工具",InterfaceMenu.Accent,21);y+=46;InterfaceMenu.DrawEntry(enabled,new Rect(x,y,w,82));y+=94;if(InterfaceMenu.Button(new Rect(x,y,w,36),"操作："+labels[(int)mode.Value]+"  ›"))mode.Value=(ToyAction)(((int)mode.Value+1)%labels.Length);y+=48;
  InterfaceMenu.Paragraph(new Rect(x,y,w,68),"AI 模板："+(SceneTemplates.Selected?.Name??"当前出生设置")+"\n模板在导演工具 → AI 模板中保存／选择；Buff、动作、部位血量和枪械也从该模板读取。",InterfaceMenu.Accent,12);y+=80;
  foreach(var entry in new ConfigEntryBase[]{nextKey,noDamage,cooldown,velocity,damage}){InterfaceMenu.DrawEntry(entry,new Rect(x,y,w,78));y+=86;}
  InterfaceMenu.Label(new Rect(x,y,w,26),"搜索替换子弹或生成物品",InterfaceMenu.Accent,15);y+=38;query=InterfaceMenu.Field(new Rect(x,y,w,32),query);y+=44;InterfaceMenu.Label(new Rect(x,y,w,24),"子弹："+(ammo.Value.Length>0?ItemSpawner.TemplateName(ammo.Value):"原玩具枪弹丸"),null,12);y+=30;if(InterfaceMenu.Button(new Rect(x,y,w,30),"恢复原玩具枪子弹"))ammo.Value="";y+=42;
  if(query.Length>0){foreach(var candidate in ItemSpawner.FindAmmo(query).Take(6)){if(InterfaceMenu.Button(new Rect(x,y,w,30),"子弹："+candidate.Name))ammo.Value=candidate.Id;y+=36;}foreach(var candidate in ItemSpawner.FindTemplates(query).Take(8)){if(InterfaceMenu.Button(new Rect(x,y,w,30),"生成物品："+candidate.Name))item.Value=candidate.Id;y+=36;}}
  InterfaceMenu.Label(new Rect(x,y,w,26),"物品："+(item.Value.Length>0?ItemSpawner.TemplateName(item.Value):"未选择"),null,12);y+=34;InterfaceMenu.DrawEntry(quantity,new Rect(x,y,w,82));y+=96;InterfaceMenu.Paragraph(new Rect(x,y,w,92),notice+"\n传送／路线：先击中 AI 选中，再击中地面操作；也可使用 AI 列表勾选。每枪最多触发一次。",InterfaceMenu.Accent,12);GUI.EndScrollView();}
}
