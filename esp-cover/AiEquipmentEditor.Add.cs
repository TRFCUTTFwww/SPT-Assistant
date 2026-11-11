using System;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class AiEquipmentEditor {
 static bool addingWeapon;static EquipmentSlot addingSlot;static string weaponQuery="";static Vector2 weaponScroll;
 static (string Id,string Name)[] weaponCandidates=Array.Empty<(string,string)>();static string weaponCache="";
 static void BeginWeaponPicker(EquipmentSlot target){Target();addingSlot=target;weaponQuery="";weaponScroll=Vector2.zero;addingWeapon=true;weaponCache="\\0";notice="";}
 static void AddWeapon(string template){
  var target=Target();if(target.IsYourPlayer||target.AIData?.BotOwner==null)throw new ArgumentException("请选择本机控制的 AI。");
  if(addingSlot is not (EquipmentSlot.FirstPrimaryWeapon or EquipmentSlot.SecondPrimaryWeapon or EquipmentSlot.Holster or EquipmentSlot.Scabbard))throw new ArgumentException("不支持该武器槽。");
  if(AiEquipmentRemoval.Busy||AiSceneEquipment.Busy)throw new ArgumentException("正在处理 AI 装备，请稍候。");
  var destination=target.Profile.Inventory.Equipment.GetSlot(addingSlot);if(destination?.ContainedItem!=null)throw new ArgumentException("该武器槽已有装备，请先删除或使用换枪功能。");if(destination==null)throw new ArgumentException("该武器槽不可用。");
  var factory=Singleton<ItemFactory>.Instance??throw new ArgumentException("物品工厂尚未就绪。");var tree=ItemAttachments.NewNode(template,0);var item=factory.CreateItem(factory.NextId.ToString(),template,null);ItemAttachments.Apply(item,factory,tree);
  if(addingSlot==EquipmentSlot.Scabbard?item.GetItemComponent<KnifeComponent>()==null:item.TryCast<Weapon>()==null)throw new ArgumentException("请选择对应类型的武器。");
  var controller=target.InventoryController;var address=destination.CreateItemAddress();var added=ItemManipulator.Add(item,address,controller,true);if(!added.Succeeded)throw new ArgumentException("该武器不适合此槽位，原装备保持不变。");
  controller.AddAndRaiseEvents(item,address);target.RecalculateEquipmentParams();target.Physical?.MarkWeightAsDirty();var manager=target.AIData.BotOwner.WeaponManager;manager?.UpdateWeaponsList();manager?.Selector?.FindItemsInSlots(true);manager?.Selector?.UpdateWeaponsList();
  if(addingSlot==EquipmentSlot.Scabbard&&manager?.Melee is {} melee)melee.HaveMelee=true;
  addingWeapon=false;ReadSlot();notice="已添加武器："+ItemSpawner.TemplateName(template);FeatureToasts.Show(notice);
 }
 static void DrawWeaponPicker(Rect rect){
  float x=rect.x,y=rect.y,w=rect.width;if(InterfaceMenu.Button(new Rect(x,y,110,30),"返回装备")){addingWeapon=false;return;}InterfaceMenu.Label(new Rect(x+124,y,w-124,30),"添加武器",InterfaceMenu.Accent,17);y+=42;
  weaponQuery=InterfaceMenu.Field(new Rect(x,y,w,30),weaponQuery);y+=42;
  if(weaponCache!=weaponQuery){weaponCache=weaponQuery;weaponScroll=Vector2.zero;weaponCandidates=ItemSpawner.FindTemplates(weaponQuery).Where(p=>ItemCatalog.IsChildOf(p.Id,addingSlot==EquipmentSlot.Scabbard?"5447e1d04bdc2dff2f8b4567":addingSlot==EquipmentSlot.Holster?"5447b5cf4bdc2d65278b4567":"5422acb9af1c889c16000029")).Take(250).ToArray();}var candidates=weaponCandidates;
  var area=new Rect(x,y,w,Math.Max(40,rect.yMax-y-74));weaponScroll=GUI.BeginScrollView(area,weaponScroll,new Rect(0,0,w-22,candidates.Length*44));try{for(int i=0;i<candidates.Length;i++){var entry=candidates[i];if(InterfaceMenu.Button(new Rect(0,i*44,w-28,36),entry.Name))Action(()=>AddWeapon(entry.Id));}}finally{GUI.EndScrollView();}
  InterfaceMenu.Paragraph(new Rect(x,rect.yMax-64,w,60),notice.Length>0?notice:"搜索并添加到空武器槽；自动装配模板默认配件。槽位兼容性由游戏检查。",InterfaceMenu.Accent,12);
 }
}
