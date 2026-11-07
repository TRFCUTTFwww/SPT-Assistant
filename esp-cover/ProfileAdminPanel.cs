using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SPTushonka.Common.Http;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ProfileAdminPanel {
 static readonly Dictionary<string,string> values=new(),changes=new();
 static readonly Dictionary<string,string> slotNames=new(){["FirstPrimaryWeapon"]="第一主武器",["SecondPrimaryWeapon"]="第二主武器",["Holster"]="手枪",["Headwear"]="头盔",["Earpiece"]="耳机",["FaceCover"]="面罩",["ArmorVest"]="防弹衣",["TacticalVest"]="胸挂",["Backpack"]="背包",["Pockets"]="口袋",["Eyewear"]="眼镜"};
 static Task<string>? request;
 static JsonElement data;
 static bool requested,ready;
 static string tab="藏身处",status="",mode="once",source="pmc",activeSlot="FirstPrimaryWeapon",search="";
 static Vector2 scroll,itemScroll;
 internal static void OpenScav(){tab="Scav 装备";scroll=itemScroll=Vector2.zero;}
 static async Task<string> Call(Dictionary<string,object?> payload){
  try{
   using var capability=JsonDocument.Parse(await RequestHandler.PostJsonAsync("/local/gear/status","{}"));
   var info=Data(capability.RootElement);
   if(!info.TryGetProperty("profileManagement",out var supported)||supported.ValueKind!=JsonValueKind.True)return "{\"err\":228,\"errmsg\":\"请重启服务端以启用角色管理接口。\"}";
   payload["apiKey"]=File.ReadAllText(Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","user","mods","LocalGearMail","api-key.txt")).Trim();
   return await RequestHandler.PostJsonAsync("/local/gear/profile",JsonSerializer.Serialize(payload));
  }catch{return "{\"err\":228,\"errmsg\":\"连接角色管理服务失败，请检查本地服务端。\"}";}
 }
 static JsonElement Data(JsonElement root)=>root.TryGetProperty("data",out var value)&&value.ValueKind==JsonValueKind.Object?value:root;
 static void Begin(Dictionary<string,object?> payload){if(request!=null)return;request=Call(payload);status="处理中…";}
 internal static void Draw(Rect rect,bool scavOnly=false){
  if(ItemSpawner.InRaid()){InterfaceMenu.Label(rect,"请退出战局后再管理角色。",InterfaceMenu.Accent);return;}
  if(!requested){requested=true;Begin(new(){{"action","status"}});}
  if(request?.IsCompleted==true){
   try{using var response=JsonDocument.Parse(request.GetAwaiter().GetResult());var root=response.RootElement;
    if(root.TryGetProperty("err",out var error)&&error.TryGetInt32(out int code)&&code!=0)status=root.TryGetProperty("errmsg",out var message)?message.GetString()??"操作失败":"操作失败";
    else{data=Data(root).Clone();ready=true;values.Clear();changes.Clear();mode=data.TryGetProperty("scavMode",out var savedMode)&&savedMode.GetString()=="always"?"always":"once";status="已读取角色数据；保存后重新登录角色生效。";}
   }catch{status="服务响应无法读取。";}request=null;
  }
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width-90,28),scavOnly?"Scav 装备":"角色管理",InterfaceMenu.Accent,18);
  if(InterfaceMenu.Button(new Rect(rect.xMax-80,rect.y,80,30),changes.Count>0?"撤销并刷新":"刷新"))Begin(new(){{"action","status"}});
  if(!scavOnly){int index=0;foreach(var name in new[]{"藏身处","商人好感","Scav 装备"}){if(InterfaceMenu.Button(new Rect(rect.x+index++*rect.width/3,rect.y+43,rect.width/3-6,31),name,tab==name)){tab=name;scroll=Vector2.zero;}}}
  var body=new Rect(rect.x,rect.y+(scavOnly?44:88),rect.width,Math.Max(40,rect.height-(scavOnly?91:135)));
  if(ready){if(scavOnly||tab=="Scav 装备")Scav(body);else if(tab=="藏身处")Hideout(body);else Traders(body);}
  InterfaceMenu.Label(new Rect(rect.x,rect.yMax-40,rect.width,38),status,null,12);
 }
 static string Edit(string key,string fallback,Rect rect){var value=values.TryGetValue(key,out var old)?old:fallback;value=InterfaceMenu.Field(rect,value);values[key]=value;return value;}
 static void Hideout(Rect rect){
  HideoutCraftControl.Draw(new Rect(rect.x,rect.y,rect.width,70));rect=new Rect(rect.x,rect.y+78,rect.width,Math.Max(40,rect.height-78));
  if(!data.TryGetProperty("areas",out var areas)||areas.ValueKind!=JsonValueKind.Array)return;
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-18,areas.GetArrayLength()*52));float y=0,width=rect.width-25;
  foreach(var area in areas.EnumerateArray()){
   int type=area.GetProperty("type").GetInt32(),level=area.GetProperty("level").GetInt32(),maximum=area.GetProperty("maximum").GetInt32();
   string key="hideout_area_"+type+"_name",name=LocalizationExtensions.Localized(key,EStringCase.None);if(name==key)name=area.GetProperty("name").GetString()??key;
   InterfaceMenu.Label(new Rect(0,y,width-190,32),$"{name}  {level}/{maximum}");
   var raw=Edit("area_"+type,level.ToString(),new Rect(width-180,y,90,31));
   if(InterfaceMenu.Button(new Rect(width-80,y,80,31),"升级")&&request==null)try{int target=ItemSpawnRules.Integer(raw,level,maximum,"区域等级");Begin(new(){{"action","hideout"},{"areaType",type},{"level",target}});}catch(ArgumentException error){status=error.Message;}
   y+=52;
  }GUI.EndScrollView();
 }
 static void Traders(Rect rect){
  if(!data.TryGetProperty("traders",out var traders)||traders.ValueKind!=JsonValueKind.Array)return;
  scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-18,traders.GetArrayLength()*52));float y=0,width=rect.width-25;
  foreach(var trader in traders.EnumerateArray()){
   string id=trader.GetProperty("id").GetString()!;double standing=trader.GetProperty("standing").GetDouble();
   string key=id+" Nickname",name=LocalizationExtensions.Localized(key,EStringCase.None);if(name==key)name=trader.GetProperty("name").GetString()??id;
   InterfaceMenu.Label(new Rect(0,y,width-190,32),$"{name} · LL{trader.GetProperty("loyalty").GetInt32()}");
   var raw=Edit("trader_"+id,standing.ToString("0.##",CultureInfo.InvariantCulture),new Rect(width-180,y,90,31));
   if(InterfaceMenu.Button(new Rect(width-80,y,80,31),"保存")&&request==null)try{var value=ItemSpawnRules.Number(raw,-20,100,"商人好感");Begin(new(){{"action","trader"},{"traderId",id},{"standing",value}});}catch(ArgumentException error){status=error.Message;}
   y+=52;
  }GUI.EndScrollView();
 }
 static string SourceItem(string slot){string field=source=="pmc"?"pmcItems":"scavItems";if(data.TryGetProperty(field,out var rows)&&rows.ValueKind==JsonValueKind.Array)foreach(var row in rows.EnumerateArray())if(row.GetProperty("slot").GetString()==slot)return row.GetProperty("template").GetString()??"";return "";}
 static string EquipmentName(string template)=>template.Length==0?I18n.Text("空"):ItemSpawner.TemplateName(template);
 static void Scav(Rect rect){
  float x=rect.x,y=rect.y,w=rect.width,half=(w-8)/2;bool old=GUI.enabled;GUI.enabled=old&&request==null;
  InterfaceMenu.Label(new(x,y,w,25),"1. 选择装备来源",InterfaceMenu.Accent,16);y+=33;
  if(InterfaceMenu.Button(new(x,y,half,32),"复制当前 PMC 穿戴",source=="pmc")){source="pmc";changes.Clear();search="";}
  if(InterfaceMenu.Button(new(x+half+8,y,half,32),"使用当前 Scav 装备",source=="scav")){source="scav";changes.Clear();search="";}y+=40;
  InterfaceMenu.Paragraph(new(x,y,w,43),"未修改的槽位保留来源装备。点选下面的槽位，再从右侧选择要替换的装备；选中只是草稿，最后点击保存。",null,12);y+=52;
  InterfaceMenu.Label(new(x,y,w,25),"2. 按槽位选择装备",InterfaceMenu.Accent,16);y+=33;
  float footer=160,listWidth=Math.Min(255,w*.34f);var list=new Rect(x,y,listWidth,Math.Max(100,rect.yMax-y-footer));
  scroll=GUI.BeginScrollView(list,scroll,new(0,0,listWidth-18,slotNames.Count*64));int index=0;
  foreach(var slot in slotNames){float top=index++*64;bool changed=changes.TryGetValue(slot.Key,out var selected);string item=changed?selected!:SourceItem(slot.Key);if(InterfaceMenu.WrappedButton(new(0,top,listWidth-23,58),I18n.Text(slot.Value)+(changed?"  ●":"")+"\n"+EquipmentName(item),activeSlot==slot.Key)){activeSlot=slot.Key;search="";itemScroll=Vector2.zero;}}
  GUI.EndScrollView();var edit=new Rect(list.xMax+12,list.y,w-listWidth-12,list.height);string original=SourceItem(activeSlot),current=changes.TryGetValue(activeSlot,out var replacement)?replacement:original;
  InterfaceMenu.Label(new(edit.x,edit.y,edit.width,26),slotNames[activeSlot],InterfaceMenu.Accent,16);
  InterfaceMenu.Paragraph(new(edit.x,edit.y+31,edit.width,43),"来源装备："+EquipmentName(original),null,12);
  InterfaceMenu.Paragraph(new(edit.x,edit.y+77,edit.width,43),"保存后装备："+EquipmentName(current),InterfaceMenu.Accent,12);
  float buttonWidth=(edit.width-8)/2;if(InterfaceMenu.Button(new(edit.x,edit.y+125,buttonWidth,30),"撤销此槽修改"))changes.Remove(activeSlot);
  bool enabled=GUI.enabled;GUI.enabled=enabled&&activeSlot!="Pockets";if(InterfaceMenu.Button(new(edit.x+buttonWidth+8,edit.y+125,buttonWidth,30),"清空此槽位"))changes[activeSlot]="";GUI.enabled=enabled;
  search=InterfaceMenu.Field(new(edit.x,edit.y+166,edit.width,30),search);if(search.Length==0)InterfaceMenu.Label(new(edit.x+7,edit.y+166,edit.width-14,30),"搜索可装入此槽位的装备…",null,12);
  var allowed=new HashSet<string>(StringComparer.Ordinal);var excluded=new HashSet<string>(StringComparer.Ordinal);
  if(data.TryGetProperty("slotFilters",out var filters))foreach(var slot in filters.EnumerateArray())if(slot.GetProperty("slot").GetString()==activeSlot)foreach(var filter in slot.GetProperty("filters").EnumerateArray()){
   if(filter.TryGetProperty("Filter",out var included))foreach(var id in included.EnumerateArray())allowed.Add(id.GetString()!);
   if(filter.TryGetProperty("ExcludedFilter",out var blocked))foreach(var id in blocked.EnumerateArray())excluded.Add(id.GetString()!);
  }
  var matches=ItemSpawner.FindEquipment(search,allowed,excluded).ToList();var choices=new Rect(edit.x,edit.y+207,edit.width,Math.Max(25,edit.height-207));itemScroll=GUI.BeginScrollView(choices,itemScroll,new(0,0,choices.width-18,matches.Count*43));
  for(int i=Math.Max(0,(int)(itemScroll.y/43)-1);i<Math.Min(matches.Count,(int)((itemScroll.y+choices.height)/43)+2);i++){var item=matches[i];if(InterfaceMenu.WrappedButton(new(0,i*43,choices.width-23,38),item.Name,current==item.Id))changes[activeSlot]=item.Id;}
  if(matches.Count==0)InterfaceMenu.Paragraph(new(6,6,choices.width-30,70),"没有兼容装备；可清空搜索或选择其他槽位。",null,12);GUI.EndScrollView();
  y=rect.yMax-footer+8;InterfaceMenu.Label(new(x,y,w,25),"3. 选择生效次数并保存",InterfaceMenu.Accent,16);y+=32;
  if(InterfaceMenu.Button(new(x,y,half,31),"仅下一次 Scav 出击",mode=="once"))mode="once";
  if(InterfaceMenu.Button(new(x+half+8,y,half,31),"以后每次 Scav 出击",mode=="always"))mode="always";y+=39;
  if(InterfaceMenu.Button(new(x,y,half,35),"保存并应用 Scav 装备",true))Begin(new(){{"action","scav-save"},{"source",source},{"mode",mode},{"equipment",new Dictionary<string,string>(changes)}});
  if(InterfaceMenu.Button(new(x+half+8,y,half,35),"停止以后套用"))Begin(new(){{"action","scav-clear"}});y+=42;
  string saved=data.GetProperty("scavMode").GetString()??"off";
  InterfaceMenu.Paragraph(new(x,y,w,42),$"待保存槽位：{changes.Count} · 当前预设："+(saved=="always"?"以后每次套用":saved=="once"?"仅下一次出击":"未启用"),null,12);GUI.enabled=old;
 }
}
