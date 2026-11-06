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
 internal static void Draw(Rect rect){
  if(ItemSpawner.InRaid()){InterfaceMenu.Label(rect,"请退出战局后再管理角色。",InterfaceMenu.Accent);return;}
  if(!requested){requested=true;Begin(new(){{"action","status"}});}
  if(request?.IsCompleted==true){
   try{using var response=JsonDocument.Parse(request.GetAwaiter().GetResult());var root=response.RootElement;
    if(root.TryGetProperty("err",out var error)&&error.TryGetInt32(out int code)&&code!=0)status=root.TryGetProperty("errmsg",out var message)?message.GetString()??"操作失败":"操作失败";
    else{data=Data(root).Clone();ready=true;values.Clear();status="已读取角色数据；保存后重新登录角色生效。";}
   }catch{status="服务响应无法读取。";}request=null;
  }
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width-90,28),"角色管理",InterfaceMenu.Accent,18);
  if(InterfaceMenu.Button(new Rect(rect.xMax-80,rect.y,80,30),"刷新"))Begin(new(){{"action","status"}});
  int index=0;foreach(var name in new[]{"藏身处","商人好感","Scav 装备"}){if(InterfaceMenu.Button(new Rect(rect.x+index++*rect.width/3,rect.y+43,rect.width/3-6,31),name,tab==name)){tab=name;scroll=Vector2.zero;}}
  var body=new Rect(rect.x,rect.y+88,rect.width,Math.Max(40,rect.height-135));
  if(ready){if(tab=="藏身处")Hideout(body);else if(tab=="商人好感")Traders(body);else Scav(body);}
  InterfaceMenu.Label(new Rect(rect.x,rect.yMax-40,rect.width,38),status,null,12);
 }
 static string Edit(string key,string fallback,Rect rect){var value=values.TryGetValue(key,out var old)?old:fallback;value=InterfaceMenu.Field(rect,value);values[key]=value;return value;}
 static void Hideout(Rect rect){
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
 static void Scav(Rect rect){
  float half=(rect.width-8)/2;
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y,half,30),"仅下一次出击",mode=="once"))mode="once";
  if(InterfaceMenu.Button(new Rect(rect.x+half+8,rect.y,half,30),"以后每次套用",mode=="always"))mode="always";
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y+40,half,30),"以当前 PMC 装备为模板",source=="pmc")){source="pmc";changes.Clear();}
  if(InterfaceMenu.Button(new Rect(rect.x+half+8,rect.y+40,half,30),"以当前 Scav 装备为模板",source=="scav")){source="scav";changes.Clear();}
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y+80,half,31),"复制并保存装备预设",true)&&request==null)Begin(new(){{"action","scav-save"},{"source",source},{"mode",mode},{"equipment",new Dictionary<string,string>(changes)}});
  if(InterfaceMenu.Button(new Rect(rect.x+half+8,rect.y+80,half,31),"停止套用预设")&&request==null)Begin(new(){{"action","scav-clear"}});
  string saved=data.GetProperty("scavMode").GetString()??"off";
  InterfaceMenu.Label(new Rect(rect.x,rect.y+118,rect.width,24),"当前预设："+(saved=="always"?"每次套用":saved=="once"?"仅下一次":"未启用"),InterfaceMenu.Accent,12);
  float left=150;var list=new Rect(rect.x,rect.y+151,left,Math.Max(40,rect.height-151));
  scroll=GUI.BeginScrollView(list,scroll,new Rect(0,0,left-18,slotNames.Count*38));int index=0;foreach(var slot in slotNames)if(InterfaceMenu.Button(new Rect(0,index++*38,left-23,31),slot.Value,activeSlot==slot.Key)){activeSlot=slot.Key;search="";itemScroll=Vector2.zero;}
  GUI.EndScrollView();
  var edit=new Rect(list.xMax+12,list.y,rect.width-left-12,list.height);
  string current="";string field=source=="pmc"?"pmcItems":"scavItems";
  if(data.TryGetProperty(field,out var equipped)&&equipped.ValueKind==JsonValueKind.Array)foreach(var item in equipped.EnumerateArray())if(item.GetProperty("slot").GetString()==activeSlot)current=item.GetProperty("template").GetString()??"";
  if(changes.TryGetValue(activeSlot,out var replacement))current=replacement;
  InterfaceMenu.Label(new Rect(edit.x,edit.y,edit.width,25),slotNames[activeSlot]+" · "+(current.Length==0?"空":ItemSpawner.TemplateName(current)),InterfaceMenu.Accent,12);
  if(InterfaceMenu.Button(new Rect(edit.x,edit.y+33,edit.width,28),activeSlot=="Pockets"?"口袋容器需要保留":"清空此槽位")&&activeSlot!="Pockets")changes[activeSlot]="";
  search=InterfaceMenu.Field(new Rect(edit.x,edit.y+69,edit.width,29),search);
  var allowed=new HashSet<string>(StringComparer.Ordinal);var excluded=new HashSet<string>(StringComparer.Ordinal);
  if(data.TryGetProperty("slotFilters",out var filters))foreach(var slot in filters.EnumerateArray())if(slot.GetProperty("slot").GetString()==activeSlot)foreach(var filter in slot.GetProperty("filters").EnumerateArray()){
   if(filter.TryGetProperty("Filter",out var included))foreach(var id in included.EnumerateArray())allowed.Add(id.GetString()!);
   if(filter.TryGetProperty("ExcludedFilter",out var blocked))foreach(var id in blocked.EnumerateArray())excluded.Add(id.GetString()!);
  }
  var matches=ItemSpawner.FindEquipment(search,allowed,excluded).ToList();var choices=new Rect(edit.x,edit.y+108,edit.width,Math.Max(25,edit.height-108));
  itemScroll=GUI.BeginScrollView(choices,itemScroll,new Rect(0,0,choices.width-18,matches.Count*34));
  for(int i=0;i<matches.Count;i++){var item=matches[i];if(InterfaceMenu.Button(new Rect(0,i*34,choices.width-23,28),item.Name,current==item.Id))changes[activeSlot]=item.Id;}
  GUI.EndScrollView();
 }
}
