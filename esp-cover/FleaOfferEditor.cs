using System.Globalization;
using System.Text.Json;
using EFT.InventoryLogic;
using UnityEngine.EventSystems;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class FleaOfferEditor {
 static readonly MenuInputGate input=new();
 static EventSystem? nativeInput;static bool nativeInputEnabled;
 static bool open,inactive;
 static string id="",template="",search="",name="Player",price="10000",stock="1",minutes="1440",rating="1",durability="",maximum="",originalMinutes="",notice="";
 static bool persistent=true,paused;
 static Task<string>? saving,reading;
 static Vector2 scroll;
 static ChatItemSearch? catalog;static string cachedSearch="\0";static bool cachedEnglish;
 static ChatItemSearch.Item[] items=[];static string[] labels=[];
 internal static bool Open=>open;
 internal static bool InputBlocked=>input.Blocked;
 internal static bool Saving=>saving!=null||reading!=null;
 static string Number(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
 internal static void Show(FleaMarketPanel.CustomOffer? offer=null){
  if(Open||Saving)return;
  id=offer?.Id??"";template=offer?.Template??"";name=offer?.PlayerName??"Player";
  price=Number(offer?.UnitPrice??10000);stock=Number(offer?.Stock??1);rating=Number(offer?.Rating??1);
  minutes=offer==null?"1440":offer.Permanent?"-1":Math.Max(0,(long)Math.Ceiling((offer.EndTime-DateTimeOffset.UtcNow.ToUnixTimeSeconds())/60d)).ToString(CultureInfo.InvariantCulture);
  paused=offer?.PausedRemainingSeconds!=null;if(offer?.PausedRemainingSeconds is {} frozen)minutes=((long)Math.Ceiling(frozen/60d)).ToString(CultureInfo.InvariantCulture);
  originalMinutes=minutes;durability=offer?.Durability is {} d?Number(d):"";maximum=offer?.MaxDurability is {} m?Number(m):"";
  persistent=offer?.Persistent??true;inactive=offer!=null&&!offer.Active;
  search="";notice="";scroll=Vector2.zero;open=true;input.SetOpen(true,Time.frameCount);
  nativeInput=EventSystem.current;if(nativeInput!=null){nativeInputEnabled=nativeInput.enabled;nativeInput.enabled=false;}
 }
 internal static void Show(EFT.UI.Ragfair.Offer offer){
  if(Open||Saving)return;
  var repair=offer.Item?.GetItemComponent<RepairableComponent>();
  long expiry=new DateTimeOffset(DateTime.FromBinary(offer.EndTime.ToBinary()).ToUniversalTime()).ToUnixTimeSeconds();
  Show(new FleaMarketPanel.CustomOffer(offer.Id,offer.Item?.StringTemplateId??"",offer.User?.Nickname??"Player",offer.RequirementsCost,offer.Quantity,expiry,offer.User?.Rating??0,repair?.Durability,repair?.MaxDurability,false,true,"active",expiry>=253402300799));
  reading=CharacterDataPanel.Call("/local/gear/data","characterData",new Dictionary<string,object?>{["action"]="fleaPriceStatus",["template"]=template});
  notice="正在读取报价保存状态…";
 }
 static int Integer(string text,string caption)=>int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var n)?n:throw new ArgumentException(caption+": 请输入有效整数。");
 static double Decimal(string text,string caption)=>double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&double.IsFinite(n)?n:throw new ArgumentException(caption+": 请输入有限数值。");
 static void Save(){try{
  if(template.Length==0)throw new ArgumentException("请先选择出售物品。");
  var payload=new Dictionary<string,object?>{["action"]=id.Length==0?"fleaOfferAdd":"fleaOfferEdit",["id"]=id,["template"]=template,["fleaOffer"]=new{
   playerName=name,unitPrice=Integer(price,"单价"),stock=Integer(stock,"存量"),remainingMinutes=Integer(minutes,"剩余分钟"),rating=Decimal(rating,"市场信誉"),
   durability=string.IsNullOrWhiteSpace(durability)?(double?)null:Decimal(durability,"当前耐久"),maxDurability=string.IsNullOrWhiteSpace(maximum)?(double?)null:Decimal(maximum,"最大耐久"),persistent,paused,
   preserveExpiry=id.Length>0&&!inactive&&minutes==originalMinutes}};
  saving=CharacterDataPanel.Call("/local/gear/data","characterData",payload);notice="正在保存报价…";
 }catch(Exception e){notice=e.Message;}}
 internal static void Tick(){
  input.Tick(Time.frameCount,Input.GetMouseButton(0)||Input.GetMouseButton(1)||Input.GetMouseButton(2));
  if(reading?.IsCompleted==true){try{
   using var doc=JsonDocument.Parse(reading.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0)throw new ArgumentException("读取报价状态失败，请重试。");
   FleaMarketPanel.OffersChanged(CharacterDataPanel.Data(root),template);
   var saved=FleaMarketPanel.FindOffer(id);if(saved!=null){id=saved.Id;persistent=saved.Persistent;paused=saved.PausedRemainingSeconds!=null;minutes=saved.PausedRemainingSeconds is {} frozen?((long)Math.Ceiling(frozen/60d)).ToString(CultureInfo.InvariantCulture):saved.Permanent?"-1":minutes;originalMinutes=minutes;}notice="";
  }catch(Exception e){Close();FeatureToasts.Show(e.Message);}finally{reading=null;}}
  if(saving?.IsCompleted==true){try{
   using var doc=JsonDocument.Parse(saving.GetAwaiter().GetResult());var root=doc.RootElement;
   if(root.TryGetProperty("err",out var error)&&error.GetInt32()!=0)throw new ArgumentException(root.TryGetProperty("errmsg",out var message)?message.GetString():"保存失败。");
   FleaMarketPanel.OffersChanged(CharacterDataPanel.Data(root),template);Close();FeatureToasts.Show("报价已保存，刷新游戏跳蚤列表即可查看。");
  }catch(Exception e){notice=e.Message;}finally{saving=null;}}
  if(Open&&!Saving&&Input.GetKeyDown(KeyCode.Escape)&&!ClickSelect.IsOpen)Close();
 }
 static string Field(float x,float y,float width,string label,string value){InterfaceMenu.Label(new(x,y,width,22),label,null,12);return InterfaceMenu.Field(new(x,y+24,width,29),value);}
 internal static void Draw(){
  if(!open)return;bool enabled=GUI.enabled;var color=GUI.color;
  try{
   GUI.color=new(0,0,0,.65f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=color;
   float width=Math.Min(640,Screen.width-20),height=Math.Min(670,Screen.height-20);
   var bounds=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);InterfaceMenu.Panel(bounds);
   float left=bounds.x+20,top=bounds.y+14,w=width-40;
   InterfaceMenu.Label(new(left,top,w,32),id.Length==0?"新增报价":"修改报价",InterfaceMenu.Accent,18);
   GUI.enabled=enabled&&!Saving;
   var area=new Rect(left,top+44,w,height-172);
   scroll=GUI.BeginScrollView(area,scroll,new(0,0,w-22,478));
   try{
    float inner=w-22,cell=(inner-12)/2;
    search=Field(0,0,inner,"搜索出售物品",search);
    var source=ItemSpawner.CommandItems;
    if(catalog!=source||cachedSearch!=search||cachedEnglish!=I18n.English){catalog=source;cachedSearch=search;cachedEnglish=I18n.English;items=source.Search(search);labels=items.Select(i=>I18n.English?i.Title:i.Name).ToArray();}
    int selected=Array.FindIndex(items,i=>i.Id==template);
    int chosen=ClickSelect.Draw(new(0,64,inner,30),"offer-editor-item",template.Length==0?"选择出售物品…":LootDisplay.TemplateName(template),labels,selected);
    if(chosen>=0&&template!=items[chosen].Id){template=items[chosen].Id;durability=maximum="";}
    InterfaceMenu.Label(new(0,100,inner,22),template,null,11,false,false);
    name=Field(0,130,cell,"玩家名",name);price=Field(cell+12,130,cell,"单价（卢布）",price);
    stock=Field(0,194,cell,"存量",stock);minutes=Field(cell+12,194,cell,"剩余分钟（负数永久）",minutes);
    rating=Field(0,258,inner,"市场信誉",rating);
    durability=Field(0,322,cell,"当前耐久（可留空）",durability);maximum=Field(cell+12,322,cell,"最大耐久（可留空）",maximum);
    if(InterfaceMenu.Button(new(0,390,inner,30),persistent?"保存到 SQLite · 开启":"保存到 SQLite · 关闭",persistent))persistent=!persistent;
    if(InterfaceMenu.Button(new(0,430,inner,30),paused?"暂停剩余时间 · 开启":"暂停剩余时间 · 关闭",paused))paused=!paused;
   }finally{GUI.EndScrollView();}
   InterfaceMenu.Paragraph(new(left,bounds.yMax-112,w,64),notice.Length>0?notice:inactive?"保存后重新上架；存量和有效期按本次设置。":"未修改剩余分钟时保留原到期时间。暂停时保留剩余时长，关闭暂停后继续倒计时。SQLite 重启恢复；负数永久。耐久同时留空保留原值。",null,12);
   if(InterfaceMenu.Button(new(left,bounds.yMax-44,(w-12)/2,30),"取消"))Close();
   GUI.enabled=enabled&&!Saving&&template.Length>0;
   if(InterfaceMenu.Button(new(left+(w+12)/2,bounds.yMax-44,(w-12)/2,30),inactive?"保存并重新上架":id.Length==0?"添加报价":"保存修改",true))Save();
  }finally{GUI.enabled=enabled;GUI.color=color;var e=Event.current;if(e!=null&&(e.isMouse||e.type==EventType.ScrollWheel))e.Use();}
 }
 static void Close(){open=false;input.SetOpen(false,Time.frameCount);RestoreNativeInput();}
 static void RestoreNativeInput(){if(nativeInput!=null)nativeInput.enabled=nativeInputEnabled;nativeInput=null;}
 internal static void Reset(){RestoreNativeInput();open=false;saving=reading=null;input.Reset();}
}
