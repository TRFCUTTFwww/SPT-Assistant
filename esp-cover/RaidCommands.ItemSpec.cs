using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using EFT;
using EFT.InventoryLogic;
namespace LocalSpt5Esp;
internal static partial class RaidCommands {
 sealed record ItemSpec(string Template,int Count,JsonElement? Options);
 static ItemComponentParameters Components(JsonElement o){
  var p=new ItemComponentParameters();foreach(var field in o.EnumerateObject())switch(field.Name){
   case "durability":p.Durability=(float)ChatCommandOptions.Number(o,field.Name,0,float.MaxValue);break;
   case "maxDurability":p.MaxDurability=(float)ChatCommandOptions.Number(o,field.Name,.01,float.MaxValue);break;
   case "medicalResource":p.MedicalResource=(float)ChatCommandOptions.Number(o,field.Name,0,float.MaxValue);break;
   case "foodResource":p.FoodResource=(float)ChatCommandOptions.Number(o,field.Name,0,float.MaxValue);break;
   case "keyRemaining":p.KeyRemaining=(int)ChatCommandOptions.Number(o,field.Name,0,int.MaxValue,true);break;
   case "poisonUses":p.PoisonUses=(float)ChatCommandOptions.Number(o,field.Name,0,float.MaxValue);break;
   case "resource":p.Resource=(float)ChatCommandOptions.Number(o,field.Name,0,float.MaxValue);break;
   case "repairKitResource":p.RepairKitResource=(float)ChatCommandOptions.Number(o,field.Name,0,float.MaxValue);break;
   case "toggledOn":p.ToggledOn=ChatCommandOptions.Bool(o,field.Name);break;
   case "foundInRaid":p.FoundInRaid=ChatCommandOptions.Bool(o,field.Name);break;
  }return p;
 }
 static ItemSpec NestedItem(JsonElement e,bool contents){ChatCommandOptions.Keys(e,contents?new[]{"item","count","options","grid","x","y","rotation"}:new[]{"item","count","options"});string id=Template(ChatCommandOptions.String(e,"item"));int count=e.TryGetProperty("count",out _)?(int)ChatCommandOptions.Number(e,"count",1,100000,true):1;JsonElement? o=e.TryGetProperty("options",out var options)?options:null;if(o is {} v)ChatCommandOptions.Keys(v,ChatCommandOptions.ItemKeys);return new(id,count,o);}
 static Item BuildSpecified(ItemSpec spec,Player owner,int depth=0,bool? inheritedFir=null,BuildBudget? budget=null){
  budget??=new();if(depth>8||++budget.Items>256)throw new ArgumentException("单次物品树最多 256 个节点、8 层。");
  var item=BuildItem(spec.Template,spec.Count);budget.Built.Add(item);bool? fir=spec.Options is {} opt&&opt.TryGetProperty("foundInRaid",out _)?ChatCommandOptions.Bool(opt,"foundInRaid"):inheritedFir;if(fir.HasValue)ItemSpawner.SetCommandFir(item,fir.Value);if(spec.Options is not {} options)return item;
  ChatCommandOptions.Keys(options,ChatCommandOptions.ItemKeys);Components(options).Apply(item,fir);
  if(options.TryGetProperty("attachments",out var attachments)){
   if(attachments.ValueKind!=JsonValueKind.Object)throw new ArgumentException("attachments 需要按槽位名称填写对象。");
   var names=new HashSet<string>();foreach(var entry in attachments.EnumerateObject()){
    if(!names.Add(entry.Name))throw new ArgumentException("附件槽位重复。");var compound=item.TryCast<CompoundItem>();var slot=compound?.Slots?.FirstOrDefault(s=>s?.ID==entry.Name)??item.TryCast<Weapon>()?.Chambers?.FirstOrDefault(s=>s?.ID==entry.Name)??throw new ArgumentException("没有附件槽："+entry.Name);
    if(entry.Value.ValueKind==JsonValueKind.Null){if(slot.Locked||slot.Required)throw new ArgumentException("不能删除固定或必需附件。");if(slot.ContainedItem!=null&&!slot.RemoveItemWithoutRestrictions().Succeeded)throw new ArgumentException("默认附件无法移除。");continue;}
    var child=BuildSpecified(NestedItem(entry.Value,false),owner,depth+1,fir,budget);
    if(slot.Locked&&slot.ContainedItem?.StringTemplateId!=child.StringTemplateId)throw new ArgumentException("固定附件只允许同模板参数编辑。");
    if(slot.ContainedItem!=null&&!slot.RemoveItemWithoutRestrictions().Succeeded)throw new ArgumentException("默认附件无法移除。");
    if(!(slot.Locked?slot.AddWithoutRestrictions(child):slot.Add(child,false,true)).Succeeded)throw new ArgumentException("附件不兼容或冲突："+entry.Name);
   }
  }
  if(options.TryGetProperty("cartridges",out var cartridges)){
   if(cartridges.ValueKind!=JsonValueKind.Array||cartridges.GetArrayLength()>100)throw new ArgumentException("cartridges 需要不超过 100 项的弹药数组。");
   var magazine=item.TryCast<Magazine>()??throw new ArgumentException("cartridges 只适用于弹匣。");
   if(magazine.Cartridges.Count!=0)throw new ArgumentException("弹匣默认已有弹药。");
   foreach(var e in cartridges.EnumerateArray()){var child=BuildSpecified(NestedItem(e,false),owner,depth+1,fir,budget);if(child.TryCast<Ammo>()==null||!magazine.Cartridges.Add(child,false).Succeeded)throw new ArgumentException("弹药不兼容或超过弹匣容量。");}
  }
  if(options.TryGetProperty("contents",out var contents)){
   if(contents.ValueKind!=JsonValueKind.Array||contents.GetArrayLength()>100)throw new ArgumentException("contents 需要不超过 100 项的数组。");
   var bag=item.TryCast<CompoundItem>()??throw new ArgumentException("此物品没有储物格。");if(bag.Grids==null||bag.Grids.Length==0)throw new ArgumentException("此物品没有储物格。");
   foreach(var e in contents.EnumerateArray()){
    var child=BuildSpecified(NestedItem(e,true),owner,depth+1,fir,budget);string? gridId=e.TryGetProperty("grid",out _)?ChatCommandOptions.String(e,"grid"):null;
    bool explicitPosition=e.TryGetProperty("x",out _)||e.TryGetProperty("y",out _)||e.TryGetProperty("rotation",out _),placed=false;
    if(explicitPosition&&(!e.TryGetProperty("x",out _)||!e.TryGetProperty("y",out _)))throw new ArgumentException("指定格子位置必须同时提供 x 和 y。");
    foreach(var grid in bag.Grids){if(grid==null||gridId!=null&&grid.ID!=gridId)continue;
     if(explicitPosition){int x=(int)ChatCommandOptions.Number(e,"x",0,1000,true),y=(int)ChatCommandOptions.Number(e,"y",0,1000,true);int rotation=e.TryGetProperty("rotation",out _)?(int)ChatCommandOptions.Number(e,"rotation",0,1,true):0;var loc=new LocationInGrid(x,y,(ItemRotation)rotation);if(grid.Add(child,loc,false).Succeeded){placed=true;break;}}
     else if(grid.AddAnywhere(child,EErrorHandlingType.Ignore).Succeeded){placed=true;break;}
    }
    if(!placed)throw new ArgumentException("内容物空间不足、位置冲突或过滤器拒绝；整件新物品未发放。");
   }
  }
  if(options.TryGetProperty("dogtag",out var dogtag))ApplyDogtag(item,dogtag);
  if(options.TryGetProperty("weapon",out var weapon)){ChatCommandOptions.Keys(weapon,"values","ignoreAttachments");var gun=item.TryCast<Weapon>()??throw new ArgumentException("weapon 仅适用于枪械。");var values=WeaponValues(weapon);bool ignore=weapon.TryGetProperty("ignoreAttachments",out _)&&ChatCommandOptions.Bool(weapon,"ignoreAttachments");WeaponEditor.CommandTune(owner,gun,values,ignore);}
  return item;
 }
 sealed class BuildBudget:IDisposable{internal int Items;internal readonly List<Item> Built=new();readonly HashSet<IntPtr> kept=new();internal void Keep(Item item){kept.Add(item.Pointer);foreach(var child in LootDisplay.Children(item))Keep(child);}public void Dispose(){foreach(var item in Built)if(!kept.Contains(item.Pointer))WeaponEditor.ForgetRemoved(item);}}
 static Dictionary<string,double> WeaponValues(JsonElement weapon){var result=new Dictionary<string,double>();if(!weapon.TryGetProperty("values",out var values)||values.ValueKind!=JsonValueKind.Object)throw new ArgumentException("weapon.values 需要参数名与数值对象。");foreach(var v in values.EnumerateObject()){if(result.ContainsKey(v.Name))throw new ArgumentException("枪械参数重复。");result.Add(v.Name,ChatCommandOptions.Number(values,v.Name,-float.MaxValue,float.MaxValue));}return result;}
 static void ApplyDogtag(Item item,JsonElement options){ChatCommandOptions.Keys(options,"nickname","level","side","accountId","profileId","killerName","weaponName");var tag=item.GetItemComponent<DogtagComponent>()??throw new ArgumentException("dogtag 仅适用于狗牌。");foreach(var p in options.EnumerateObject())switch(p.Name){case "nickname":tag.Nickname=ChatCommandOptions.String(options,p.Name,64);break;case "level":tag.Level=(int)ChatCommandOptions.Number(options,p.Name,1,10000,true);break;case "side":if(!Enum.TryParse<EPlayerSide>(ChatCommandOptions.String(options,p.Name),true,out var side)||!Enum.IsDefined(side))throw new ArgumentException("side 为 Usec、Bear 或 Savage。");tag.Side=side;break;case "accountId":tag.AccountId=ChatCommandOptions.String(options,p.Name);break;case "profileId":tag.ProfileId=ChatCommandOptions.String(options,p.Name);break;case "killerName":tag.KillerName=ChatCommandOptions.String(options,p.Name,64);break;case "weaponName":tag.WeaponName=ChatCommandOptions.String(options,p.Name);break;}}
}
