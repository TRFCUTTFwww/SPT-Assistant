using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class RaidHistoryPanel {
 static JsonElement[] rows=Array.Empty<JsonElement>();static JsonElement detail;static Task<string>? request;static string operation="",picked="",notice="",tab="入场装备",owner="",filter="全部状态";static int page,total;static bool loaded,received;static Vector2 listScroll,detailScroll;static float nextRefresh;
 static TarkovApplication? application;static float nextApplication;
 static readonly string[] states={"全部状态","PENDING","SURVIVED","RUNNER","KILLED","MISSINGINACTION","LEFT","TRANSIT","SAVE_FAILED"};
 static string Text(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()??"":"";
 static string Status(string value)=>value switch{"PENDING"=>"进行中／未结算","SURVIVED"=>"成功撤离","RUNNER"=>"匆匆逃离","KILLED"=>"阵亡","MISSINGINACTION"=>"失踪","LEFT"=>"离开战局","TRANSIT"=>"地图过渡","SAVE_FAILED"=>"结算保存失败",_=>"未知状态"};
 static string Stamp(string s)=>DateTime.TryParse(s,out var t)?t.ToLocalTime().ToString("MM-dd HH:mm"):"—";
 static string Duration(JsonElement e)=>e.TryGetProperty("Seconds",out var n)&&n.ValueKind==JsonValueKind.Number?RaidRules.Clock(n.GetDouble()):"—";
 static string Map(string id)=>I18n.Text(id.ToLowerInvariant() switch{"bigmap"=>"海关","factory4_day"=>"工厂（白天）","factory4_night"=>"工厂（夜间）","woods"=>"森林","shoreline"=>"海岸线","interchange"=>"立交桥","rezervbase"=>"储备站","lighthouse"=>"灯塔","tarkovstreets"=>"塔科夫街区","sandbox" or "sandbox_high" or "sandbox_start"=>"中心区","laboratory"=>"实验室","labyrinth"=>"迷宫",_=>CharacterDataPanel.Name(id+" Name",id)});
 static string Part(string key)=>I18n.Text(key switch{"Head"=>"头部","Chest"=>"胸部","Stomach"=>"腹部","LeftArm"=>"左臂","RightArm"=>"右臂","LeftLeg"=>"左腿","RightLeg"=>"右腿","FirstPrimaryWeapon"=>"第一主武器","SecondPrimaryWeapon"=>"第二主武器","Holster"=>"手枪","Scabbard"=>"近战武器","Headwear"=>"头部装备","FaceCover"=>"面罩","Eyewear"=>"眼镜","Earpiece"=>"耳机","ArmorVest"=>"防弹衣","TacticalVest"=>"战术胸挂","Backpack"=>"背包","Pockets"=>"口袋","SecuredContainer"=>"安全箱","Dogtag"=>"狗牌","mod_scope"=>"瞄具","mod_magazine"=>"弹匣",_=>CharacterDataPanel.Name(key,key)});
 static string Role(string key)=>I18n.Text(AiSpawnCatalog.Options.FirstOrDefault(o=>o.Role.Equals(key,StringComparison.OrdinalIgnoreCase))?.Name.Split(new[]{" · "},StringSplitOptions.None)[0]??CharacterDataPanel.Name(key,key));
 static string Item(string id)=>CharacterDataPanel.Name(id+" Name",id);
 internal static object Diagnostics(bool refresh=false){if(refresh)Tick();var app=UnityEngine.Object.FindObjectOfType<TarkovApplication>();return new{singletonApplication=Singleton<TarkovApplication>.Instance!=null,abstractApplication=Singleton<AbstractApplication>.Instance!=null,sceneApplication=app!=null,sceneBackendProfile=app?.GetClientBackEndSession()?.TryCast<ClientBackendSession>()?.Profile!=null,menuProfile=app?._menuOperation?.QuestController?.Profile!=null,identityReady=Identity().Length>0,ownerReady=owner.Length>0,loaded,received,total,rows=rows.Length,filter,notice,operation,requestPending=request!=null,requestComplete=request?.IsCompleted,nextRefresh,now=Time.unscaledTime};}
 static string Identity(){try{
  // IL2CPP can leave both generic Singleton<T> entries empty even while the
  // application and its backend profile are live in the scene.
  application??=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>();
  if(application==null&&Time.unscaledTime>=nextApplication){nextApplication=Time.unscaledTime+2;application=UnityEngine.Object.FindObjectOfType<TarkovApplication>();}
  var session=application?.GetClientBackEndSession()?.TryCast<ClientBackendSession>()??application?.Session?.TryCast<ClientBackendSession>();
  return session?.Profile?.Id??application?._menuOperation?.QuestController?.Profile?.Id??"";
 }catch{return "";}}
 internal static void Reset(){if(request is {} pending)_=pending.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);request=null;rows=Array.Empty<JsonElement>();detail=default;loaded=received=false;owner=picked=notice="";page=total=0;application=null;nextApplication=0;}
 internal static string EmptyText=>request!=null?"读取中…":owner.Length==0?"等待角色就绪":notice.Length>0?notice:received?"暂无战局记录":"正在读取战局记录…";
 static void Begin(string action){if(request!=null||owner.Length==0)return;operation=action;request=CharacterDataPanel.Call("/local/gear/data","characterData",new(){{"action",action},{"id",picked},{"value",page},{"settings",new Dictionary<string,object>{{"status",filter=="全部状态"?"":filter}}}}).WaitAsync(TimeSpan.FromSeconds(30));}
 internal static void Tick(){string current=Identity();if(current!=owner){Reset();owner=current;}if(current.Length==0){notice="等待角色就绪";return;}
  if(request?.IsCompleted==true){var task=request;request=null;try{using var doc=JsonDocument.Parse(task.GetAwaiter().GetResult());var root=doc.RootElement;if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0)throw new ArgumentException(Text(root,"errmsg"));var data=CharacterDataPanel.Data(root);if(data.TryGetProperty("history",out var history)){var fresh=history.GetProperty("rows").EnumerateArray().Select(r=>r.Clone()).ToArray();int count=history.GetProperty("total").GetInt32();rows=fresh;total=count;received=true;notice=Text(data,"warning");}else if(data.TryGetProperty("raid",out var raid)){detail=raid.Clone();notice="";}else throw new ArgumentException("服务响应无法读取，请刷新。");}catch(Exception e){notice=e is TimeoutException?"连接失败，请检查本地服务端。":e.Message;nextRefresh=Time.unscaledTime+3;}}
  if(!loaded){loaded=true;Begin("raidHistoryList");nextRefresh=Time.unscaledTime+15;}else if(Time.unscaledTime>=nextRefresh&&request==null){nextRefresh=Time.unscaledTime+15;Begin(detail.ValueKind==JsonValueKind.Object?"raidHistoryDetail":"raidHistoryList");}
 }
 internal static void Draw(Rect rect){Tick();float w=rect.width;bool busy=request!=null;bool old=GUI.enabled;GUI.enabled=old&&!busy&&owner.Length>0;
  if(detail.ValueKind==JsonValueKind.Object){if(InterfaceMenu.Button(new(rect.x,rect.y,100,32),"返回列表")){detail=default;Begin("raidHistoryList");}if(InterfaceMenu.Button(new(rect.xMax-100,rect.y,100,32),"刷新"))Begin("raidHistoryDetail");GUI.enabled=old;DrawDetail(new(rect.x,rect.y+44,w,rect.height-86));}
  else{if(InterfaceMenu.Button(new(rect.x,rect.y,190,32),filter=="全部状态"?filter:Status(filter))){filter=states[(Array.IndexOf(states,filter)+1)%states.Length];page=0;Begin("raidHistoryList");}if(InterfaceMenu.Button(new(rect.xMax-100,rect.y,100,32),"刷新列表"))Begin("raidHistoryList");GUI.enabled=old;
   var area=new Rect(rect.x,rect.y+46,w,Math.Max(80,rect.height-133));listScroll=GUI.BeginScrollView(area,listScroll,new(0,0,w-20,Math.Max(area.height-1,rows.Length*90)),false,true);float y=0;
   foreach(var row in rows){var color=Text(row,"Status")=="SURVIVED"?new Color(.4f,.85f,.6f):InterfaceMenu.Accent;InterfaceMenu.Surface(new(0,y,w-24,80),Color.Lerp(InterfaceMenu.Background,color,.055f),8);InterfaceMenu.Label(new(14,y+8,w-150,25),Map(Text(row,"Map"))+" · "+Text(row,"Side")+" · "+I18n.Text(Status(Text(row,"Status"))),color,15,translate:false);InterfaceMenu.Label(new(14,y+39,w-160,25),Stamp(Text(row,"Started"))+"   ·   "+Duration(row)+"   ·   "+I18n.Text("击杀")+" "+row.GetProperty("kills").GetInt32(),null,12,translate:false);GUI.enabled=old&&!busy;if(InterfaceMenu.Button(new(w-124,y+23,90,34),"查看详情")){picked=Text(row,"Id");detailScroll=Vector2.zero;Begin("raidHistoryDetail");}GUI.enabled=old;y+=90;}
   if(rows.Length==0)InterfaceMenu.Paragraph(new(12,20,w-50,60),EmptyText,null,15);GUI.EndScrollView();float py=rect.yMax-76;GUI.enabled=old&&!busy&&page>0;if(InterfaceMenu.Button(new(rect.x,py,100,30),"上一页")){page--;listScroll=Vector2.zero;Begin("raidHistoryList");}GUI.enabled=old&&!busy&&(page+1)*20<total;if(InterfaceMenu.Button(new(rect.xMax-100,py,100,30),"下一页")){page++;listScroll=Vector2.zero;Begin("raidHistoryList");}GUI.enabled=old;InterfaceMenu.Label(new(rect.x+115,py,w-230,30),$"{page+1} / {Math.Max(1,(total+19)/20)}   ·   {total}",null,13,translate:false);
  }GUI.enabled=old;InterfaceMenu.Paragraph(new(rect.x,rect.yMax-35,w,32),notice.Length>0?notice:busy?"读取中…":"",notice.Length>0?Color.yellow:InterfaceMenu.Accent,12);
 }
 static void DrawDetail(Rect rect){float w=rect.width;InterfaceMenu.Label(new(rect.x,rect.y,w,28),Map(Text(detail,"Map"))+" · "+Text(detail,"Side")+" · "+I18n.Text(Status(Text(detail,"Status"))),InterfaceMenu.Accent,19,translate:false);InterfaceMenu.Label(new(rect.x,rect.y+32,w,25),Stamp(Text(detail,"Started"))+"   ·   "+Duration(detail)+"   ·   "+Text(detail,"Nickname")+"   ·   "+CharacterDataPanel.Name(Text(detail,"Exit"),Text(detail,"Exit")),null,12,translate:false);
  var choices=new[]{"入场装备","带出与保留","新增物品","击杀名单"};for(int i=0;i<choices.Length;i++)if(InterfaceMenu.Button(new(rect.x+i*w/4,rect.y+70,w/4-6,32),choices[i],tab==choices[i])){tab=choices[i];detailScroll=Vector2.zero;}
  bool known=detail.TryGetProperty("EntryKnown",out var entry)&&entry.GetBoolean(),settled=detail.TryGetProperty("Settled",out var done)&&done.GetBoolean();string key=tab switch{"入场装备"=>"Entry","带出与保留"=>"Returned","新增物品"=>"Loot",_=>"Kills"};var records=detail.TryGetProperty(key,out var array)?array.EnumerateArray().ToArray():Array.Empty<JsonElement>();var area=new Rect(rect.x,rect.y+116,w,Math.Max(60,rect.height-116));detailScroll=GUI.BeginScrollView(area,detailScroll,new(0,0,w-20,Math.Max(area.height-1,records.Length*(key=="Kills"?70:48)+60)),false,true);float y=0;
  string empty=tab=="入场装备"&&!known?"未记录入场装备":tab is "带出与保留" or "新增物品"&&!settled?"等待结算":tab=="新增物品"&&!known?"入场数据缺失，无法比较新增物品":"暂无记录";
  if(records.Length==0)InterfaceMenu.Paragraph(new(10,12,w-40,45),empty,null,14);
  foreach(var row in records){if(key=="Kills"){InterfaceMenu.Label(new(10,y,w-45,28),Text(row,"Name")+" · "+Text(row,"Side")+" · Lv."+row.GetProperty("Level").GetInt32()+" · "+Role(Text(row,"Role")),InterfaceMenu.Text,14,translate:false);InterfaceMenu.Label(new(10,y+29,w-45,25),Item(Text(row,"Weapon"))+" · "+Part(Text(row,"BodyPart"))+" · "+row.GetProperty("Distance").GetDouble().ToString("0.0")+" m · "+Text(row,"Time"),null,12,translate:false);y+=70;}else{string slot=Text(row,"Slot");InterfaceMenu.Label(new(10,y,w-45,38),Item(Text(row,"Template"))+" × "+row.GetProperty("Count").GetInt32()+"   ·   "+Part(slot),null,14,translate:false);y+=48;}}
  GUI.EndScrollView();
 }
}
