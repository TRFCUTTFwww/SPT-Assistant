using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class FleaMarketPanel {
 internal sealed record CustomOffer(
  [property:JsonPropertyName("id")]string Id,
  [property:JsonPropertyName("template")]string Template,
  [property:JsonPropertyName("playerName")]string PlayerName,
  [property:JsonPropertyName("unitPrice")]double UnitPrice,
  [property:JsonPropertyName("stock")]double Stock,
  [property:JsonPropertyName("endTime")]long EndTime,
  [property:JsonPropertyName("rating")]double Rating,
  [property:JsonPropertyName("durability")]double? Durability,
  [property:JsonPropertyName("maxDurability")]double? MaxDurability,
  [property:JsonPropertyName("persistent")]bool Persistent=false,
  [property:JsonPropertyName("active")]bool Active=true,
  [property:JsonPropertyName("status")]string Status="active",
  [property:JsonPropertyName("permanent")]bool Permanent=false,
  [property:JsonPropertyName("offerId")]string? OfferId=null,
  [property:JsonPropertyName("pausedRemainingSeconds")]long? PausedRemainingSeconds=null){
  internal LocalFleaShared.FleaOfferLifetime Lifetime=>new(EndTime,Permanent,PausedRemainingSeconds);
 }
 static string offerFilter="",offerDelete="";
 static int offerState;
 static Vector2 offerScroll;
 internal static long OfferRevision=>stateRevision;
 internal static bool HasOffersLoaded=>prices!=null;
 internal static CustomOffer? FindOffer(string nativeId)=>(prices?.CustomOffers??[]).FirstOrDefault(o=>o.Id==nativeId||o.OfferId==nativeId);
 internal static void OffersChanged(System.Text.Json.JsonElement data,string templateId){
  stateRevision++;
  var next=data.Deserialize<Prices>()??throw new ArgumentException("报价响应无效。");
  if(prices!=null&&selected?.Id!=templateId){prices=prices with{CustomOffers=next.CustomOffers};NativeFleaOfferEdit.RefreshTimingAll();}else ApplyPrices(next,false,false);
  nextPoll=Time.unscaledTime+12;
 }
 private static void DrawCustomOffers(Rect area,bool enabled,bool busy){
  GUI.enabled=enabled&&!busy;
  const float control=34,gap=8,padding=12;
  float top=area.y,w=area.width;
  int columns=w>=360?3:1;
  float actionWidth=(w-gap*(columns-1))/columns;
  Rect Tool(int index)=>new(area.x+index%columns*(actionWidth+gap),top+index/columns*(control+gap),actionWidth,control);
  bool compact=w<660;
  if(InterfaceMenu.Button(Tool(0),"新增报价",true))FleaOfferEditor.Show();
  string edit=compact?(NativeFleaOfferEdit.Enabled?"修改按钮 · 开启":"修改按钮 · 关闭"):(NativeFleaOfferEdit.Enabled?"显示修改按钮 · 开启":"显示修改按钮 · 关闭");
  string withdraw=compact?(NativeFleaOfferEdit.WithdrawEnabled?"下架按钮 · 开启":"下架按钮 · 关闭"):(NativeFleaOfferEdit.WithdrawEnabled?"显示下架按钮 · 开启":"显示下架按钮 · 关闭");
  if(InterfaceMenu.Button(Tool(1),edit,NativeFleaOfferEdit.Enabled))NativeFleaOfferEdit.Toggle();
  if(InterfaceMenu.Button(Tool(2),withdraw,NativeFleaOfferEdit.WithdrawEnabled))NativeFleaOfferEdit.ToggleWithdraw();
  top+=((3+columns-1)/columns)*(control+gap);
  bool stackedSearch=w<360;
  float filterWidth=stackedSearch?w:Math.Min(180,w*.4f);
  float searchWidth=stackedSearch?w:w-filterWidth-gap;
  string nextFilter=InterfaceMenu.Field(new(area.x,top,searchWidth,control),offerFilter);
  if(nextFilter!=offerFilter){offerFilter=nextFilter;offerScroll=Vector2.zero;}
  if(offerFilter.Length==0)InterfaceMenu.Label(new(area.x+8,top,searchWidth-16,control),"搜索物品、卖家或模板 ID…",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.55f),12);
  int state=ClickSelect.Draw(new(stackedSearch?area.x:area.xMax-filterWidth,top+(stackedSearch?control+gap:0),filterWidth,control),"offer-status",new[]{"全部报价","出售中","已下架 / 到期"}[offerState],new[]{"全部报价","出售中","已下架 / 到期"},offerState);
  if(state>=0){offerState=state;offerScroll=Vector2.zero;}
  top+=(stackedSearch?2:1)*(control+gap);
  var offers=(prices?.CustomOffers??[]).Where(o=>(offerState==0||o.Active==(offerState==1))&&
   (offerFilter.Length==0||(o.PlayerName+" "+LootDisplay.TemplateName(o.Template)+" "+o.Template).Contains(offerFilter,StringComparison.OrdinalIgnoreCase))).ToArray();
  InterfaceMenu.Label(new(area.x,top,w,22),I18n.Text("报价数量")+": "+offers.Length,null,12,false,false);
  top+=22+gap;
  var viewport=new Rect(area.x,top,w,Math.Max(1,area.yMax-top));
  float inner=w-22,card=compact?186:142;
  offerScroll=GUI.BeginScrollView(viewport,offerScroll,new(0,0,inner,Math.Max(viewport.height,offers.Length*(card+gap)-gap)));
  try{
   if(offers.Length==0)InterfaceMenu.Paragraph(new(10,12,inner-20,65),"没有匹配的报价。点击“新增报价”创建，持久化报价将在服务端重启后恢复。",null,13);
   for(int i=0;i<offers.Length;i++){
    var offer=offers[i];float y=i*(card+gap),textWidth=inner-padding*2-(compact?0:90+gap);
    InterfaceMenu.Surface(new(0,y,inner,card),Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.05f),7);
    InterfaceMenu.Paragraph(new(padding,y+padding,textWidth,compact?32:26),LootDisplay.TemplateName(offer.Template),InterfaceMenu.Accent,14);
    float info=y+(compact?50:44);
    string seller=offer.PlayerName;
    if(offer.Durability is {} durability)seller+=" · "+I18n.Text("耐久")+$" {durability:0.##}/{offer.MaxDurability:0.##}";
    InterfaceMenu.Label(new(padding,info,textWidth,22),seller,null,12,false,false);
    string details="₽"+offer.UnitPrice.ToString("N0")+" · "+I18n.Text("存量")+" "+offer.Stock.ToString("N0")+" · "+I18n.Text("市场信誉")+" "+offer.Rating.ToString("0.##");
    InterfaceMenu.Label(new(padding,info+26,textWidth,22),details,null,12,false,false);
    long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    string lifetime=offer.Lifetime.Paused?I18n.Text("已暂停")+" "+offer.Lifetime.Clock(now):offer.Lifetime.IsPermanent?I18n.Text("永久"):offer.Lifetime.Clock(now);
    string status=offer.Status switch{"sold"=>"已售罄","expired"=>"已到期","withdrawn"=>"已下架","unavailable"=>"物品不可用","pending"=>"等待恢复",_=>"出售中"};
    InterfaceMenu.Label(new(padding,info+52,textWidth,22),I18n.Text(status)+" · "+lifetime+" · "+I18n.Text(offer.Persistent?"SQLite 已保存":"临时报价"),null,12,false,false);
    float buttonWidth=compact?(inner-padding*2-gap*2)/3:90;
    Rect Action(int index)=>compact?new(padding+index*(buttonWidth+gap),y+card-padding-control,buttonWidth,control):new(inner-padding-buttonWidth,y+padding+index*(control+gap),buttonWidth,control);
    if(InterfaceMenu.Button(Action(0),"编辑")){offerDelete="";FleaOfferEditor.Show(offer);}
    bool confirming=offerDelete==offer.Id;
    if(InterfaceMenu.Button(Action(1),confirming?"确认删除":"删除")){
     if(confirming){Begin("fleaOfferDelete",offerId:offer.Id);offerDelete="";}else offerDelete=offer.Id;
    }
    if(offer.Persistent&&InterfaceMenu.Button(Action(2),offer.Active?"下架":"重新上架")){
     Begin(offer.Active?"fleaOfferWithdraw":"fleaOfferRecreate",offerId:offer.Id);offerDelete="";
    }
   }
  }finally{GUI.EndScrollView();GUI.enabled=enabled;}
 }
}
