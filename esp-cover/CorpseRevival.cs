using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
namespace LocalSpt5Esp;
internal sealed class CorpseRevival {
 static readonly HashSet<(IntPtr World,IntPtr Body)> reserved=new();
 sealed record Seed(Profile Profile,string Name,string Role,string Side,int Difficulty);
 static readonly Dictionary<string,Seed> history=new(StringComparer.Ordinal);static IntPtr historyWorld;
 internal readonly CorpseReference Source;readonly string fingerprint;readonly ItemDescriptor equipment;
 internal bool Completed{get;private set;}internal string Failure="";
 CorpseRevival(CorpseReference source,ItemDescriptor equipment,string fingerprint){Source=source;this.equipment=equipment;this.fingerprint=fingerprint;}
 internal static void Tick(GameWorld? world){var pointer=world?.Pointer??IntPtr.Zero;if(pointer==historyWorld)return;history.Clear();historyWorld=pointer;}
 internal static void CopyHistory(GameWorld world,string source,string destination){Tick(world);if(!history.ContainsKey(source)){var old=world.GetEverExistedPlayerByProfileId(source);if(old!=null)RecordDeath(old);}if(history.TryGetValue(source,out var seed))history[destination]=seed;}
 internal static void RecordDeath(Player actor){var world=Singleton<GameWorld>.Instance;if(world==null||actor.IsYourPlayer||!(actor.IsAI||actor.AIData?.BotOwner!=null)||actor.Profile?.Info?.Settings==null)return;Tick(world);var profile=actor.Profile;history[actor.ProfileId]=new(profile,profile.Nickname,profile.Info.Settings.Role.ToString(),profile.Side.ToString(),Math.Clamp((int)profile.Info.Settings.BotDifficulty,0,3));}
 static InventoryEquipment Equipment(Corpse body)=>body.ItemOwner?.RootItem?.TryCast<InventoryEquipment>()??throw new ArgumentException("尸体装备尚未就绪。");
 static void ValidateWeapons(InventoryEquipment root){
  foreach(var slot in new[]{EquipmentSlot.FirstPrimaryWeapon,EquipmentSlot.SecondPrimaryWeapon,EquipmentSlot.Holster})
   if(root.GetSlot(slot)?.ContainedItem?.TryCast<Weapon>()!=null)return;
  throw new ArgumentException("尸体没有可用枪械，无法安全初始化 AI；请先保留至少一把枪再复活。原尸体保留。");
 }
 static string Fingerprint(Item root){var values=new List<string>();var seen=new HashSet<IntPtr>();void Walk(Item item,int depth){if(depth>24||values.Count>=4096||!seen.Add(item.Pointer))throw new ArgumentException("尸体物品层级异常或过多。");values.Add(item.Id+"|"+item.StackObjectsCount+"|"+item.Version+"|"+JsonSerializer.Serialize(LoadoutPlans.Parameters(item)));foreach(var child in LootDisplay.Children(item))Walk(child,depth+1);}Walk(root,0);values.Sort(StringComparer.Ordinal);return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",values))));}
 internal static void Enqueue(GameWorld world,CorpseReference source){
  var body=source.Resolve();if(!reserved.Add((source.World,source.Pointer)))throw new ArgumentException("这具尸体已经在复活队列中。");
  try{
   Tick(world);if(!history.TryGetValue(source.Profile,out var seed)){var old=world.GetEverExistedPlayerByProfileId(source.Profile);if(old==null||old.IsYourPlayer||old.HealthController?.IsAlive==true||!(old.IsAI||old.AIData?.BotOwner!=null))throw new ArgumentException("只能复活本局有原始角色资料的 AI 尸体。");RecordDeath(old);if(!history.TryGetValue(source.Profile,out seed))throw new ArgumentException("原始 AI 资料不可用；更新后新产生的 AI 尸体会自动保留复活资料。");}
   var root=Equipment(body);ValidateWeapons(root);var inventory=seed.Profile.Inventory?.MemberwiseClone().Cast<Inventory>()??throw new ArgumentException("原始物品资料不可用。");inventory.Equipment=root;
   var descriptor=new InventoryDescriptor(inventory,null).Equipment??throw new ArgumentException("无法读取尸体物品资料。");if(descriptor.Id.ToString()!=root.Id)throw new ArgumentException("尸体物品快照与当前库存不一致。");
   var request=new CorpseRevival(source,descriptor,Fingerprint(root));string role=seed.Role,side=seed.Side;
   var option=AiSpawnCatalog.Options.FirstOrDefault(o=>o.Role==role)??new AiSpawnOption("复活",seed.Name,role,side,AiBehaviorRules.BossFamily(role));option=option with{Side=side};
   var appearance=new Dictionary<string,string>();if(body.Customization!=null)foreach(var pair in body.Customization)appearance[pair.Key.ToString()]=pair.Value.ToString();
   var birth=new AiBirthPreset(0,100,false,false,true,0,0,Array.Empty<string>(),EBodyPart.Common,60,1,Appearance:appearance.Count>0?appearance:null,Name:seed.Name);
   AiSpawnService.Enqueue(world,CorpseDisplay.Position(body),option,seed.Difficulty,1,birth,"玩具枪复活 · "+seed.Name,revival:request);
  }catch{reserved.Remove((source.World,source.Pointer));throw;}
 }
 internal void Prepare(Profile profile){
  if(Fingerprint(Equipment(Source.Resolve()))!=fingerprint)throw new ArgumentException("准备期间尸体物品发生变化，请重新射击复活。");
  var seen=new HashSet<IntPtr>();var factory=Singleton<ItemFactory>.Instance??throw new ArgumentException("物品工厂未就绪。");
  void Fresh(ItemDescriptor item,int depth){if(depth>24||seen.Count>=4096||!seen.Add(item.Pointer))throw new ArgumentException("尸体物品描述结构异常。");item.Id=factory.NextId;
   if(item.Slots!=null)foreach(var slot in item.Slots)if(slot.ContainedItem!=null)Fresh(slot.ContainedItem,depth+1);
   if(item.Grids!=null)foreach(var grid in item.Grids)if(grid.ContainedItems!=null)foreach(var child in grid.ContainedItems)if(child.Item!=null)Fresh(child.Item,depth+1);
   if(item.StackSlots!=null)foreach(var stack in item.StackSlots)if(stack.ContainedItems!=null)foreach(var child in stack.ContainedItems)if(child!=null)Fresh(child,depth+1);
  }
  Fresh(equipment,0);var copied=equipment.Deserialize(new Il2CppSystem.Collections.Generic.Dictionary<MongoID,Item>()).TryCast<InventoryEquipment>()??throw new ArgumentException("尸体装备重建失败。");
  ValidateWeapons(copied);profile.Inventory.Equipment=copied;
 }
 internal void Complete(GameWorld world,Player actor){
  if(Completed)return;var body=Source.Resolve();if(Fingerprint(Equipment(body))!=fingerprint)throw new ArgumentException("复活期间尸体物品被改变，已撤销新角色，请重试。");
  new LiveActorReference(world,actor).Resolve();world.DestroyLoot(body.Cast<IKillable>());Completed=true;CorpseDisplay.Reset();RaidLoot.Invalidate();FeatureToasts.Show("已复活："+(actor.Profile?.Nickname??Source.Name));Release();
 }
 internal void Release()=>reserved.Remove((Source.World,Source.Pointer));
}
