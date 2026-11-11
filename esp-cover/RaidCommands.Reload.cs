using System;
using System.Linq;
using EFT;
using EFT.InventoryLogic;
namespace LocalSpt5Esp;
internal static partial class RaidCommands {
 static void Reload(GameWorld w,string[] a,int limit=100000){Require(a,1);var targets=Resolve(w,a[0]);var slot=a.Length>1?a[1]:"FirstPrimaryWeapon";foreach(var p in targets){var gun=Slot(p,slot).ContainedItem?.TryCast<Weapon>()??throw new ArgumentException("指定槽位不是武器。");if(p.HandsController?.Item?.Pointer==gun.Pointer)throw new ArgumentException("请先收起该枪再补弹，避免与原生装填动画冲突。");var mag=gun.GetCurrentMagazine()??throw new ArgumentException("武器没有弹匣。");var stack=mag.Cartridges??throw new ArgumentException("弹匣没有弹药槽。");int missing=Math.Min(limit,mag.MaxCount-stack.Count);if(missing<=0){RaidChat.Output(Name(p)+" 的弹匣已满。");continue;}string? id=a.Length>2?Template(a[2]):stack.Items?.FirstOrDefault()?.StringTemplateId;if(id==null)throw new ArgumentException("空弹匣需要指定弹药：/reload <目标> <装备槽> item:<弹药模板ID>");var test=BuildItem(id,1);if(test.TryCast<Ammo>()==null||!stack.Add(test,true).Succeeded)throw new ArgumentException("弹药不兼容当前弹匣。");int added=0;while(added<missing){var ammo=BuildItem(id,1);ammo.StackObjectsCount=Math.Min(missing-added,Math.Max(1,ammo.StackMaxSize));var result=stack.Add(ammo,false);if(!result.Succeeded)break;added+=ammo.StackObjectsCount;}p.Profile.Inventory.Equipment.RaiseRefreshEvent(false,true);RaidChat.Output(Name(p)+" 弹匣已补充 "+added+" 发；不改变弹膛和原有混装弹药。");}}
}
