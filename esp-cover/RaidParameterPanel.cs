using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class RaidParameterPanel {
 static readonly Page ai=new("AI"),raid=new("战局");static bool editAi;
 internal static void Reset(){ai.Reset();raid.Reset();editAi=false;}
 internal static void DrawAi(Rect rect){float w=(rect.width-8)/2;if(InterfaceMenu.Button(new(rect.x,rect.y,w,30),"生成参数说明",!editAi))editAi=false;if(InterfaceMenu.Button(new(rect.x+w+8,rect.y,w,30),"修改参数 / 配置文件",editAi))editAi=true;rect.yMin+=38;if(editAi)ai.Draw(rect);else AiServerInfo.Draw(rect);}
 internal static void DrawRaid(Rect rect)=>raid.Draw(rect);
 sealed class Page(string group){
  static readonly string[] Maps={"bigmap","woods","shoreline","interchange","rezervbase","lighthouse","tarkovstreets","laboratory","labyrinth","factory4_day","factory4_night","sandbox","sandbox_high"};
  string map="bigmap",revision="",notice="",search="",name="默认方案",confirm="",operation="";int presetIndex;bool started,onlyChanged;IntPtr world;GUIStyle? style;bool dirty=true;float layoutWidth=-1;JsonElement[] visible=Array.Empty<JsonElement>();float[] heights=Array.Empty<float>();
  Task<JsonElement>? pending;Task<string?>? filePick;string pickAction="";JsonElement[] rows=Array.Empty<JsonElement>();string[] presets=Array.Empty<string>();Vector2 scroll;readonly Dictionary<string,string> drafts=new();
  internal void Reset(){if(pending is {} task)_=task.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);if(filePick is {} pick)_=pick.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);pending=null;filePick=null;pickAction="";rows=Array.Empty<JsonElement>();drafts.Clear();revision=notice=confirm="";started=false;world=IntPtr.Zero;scroll=Vector2.zero;presets=Array.Empty<string>();presetIndex=0;dirty=true;name=map+"_"+group;}
  void Request(string action){try{
   if(pending!=null||filePick!=null)return;
   if(drafts.Count>0&&action is "read" or "load" or "defaults" or "save" or "delete"){if(confirm!=action){confirm=action;notice=action=="save"?"保存的是已应用参数；请先应用修改，或再次点击以保存服务端当前值。":"还有未应用的输入；再次点击将丢弃输入并继续。";return;}}
   if(action is "delete" or "applyAll"&&confirm!=action){confirm=action;notice=action=="delete"?$"再次点击「删除方案」将从服务端永久删除 {name}。":"再次点击「应用到全部地图」将把本页修改的通用参数同步覆盖所有 13 张地图！";return;}
   var edits=new Dictionary<string,object>();
   if(action is "apply" or "applyAll")foreach(var row in rows){string id=row.GetProperty("id").GetString()!;if(!drafts.TryGetValue(id,out string? text))continue;if(action=="applyAll"&&(id.StartsWith("map/Waves/",StringComparison.Ordinal)||id.StartsWith("map/BossLocationSpawn/",StringComparison.Ordinal)))throw new ArgumentException("批量应用不支持单图波次明细（"+id+"），请先撤销波次输入或用「应用修改」。");if(row.GetProperty("boolean").GetBoolean())edits[id]=bool.Parse(text);else {if(!double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out double v)||!double.IsFinite(v))throw new ArgumentException("请输入有效数字："+row.GetProperty("name").GetString());edits[id]=v;}}
   confirm="";operation=action;pending=WorldEditorClient.Call("parameters",map,parameters:new{operation=action,group,name,revision,edits});notice="正在访问服务端…";
  }catch(Exception e){notice=e.GetBaseException().Message;}}
  void StartExport(){
   if(pending!=null||filePick!=null||rows.Length==0)return;
   pickAction="export";filePick=WindowsDialogs.PickFolder(I18n.Text("选择参数方案导出文件夹"));notice="请在 Windows 窗口中选择导出文件夹…";
  }
  void StartImport(){
   if(pending!=null||filePick!=null||rows.Length==0)return;
   pickAction="import";filePick=WindowsDialogs.PickOpenFile(I18n.Text("选择要导入的参数方案 JSON"));notice="请在 Windows 窗口中选择方案文件…";
  }
  void FinishPick(){
   if(filePick?.IsCompleted!=true)return;var pick=filePick;string action=pickAction;filePick=null;pickAction="";
   try{
    string? chosen=pick.GetAwaiter().GetResult();if(string.IsNullOrWhiteSpace(chosen)){notice="已取消。";return;}
    if(action=="export"){
     string safe=string.Concat((string.IsNullOrWhiteSpace(name)?map+"_"+group:name).Select(c=>char.IsLetterOrDigit(c)||c is '-' or '_' ? c : '_'));
     Directory.CreateDirectory(chosen);string file=Path.Combine(chosen,safe+".json");
     var values=new JsonObject();
     foreach(var row in rows){
      string id=row.GetProperty("id").GetString()!;
      if(drafts.TryGetValue(id,out string? text)){
       if(row.GetProperty("boolean").GetBoolean())values[id]=bool.Parse(text);
       else if(double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out double v)&&double.IsFinite(v))values[id]=v;
      }else values[id]=JsonNode.Parse(row.GetProperty("value").GetRawText());
     }
     var doc=new JsonObject{["schema"]=1,["map"]=map,["group"]=group,["values"]=values};
     File.WriteAllText(file,doc.ToJsonString(new JsonSerializerOptions{WriteIndented=true}),new UTF8Encoding(false));
     notice="已导出参数方案："+file;
    }else{
     var saved=JsonNode.Parse(File.ReadAllText(chosen)) as JsonObject??throw new ArgumentException("方案文件必须是 JSON 对象。");
     if(saved["schema"]?.ToString()!="1")throw new ArgumentException("方案版本不受支持。");
     string? fileGroup=saved["group"]?.ToString();if(!string.IsNullOrEmpty(fileGroup)&&fileGroup!=group)throw new ArgumentException($"此方案属于「{fileGroup}」分组，请切换到对应页面导入。");
     var values=saved["values"] as JsonObject??throw new ArgumentException("方案缺少 values 参数。");
     var byId=rows.ToDictionary(r=>r.GetProperty("id").GetString()!,r=>r);
     int imported=0;
     foreach(var pair in values){
      if(!byId.TryGetValue(pair.Key,out var row)||pair.Value==null)continue;
      string current=row.GetProperty("value").ToString();
      string next=pair.Value is JsonValue jv&&jv.TryGetValue<bool>(out bool b)?b.ToString():pair.Value.ToString();
      if(next!=current)drafts[pair.Key]=next;else drafts.Remove(pair.Key);
      imported++;
     }
     dirty=true;notice=$"已读取 {imported} 项参数到输入栏；核对后点击「应用修改」或「应用到全部地图」。";
    }
   }catch(Exception e){notice="操作失败："+e.GetBaseException().Message;}
  }
  void Finish(){if(pending?.IsCompleted!=true)return;var done=pending;pending=null;try{var data=done.GetAwaiter().GetResult();if(data.GetProperty("map").GetString()!=map)throw new InvalidOperationException("地图不匹配，请刷新。");revision=data.GetProperty("revision").GetString()!;rows=data.GetProperty("rows").EnumerateArray().Where(r=>r.GetProperty("group").GetString()==group).Select(r=>r.Clone()).ToArray();presets=data.GetProperty("presets").EnumerateArray().Select(p=>p.GetString()!).ToArray();if(operation is "apply" or "applyAll" or "load" or "defaults")drafts.Clear();dirty=true;notice=operation switch{"apply"=>"已应用并持久保存；下次地图生成时生效。","applyAll"=>"已将修改应用到全部地图并持久保存；下次地图生成时生效。","save"=>"方案已保存到服务端 parameter-presets/"+name+".json。","delete"=>"已删除服务端方案："+name,"load"=>"方案已载入并应用；下次地图生成时生效。","defaults"=>"已恢复当前地图的默认值及本分组助手默认值。",_=>"已读取服务端参数。"};}catch(Exception e){notice="操作失败："+e.GetBaseException().Message+"；可手动刷新重试。";}}
  static bool RowChanged(JsonElement row,Dictionary<string,string> currentDrafts){
   string id=row.GetProperty("id").GetString()!;if(currentDrafts.ContainsKey(id))return true;
   return row.GetProperty("value").ToString()!=row.GetProperty("defaultValue").ToString();
  }
  internal void Draw(Rect r){
   var current=Singleton<GameWorld>.Instance;var pointer=RaidClock.Active&&current!=null?current.Pointer:IntPtr.Zero;if(pointer!=world){if(pointer!=IntPtr.Zero)map=AiSpawnInfoRows.NormalizeMap(current!.LocationId);Reset();world=pointer;}
   FinishPick();Finish();if(!started){started=true;if(name=="默认方案")name=map+"_"+group;Request("read");}float x=r.x,y=r.y,w=r.width;bool enabled=GUI.enabled,busy=pending!=null||filePick!=null;
   InterfaceMenu.Label(new(x,y,w-140,28),group=="AI"?"服务端 AI 参数与方案":"战局信息 · 服务端参数",InterfaceMenu.Accent,18);
   if(InterfaceMenu.Button(new(r.xMax-134,y,134,28),onlyChanged?"只看已改项 ✓":"只看已改项",onlyChanged)){onlyChanged=!onlyChanged;scroll=Vector2.zero;dirty=true;}
   y+=34;
   try{GUI.enabled=enabled&&!busy&&drafts.Count==0;int index=Array.IndexOf(Maps,map);if(InterfaceMenu.Button(new(x,y,70,28),"上一图")){map=Maps[(index+Maps.Length-1)%Maps.Length];Reset();world=pointer;}if(InterfaceMenu.Button(new(r.xMax-70,y,70,28),"下一图")){map=Maps[(index+1)%Maps.Length];Reset();world=pointer;}GUI.enabled=enabled;InterfaceMenu.Label(new(x+78,y,w-156,28),AiSpawnInfoRows.MapName(map)+(pointer!=IntPtr.Zero?" · 本局 "+AiSpawnInfoRows.MapName(current!.LocationId):" · 可在进局前配置"),null,12);y+=34;
   InterfaceMenu.Paragraph(new(x,y,w,48),"地图参数按地图保存，助手覆盖仅作用于所选地图。应用 / 载入 / 恢复默认从下次地图生成生效。",null,12);y+=52;
   GUI.enabled=enabled&&!busy;float cell=(w-24)/5;
   if(InterfaceMenu.Button(new(x,y,cell,28),"刷新"))Request("read");
   GUI.enabled=enabled&&!busy&&drafts.Count>0;if(InterfaceMenu.Button(new(x+cell+6,y,cell,28),"应用 ("+drafts.Count+")"))Request("apply");
   if(InterfaceMenu.Button(new(x+2*(cell+6),y,cell,28),"应用全图"))Request("applyAll");
   GUI.enabled=enabled&&!busy&&revision.Length>0;if(InterfaceMenu.Button(new(x+3*(cell+6),y,cell,28),"恢复默认"))Request("defaults");
   GUI.enabled=enabled&&!busy&&drafts.Count>0;if(InterfaceMenu.Button(new(x+4*(cell+6),y,cell,28),"撤销输入")){drafts.Clear();confirm="";dirty=true;}y+=34;
   GUI.enabled=enabled&&!busy;
   float btnW=68;name=InterfaceMenu.Field(new(x,y,Math.Max(80,w-(btnW+6)*5),28),name);
   if(InterfaceMenu.Button(new(r.xMax-5*btnW-24,y,btnW,28),"保存"))Request("save");
   if(InterfaceMenu.Button(new(r.xMax-4*btnW-18,y,btnW,28),"载入"))Request("load");
   GUI.enabled=enabled&&!busy&&presets.Contains(name,StringComparer.Ordinal);if(InterfaceMenu.Button(new(r.xMax-3*btnW-12,y,btnW,28),"删除"))Request("delete");
   GUI.enabled=enabled&&!busy&&rows.Length>0;
   if(InterfaceMenu.Button(new(r.xMax-2*btnW-6,y,btnW,28),"导出"))StartExport();
   if(InterfaceMenu.Button(new(r.xMax-btnW,y,btnW,28),"导入"))StartImport();
   y+=34;
   if(presets.Length>0){if(InterfaceMenu.Button(new(x,y,100,26),"切换已存方案")){presetIndex=(presetIndex+1)%presets.Length;name=presets[presetIndex];}InterfaceMenu.Label(new(x+108,y,w-108,26),"已存 "+presets.Length+" 个 · "+name,null,11);y+=30;}
   GUI.enabled=enabled;InterfaceMenu.Paragraph(new(x,y,w,44),notice,null,12);y+=48;
   InterfaceMenu.Label(new(x,y,70,28),"搜索参数",null,12);string next=InterfaceMenu.Field(new(x+76,y,w-76,28),search);if(next!=search){search=next;scroll=Vector2.zero;dirty=true;}y+=34;
   float width=Math.Max(150,w-22),height=Math.Max(30,r.yMax-y);style??=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=12};if(dirty||layoutWidth!=width){dirty=false;layoutWidth=width;visible=rows.Where(row=>(!onlyChanged||RowChanged(row,drafts))&&(string.IsNullOrWhiteSpace(search)||(row.GetProperty("name").GetString()+" "+row.GetProperty("id").GetString()+" "+row.GetProperty("help").GetString()).Contains(search,StringComparison.OrdinalIgnoreCase))).ToArray();
   heights=visible.Select(row=>Math.Max(30,style.CalcHeight(new GUIContent(row.GetProperty("name").GetString()),width-20))+Math.Max(40,style.CalcHeight(new GUIContent(row.GetProperty("help").GetString()),width-20))+86).ToArray();}scroll=GUI.BeginScrollView(new(x,y,w,height),scroll,new Rect(0,0,width,Math.Max(height-1,heights.Sum())));
   try{float top=0;if(visible.Length==0)InterfaceMenu.Paragraph(new(10,12,width-20,40),onlyChanged?"当前没有偏离默认值的已修改参数。":"没有匹配的参数。",null,12);for(int i=0;i<visible.Length;i++){var row=visible[i];float h=heights[i];if(top+h>=scroll.y&&top<scroll.y+height){string id=row.GetProperty("id").GetString()!,value=row.GetProperty("value").ToString();bool boolean=row.GetProperty("boolean").GetBoolean();string draft=drafts.GetValueOrDefault(id,value);InterfaceMenu.Surface(new(0,top,width,h-6),new Color(.2f,.25f,.3f,.16f),6);float title=Math.Max(30,style.CalcHeight(new GUIContent(row.GetProperty("name").GetString()),width-20));InterfaceMenu.Paragraph(new(10,top+4,width-20,title),row.GetProperty("name").GetString()!,InterfaceMenu.Accent,12);InterfaceMenu.Paragraph(new(10,top+title+4,width-20,h-title-86),row.GetProperty("help").GetString()!,null,12);GUI.enabled=enabled&&!busy;float inputY=top+h-76;if(boolean){bool v=bool.TryParse(draft,out bool b)&&b;if(InterfaceMenu.Button(new(10,inputY,110,28),v?"开启":"关闭",v))draft=(!v).ToString();}else draft=InterfaceMenu.Field(new(10,inputY,Math.Min(170,width*.4f),28),draft);if(draft!=value)drafts[id]=draft;else drafts.Remove(id);GUI.enabled=enabled;InterfaceMenu.Label(new(10+(boolean?120:Math.Min(170,width*.4f)+10),inputY,width-(boolean?140:Math.Min(170,width*.4f)+30),28),"默认 "+(boolean?(row.GetProperty("defaultValue").GetBoolean()?"开启":"关闭"):row.GetProperty("defaultValue").ToString())+(boolean?"":" · "+row.GetProperty("minimum")+"～"+row.GetProperty("maximum")),null,11);InterfaceMenu.Label(new(10,top+h-42,width-20,30),id.Replace('/','.'),null,10);}top+=h;}}
   finally{GUI.EndScrollView();}
   }finally{GUI.enabled=enabled;}
  }
 }
}
