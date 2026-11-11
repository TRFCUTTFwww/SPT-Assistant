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
 internal static void DrawSettings(Rect rect){InterfaceMenu.DrawEntry(enabled!,new Rect(rect.x,rect.y,rect.width,84));InterfaceMenu.Paragraph(new Rect(rect.x,rect.y+100,rect.width,200),"右键打开场景菜单；移动时左键放下，右键或 Esc 取消，滚轮调整距离。\n创建位于菜单顶层，空地也可右键创建。复制与粘贴使用独立快照。\nAI 只允许编辑本机控制的角色；落点必须有可行走地面。物品支持地面散落物及其完整内容。",null,14);InterfaceMenu.Paragraph(new Rect(rect.x,rect.y+310,rect.width,80),notice,InterfaceMenu.Accent,12);}
 internal static void Draw(){
  if(!Active||InterfaceMenu.Open)return;int depth=GUI.depth;GUI.depth=-900;
  try{
   if(dialog.Length==0){InterfaceMenu.Label(new Rect(Screen.width/2f-12,Screen.height/2f-16,24,32),"+",InterfaceMenu.Accent,24);InterfaceMenu.Label(new Rect(Screen.width/2f-260,Screen.height/2f+35,520,30),moving?"左键放下 · 右键 / Esc 取消 · 滚轮调整距离":"编辑模式 · 右键打开菜单",InterfaceMenu.Accent,14);return;}
   KeepCursor();float w=Math.Min(700,Screen.width-24),h=Math.Min(dialog=="context"?490:760,Screen.height-24);var rect=new Rect((Screen.width-w)/2,(Screen.height-h)/2,w,h);InterfaceMenu.Panel(rect);float x=rect.x+18,y=rect.y+14,width=w-36;
   InterfaceMenu.Label(new Rect(x,y,width-90,30),dialog switch{"context"=>"场景编辑","create"=>"创建","rename"=>"重命名",_=>"属性"},InterfaceMenu.Accent,20);
   if(InterfaceMenu.Button(new Rect(rect.xMax-94,y,76,30),"关闭")){CloseDialog();return;}y+=44;
   switch(dialog){case "context":DrawContext(x,y,width);break;case "create":DrawCreate(new Rect(x,y,width,rect.yMax-y-18));break;case "rename":DrawRename(x,y,width);break;case "properties":DrawProperties(new Rect(x,y,width,rect.yMax-y-18));break;}
   var e=Event.current;if(e.isMouse||e.type==EventType.ScrollWheel)e.Use();
  }catch(Exception error){notice=error.Message;CloseDialog();FeatureToasts.Show(notice);}finally{GUI.depth=depth;}
 }
 static bool Row(float x,ref float y,float width,string title,bool available=true){bool old=GUI.enabled;GUI.enabled=old&&available;bool clicked=InterfaceMenu.Button(new Rect(x,y,width,32),title);GUI.enabled=old;y+=39;return clicked;}
 static void DrawContext(float x,float y,float width){
  // Create stays the first top-level action, including when there is no target.
  if(Row(x,ref y,width,"创建")){query=lastQuery="";items=Array.Empty<(string,string)>();Modal("create");return;}
  bool selected=actor!=null||loot!=null;
  if(Row(x,ref y,width,"移动",selected))Run(BeginMove);
  if(Row(x,ref y,width,"复制",selected))Run(Copy);
  if(Row(x,ref y,width,"粘贴",aiCopy!=null||itemCopy!=null))Run(Paste);
  if(actor!=null){if(Row(x,ref y,width,"重命名"))Run(()=>{rename=Actor().Profile.Nickname;Modal("rename");});if(Row(x,ref y,width,"杀死"))Run(Kill);}
  if(Row(x,ref y,width,"删除",selected))Run(Delete);
  if(Row(x,ref y,width,"属性",selected))Run(OpenProperties);
  InterfaceMenu.Paragraph(new Rect(x,y,width,62),notice,InterfaceMenu.Accent,12);
 }
 static void DrawCreate(Rect rect){
  float half=(rect.width-10)/2;
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y,half,32),"AI",createAi)){createAi=true;query="";scroll=Vector2.zero;}
  if(InterfaceMenu.Button(new Rect(rect.x+half+10,rect.y,half,32),"物品",!createAi)){createAi=false;query="";scroll=Vector2.zero;}
  query=InterfaceMenu.Field(new Rect(rect.x,rect.y+42,rect.width,30),query);
  float top=rect.y+82;
  if(createAi){if(InterfaceMenu.Button(new Rect(rect.x,top,rect.width,30),"难度："+new[]{"简单","普通","困难","不可能"}[difficulty]))difficulty=(difficulty+1)%4;top+=40;
   var options=AiSpawnCatalog.Options.Where(o=>o.Enabled&&(query.Length==0||(o.Name+o.Role+o.Group).Contains(query,StringComparison.OrdinalIgnoreCase))).ToArray();var view=new Rect(rect.x,top,rect.width,Math.Max(40,rect.yMax-top-64));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,rect.width-22,options.Length*40));for(int i=0;i<options.Length;i++){var option=options[i];if(InterfaceMenu.Button(new Rect(0,i*40,rect.width-28,32),option.Group+" · "+option.Name))Run(()=>{AiSpawnService.Enqueue(Current(),point,option,difficulty,1,new AiBirthPreset(0,100,false,false,false,0,0,Array.Empty<string>(),EBodyPart.LeftLeg,60,1),"场景编辑");CloseDialog();});}GUI.EndScrollView();
  }else{
   if(items.Length==0||lastQuery!=query){items=ItemSpawner.FindTemplates(query).Take(300).ToArray();lastQuery=query;}
   var view=new Rect(rect.x,top,rect.width,Math.Max(40,rect.yMax-top-64));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,rect.width-22,items.Length*40));for(int i=0;i<items.Length;i++){var option=items[i];if(InterfaceMenu.Button(new Rect(0,i*40,rect.width-28,32),option.Name))Run(()=>{var factory=Singleton<ItemFactory>.Instance;PlaceItem(factory.CreateItem(factory.NextId.ToString(),option.Id,null));CloseDialog();});}GUI.EndScrollView();
  }
  InterfaceMenu.Paragraph(new Rect(rect.x,rect.yMax-60,rect.width,60),createAi?AiSpawnService.Status:"显示前 300 项，请输入名称或模板 ID 缩小范围。",InterfaceMenu.Accent,12);
 }
 static void DrawRename(float x,float y,float width){rename=InterfaceMenu.Field(new Rect(x,y,width,32),rename);if(InterfaceMenu.Button(new Rect(x,y+44,width,32),"应用"))Run(()=>{string name=rename.Trim();if(name.Length is <1 or >64)throw new ArgumentException("名称长度须为 1～64 个字符。");Actor().Profile.Info.Nickname=name;CloseDialog();});}
 static void OpenProperties(){
  health.Clear();componentEditor=null;
  if(actor!=null){var target=Actor();rename=target.Profile.Nickname;yaw=target.transform.eulerAngles.y.ToString("G7",CultureInfo.InvariantCulture);paused=AiTestControl.IsPaused(target);foreach(var pair in AiBodyHealth.Capture(target))health[pair.Key]=(pair.Value.Maximum.ToString("G7",CultureInfo.InvariantCulture),pair.Value.Current.ToString("G7",CultureInfo.InvariantCulture));}
  else{var item=loot!.Resolve().Item;stack=item.StackObjectsCount.ToString(CultureInfo.InvariantCulture);componentEditor=new(item,new ItemComponentParameters());}Modal("properties");
 }
 static void DrawProperties(Rect rect){
  var details=loot!=null?ItemSpawner.TemplateDetails(loot.Resolve().Item.TemplateId.ToString()):Array.Empty<ItemDetails.Detail>();
  float contentHeight=actor!=null?610:260+(componentEditor?.Height??0)+details.Length*26;scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-22,contentHeight));float y=0,w=rect.width-28;
  try{
  if(actor!=null){var target=Actor();InterfaceMenu.Paragraph(new Rect(0,y,w,62),target.ProfileId+"\n"+target.Profile.Info.Settings.Role+" · "+target.Position,null,12);y+=70;
   InterfaceMenu.Label(new Rect(0,y,w,24),"名称");y+=26;rename=InterfaceMenu.Field(new Rect(0,y,w,30),rename);y+=38;
   InterfaceMenu.Label(new Rect(0,y,w,24),"朝向角度");y+=26;yaw=InterfaceMenu.Field(new Rect(0,y,w,30),yaw);y+=38;
   if(InterfaceMenu.Button(new Rect(0,y,w,32),paused?"暂停 AI · 开":"暂停 AI · 关",paused))paused=!paused;y+=42;
   InterfaceMenu.Label(new Rect(0,y,w,24),"身体部位 · 最大血量 / 当前血量");y+=30;
   foreach(string key in health.Keys.ToArray()){var value=health[key];InterfaceMenu.Label(new Rect(0,y,110,30),key,null,12);string max=InterfaceMenu.Field(new Rect(115,y,(w-125)/2,30),value.Max);string cur=InterfaceMenu.Field(new Rect(125+(w-125)/2,y,(w-125)/2,30),value.Current);health[key]=(max,cur);y+=38;}
   if(InterfaceMenu.Button(new Rect(0,y+8,w,34),"应用"))Run(()=>{var current=Actor();string name=rename.Trim();if(name.Length is <1 or >64)throw new ArgumentException("名称长度须为 1～64 个字符。");float angle=ItemSpawnRules.Number(yaw,-36000,36000,"朝向角度");var values=new Dictionary<string,BodyHealthPreset>();foreach(var pair in health)values[pair.Key]=new(ItemSpawnRules.Number(pair.Value.Max,.01f,100000000,"最大血量"),ItemSpawnRules.Number(pair.Value.Current,0,100000000,"当前血量"));AiBodyHealth.Validate(values);current.Profile.Info.Nickname=name;current.Rotation=new Vector2(angle,current.Rotation.y);AiTestControl.SetPaused(new[]{current},paused);AiBodyHealth.Apply(current,values);CloseDialog();});
  }else{
   var item=loot!.Resolve().Item;InterfaceMenu.Paragraph(new Rect(0,y,w,66),ItemSpawner.TemplateName(item.TemplateId.ToString())+"\n"+item.Id+" · "+item.TemplateId,null,12);y+=74;
   foreach(var detail in details){InterfaceMenu.Label(new Rect(0,y,w,24),I18n.Text(detail.Label)+": "+I18n.Text(detail.Value)+" "+detail.Unit,null,12);y+=26;}
   InterfaceMenu.Label(new Rect(0,y,w,24),"堆叠数量");y+=26;stack=InterfaceMenu.Field(new Rect(0,y,w,30),stack);y+=42;GUI.BeginGroup(new Rect(0,y,w,componentEditor!.Height));componentEditor.Draw(w);GUI.EndGroup();y+=componentEditor.Height+16;
   if(InterfaceMenu.Button(new Rect(0,y,w,34),"应用"))Run(()=>{var value=loot!.Resolve().Item;int count=ItemSpawnRules.Integer(stack,1,Math.Max(1,value.StackMaxSize),"堆叠数量");var parameters=componentEditor.Read();var detached=CloneItem(CaptureItem(value));parameters.Apply(detached);parameters.Apply(value);value.StackObjectsCount=count;CloseDialog();});
  }}finally{GUI.EndScrollView();}
 }
}
