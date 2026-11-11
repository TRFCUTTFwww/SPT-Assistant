using System;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 static Item? createItemSample;static ItemComponentEditor? createItemEditor;
 static string createStack="1",createItemError="";static Vector2 createItemScroll;
 static void BeginCreateItem(string template){
  var factory=Singleton<ItemFactory>.Instance??throw new ArgumentException("物品工厂尚未就绪。");
  var sample=factory.CreateItem(factory.NextId.ToString(),template,null)??throw new ArgumentException("无法生成所选物品。");
  createItemSample=sample;createItemEditor=new(sample,new ItemComponentParameters());createStack="1";createItemError="";createItemScroll=Vector2.zero;
 }
 static void DrawCreateItem(Rect rect){
  var sample=createItemSample!;
  if(InterfaceMenu.Button(new(rect.x,rect.y,130,30),"返回列表")){createItemSample=null;createItemEditor=null;return;}
  InterfaceMenu.Paragraph(new(rect.x+142,rect.y,rect.width-142,58),ItemSpawner.TemplateName(sample.TemplateId.ToString())+"\n"+sample.TemplateId,null,12);
  var view=new Rect(rect.x,rect.y+68,rect.width,Math.Max(30,rect.height-182));
  createItemScroll=GUI.BeginScrollView(view,createItemScroll,new Rect(0,0,rect.width-22,createItemEditor!.Height+84));
  try{
   InterfaceMenu.Label(new(0,0,rect.width-28,24),"堆叠数量");createStack=InterfaceMenu.Field(new(0,28,rect.width-28,30),createStack);
   createItemEditor.Draw(rect.width-28,76);
  }finally{GUI.EndScrollView();}
  bool was=GUI.enabled;try{GUI.enabled=was&&!WorldItemPlacement.Busy;if(InterfaceMenu.Button(new(rect.x,rect.yMax-102,rect.width,34),WorldItemPlacement.Busy?"正在加载物品资源…":"应用参数并生成")){
   try{SubmitCreateItem();createItemError="";}catch(Exception error){createItemError=error.Message;FeatureToasts.Show(createItemError);}
  }}finally{GUI.enabled=was;}
  InterfaceMenu.Paragraph(new(rect.x,rect.yMax-60,rect.width,60),createItemError.Length>0?createItemError:WorldItemPlacement.Status,InterfaceMenu.Accent,12);
 }
 static void SubmitCreateItem(){
  var sample=createItemSample??throw new ArgumentException("请先选择物品。");
  int count=ItemSpawnRules.Integer(createStack,1,Math.Max(1,sample.StackMaxSize),"堆叠数量");var settings=createItemEditor!.Read();
  var factory=Singleton<ItemFactory>.Instance;var item=factory.CreateItem(factory.NextId.ToString(),sample.TemplateId.ToString(),null);settings.Apply(item);item.StackObjectsCount=count;
  int opened=dialogFrame;
  WorldItemPlacement.Enqueue(Current(),item,point,()=>{if(dialog=="create"&&dialogFrame==opened&&ReferenceEquals(sample,createItemSample))CompleteOperation();});
 }
}
