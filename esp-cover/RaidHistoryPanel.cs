using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class RaidHistoryPanel {
 static JsonElement[] rows=Array.Empty<JsonElement>();static JsonElement detail;static Task<string>? request;static Task<string?>? folderPick;
 static string operation="",picked="",notice="",tab="入场装备",owner="",filter="全部状态",mapFilter="全部地图",exportFormat="",exportFolder="";
 static int page,total,summaryTotal,summarySurvived,summaryKills;static bool loaded,received,exportSingle;static Vector2 listScroll,detailScroll;static float nextRefresh;
 static readonly HashSet<string> expanded=new(StringComparer.Ordinal);
 static TarkovApplication? application;static float nextApplication;
 static readonly string[] states={"全部状态","PENDING","SURVIVED","RUNNER","KILLED","MISSINGINACTION","LEFT","TRANSIT","SAVE_FAILED"};
 static readonly string[] maps={"全部地图","bigmap","woods","shoreline","interchange","rezervbase","lighthouse","tarkovstreets","laboratory","labyrinth","factory4_day","factory4_night","sandbox","sandbox_high"};
 static string Text(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()??"":"";
 static bool Flag(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.True;
 static int Integer(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.Number&&p.TryGetInt32(out int v)?v:0;
 static double Number(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.Number&&p.TryGetDouble(out double v)&&double.IsFinite(v)?v:0;
 static string Status(string value)=>value switch{"PENDING"=>"进行中／未结算","SURVIVED"=>"成功撤离","RUNNER"=>"匆匆逃离","KILLED"=>"阵亡","MISSINGINACTION"=>"失踪","LEFT"=>"离开战局","TRANSIT"=>"地图过渡","SAVE_FAILED"=>"结算保存失败",_=>"未知状态"};
 static string Stamp(string s)=>DateTime.TryParse(s,out var t)?t.ToLocalTime().ToString("MM-dd HH:mm"):"—";
 static string Duration(JsonElement e)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty("Seconds",out var n)&&n.ValueKind==JsonValueKind.Number?RaidRules.Clock(n.GetDouble()):"—";
 static string Map(string id)=>I18n.Text(id.ToLowerInvariant() switch{"bigmap"=>"海关","factory4_day"=>"工厂（白天）","factory4_night"=>"工厂（夜间）","woods"=>"森林","shoreline"=>"海岸线","interchange"=>"立交桥","rezervbase"=>"储备站","lighthouse"=>"灯塔","tarkovstreets"=>"塔科夫街区","sandbox" or "sandbox_high" or "sandbox_start"=>"中心区","laboratory"=>"实验室","labyrinth"=>"迷宫",_=>CharacterDataPanel.Name(id+" Name",id)});
 static string Part(string key)=>I18n.Text(key switch{"Head"=>"头部","Chest"=>"胸部","Stomach"=>"腹部","LeftArm"=>"左臂","RightArm"=>"右臂","LeftLeg"=>"左腿","RightLeg"=>"右腿","FirstPrimaryWeapon"=>"第一主武器","SecondPrimaryWeapon"=>"第二主武器","Holster"=>"手枪","Scabbard"=>"近战武器","Headwear"=>"头部装备","FaceCover"=>"面罩","Eyewear"=>"眼镜","Earpiece"=>"耳机","ArmorVest"=>"防弹衣","TacticalVest"=>"战术胸挂","Backpack"=>"背包","Pockets"=>"口袋","SecuredContainer"=>"安全箱","Dogtag"=>"狗牌","mod_scope"=>"瞄具","mod_magazine"=>"弹匣",_=>CharacterDataPanel.Name(key,key)});
 static string Role(string key)=>I18n.Text(AiSpawnCatalog.Options.FirstOrDefault(o=>o.Role.Equals(key,StringComparison.OrdinalIgnoreCase))?.Name.Split(new[]{" · "},StringSplitOptions.None)[0]??CharacterDataPanel.Name(key,key));
 static string Item(string id)=>CharacterDataPanel.Name(id+" Name",id);
 static double ItemValue(JsonElement item)=>LootDisplay.TemplatePrice(Text(item,"Template"))*Math.Max(1,Integer(item,"Count"));
 static double ListValue(JsonElement source,string key)=>source.ValueKind==JsonValueKind.Object&&source.TryGetProperty(key,out var arr)&&arr.ValueKind==JsonValueKind.Array?arr.EnumerateArray().Sum(ItemValue):0;
 static double GroupValue(RaidHistoryPresentation.Equipment group)=>(group.ContextOnly?0:ItemValue(group.Item))+group.Attachments.Sum(ItemValue);
 static string Money(double value)=>value>0?"₽"+Math.Round(value).ToString("N0",CultureInfo.InvariantCulture):"₽0";
 internal static object Diagnostics(bool refresh=false){if(refresh)Tick();var app=UnityEngine.Object.FindObjectOfType<TarkovApplication>();return new{singletonApplication=Singleton<TarkovApplication>.Instance!=null,abstractApplication=Singleton<AbstractApplication>.Instance!=null,sceneApplication=app!=null,sceneBackendProfile=app?.GetClientBackEndSession()?.TryCast<ClientBackendSession>()?.Profile!=null,menuProfile=app?._menuOperation?.QuestController?.Profile!=null,identityReady=Identity().Length>0,ownerReady=owner.Length>0,loaded,received,total,rows=rows.Length,filter,mapFilter,notice,operation,requestPending=request!=null,requestComplete=request?.IsCompleted,nextRefresh,now=Time.unscaledTime};}
 static string Identity(){try{
  // IL2CPP can leave both generic Singleton<T> entries empty even while the
  // application and its backend profile are live in the scene.
  application??=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>();
  if(application==null&&Time.unscaledTime>=nextApplication){nextApplication=Time.unscaledTime+2;application=UnityEngine.Object.FindObjectOfType<TarkovApplication>();}
  var session=application?.GetClientBackEndSession()?.TryCast<ClientBackendSession>()??application?.Session?.TryCast<ClientBackendSession>();
  return session?.Profile?.Id??application?._menuOperation?.QuestController?.Profile?.Id??"";
 }catch{return "";}}
 internal static void Reset(){if(request is {} pending)_=pending.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);if(folderPick is {} pick)_=pick.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);request=null;folderPick=null;rows=Array.Empty<JsonElement>();detail=default;loaded=received=exportSingle=false;owner=picked=notice=exportFormat=exportFolder="";page=total=summaryTotal=summarySurvived=summaryKills=0;expanded.Clear();application=null;nextApplication=0;}
 internal static string EmptyText=>request!=null?"读取中…":owner.Length==0?"等待角色就绪":notice.Length>0?notice:received?"暂无战局记录":"正在读取战局记录…";
 static void Begin(string action){if(request!=null||owner.Length==0)return;operation=action;request=CharacterDataPanel.Call("/local/gear/data","characterData",new(){{"action",action},{"id",picked},{"value",page},{"settings",new Dictionary<string,object>{{"status",filter=="全部状态"?"":filter},{"map",mapFilter=="全部地图"?"":mapFilter}}}}).WaitAsync(TimeSpan.FromSeconds(30));}
 static void StartExport(string format,bool single){if(folderPick!=null||request!=null||owner.Length==0)return;exportFormat=format;exportSingle=single;folderPick=WindowsDialogs.PickFolder(I18n.Text("选择战局记录导出文件夹"));notice="请在 Windows 窗口中选择导出文件夹…";}
 static void WriteExport(string directory,JsonElement[] items){
  Directory.CreateDirectory(directory);string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture);
  string file=Path.Combine(directory,$"raid-history-{stamp}.{exportFormat}");
  if(exportFormat=="json"){
   var payload=items.Length==1&&exportSingle?(object)items[0]:items;
   File.WriteAllText(file,JsonSerializer.Serialize(payload,new JsonSerializerOptions{WriteIndented=true}),new UTF8Encoding(false));
  }else{
   var sb=new StringBuilder("Id,Started,Ended,Map,Side,Nickname,Status,Exit,Seconds,Kills,EntryValue,ReturnedValue,LootValue,Route,TransitDestination\r\n");
   static string Csv(string s)=>"\""+s.Replace("\"","\"\"")+"\"";
   foreach(var r in items){
    int kills=r.TryGetProperty("Kills",out var ka)&&ka.ValueKind==JsonValueKind.Array?ka.GetArrayLength():Integer(r,"kills");
    string route=string.Join(" -> ",RaidHistoryPresentation.Route(r));
    sb.AppendLine(string.Join(",",Csv(Text(r,"Id")),Csv(Text(r,"Started")),Csv(Text(r,"Ended")),Csv(Map(Text(r,"Map"))),Csv(Text(r,"Side")),Csv(Text(r,"Nickname")),Csv(Status(Text(r,"Status"))),Csv(Text(r,"Exit")),Number(r,"Seconds").ToString("0.#",CultureInfo.InvariantCulture),kills.ToString(CultureInfo.InvariantCulture),Math.Round(ListValue(r,"Entry")).ToString(CultureInfo.InvariantCulture),Math.Round(ListValue(r,"Returned")).ToString(CultureInfo.InvariantCulture),Math.Round(ListValue(r,"Loot")).ToString(CultureInfo.InvariantCulture),Csv(route),Csv(Text(r,"TransitDestination"))));
   }
   File.WriteAllText(file,sb.ToString(),new UTF8Encoding(true));
  }
  notice=I18n.Text("已导出战局记录：")+file;
 }
 internal static void Tick(){string current=Identity();if(current!=owner){Reset();owner=current;}if(current.Length==0){notice="等待角色就绪";return;}
  if(folderPick?.IsCompleted==true){var pick=folderPick;folderPick=null;try{string? chosen=pick.GetAwaiter().GetResult();if(string.IsNullOrWhiteSpace(chosen)){notice="已取消导出。";}else if(exportSingle&&detail.ValueKind==JsonValueKind.Object){WriteExport(chosen,new[]{detail});}else{exportFolder=chosen;Begin("raidHistoryExport");}}catch(Exception e){notice=e.GetBaseException().Message;}}
  if(request?.IsCompleted==true){var task=request;request=null;try{using var doc=JsonDocument.Parse(task.GetAwaiter().GetResult());var root=doc.RootElement;if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0)throw new ArgumentException(Text(root,"errmsg"));var data=CharacterDataPanel.Data(root);if(data.TryGetProperty("history",out var history)){var fresh=history.GetProperty("rows").EnumerateArray().Select(r=>r.Clone()).ToArray();int count=history.GetProperty("total").GetInt32();rows=fresh;total=count;received=true;if(history.TryGetProperty("summary",out var sum)&&sum.ValueKind==JsonValueKind.Object){summaryTotal=Integer(sum,"total");summarySurvived=Integer(sum,"survived");summaryKills=Integer(sum,"kills");}else{summaryTotal=count;summarySurvived=fresh.Count(r=>Text(r,"Status")=="SURVIVED");summaryKills=fresh.Sum(r=>Integer(r,"kills"));}notice=Text(data,"warning");}else if(data.TryGetProperty("raid",out var raid)){detail=raid.Clone();notice="";}else if(data.TryGetProperty("export",out var exported)&&exported.ValueKind==JsonValueKind.Array){var list=exported.EnumerateArray().Select(r=>r.Clone()).ToArray();if(exportFolder.Length>0)WriteExport(exportFolder,list);exportFolder="";}else throw new ArgumentException("服务响应无法读取，请刷新。");}catch(Exception e){exportFolder="";notice=e is TimeoutException?"连接失败，请检查本地服务端。":e.Message;nextRefresh=Time.unscaledTime+3;}}
  if(!loaded){loaded=true;Begin("raidHistoryList");nextRefresh=Time.unscaledTime+15;}else if(Time.unscaledTime>=nextRefresh&&request==null&&folderPick==null){nextRefresh=Time.unscaledTime+15;Begin(detail.ValueKind==JsonValueKind.Object?"raidHistoryDetail":"raidHistoryList");}
 }
 internal static void Draw(Rect rect){Tick();float w=rect.width;bool busy=request!=null||folderPick!=null;bool old=GUI.enabled;GUI.enabled=old&&!busy&&owner.Length>0;
  if(detail.ValueKind==JsonValueKind.Object){
   if(InterfaceMenu.Button(new(rect.x,rect.y,94,32),"返回列表")){detail=default;Begin("raidHistoryList");}
   if(InterfaceMenu.Button(new(rect.x+102,rect.y,94,32),"导出 JSON"))StartExport("json",true);
   if(InterfaceMenu.Button(new(rect.x+204,rect.y,88,32),"导出 CSV"))StartExport("csv",true);
   if(tab=="入场装备"&&detail.TryGetProperty("Entry",out var entryArr)&&entryArr.ValueKind==JsonValueKind.Array&&entryArr.GetArrayLength()>0&&InterfaceMenu.Button(new(rect.x+300,rect.y,134,32),"存为整套装备"))try{notice=LoadoutPlans.SaveFromHistory(Map(Text(detail,"Map"))+" "+Stamp(Text(detail,"Started")),entryArr.EnumerateArray().ToArray());}catch(Exception e){notice=e.Message;}
   if(InterfaceMenu.Button(new(rect.xMax-92,rect.y,92,32),"刷新"))Begin("raidHistoryDetail");
   GUI.enabled=old;if(detail.ValueKind==JsonValueKind.Object)DrawDetail(new(rect.x,rect.y+44,w,rect.height-86));
  }
  else{
   if(InterfaceMenu.Button(new(rect.x,rect.y,168,32),filter=="全部状态"?filter:Status(filter))){filter=states[(Array.IndexOf(states,filter)+1)%states.Length];page=0;Begin("raidHistoryList");}
   if(InterfaceMenu.Button(new(rect.x+176,rect.y,146,32),mapFilter=="全部地图"?mapFilter:Map(mapFilter))){mapFilter=maps[(Array.IndexOf(maps,mapFilter)+1)%maps.Length];page=0;Begin("raidHistoryList");}
   if(InterfaceMenu.Button(new(rect.xMax-284,rect.y,90,32),"导出 JSON"))StartExport("json",false);
   if(InterfaceMenu.Button(new(rect.xMax-188,rect.y,86,32),"导出 CSV"))StartExport("csv",false);
   if(InterfaceMenu.Button(new(rect.xMax-96,rect.y,96,32),"刷新列表"))Begin("raidHistoryList");GUI.enabled=old;
   string rate=summaryTotal>0?$"{(summarySurvived*100.0/summaryTotal):0:0.0}%":"0%";
   InterfaceMenu.Label(new(rect.x,rect.y+38,w,24),$"统计：{summaryTotal} 场 · 撤离 {summarySurvived}（{rate}）· 累计击杀 {summaryKills}",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.3f),12,translate:false);
   var area=new Rect(rect.x,rect.y+66,w,Math.Max(80,rect.height-153));listScroll=GUI.BeginScrollView(area,listScroll,new(0,0,w-20,Math.Max(area.height-1,rows.Length*90)),false,true);
   try{float y=0;
    foreach(var row in rows){var color=Text(row,"Status")=="SURVIVED"?new Color(.4f,.85f,.6f):InterfaceMenu.Accent;InterfaceMenu.Surface(new(0,y,w-24,80),Color.Lerp(InterfaceMenu.Background,color,.055f),8);InterfaceMenu.Label(new(14,y+8,w-150,25),Map(Text(row,"Map"))+" · "+Text(row,"Side")+" · "+I18n.Text(Status(Text(row,"Status"))),color,15,translate:false);InterfaceMenu.Label(new(14,y+39,w-160,25),Stamp(Text(row,"Started"))+"   ·   "+Duration(row)+"   ·   "+I18n.Text("击杀")+" "+Integer(row,"kills"),null,12,translate:false);GUI.enabled=old&&!busy;if(InterfaceMenu.Button(new(w-124,y+23,90,34),"查看详情")){picked=Text(row,"Id");expanded.Clear();detailScroll=Vector2.zero;Begin("raidHistoryDetail");}GUI.enabled=old;y+=90;}
    if(rows.Length==0)InterfaceMenu.Paragraph(new(12,20,w-50,60),EmptyText,null,15);
   }finally{GUI.EndScrollView();}float py=rect.yMax-76;GUI.enabled=old&&!busy&&page>0;if(InterfaceMenu.Button(new(rect.x,py,100,30),"上一页")){page--;listScroll=Vector2.zero;Begin("raidHistoryList");}GUI.enabled=old&&!busy&&(page+1)*20<total;if(InterfaceMenu.Button(new(rect.xMax-100,py,100,30),"下一页")){page++;listScroll=Vector2.zero;Begin("raidHistoryList");}GUI.enabled=old;InterfaceMenu.Label(new(rect.x+115,py,w-230,30),$"{page+1} / {Math.Max(1,(total+19)/20)}   ·   {total}",null,13,translate:false);
  }GUI.enabled=old;InterfaceMenu.Paragraph(new(rect.x,rect.yMax-35,w,32),notice.Length>0?notice:busy?"读取中…":"",notice.Length>0?Color.yellow:InterfaceMenu.Accent,12);
 }
 static void DrawDetail(Rect rect){float w=rect.width;InterfaceMenu.Label(new(rect.x,rect.y,w,28),Map(Text(detail,"Map"))+" · "+Text(detail,"Side")+" · "+I18n.Text(Status(Text(detail,"Status"))),InterfaceMenu.Accent,19,translate:false);
  double entryVal=ListValue(detail,"Entry"),retVal=ListValue(detail,"Returned"),lootVal=ListValue(detail,"Loot");
  InterfaceMenu.Label(new(rect.x,rect.y+30,w,22),Stamp(Text(detail,"Started"))+" · "+Duration(detail)+" · "+Text(detail,"Nickname")+" · "+CharacterDataPanel.Name(Text(detail,"Exit"),Text(detail,"Exit")),null,12,translate:false);
  InterfaceMenu.Label(new(rect.x,rect.y+52,w,20),$"估值：入场 {Money(entryVal)} · 带出 {Money(retVal)} · 新增 {Money(lootVal)}",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Accent,.4f),11,translate:false);
  var choices=new[]{"转移路线","入场装备","带出与保留","新增物品","击杀名单"};for(int i=0;i<choices.Length;i++)if(InterfaceMenu.Button(new(rect.x+i*w/choices.Length,rect.y+78,w/choices.Length-6,30),choices[i],tab==choices[i])){tab=choices[i];detailScroll=Vector2.zero;}
  if(tab=="转移路线"){DrawRoute(new(rect.x,rect.y+120,w,Math.Max(60,rect.height-120)));return;}
  bool known=Flag(detail,"EntryKnown"),settled=Flag(detail,"Settled");string key=tab switch{"入场装备"=>"Entry","带出与保留"=>"Returned","新增物品"=>"Loot",_=>"Kills"};var records=detail.TryGetProperty(key,out var array)&&array.ValueKind==JsonValueKind.Array?array.EnumerateArray().ToArray():Array.Empty<JsonElement>();var context=detail.TryGetProperty(key=="Loot"?"Returned":key,out var full)&&full.ValueKind==JsonValueKind.Array?full.EnumerateArray().ToArray():records;var groups=key=="Kills"?Array.Empty<RaidHistoryPresentation.Equipment>():RaidHistoryPresentation.Group(records,context);var area=new Rect(rect.x,rect.y+120,w,Math.Max(60,rect.height-120));float content=key=="Kills"?records.Length*70:groups.Sum(g=>56+(expanded.Contains(Text(g.Item,"Id"))?g.Attachments.Length*40:0));detailScroll=GUI.BeginScrollView(area,detailScroll,new(0,0,w-20,Math.Max(area.height-1,content+60)),false,true);
  try{float y=0;
   string empty=tab=="入场装备"&&!known?"未记录入场装备":tab is "带出与保留" or "新增物品"&&!settled?"等待结算":tab=="新增物品"&&!known?"入场数据缺失，无法比较新增物品":"暂无记录";
   if(records.Length==0)InterfaceMenu.Paragraph(new(10,12,w-40,45),empty,null,14);
   if(key=="Kills")foreach(var row in records){InterfaceMenu.Label(new(10,y,w-45,28),Text(row,"Name")+" · "+Text(row,"Side")+" · Lv."+Integer(row,"Level")+" · "+Role(Text(row,"Role")),InterfaceMenu.Text,14,translate:false);InterfaceMenu.Label(new(10,y+29,w-45,25),Item(Text(row,"Weapon"))+" · "+Part(Text(row,"BodyPart"))+" · "+Number(row,"Distance").ToString("0.0")+" m · "+Text(row,"Time"),null,12,translate:false);y+=70;}
   else foreach(var group in groups){var row=group.Item;string id=Text(row,"Id"),slot=Text(row,"Slot");bool open=expanded.Contains(id);double gv=GroupValue(group);string valTag=gv>0?" · "+Money(gv):"";string label=Item(Text(row,"Template"))+(group.ContextOnly?" · "+I18n.Text("原装备的新增部件"):" × "+Integer(row,"Count"))+" · "+Part(slot)+valTag;InterfaceMenu.Label(new(10,y,w-180,46),label,null,14,translate:false);if(group.Attachments.Length>0&&InterfaceMenu.Button(new(w-162,y+8,132,30),(open?"收起部件":"展开部件")+" ("+group.Attachments.Length+")")){if(open)expanded.Remove(id);else expanded.Add(id);}y+=56;if(open)foreach(var child in group.Attachments){double cv=ItemValue(child);InterfaceMenu.Label(new(28,y,w-60,36),"↳ "+Item(Text(child,"Template"))+" × "+Integer(child,"Count")+" · "+Part(Text(child,"Slot"))+(cv>0?" · "+Money(cv):""),null,12,translate:false);y+=40;}}
  }finally{GUI.EndScrollView();}
 }
 static void DrawRoute(Rect rect){var route=RaidHistoryPresentation.Route(detail);string destination=Text(detail,"TransitDestination");float height=Math.Max(rect.height-1,130+route.Length*60+(destination.Length>0?90:0));detailScroll=GUI.BeginScrollView(rect,detailScroll,new(0,0,rect.width-20,height),false,true);try{float y=8;if(route.Length==0){InterfaceMenu.Paragraph(new(10,y,rect.width-44,64),"此旧记录没有保存转移路线；只知道本段地图，无法还原此前经过。",null,13);y+=72;InterfaceMenu.Label(new(10,y,rect.width-44,30),Map(Text(detail,"Map")),InterfaceMenu.Accent,16,translate:false);y+=40;}else{InterfaceMenu.Paragraph(new(10,y,rect.width-44,44),"按服务端记录顺序显示经过的地图。本段装备、时长和击杀仍属于当前记录。",null,12);y+=52;for(int i=0;i<route.Length;i++){InterfaceMenu.Label(new(10,y,rect.width-44,32),(i+1)+". "+Map(route[i])+(i==route.Length-1?" · "+I18n.Text("本段地图"):""),i==route.Length-1?InterfaceMenu.Accent:null,16,translate:false);y+=34;if(i<route.Length-1){InterfaceMenu.Label(new(20,y,30,22),"↓",null,14,translate:false);y+=26;}}}if(destination.Length>0){InterfaceMenu.Label(new(10,y+8,rect.width-44,34),"→ "+Map(destination),InterfaceMenu.Accent,16,translate:false);InterfaceMenu.Paragraph(new(10,y+46,rect.width-44,44),"转移目的地（结算请求）；是否到达请以目的地图的新记录为准。",null,12);}else if(Text(detail,"Status")=="TRANSIT")InterfaceMenu.Paragraph(new(10,y+8,rect.width-44,48),"已记录地图转移，但服务端未提供目的地。",null,12);}finally{GUI.EndScrollView();}}
}
