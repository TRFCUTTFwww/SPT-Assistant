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
 static readonly Dictionary<string,string> slotNames=new(){["FirstPrimaryWeapon"]="第一主武器",["SecondPrimaryWeapon"]="第二主武器",["Holster"]="手枪",["Scabbard"]="近战武器",["Headwear"]="头盔",["Earpiece"]="耳机",["FaceCover"]="面罩",["ArmorVest"]="防弹衣",["TacticalVest"]="胸挂",["Backpack"]="背包",["Pockets"]="口袋",["Eyewear"]="眼镜"};
 sealed class ContentDraft { public string id{get;set;}="";public string template{get;set;}="";public int count{get;set;}=1;[System.Text.Json.Serialization.JsonIgnore] public string? Quantity{get;set;} }
 static readonly Dictionary<string,List<ContentDraft>> contents=new();
 static bool contentsEditor,addingContent;
 static Task<string>? request;
 static JsonElement data;
 static bool requested,ready,draining,picker;
 static string action="",requestOwner="",loadedOwner="",submittedMode="",cacheKey="";
 static readonly Dictionary<string,string> submitted=new();
 static List<(string Id,string Name)> matches=new();
 static string tab="藏身处",status="",mode="once",source="pmc",activeSlot="FirstPrimaryWeapon",search="";
 static Vector2 scroll,itemScroll,compactScroll;
 internal static bool IsScavSlot(string slot)=>slotNames.ContainsKey(slot);
 internal static void OpenScav(){tab="Scav 装备";picker=contentsEditor=addingContent=false;scroll=itemScroll=compactScroll=Vector2.zero;}
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
 static void Begin(Dictionary<string,object?> payload){
  if(request!=null||draining||ScavEquipmentSync.Busy)return;
  requestOwner=ScavEquipmentSync.Owner;action=(string)payload["action"]!;
  if(action.StartsWith("scav-",StringComparison.Ordinal)&&ScavEquipmentSync.Matching){status="请先退出出击预览，再修改 Scav 装备。";return;}
  if(requestOwner.Length==0){status="角色尚未就绪，请稍后重试。";return;}
  submitted.Clear();if(action=="scav-save"){submittedMode=mode;foreach(var slot in slotNames.Keys){string item=changes.TryGetValue(slot,out var replacement)?replacement:SourceItem(slot);if(item.Length>0)submitted[slot]=item;}}
  if(action=="scav-save"){
   try{foreach(var row in contents.Values.SelectMany(c=>c))row.count=ItemSpawnRules.Integer(row.Quantity??row.count.ToString(CultureInfo.InvariantCulture),1,int.MaxValue,"内容物数量");}catch(ArgumentException error){status=error.Message;return;}
   payload["contents"]=contents;payload["sourceRevision"]=data.TryGetProperty(source+"Revision",out var revision)?revision.GetString():"";
  }
  ScavEquipmentSync.Reset();request=Call(payload);status="处理中…";
 }
 internal static void Tick(){
  ScavEquipmentSync.Tick();
  string owner=ScavEquipmentSync.Owner;
  if(ready&&owner!=loadedOwner){ready=requested=false;data=default;values.Clear();changes.Clear();contents.Clear();picker=contentsEditor=false;cacheKey="";ScavEquipmentSync.Reset();}
  if(request?.IsCompleted!=true)return;
  var completed=request;request=null;
  try{
   using var response=JsonDocument.Parse(completed.GetAwaiter().GetResult());var root=response.RootElement;
   if(owner!=requestOwner||ItemSpawner.InRaid()){requested=false;status="角色已切换或进入战局，请退出战局后刷新。";return;}
   if(root.TryGetProperty("err",out var error)&&error.TryGetInt32(out int code)&&code!=0){status=root.TryGetProperty("errmsg",out var message)?message.GetString()??"操作失败":"操作失败";return;}
   var result=Data(root);
   if(!result.TryGetProperty("ok",out var ok)||ok.ValueKind!=JsonValueKind.True)throw new InvalidOperationException("Profile action was not acknowledged");
   if(action=="scav-save"&&(result.GetProperty("scavMode").GetString()!=submittedMode||!ScavEquipmentRules.Matches(submitted,ReadSlots(result,"scavItems")))){status="保存结果与装备方案不一致，草稿已保留，请刷新核对。";return;}
   data=result.Clone();ready=true;loadedOwner=owner;values.Clear();changes.Clear();contents.Clear();cacheKey="";
   mode=data.TryGetProperty("scavMode",out var savedMode)&&savedMode.GetString()=="always"?"always":"once";
   if(action=="scav-save"){source="scav";picker=contentsEditor=false;ScavEquipmentSync.Begin(data);status=ScavEquipmentSync.Status;}
   else {ScavEquipmentSync.Reset();status=action=="scav-clear"?"已停止以后套用；当前 Scav 已穿戴的装备保留。":action=="status"?"已读取角色数据；选择槽位修改，最后保存并应用。":"已保存角色数据，请重新登录角色刷新原生界面。";}
  }catch(Exception error){status="服务响应无法读取，草稿已保留；请刷新核对保存结果。";Plugin.Self.Log.LogWarning("Profile admin response: "+error.Message);}
 }
 static Dictionary<string,string> ReadSlots(JsonElement result,string field){var slots=new Dictionary<string,string>(StringComparer.Ordinal);if(result.TryGetProperty(field,out var rows)&&rows.ValueKind==JsonValueKind.Array)foreach(var row in rows.EnumerateArray())slots[row.GetProperty("slot").GetString()!]=row.GetProperty("template").GetString()!;return slots;}
 internal static bool PrepareForReload(){draining=true;return request==null;}
 internal static void Reset(){request=null;ready=requested=false;draining=false;data=default;values.Clear();changes.Clear();contents.Clear();contentsEditor=addingContent=false;submitted.Clear();matches.Clear();cacheKey="";ScavEquipmentSync.Reset();}
 internal static void Draw(Rect rect,bool scavOnly=false){
  if(ItemSpawner.InRaid()){InterfaceMenu.Label(rect,"请退出战局后再管理角色。",InterfaceMenu.Accent);return;}
  if(!requested){requested=true;Begin(new(){{"action","status"}});}
  bool enabled=GUI.enabled;GUI.enabled=enabled&&request==null&&!ScavEquipmentSync.Busy&&!draining;
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width-90,28),scavOnly?"Scav 装备":"角色管理",InterfaceMenu.Accent,18);
  if(InterfaceMenu.Button(new Rect(rect.xMax-80,rect.y,80,30),changes.Count+contents.Count>0?"撤销并刷新":"刷新")){picker=contentsEditor=false;Begin(new(){{"action","status"}});}
  if(!scavOnly){int index=0;foreach(var name in new[]{"藏身处","商人好感","Scav 装备"}){if(InterfaceMenu.Button(new Rect(rect.x+index++*rect.width/3,rect.y+43,rect.width/3-6,31),name,tab==name)){tab=name;scroll=Vector2.zero;}}}
  var body=new Rect(rect.x,rect.y+(scavOnly?44:88),rect.width,Math.Max(40,rect.height-(scavOnly?91:135)));
  if(ready){if(scavOnly||tab=="Scav 装备")Scav(body);else if(tab=="藏身处")Hideout(body);else Traders(body);}
  GUI.enabled=enabled;
  InterfaceMenu.Paragraph(new Rect(rect.x,rect.yMax-40,rect.width,38),ScavEquipmentSync.Status.Length>0?ScavEquipmentSync.Status:status,null,12);
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
  if(ScavEquipmentSync.Matching){InterfaceMenu.Paragraph(rect,"请先退出出击预览，再修改 Scav 装备。",InterfaceMenu.Accent);return;}
  // Very short windows scroll the whole page; normal windows keep actions fixed.
  float contentHeight=ScavEquipmentRules.ContentHeight(rect.height,picker||contentsEditor);bool compact=rect.height<contentHeight;
  if(compact){compactScroll=GUI.BeginScrollView(rect,compactScroll,new Rect(0,0,rect.width-18,contentHeight));rect=new Rect(0,0,rect.width-23,contentHeight);}
  if(contentsEditor)Contents(rect);else if(picker)Picker(rect);else Overview(rect);
  if(compact)GUI.EndScrollView();
 }
 static void SelectSource(string selected){if(source==selected)return;source=selected;changes.Clear();contents.Clear();cacheKey="";scroll=Vector2.zero;}
 static bool CanEditContents=>data.TryGetProperty("scavContentsEditing",out var supported)&&supported.ValueKind==JsonValueKind.True;
 static List<ContentDraft> ReadContents(string slot){
  if(contents.TryGetValue(slot,out var draft))return draft;
  var rows=new List<ContentDraft>();
  if(data.TryGetProperty(source+"Contents",out var all)&&all.ValueKind==JsonValueKind.Object&&all.TryGetProperty(slot,out var items))foreach(var item in items.EnumerateArray())rows.Add(new(){id=item.GetProperty("id").GetString()!,template=item.GetProperty("template").GetString()!,count=item.GetProperty("count").GetInt32()});
  return rows;
 }
 static void ReplaceSlot(string template){changes[activeSlot]=template;if(activeSlot is "Backpack" or "TacticalVest")contents[activeSlot]=new();picker=false;compactScroll=Vector2.zero;}
 static void Overview(Rect rect){
  var layout=ScavEquipmentRules.Layout(rect.width,rect.height);float x=rect.x,y=rect.y,w=rect.width,half=(w-8)/2;
  if(InterfaceMenu.WrappedButton(new(x,y,half,34),"复制当前 PMC 穿戴",source=="pmc"))SelectSource("pmc");
  if(InterfaceMenu.WrappedButton(new(x+half+8,y,half,34),"使用当前 Scav 装备",source=="scav"))SelectSource("scav");
  string saved=data.GetProperty("scavMode").GetString()??"off";
  string savedText=saved=="always"?I18n.Text("以后每次套用"):saved=="once"?I18n.Text("仅下一次出击"):I18n.Text("未启用");
  InterfaceMenu.Paragraph(new(x,y+40,w,27),string.Format(I18n.Text("已保存：{0} · 待保存修改：{1} 个槽位"),savedText,changes.Keys.Union(contents.Keys).Count()),null,12);
  var list=new Rect(x,y+layout.Header,w,layout.ViewHeight);float cardWidth=(w-25-(layout.Columns-1)*8)/layout.Columns;
  scroll=GUI.BeginScrollView(list,scroll,new Rect(0,0,w-18,(float)Math.Ceiling(slotNames.Count/(double)layout.Columns)*110));int index=0;
  foreach(var slot in slotNames){int row=index/layout.Columns,column=index++%layout.Columns;bool changed=changes.TryGetValue(slot.Key,out var selected);string item=changed?selected!:SourceItem(slot.Key);
   bool container=slot.Key is "Backpack" or "TacticalVest";float left=column*(cardWidth+8),top=row*110;
   if(InterfaceMenu.WrappedButton(new(left,top,cardWidth,container?65:100),I18n.Text(slot.Value)+(changed?"  •":"")+"\n"+EquipmentName(item),changed)){activeSlot=slot.Key;picker=true;search="";cacheKey="";itemScroll=compactScroll=Vector2.zero;}
   if(container){bool old=GUI.enabled;GUI.enabled=old&&CanEditContents&&item.Length>0;
    if(InterfaceMenu.WrappedButton(new(left,top+70,cardWidth,30),string.Format(I18n.Text("编辑内容物 ({0})"),ReadContents(slot.Key).Count)+(contents.ContainsKey(slot.Key)?" •":""))){activeSlot=slot.Key;contentsEditor=true;addingContent=false;itemScroll=compactScroll=Vector2.zero;}
    GUI.enabled=old;
   }
  }GUI.EndScrollView();
  y=rect.yMax-layout.Footer;
  if(InterfaceMenu.WrappedButton(new(x,y,half,32),"仅下一次 Scav 出击",mode=="once"))mode="once";
  if(InterfaceMenu.WrappedButton(new(x+half+8,y,half,32),"以后每次 Scav 出击",mode=="always"))mode="always";
  if(InterfaceMenu.WrappedButton(new(x,y+40,half,42),"保存并应用 Scav 装备",true)){if(!CanEditContents)status="请重启服务端以启用新版 Scav 装备编辑。";else Begin(new(){{"action","scav-save"},{"source",source},{"mode",mode},{"equipment",new Dictionary<string,string>(changes)}});}
  if(InterfaceMenu.WrappedButton(new(x+half+8,y+40,half,42),"停止以后套用"))Begin(new(){{"action","scav-clear"}});
 }
 static void Picker(Rect rect){
  float x=rect.x,y=rect.y,w=rect.width,half=(w-8)/2;
  if(InterfaceMenu.Button(new(x,y,135,32),"返回装备方案")){picker=false;compactScroll=Vector2.zero;}
  InterfaceMenu.Label(new(x+145,y,w-145,32),slotNames[activeSlot],InterfaceMenu.Accent,16);
  string original=SourceItem(activeSlot),current=changes.TryGetValue(activeSlot,out var replacement)?replacement:original;
  InterfaceMenu.Paragraph(new(x,y+40,w,39),I18n.Text("来源装备：")+EquipmentName(original),null,12);
  InterfaceMenu.Paragraph(new(x,y+82,w,39),I18n.Text("保存后装备：")+EquipmentName(current),InterfaceMenu.Accent,12);
  string query=InterfaceMenu.Field(new(x,y+128,w,31),search);if(query!=search){search=query;itemScroll=Vector2.zero;}
  if(search.Length==0)InterfaceMenu.Label(new(x+7,y+128,w-14,31),"搜索可装入此槽位的装备…",null,12);
  UpdateMatches();var choices=new Rect(x,y+169,w,Math.Max(0,rect.height-219));
  itemScroll=GUI.BeginScrollView(choices,itemScroll,new(0,0,w-18,Math.Max(matches.Count*52,60)));
  for(int i=Math.Max(0,(int)(itemScroll.y/52)-1);i<Math.Min(matches.Count,(int)((itemScroll.y+choices.height)/52)+2);i++){var item=matches[i];if(InterfaceMenu.WrappedButton(new(0,i*52,w-25,46),item.Name,current==item.Id))ReplaceSlot(item.Id);}
  if(matches.Count==0)InterfaceMenu.Paragraph(new(6,6,w-38,54),"没有兼容装备；可清空搜索或选择其他槽位。",null,12);GUI.EndScrollView();
  y=rect.yMax-40;
  if(InterfaceMenu.WrappedButton(new(x,y,half,36),"撤销此槽修改")){changes.Remove(activeSlot);contents.Remove(activeSlot);picker=false;compactScroll=Vector2.zero;}
  bool enabled=GUI.enabled;GUI.enabled=enabled&&activeSlot!="Pockets";
  if(InterfaceMenu.WrappedButton(new(x+half+8,y,half,36),"清空此槽位"))ReplaceSlot("");GUI.enabled=enabled;
 }
 static void Contents(Rect rect){
  float x=rect.x,y=rect.y,w=rect.width;
  if(InterfaceMenu.Button(new(x,y,135,32),addingContent?"返回内容物":"返回装备方案")){if(addingContent)addingContent=false;else contentsEditor=false;itemScroll=compactScroll=Vector2.zero;}
  InterfaceMenu.Label(new(x+145,y,w-145,32),I18n.Text(slotNames[activeSlot])+" · "+I18n.Text("内容物"),InterfaceMenu.Accent,16);
  InterfaceMenu.Paragraph(new(x,y+40,w,46),"修改暂存于装备方案，返回后点保存并应用。数量为单组数量；原有物品的配件与内嵌物品会保留。",null,12);
  var draft=ReadContents(activeSlot);
  if(addingContent){
   string query=InterfaceMenu.Field(new(x,y+92,w,32),search);if(query!=search){search=query;cacheKey="";itemScroll=Vector2.zero;}
   string key="contents\n"+search+"\n"+I18n.English;if(key!=cacheKey){cacheKey=key;matches=ItemSpawner.FindTemplates(search).ToList();}
   var view=new Rect(x,y+134,w,Math.Max(40,rect.height-140));itemScroll=GUI.BeginScrollView(view,itemScroll,new(0,0,w-18,matches.Count*52));
   for(int i=Math.Max(0,(int)(itemScroll.y/52)-1);i<Math.Min(matches.Count,(int)((itemScroll.y+view.height)/52)+2);i++)if(InterfaceMenu.WrappedButton(new(0,i*52,w-25,46),matches[i].Name)){draft.Add(new(){template=matches[i].Id});contents[activeSlot]=draft;addingContent=false;itemScroll=Vector2.zero;break;}GUI.EndScrollView();return;
  }
  if(InterfaceMenu.Button(new(x,y+92,(w-8)/2,32),"搜索添加物品")){addingContent=true;search="";cacheKey="";itemScroll=Vector2.zero;}
  if(InterfaceMenu.Button(new(x+(w+8)/2,y+92,(w-8)/2,32),"撤销内容物修改")){contents.Remove(activeSlot);draft=ReadContents(activeSlot);}
  var list=new Rect(x,y+136,w,Math.Max(40,rect.height-142));itemScroll=GUI.BeginScrollView(list,itemScroll,new(0,0,w-18,Math.Max(60,draft.Count*92)));
  if(draft.Count==0)InterfaceMenu.Label(new(6,8,w-35,32),"容器内容物为空");
  for(int i=0;i<draft.Count;i++){var item=draft[i];float top=i*92;
   InterfaceMenu.Paragraph(new(4,top,w-32,42),EquipmentName(item.template),null,13);
   InterfaceMenu.Label(new(4,top+46,48,31),"数量",null,12);
   string previous=item.Quantity??item.count.ToString(CultureInfo.InvariantCulture);
   string quantity=InterfaceMenu.Field(new(56,top+46,Math.Max(70,w-180),31),previous);
   if(quantity!=previous){item.Quantity=quantity;contents[activeSlot]=draft;}
   if(InterfaceMenu.Button(new(w-112,top+46,84,31),"移除")){draft.RemoveAt(i);contents[activeSlot]=draft;break;}
  }GUI.EndScrollView();
 }
 static void UpdateMatches(){
  string key=activeSlot+"\n"+search+"\n"+I18n.English;if(cacheKey==key)return;cacheKey=key;
  var allowed=new HashSet<string>(StringComparer.Ordinal);var excluded=new HashSet<string>(StringComparer.Ordinal);
  if(data.TryGetProperty("slotFilters",out var filters))foreach(var slot in filters.EnumerateArray())if(slot.GetProperty("slot").GetString()==activeSlot)foreach(var filter in slot.GetProperty("filters").EnumerateArray()){
   if(filter.TryGetProperty("Filter",out var included))foreach(var id in included.EnumerateArray())allowed.Add(id.GetString()!);
   if(filter.TryGetProperty("ExcludedFilter",out var blocked))foreach(var id in blocked.EnumerateArray())excluded.Add(id.GetString()!);
  }
  matches=ItemSpawner.FindEquipment(search,allowed,excluded).ToList();
 }
}
