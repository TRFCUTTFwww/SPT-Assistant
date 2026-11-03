using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal sealed record AttachmentSlot(string Id,string Name,bool Locked,bool Required,string? DefaultTemplate,HashSet<string> Allowed,HashSet<string> Excluded);
internal sealed record AttachmentCartridge(string Template,int Count,ItemComponentParameters Parameters);
internal sealed class ItemAttachmentNode {
 public ItemAttachmentNode(){}
 public string Template{get;set;}="";
 public ItemComponentParameters Parameters{get;set;}=new();
 public Dictionary<string,ItemAttachmentNode> Children{get;set;}=new(StringComparer.Ordinal);
 public List<AttachmentCartridge> Cartridges{get;set;}=new();
}
internal static class ItemAttachments {
 static ItemAttachmentNode root=new();
 static readonly List<ItemAttachmentNode> path=new();
 static ItemComponentEditor? parameterEditor;
 static string page="附件槽位",activeSlot="",search="";
 static Vector2 scroll;
 static readonly MenuRowLayout rows=new();
 static string rowKey="";
 internal static void Reset(string template){root=NewNode(template,0);path.Clear();path.Add(root);parameterEditor=null;activeSlot=search="";page="附件槽位";scroll=Vector2.zero;}
 static ItemAttachmentNode NewNode(string template,int depth){var node=new ItemAttachmentNode{Template=template};if(depth<8)foreach(var slot in ItemSpawner.SlotsFor(template))if(slot.DefaultTemplate is string child&&child!=template)node.Children[slot.Id]=NewNode(child,depth+1);return node;}
 internal static ItemAttachmentNode Capture(){Commit();return JsonSerializer.Deserialize<ItemAttachmentNode>(JsonSerializer.Serialize(root))!;}
 internal static void Restore(ItemAttachmentNode value){root=JsonSerializer.Deserialize<ItemAttachmentNode>(JsonSerializer.Serialize(value))!;path.Clear();path.Add(root);parameterEditor=null;activeSlot=search="";page="附件槽位";scroll=Vector2.zero;}
 static void Commit(){if(parameterEditor!=null)path[^1].Parameters=parameterEditor.Read();}
 internal static void Draw(Rect rect){
  var node=path[^1];float y=rect.y;
  if(path.Count>1){if(InterfaceMenu.Button(new Rect(rect.x,y,120,30),"返回上一级")){Commit();path.RemoveAt(path.Count-1);parameterEditor=null;activeSlot="";page="附件槽位";scroll=Vector2.zero;return;}InterfaceMenu.Label(new Rect(rect.x+132,y,rect.width-132,30),ItemSpawner.TemplateName(node.Template),InterfaceMenu.Accent,12);y+=40;}
  if(path.Count>1){
   if(InterfaceMenu.Button(new Rect(rect.x,y,(rect.width-8)/2,30),"组件参数",page=="组件参数")){page="组件参数";scroll=Vector2.zero;}
   if(InterfaceMenu.Button(new Rect(rect.x+(rect.width+8)/2,y,(rect.width-8)/2,30),"附件槽位",page=="附件槽位")){Commit();page="附件槽位";scroll=Vector2.zero;}y+=40;
  }
  var body=new Rect(rect.x,y,rect.width,Math.Max(30,rect.yMax-y));
  if(page=="组件参数"){
   if(parameterEditor==null){var factory=Singleton<ItemFactory>.Instance??throw new ArgumentException("物品工厂尚未就绪。");var sample=factory.CreateItem(factory.NextId.ToString(),node.Template,null);parameterEditor=new ItemComponentEditor(sample,node.Parameters);}
   scroll=GUI.BeginScrollView(body,scroll,new Rect(0,0,body.width-18,parameterEditor.Height));parameterEditor.Draw(body.width-24);GUI.EndScrollView();return;
  }
  var slots=ItemSpawner.SlotsFor(node.Template);
  var selected=slots.FirstOrDefault(slot=>slot.Id==activeSlot);
  if(selected==null){
   scroll=GUI.BeginScrollView(body,scroll,new Rect(0,0,body.width-18,Math.Max(50,slots.Count*76)));
   if(slots.Count==0)InterfaceMenu.Paragraph(new Rect(0,0,body.width-24,55),"这个组件没有附件槽位。可返回上一级继续配置。",null,12);
   for(int i=0;i<slots.Count;i++){var slot=slots[i];string current=node.Children.TryGetValue(slot.Id,out var child)?ItemSpawner.TemplateName(child.Template):"未安装";if(InterfaceMenu.WrappedButton(new Rect(0,i*76,body.width-24,68),slot.Name+"\n"+current)){activeSlot=slot.Id;search="";scroll=Vector2.zero;rowKey="";}}
   GUI.EndScrollView();return;
  }
  if(InterfaceMenu.Button(new Rect(body.x,body.y,96,30),"返回槽位")){activeSlot="";scroll=Vector2.zero;return;}
  InterfaceMenu.Label(new Rect(body.x+108,body.y,body.width-108,30),selected.Name,InterfaceMenu.Accent,12);
  bool installed=node.Children.TryGetValue(selected.Id,out var currentNode);
  if(InterfaceMenu.Button(new Rect(body.x,body.y+40,(body.width-8)/2,32),installed?"配置已装配组件":"请先选择附件",installed)&&installed){Commit();path.Add(currentNode!);parameterEditor=null;activeSlot="";page="组件参数";scroll=Vector2.zero;return;}
  if(InterfaceMenu.Button(new Rect(body.x+(body.width+8)/2,body.y+40,(body.width-8)/2,32),selected.Locked?"固定组件":"移除附件")&&!selected.Locked){node.Children.Remove(selected.Id);installed=false;}
  if(selected.Locked){InterfaceMenu.Paragraph(new Rect(body.x,body.y+87,body.width,80),"固定组件保留原模板，可以进入“配置已装配组件”修改它的参数。",null,12);return;}
  search=InterfaceMenu.Field(new Rect(body.x,body.y+87,body.width,30),search);
  var matches=ItemSpawner.FindEquipment(search,selected.Allowed,selected.Excluded).ToList();
  var view=new Rect(body.x,body.y+129,body.width,Math.Max(25,body.height-129));string key=node.Template+"|"+selected.Id+"|"+search+"|"+view.width;
  if(key!=rowKey){rowKey=key;rows.Build(matches.Select(item=>InterfaceMenu.ButtonHeight(item.Name,view.width-24)));scroll=Vector2.zero;}
  scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,rows.Height));var visible=rows.Visible(scroll.y,view.height);
  for(int i=visible.First;i<visible.End;i++){var item=matches[i];if(InterfaceMenu.WrappedButton(new Rect(0,rows.Tops[i],view.width-24,rows.Heights[i]),item.Name,installed&&currentNode!.Template==item.Id))node.Children[selected.Id]=NewNode(item.Id,0);}
  GUI.EndScrollView();
 }
 internal static void Apply(Item item,ItemFactory factory,ItemAttachmentNode specification,bool? inheritedFir=null){ApplyChildren(item,factory,specification,inheritedFir,0);}
 static void ApplyChildren(Item parent,ItemFactory factory,ItemAttachmentNode node,bool? inheritedFir,int depth){
  if(depth>12)throw new ArgumentException("附件层级超过限制。");
  if(node.Cartridges.Count>0){var magazine=parent.TryCast<Magazine>()??throw new ArgumentException("弹药预设的父物品不是弹匣。");var slot=magazine.Cartridges;if(slot.Count!=0)throw new ArgumentException("默认弹匣已有弹药。");foreach(var specification in node.Cartridges){var ammo=factory.CreateItem(factory.NextId.ToString(),specification.Template,null)?.TryCast<Ammo>()??throw new ArgumentException("弹药创建失败。");ammo.StackObjectsCount=specification.Count;specification.Parameters.Apply(ammo,inheritedFir);if(!slot.Add(ammo,false).Succeeded)throw new ArgumentException("弹药装填失败。");}}
  foreach(var pair in node.Children){
   var definition=ItemSpawner.SlotsFor(node.Template).FirstOrDefault(slot=>slot.Id==pair.Key)??throw new ArgumentException("物品没有附件槽位："+pair.Key);
   if(!ItemSpawner.Compatible(pair.Value.Template,definition.Allowed,definition.Excluded))throw new ArgumentException("不兼容的附件："+ItemSpawner.TemplateName(pair.Value.Template));
   var compound=parent.TryCast<CompoundItem>();Slot? slot=compound?.Slots?.FirstOrDefault(slot=>slot?.ID==pair.Key);
   if(slot==null&&parent.TryCast<Weapon>() is Weapon weapon)slot=weapon.Chambers?.FirstOrDefault(slot=>slot?.ID==pair.Key);
   if(slot==null)throw new ArgumentException("物品缺少实际附件槽位："+pair.Key);
   if(slot.Locked&&slot.ContainedItem!=null&&slot.ContainedItem.StringTemplateId!=pair.Value.Template)throw new ArgumentException("不能替换固定组件。");
   var child=factory.CreateItem(factory.NextId.ToString(),pair.Value.Template,null)??throw new ArgumentException("附件生成失败。");
   pair.Value.Parameters.Apply(child,inheritedFir);bool? fir=pair.Value.Parameters.FoundInRaid??inheritedFir;
   ApplyChildren(child,factory,pair.Value,fir,depth+1);
   if(slot.ContainedItem!=null&&!slot.RemoveItemWithoutRestrictions().Succeeded)throw new ArgumentException("无法替换默认附件。");
   var result=slot.Locked?slot.AddWithoutRestrictions(child):slot.Add(child,false,true);
   if(!result.Succeeded)throw new ArgumentException("附件安装失败，可能与其他附件冲突。");
  }
 }
 internal static void AppendMail(List<Dictionary<string,object?>> items,string rootRef,ItemAttachmentNode node,bool? inheritedFir=null,int depth=0){
  if(depth>12)throw new ArgumentException("附件层级超过限制。");
  int position=0;foreach(var cartridge in node.Cartridges){var attributes=cartridge.Parameters.Attributes();if(!attributes.ContainsKey("foundInRaid")&&inheritedFir.HasValue)attributes["foundInRaid"]=inheritedFir.Value;items.Add(new Dictionary<string,object?>{{"ref","cartridge_"+items.Count},{"template",cartridge.Template},{"parent",rootRef},{"slot","cartridges"},{"location",position++},{"count",cartridge.Count},{"attributes",attributes}});if(items.Count>100)throw new ArgumentException("整套装备含弹药和附件最多 100 个条目。");}
  foreach(var pair in node.Children){
   var definition=ItemSpawner.SlotsFor(node.Template).FirstOrDefault(slot=>slot.Id==pair.Key)??throw new ArgumentException("物品没有附件槽位。");
   if(!ItemSpawner.Compatible(pair.Value.Template,definition.Allowed,definition.Excluded))throw new ArgumentException("不兼容的附件。");
   string id="part_"+items.Count;var properties=pair.Value.Parameters.Attributes();if(!properties.ContainsKey("foundInRaid")&&inheritedFir.HasValue)properties["foundInRaid"]=inheritedFir.Value;
   var spec=new Dictionary<string,object?>{{"ref",id},{"template",pair.Value.Template},{"parent",rootRef},{"slot",pair.Key},{"count",1}};if(properties.Count>0)spec["attributes"]=properties;items.Add(spec);
   if(items.Count>100)throw new ArgumentException("附件过多，单次最多 100 个物品条目。");
   AppendMail(items,id,pair.Value,pair.Value.Parameters.FoundInRaid??inheritedFir,depth+1);
  }
 }
}
