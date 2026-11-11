using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 static Vector2 scroll;static string query="",lastQuery="",rename="",stack="1",yaw="0";
 static bool createAi=true,paused;static int difficulty=1;
 static (string Id,string Name)[] items=Array.Empty<(string,string)>();
 static readonly Dictionary<string,(string Max,string Current)> health=new();
 static ItemComponentEditor? componentEditor;
 internal static void DrawSettings(Rect rect){
  InterfaceMenu.DrawEntry(enabled!,new Rect(rect.x,rect.y,rect.width,84));InterfaceMenu.DrawEntry(toggleEditor!,new Rect(rect.x,rect.y+94,rect.width,84));InterfaceMenu.DrawEntry(throughWallSelection!,new Rect(rect.x,rect.y+188,rect.width,84));
  InterfaceMenu.DrawEntry(moveInvincible!,new Rect(rect.x,rect.y+282,rect.width,84));
  DrawContextSettings(new Rect(rect.x,rect.y+376,rect.width,Math.Max(100,rect.height-376)));
 }
 internal static void Draw(){
  if(!Active||InterfaceMenu.Open)return;int depth=GUI.depth;bool oldEnabled=GUI.enabled;GUI.depth=-900;
  try{
   if(dialog.Length==0){DrawEditorCrosshair();DrawSelection();return;}
   if(dialog=="context"){DrawContext();return;}
   KeepCursor();GUI.enabled=oldEnabled&&Time.frameCount>dialogFrame;float w=Math.Min(dialog=="rename"?380:actor!=null&&dialog=="properties"?860:700,Screen.width-24),h=Math.Min(dialog=="rename"?174+(SelectionCount>1?38:0):760,Screen.height-24);var rect=new Rect((Screen.width-w)/2,(Screen.height-h)/2,w,h);InterfaceMenu.Panel(rect);float x=rect.x+18,y=rect.y+14,width=w-36;
   InterfaceMenu.Label(new Rect(x,y,width-90,30),dialog switch{"context-settings"=>"自定义右键菜单","context"=>"场景编辑","create"=>"创建","rename"=>"重命名",_=>"属性"},InterfaceMenu.Accent,20);
   if(InterfaceMenu.Button(new Rect(rect.xMax-94,y,76,30),"关闭")){CloseDialog();return;}y+=44;
   if(dialog is "properties" or "rename")DrawTargetNavigation(x,ref y,width);
   switch(dialog){case "context-settings":DrawContextSettings(new Rect(x,y,width,rect.yMax-y-18));break;case "create":DrawCreate(new Rect(x,y,width,rect.yMax-y-18));break;case "rename":DrawRename(x,y,width);break;case "properties":DrawProperties(new Rect(x,y,width,rect.yMax-y-18));break;}
   var e=Event.current;if(e.isMouse||e.type==EventType.ScrollWheel)e.Use();
  }catch(Exception error){notice=error.Message;CloseDialog();FeatureToasts.Show(notice);}finally{GUI.depth=depth;GUI.enabled=oldEnabled;}
 }
 static void DrawEditorCrosshair(){
  if(altCursor||dragging||Cursor.visible||Event.current.type!=EventType.Repaint)return;
  float x=MathF.Round(Screen.width*.5f),y=MathF.Round(Screen.height*.5f);
  InterfaceMenu.Surface(new Rect(x-7,y-2,15,5),new Color(0,0,0,.8f),0);
  InterfaceMenu.Surface(new Rect(x-2,y-7,5,15),new Color(0,0,0,.8f),0);
  InterfaceMenu.Surface(new Rect(x-6,y,13,1),Color.white,0);
  InterfaceMenu.Surface(new Rect(x,y-6,1,13),Color.white,0);
 }
 static void DrawCreate(Rect rect){
  float half=(rect.width-10)/2;
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y,half,32),"AI",createAi)){createAi=true;query="";scroll=Vector2.zero;}
  if(InterfaceMenu.Button(new Rect(rect.x+half+10,rect.y,half,32),"物品",!createAi)){createAi=false;query="";scroll=Vector2.zero;}
  query=InterfaceMenu.Field(new Rect(rect.x,rect.y+42,rect.width,30),query);
  float top=rect.y+82;
  if(createAi){if(InterfaceMenu.Button(new Rect(rect.x,top,rect.width,30),"难度："+new[]{"简单","普通","困难","不可能"}[difficulty]))difficulty=(difficulty+1)%4;top+=40;
   var options=AiSpawnCatalog.Options.Where(o=>o.Enabled&&(query.Length==0||(o.Name+o.Role+o.Group).Contains(query,StringComparison.OrdinalIgnoreCase))).ToArray();var view=new Rect(rect.x,top,rect.width,Math.Max(40,rect.yMax-top-64));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,rect.width-22,options.Length*40));for(int i=0;i<options.Length;i++){var option=options[i];if(InterfaceMenu.Button(new Rect(0,i*40,rect.width-28,32),option.Group+" · "+option.Name))Run(()=>{AiSpawnService.Enqueue(Current(),point,option,difficulty,1,new AiBirthPreset(0,100,false,false,false,0,0,Array.Empty<string>(),EBodyPart.LeftLeg,60,1),"场景编辑");CompleteOperation();});}GUI.EndScrollView();
  }else{
   if(items.Length==0||lastQuery!=query){items=ItemSpawner.FindTemplates(query).Take(300).ToArray();lastQuery=query;}
   var view=new Rect(rect.x,top,rect.width,Math.Max(40,rect.yMax-top-64));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,rect.width-22,items.Length*40));for(int i=0;i<items.Length;i++){var option=items[i];if(InterfaceMenu.Button(new Rect(0,i*40,rect.width-28,32),option.Name))Run(()=>{var factory=Singleton<ItemFactory>.Instance;PlaceItem(factory.CreateItem(factory.NextId.ToString(),option.Id,null));CompleteOperation();});}GUI.EndScrollView();
  }
  InterfaceMenu.Paragraph(new Rect(rect.x,rect.yMax-60,rect.width,60),createAi?AiSpawnService.Status:"显示前 300 项，请输入名称或模板 ID 缩小范围。",InterfaceMenu.Accent,12);
 }
 static void DrawRename(float x,float y,float width){rename=InterfaceMenu.Field(new Rect(x,y,width,32),rename);if(InterfaceMenu.Button(new Rect(x,y+44,width,32),"应用"))Run(()=>{string name=rename.Trim();if(name.Length is <1 or >64)throw new ArgumentException("名称长度须为 1～64 个字符。");Actor().Profile.Info.Nickname=name;if(SelectionCount>1){notice="已应用当前对象。";FeatureToasts.Show(notice);}else CloseDialog();});}
 static void OpenProperties(){ReadProperties();Modal("properties");}
 static void ReadProperties(){
  health.Clear();componentEditor=null;ReleasePropertyEditors();
  if(actor!=null)ReadAiProperties();
  else{var item=loot!.Resolve().Item;stack=item.StackObjectsCount.ToString(CultureInfo.InvariantCulture);componentEditor=new(item,new ItemComponentParameters());}
 }
 static void DrawProperties(Rect rect){
  if(actor!=null){DrawAiProperties(rect);return;}
  var details=loot!=null?ItemSpawner.TemplateDetails(loot.Resolve().Item.TemplateId.ToString()):Array.Empty<ItemDetails.Detail>();
  float contentHeight=actor!=null?610:260+(componentEditor?.Height??0)+details.Length*26;scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-22,contentHeight));float y=0,w=rect.width-28;
  try{
   var item=loot!.Resolve().Item;InterfaceMenu.Paragraph(new Rect(0,y,w,66),ItemSpawner.TemplateName(item.TemplateId.ToString())+"\n"+item.Id+" · "+item.TemplateId,null,12);y+=74;
   foreach(var detail in details){InterfaceMenu.Label(new Rect(0,y,w,24),I18n.Text(detail.Label)+": "+I18n.Text(detail.Value)+" "+detail.Unit,null,12);y+=26;}
   InterfaceMenu.Label(new Rect(0,y,w,24),"堆叠数量");y+=26;stack=InterfaceMenu.Field(new Rect(0,y,w,30),stack);y+=42;GUI.BeginGroup(new Rect(0,y,w,componentEditor!.Height));componentEditor.Draw(w);GUI.EndGroup();y+=componentEditor.Height+16;
   if(InterfaceMenu.Button(new Rect(0,y,w,34),"应用"))Run(()=>{var value=loot!.Resolve().Item;int count=ItemSpawnRules.Integer(stack,1,Math.Max(1,value.StackMaxSize),"堆叠数量");var parameters=componentEditor.Read();var detached=CloneItem(CaptureItem(value));parameters.Apply(detached);parameters.Apply(value);value.StackObjectsCount=count;if(SelectionCount>1){notice="已应用当前对象。";FeatureToasts.Show(notice);}else CloseDialog();});
  }finally{GUI.EndScrollView();}
 }
}
