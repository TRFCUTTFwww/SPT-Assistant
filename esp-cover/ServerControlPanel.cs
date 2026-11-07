using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ServerControlPanel {
 static Task<string>? request;static JsonElement data;static bool ready,requested;
 static string status="",group="全部配置",search="",lastAction="",pmc="",scav="";static Vector2 scroll;
 static readonly HashSet<string> invalid=new();
 static readonly Dictionary<string,object?> changes=new();static readonly Dictionary<string,string> drafts=new();
 static readonly MenuRowLayout layout=new();
 static async Task<string> Call(Dictionary<string,object?> payload){string result=await CharacterDataPanel.Call("/local/gear/data","characterData",payload);using var doc=JsonDocument.Parse(result);if(doc.RootElement.TryGetProperty("err",out var err)&&err.GetInt32()!=0)return result;if(payload["action"] as string=="karma")return await CharacterDataPanel.Call("/local/gear/data","characterData",new(){{"action","serverStatus"}});return result;}
 internal static async Task<object> Diagnostics(){
  if(ItemSpawner.InRaid())throw new InvalidOperationException("Return to the menu before checking server controls");
  using var settings=JsonDocument.Parse(await Call(new(){{"action","serverStatus"}}));var root=settings.RootElement;
  if(root.TryGetProperty("err",out var error)&&error.GetInt32()!=0)throw new InvalidOperationException("Server control request failed");
  var server=CharacterDataPanel.Data(root);
  using var profile=JsonDocument.Parse(await CharacterDataPanel.Call("/local/gear/profile","profileManagement",new(){{"action","status"}}));
  if(profile.RootElement.TryGetProperty("err",out error)&&error.GetInt32()!=0)throw new InvalidOperationException("Scav equipment request failed");
  var equipment=CharacterDataPanel.Data(profile.RootElement);
  int Count(string key)=>equipment.TryGetProperty(key,out var rows)&&rows.ValueKind==JsonValueKind.Array?rows.GetArrayLength():0;
  return new{settings=server.GetProperty("settings").GetArrayLength(),groups=server.GetProperty("settings").EnumerateArray().Select(v=>v.GetProperty("group").GetString()).Distinct().Count(),karmaFinite=double.IsFinite(server.GetProperty("pmcKarma").GetDouble())&&double.IsFinite(server.GetProperty("scavKarma").GetDouble()),scavSlots=Count("scavItems"),pmcSlots=Count("pmcItems"),slotFilters=Count("slotFilters"),pages=MenuSections.Pages("服务端"),generationOwner="战局与物资/刷新规则",scavOwner="数据/角色管理/Scav 装备"};
 }
 static void Begin(Dictionary<string,object?> payload){if(request!=null)return;lastAction=payload["action"] as string??"";request=Call(payload);status="处理中…";}
 static async Task<string> Reload(){string result=await CharacterDataPanel.Call("/local/gear/reload","adminHotReload",new(){{"action","reload"}});using var doc=JsonDocument.Parse(result);if(doc.RootElement.TryGetProperty("err",out var err)&&err.GetInt32()!=0)return result;return await Call(new(){{"action","serverStatus"}});}
 static void Pump(){
  if(!requested){requested=true;Begin(new(){{"action","serverStatus"}});}if(request?.IsCompleted!=true)return;
  try{using var doc=JsonDocument.Parse(request.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0)status=root.TryGetProperty("errmsg",out var msg)?msg.GetString()??"操作失败":"操作失败";
   else{data=CharacterDataPanel.Data(root).Clone();ready=true;
    if(lastAction is "serverSave" or "serverStatus"){changes.Clear();drafts.Clear();invalid.Clear();}
    pmc=data.GetProperty("pmcKarma").GetDouble().ToString("G",CultureInfo.InvariantCulture);scav=data.GetProperty("scavKarma").GetDouble().ToString("G",CultureInfo.InvariantCulture);
    status=lastAction=="karma"?"业力已保存，重新登录角色后生效。":lastAction=="serverSave"?"已备份并保存配置；请重启服务端并重新登录生效。":lastAction=="reload"?"管理模块已重载；磁盘配置仍需重启服务端生效。":"已读取服务端配置；配置参数保存后重启服务端并重新登录生效。";
   }
  }catch{status="服务响应无法读取，请刷新。";}request=null;
 }
 internal static void Draw(Rect rect,string page){
  Pump();InterfaceMenu.Label(new(rect.x,rect.y,rect.width,30),page=="业力"?"PMC 与 Scav 业力":"服务端配置",InterfaceMenu.Accent,19);
  bool old=GUI.enabled;GUI.enabled=old&&request==null;
  try{
   float button=(rect.width-24)/4,y=rect.y+40;
   if(InterfaceMenu.Button(new(rect.x,y,button,32),"重载管理模块")){lastAction="reload";request=Reload();status="处理中…";}
   if(InterfaceMenu.Button(new(rect.x+button+8,y,button,32),changes.Count+invalid.Count>0?"撤销并刷新":"刷新"))Begin(new(){{"action","serverStatus"}});
   if(InterfaceMenu.Button(new(rect.x+2*(button+8),y,button,32),"物资与 AI 刷新"))InterfaceMenu.Navigate("战局与物资","刷新规则");
   if(InterfaceMenu.Button(new(rect.x+3*(button+8),y,button,32),"Scav 装备")){ProfileAdminPanel.OpenScav();InterfaceMenu.Navigate("数据","角色管理");}
   if(ready){var body=new Rect(rect.x,rect.y+86,rect.width,Math.Max(60,rect.height-145));if(page=="业力")Karma(body);else Settings(body);}
  }finally{GUI.enabled=old;}
  InterfaceMenu.Paragraph(new(rect.x,rect.yMax-48,rect.width,48),status,null,12);
 }
 static void Settings(Rect r){
  float y=r.y,w=r.width;
  var groups=new[]{"全部配置"}.Concat(data.GetProperty("settings").EnumerateArray().Select(v=>v.GetProperty("group").GetString()!).Distinct()).ToArray();
  if(!groups.Contains(group))group=groups[0];string selected=InterfaceMenu.Tabs(new(r.x,y,w,80),groups,group,out float tabsHeight);if(selected!=group){group=selected;scroll=Vector2.zero;}y+=tabsHeight;
  InterfaceMenu.Paragraph(new(r.x,y,w,44),data.GetProperty("restartRequired").GetBoolean()?"已保存的配置等待服务端重启生效；当前服务端仍可能使用旧值。":"配置参数保存后需重启服务端；重载管理模块只更新辅助功能。",null,12);y+=52;
  string nextSearch=InterfaceMenu.Field(new(r.x,y,w,32),search);if(nextSearch!=search){search=nextSearch;scroll=Vector2.zero;}if(search.Length==0)InterfaceMenu.Label(new(r.x+8,y,w-16,32),"搜索全部分类的配置名称或说明…",null,12);y+=43;
  bool searching=!string.IsNullOrWhiteSpace(search);string query=search.Trim();
  var rows=data.GetProperty("settings").EnumerateArray().Where(v=>searching?(I18n.Text(v.GetProperty("name").GetString()!)+" "+I18n.Text(v.GetProperty("help").GetString()!)+" "+v.GetProperty("id").GetString()).Contains(query,StringComparison.OrdinalIgnoreCase):group=="全部配置"||v.GetProperty("group").GetString()==group).ToArray();
  float footer=54;var view=new Rect(r.x,y,w,Math.Max(25,r.yMax-y-footer));float cell=w-24;bool stacked=cell<620;float textWidth=stacked?cell-24:cell-272;
  layout.Build(rows.Select(row=>{float title=InterfaceMenu.ParagraphHeight(row.GetProperty("name").GetString()!,textWidth,14),help=InterfaceMenu.ParagraphHeight(row.GetProperty("help").GetString()!,textWidth,12);return Math.Max(stacked?140:100,28+title+help+(stacked?84:0));}),10);
  scroll=GUI.BeginScrollView(view,scroll,new(0,0,w-18,Math.Max(view.height-1,layout.Height)));
  var visible=layout.Visible(scroll.y,view.height);
  for(int i=visible.First;i<visible.End;i++)DrawSetting(rows[i],new(0,layout.Tops[i],cell,layout.Heights[i]),stacked,textWidth);
  if(rows.Length==0)InterfaceMenu.Paragraph(new(12,10,cell-24,60),"没有匹配的配置；请清空搜索或切换分类。",null,13);
  GUI.EndScrollView();
  y=r.yMax-footer+10;float saveWidth=Math.Min(235,w*.48f);bool old=GUI.enabled;GUI.enabled=old&&changes.Count>0&&invalid.Count==0;
  if(InterfaceMenu.Button(new(r.x,y,saveWidth,34),"保存已修改的配置",true))Begin(new(){{"action","serverSave"},{"revision",data.GetProperty("revision").GetString()},{"settings",new Dictionary<string,object?>(changes)}});GUI.enabled=old;
  InterfaceMenu.Label(new(r.x+saveWidth+14,y,w-saveWidth-14,34),$"待保存 {changes.Count} 项 · 无效 {invalid.Count} 项",invalid.Count>0?new Color(1,.42f,.38f):InterfaceMenu.Accent,12);
 }
 static void DrawSetting(JsonElement row,Rect card,bool stacked,float textWidth){
  string id=row.GetProperty("id").GetString()!;bool dirty=changes.ContainsKey(id),bad=invalid.Contains(id);
  InterfaceMenu.Surface(card,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.04f),8);
  if(dirty||bad)InterfaceMenu.Surface(new(card.x,card.y+10,3,card.height-20),bad?new Color(1,.42f,.38f):InterfaceMenu.Accent,1);
  float titleHeight=InterfaceMenu.ParagraphHeight(row.GetProperty("name").GetString()!,textWidth,14);
  InterfaceMenu.Paragraph(new(card.x+12,card.y+10,textWidth,titleHeight),row.GetProperty("name").GetString()!,dirty?InterfaceMenu.Accent:null,14);
  InterfaceMenu.Paragraph(new(card.x+12,card.y+18+titleHeight,textWidth,card.height-28-titleHeight-(stacked?84:0)),row.GetProperty("help").GetString()!,null,12);
  float x=stacked?card.x+12:card.xMax-244,y=stacked?card.yMax-75:card.y+12;
  if(row.GetProperty("boolean").GetBoolean()){
   bool original=row.GetProperty("value").GetBoolean(),value=changes.TryGetValue(id,out var edited)?(bool)edited!:original;
   if(InterfaceMenu.Button(new(x,y,142,32),value?"开启":"关闭",value)){if(!value==original)changes.Remove(id);else changes[id]=!value;status="更改尚未保存。";}
  }else{
   string raw=drafts.TryGetValue(id,out var draft)?draft:row.GetProperty("value").GetRawText(),next=InterfaceMenu.Field(new(x,y,142,32),raw);
   if(next!=raw){drafts[id]=next;try{double value=ServerEditRules.Number(next,row.GetProperty("minimum").GetDouble(),row.GetProperty("maximum").GetDouble(),row.GetProperty("integer").GetBoolean(),row.GetProperty("minusOne").GetBoolean());if(value==row.GetProperty("value").GetDouble())changes.Remove(id);else changes[id]=value;invalid.Remove(id);status="更改尚未保存。";}catch(ArgumentException error){changes.Remove(id);invalid.Add(id);status=error.Message;}}
   InterfaceMenu.Label(new(x,y+38,232,25),$"{row.GetProperty("minimum").GetDouble():G} ～ {row.GetProperty("maximum").GetDouble():G}",bad?new Color(1,.42f,.38f):null,11);
  }
  if(InterfaceMenu.Button(new(x+152,y,80,32),"撤销")){changes.Remove(id);drafts.Remove(id);invalid.Remove(id);}
 }
 static void Karma(Rect r){
  InterfaceMenu.Paragraph(new(r.x,r.y,r.width,54),"PMC 业力与 Scav 业力是两个不同系统。修改保存到当前角色；请退出战局，并重新登录角色刷新游戏状态。",null,13);
  var view=new Rect(r.x,r.y+66,r.width,Math.Max(25,r.height-66));float width=r.width-24;bool stacked=width<620;float height=stacked?170:140;
  scroll=GUI.BeginScrollView(view,scroll,new(0,0,r.width-18,height*2+10));
  Row("pmc","PMC 业力（0～1）","原生归一化业力值，0.5 为中间值；不是 Fence 好感。",ref pmc,0,1,new(0,0,width,height),stacked);
  Row("scav","Scav 业力（Fence 好感）","影响 Scav 关系、冷却与 Fence；范围按当前服务端规则读取。",ref scav,data.GetProperty("minimumScavKarma").GetDouble(),data.GetProperty("maximumScavKarma").GetDouble(),new(0,height+10,width,height),stacked);
  GUI.EndScrollView();
 }
 static void Row(string id,string title,string help,ref string raw,double min,double max,Rect card,bool stacked){
  InterfaceMenu.Surface(card,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.04f),8);float textWidth=stacked?card.width-24:card.width-274;
  InterfaceMenu.Label(new(card.x+12,card.y+10,textWidth,30),title,InterfaceMenu.Accent,16);InterfaceMenu.Paragraph(new(card.x+12,card.y+48,textWidth,60),help,null,12);
  float x=stacked?card.x+12:card.xMax-244,y=stacked?card.yMax-49:card.y+14;raw=InterfaceMenu.Field(new(x,y,142,33),raw);
  bool old=GUI.enabled;GUI.enabled=old&&!ItemSpawner.InRaid();if(InterfaceMenu.Button(new(x+152,y,80,33),"保存"))try{double value=ServerEditRules.Number(raw,min,max);Begin(new(){{"action","karma"},{"id",id},{"value",value},{"expectedValue",data.GetProperty(id=="pmc"?"pmcKarma":"scavKarma").GetDouble()}});}catch(ArgumentException error){status=error.Message;}GUI.enabled=old;
 }
}
