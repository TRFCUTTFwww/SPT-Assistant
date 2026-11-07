using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ServerControlPanel {
 static Task<string>? request;static JsonElement data;static bool ready,requested;static string status="",group="保险与修理",search="",lastAction="";static Vector2 scroll;
 static readonly HashSet<string> invalid=new();
 static readonly Dictionary<string,object?> changes=new();static readonly Dictionary<string,string> drafts=new();static string pmc="",scav="";
 static async Task<string> Call(Dictionary<string,object?> payload){string result=await CharacterDataPanel.Call("/local/gear/data","characterData",payload);using var doc=JsonDocument.Parse(result);if(doc.RootElement.TryGetProperty("err",out var err)&&err.GetInt32()!=0)return result;if(payload["action"] as string=="karma")return await CharacterDataPanel.Call("/local/gear/data","characterData",new(){{"action","serverStatus"}});return result;}
 internal static async Task<object> Diagnostics(){
  if(ItemSpawner.InRaid())throw new InvalidOperationException("Return to the menu before checking server controls");
  using var settings=JsonDocument.Parse(await Reload());var root=settings.RootElement;
  if(root.TryGetProperty("err",out var error)&&error.GetInt32()!=0)throw new InvalidOperationException("Server control request failed");
  var server=CharacterDataPanel.Data(root);
  using var profile=JsonDocument.Parse(await CharacterDataPanel.Call("/local/gear/profile","profileManagement",new(){{"action","status"}}));
  if(profile.RootElement.TryGetProperty("err",out error)&&error.GetInt32()!=0)throw new InvalidOperationException("Scav equipment request failed");
  var equipment=CharacterDataPanel.Data(profile.RootElement);
  int Count(string key)=>equipment.TryGetProperty(key,out var rows)&&rows.ValueKind==JsonValueKind.Array?rows.GetArrayLength():0;
  return new{settings=server.GetProperty("settings").GetArrayLength(),groups=server.GetProperty("settings").EnumerateArray().Select(v=>v.GetProperty("group").GetString()).Distinct().Count(),karmaFinite=double.IsFinite(server.GetProperty("pmcKarma").GetDouble())&&double.IsFinite(server.GetProperty("scavKarma").GetDouble()),scavSlots=Count("scavItems"),pmcSlots=Count("pmcItems"),slotFilters=Count("slotFilters")};
 }
 static void Begin(Dictionary<string,object?> payload){if(request!=null)return;lastAction=payload["action"] as string??"";request=Call(payload);status="处理中…";}
 static async Task<string> Reload(){string result=await CharacterDataPanel.Call("/local/gear/reload","adminHotReload",new(){{"action","reload"}});using var doc=JsonDocument.Parse(result);if(doc.RootElement.TryGetProperty("err",out var err)&&err.GetInt32()!=0)return result;return await Call(new(){{"action","serverStatus"}});}
 static void Pump(){if(!requested){requested=true;Begin(new(){{"action","serverStatus"}});}if(request?.IsCompleted!=true)return;try{using var doc=JsonDocument.Parse(request.GetAwaiter().GetResult());var root=doc.RootElement;if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0)status=root.TryGetProperty("errmsg",out var msg)?msg.GetString()??"操作失败":"操作失败";else{data=CharacterDataPanel.Data(root).Clone();ready=true;changes.Clear();drafts.Clear();invalid.Clear();pmc=data.GetProperty("pmcKarma").GetDouble().ToString("G",CultureInfo.InvariantCulture);scav=data.GetProperty("scavKarma").GetDouble().ToString("G",CultureInfo.InvariantCulture);status=lastAction=="karma"?"业力已保存，重新登录角色后生效。":lastAction=="serverSave"?"已备份并保存配置；请重启服务端并重新登录生效。":"已读取服务端配置；配置参数保存后重启服务端并重新登录生效。";}}catch{status="服务响应无法读取，请刷新。";}request=null;}
 internal static void Draw(Rect rect,string page){if(page=="Scav 装备"){ProfileAdminPanel.Draw(rect,true);return;}if(page=="刷新规则"){NextRaidLoot.Draw(rect);return;}
  Pump();float x=rect.x,y=rect.y,w=rect.width;InterfaceMenu.Label(new(x,y,w-265,30),page=="业力"?"PMC 与 Scav 业力":"服务端配置",InterfaceMenu.Accent,19);
  bool old=GUI.enabled;GUI.enabled=old&&request==null;
  if(InterfaceMenu.Button(new(rect.xMax-255,y,155,30),"重载管理模块")){request=Reload();status="处理中…";}
  if(InterfaceMenu.Button(new(rect.xMax-90,y,90,30),changes.Count>0?"撤销并刷新":"刷新"))Begin(new(){{"action","serverStatus"}});y+=43;
  if(ready){if(page=="业力")Karma(new(x,y,w,rect.height-95));else Settings(new(x,y,w,rect.height-95));}
  GUI.enabled=old;InterfaceMenu.Paragraph(new(x,rect.yMax-43,w,43),status,null,12);
 }
 static void Settings(Rect r){float y=r.y,w=r.width;var groups=data.GetProperty("settings").EnumerateArray().Select(v=>v.GetProperty("group").GetString()!).Distinct().ToArray();float width=(w-8*(groups.Length-1))/Math.Max(1,groups.Length);for(int i=0;i<groups.Length;i++)if(InterfaceMenu.Button(new(r.x+i*(width+8),y,width,32),groups[i],group==groups[i])){group=groups[i];scroll=Vector2.zero;}y+=43;
  InterfaceMenu.Paragraph(new(r.x,y,w-245,46),data.GetProperty("restartRequired").GetBoolean()?"已保存的配置等待服务端重启生效；当前服务端仍可能使用旧值。":"只显示当前版本支持的配置项。保存会备份原文件，并保留其他配置。",null,12);
  if(InterfaceMenu.Button(new(r.xMax-235,y,235,35),"保存已修改的配置",changes.Count>0&&invalid.Count==0)&&changes.Count>0&&invalid.Count==0)Begin(new(){{"action","serverSave"},{"revision",data.GetProperty("revision").GetString()},{"settings",new Dictionary<string,object?>(changes)}});y+=56;
  search=InterfaceMenu.Field(new(r.x,y,w,30),search);if(search.Length==0)InterfaceMenu.Label(new(r.x+8,y,w-16,30),"搜索配置名称或说明…",null,12);y+=41;
  var rows=data.GetProperty("settings").EnumerateArray().Where(v=>v.GetProperty("group").GetString()==group&&(search.Length==0||I18n.Text(v.GetProperty("name").GetString()!).Contains(search,StringComparison.OrdinalIgnoreCase)||I18n.Text(v.GetProperty("help").GetString()!).Contains(search,StringComparison.OrdinalIgnoreCase))).ToArray();var view=new Rect(r.x,y,w,Math.Max(30,r.yMax-y));float cell=w-24;scroll=GUI.BeginScrollView(view,scroll,new(0,0,w-18,rows.Length*111));
  for(int i=Math.Max(0,(int)(scroll.y/111)-1);i<Math.Min(rows.Length,(int)((scroll.y+view.height)/111)+2);i++){var row=rows[i];string id=row.GetProperty("id").GetString()!,name=row.GetProperty("name").GetString()!;float top=i*111;InterfaceMenu.Surface(new(0,top,cell,103),Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.04f),8);InterfaceMenu.Label(new(10,top+5,cell-245,30),name,changes.ContainsKey(id)?InterfaceMenu.Accent:null,13);InterfaceMenu.Paragraph(new(10,top+39,cell-235,58),row.GetProperty("help").GetString()!,null,12);
   if(row.GetProperty("boolean").GetBoolean()){bool value=changes.TryGetValue(id,out var edited)?(bool)edited!:row.GetProperty("value").GetBoolean();if(InterfaceMenu.Button(new(cell-215,top+7,122,32),value?"开启":"关闭",value))changes[id]=!value;}
   else{string raw=drafts.TryGetValue(id,out var draft)?draft:row.GetProperty("value").GetRawText();string next=InterfaceMenu.Field(new(cell-215,top+7,122,32),raw);if(next!=raw){drafts[id]=next;try{double value=ServerEditRules.Number(next,row.GetProperty("minimum").GetDouble(),row.GetProperty("maximum").GetDouble());if(row.GetProperty("integer").GetBoolean()&&value!=Math.Truncate(value)||row.GetProperty("minusOne").GetBoolean()&&value<0&&value!=-1)throw new ArgumentException("配置数值无效或超出允许范围。");changes[id]=value;invalid.Remove(id);status="更改尚未保存。";}catch(ArgumentException error){changes.Remove(id);invalid.Add(id);status=error.Message;}}
    InterfaceMenu.Label(new(cell-215,top+47,205,43),$"{row.GetProperty("minimum").GetDouble():G} ～ {row.GetProperty("maximum").GetDouble():G}",null,11);}
   if(InterfaceMenu.Button(new(cell-83,top+7,73,32),"撤销")){changes.Remove(id);drafts.Remove(id);invalid.Remove(id);}
  }GUI.EndScrollView();
 }
 static void Karma(Rect r){InterfaceMenu.Paragraph(new(r.x,r.y,r.width,60),"PMC 业力与 Scav 业力是两个不同系统。修改保存到当前角色；请退出战局，并重新登录角色刷新游戏状态。",null,14);float y=r.y+83;
  Row("pmc","PMC 业力（0～1）","原生归一化业力值，0.5 为中间值；不是 Fence 好感。",ref pmc,0,1,ref y,r);
  Row("scav","Scav 业力（Fence 好感）","影响 Scav 关系、冷却与 Fence；范围按当前服务端规则读取。",ref scav,data.GetProperty("minimumScavKarma").GetDouble(),data.GetProperty("maximumScavKarma").GetDouble(),ref y,r);
 }
 static void Row(string id,string title,string help,ref string raw,double min,double max,ref float y,Rect r){InterfaceMenu.Label(new(r.x,y,r.width-240,33),title,InterfaceMenu.Accent,17);raw=InterfaceMenu.Field(new(r.xMax-230,y,130,33),raw);if(InterfaceMenu.Button(new(r.xMax-90,y,90,33),"保存")&&request==null)try{if(ItemSpawner.InRaid())throw new ArgumentException("请先退出战局。");double value=ServerEditRules.Number(raw,min,max);Begin(new(){{"action","karma"},{"id",id},{"value",value},{"expectedValue",data.GetProperty(id=="pmc"?"pmcKarma":"scavKarma").GetDouble()}});}catch(ArgumentException error){status=error.Message;}InterfaceMenu.Paragraph(new(r.x,y+43,r.width,65),help,null,13);y+=137;}
}
