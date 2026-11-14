using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace ProjectE.Client;

internal sealed partial class Transmutation {
 McGui? gui;bool selectingOutput,affordableOnly;
 readonly Dictionary<string,Item> samples=[];
 string query="",inventoryQuery="",quantity="1",selectedItem="",selectedLearned="",tooltip="";
 Vector2 inventoryScroll;float knowledgeScroll;
 // ProjectE mc1.21.1 TransmutationContainer: original 228x196 panel and slot coordinates.
 static readonly (int X,int Y)[] inputSlots=[(43,23),(34,41),(52,41),(16,50),(70,50),(34,59),(52,59),(43,77)];
 static readonly (int X,int Y)[] outputSlots=[(158,9),(176,13),(193,30),(199,50),(193,70),(176,87),(158,91),(140,87),(123,70),(116,50),(123,30),(140,13),(158,31),(177,50),(158,69),(139,50)];
 const float ArtX=8,ArtY=42,Pixel=3,CanvasWidth=990,CanvasHeight=704;
 static Rect SlotAt(int x,int y)=>new(ArtX+x*Pixel,ArtY+y*Pixel,16*Pixel,16*Pixel);
 Item? Chosen=>items.FirstOrDefault(i=>i.Id.ToString()==selectedItem);
 Item? Sample(string id){
  if(id.Length==0)return null;if(samples.TryGetValue(id,out var item))return item;
  try{var factory=Singleton<ItemFactory>.Instance;if(factory==null)return null;return samples[id]=factory.CreateItem(factory.NextId.ToString(),id,null);}catch{return null;}
 }
 internal void Draw(){
  if(!Open)return;gui??=new(resources);gui.InitializeFont();
  var oldMatrix=GUI.matrix;var oldColor=GUI.color;bool oldEnabled=GUI.enabled;int oldDepth=GUI.depth;
  GUI.depth=-8000;GUI.color=Color.white;GUI.enabled=true;
  float scale=Math.Min(1,Math.Min((Screen.width-24)/CanvasWidth,(Screen.height-24)/CanvasHeight));
  float left=Mathf.Floor((Screen.width-CanvasWidth*scale)/2),top=Mathf.Floor((Screen.height-CanvasHeight*scale)/2);
  try{
   GUI.matrix=Matrix4x4.identity;McGui.Fill(new(0,0,Screen.width,Screen.height),new(0,0,0,.78f));
   GUI.matrix=Matrix4x4.TRS(new(left,top,0),Quaternion.identity,new(scale,scale,1));tooltip="";
   gui.Label(new(8,2,460,32),"转化桌  /  ProjectE",Color.white,21);
   gui.Label(new(482,4,456,28),Busy?"正在处理…":ready?"已连接 · 1 卢布 = 1 EMC":"等待连接",ready?new(.6f,1,.55f):new(1,.8f,.4f),14);
   if(gui.Button(new(950,4,32,28),"×",!(Busy&&Working)))SetOpen(false);
   // Draw the entire upstream panel, excluding the transparent atlas padding.
   var texture=resources.Texture("transmute");
   GUI.DrawTextureWithTexCoords(new(ArtX,ArtY,228*Pixel,196*Pixel),texture,new(0,1-196f/texture.height,228f/texture.width,196f/texture.height));
   gui.Label(new(ArtX+18,ArtY+12,220,30),"消耗 / 学习",null,16);
   string search=gui.Field(new(ArtX+83*Pixel,ArtY+8*Pixel,55*Pixel,27),query);
   if(search!=query){query=search;knowledgeScroll=0;}
   if(query.Length==0)gui.Label(new(ArtX+83*Pixel+5,ArtY+8*Pixel,155,27),"搜索已学物品",new(.7f,.7f,.7f),12);
   var filtered=items.Where(i=>Matches(i.StringTemplateId,inventoryQuery)).ToArray();
   var known=learned.Where(prices.ContainsKey).Where(id=>Matches(id,query)).Where(id=>!affordableOnly||prices[id].Value<=balance).OrderByDescending(id=>prices[id].Value).ThenBy(Name).ToArray();
   inventoryScroll.y=Math.Clamp(inventoryScroll.y,0,ScrollRules.Maximum(filtered.Length,9,54,216));
   knowledgeScroll=Math.Clamp(knowledgeScroll,0,Math.Max(0,known.Length-16));
   Rect knowledgeArea=new(ArtX+115*Pixel,ArtY+7*Pixel,98*Pixel,91*Pixel);
   if(Event.current.type==EventType.ScrollWheel&&knowledgeArea.Contains(Event.current.mousePosition)&&!Busy){knowledgeScroll=ScrollRules.Step(knowledgeScroll,Event.current.delta.y,known.Length,16);Event.current.Use();}
   bool wasEnabled=GUI.enabled;GUI.enabled=!Busy&&known.Length>16;
   knowledgeScroll=GUI.VerticalScrollbar(new(ArtX+218*Pixel,ArtY+15*Pixel,16,84*Pixel),knowledgeScroll,16,0,Math.Max(16,known.Length));GUI.enabled=wasEnabled;
   int firstKnown=(int)knowledgeScroll,firstInput=(int)(inventoryScroll.y/54)*9;
   // Quick inputs follow the visible inventory rows; the ring scrolls independently.
   for(int n=0;n<inputSlots.Length;n++){
    int index=firstInput+n;var item=index<filtered.Length?filtered[index]:null;
    DrawInventorySlot(SlotAt(inputSlots[n].X,inputSlots[n].Y),item);
   }
   for(int n=0;n<outputSlots.Length;n++){
    int index=firstKnown+n;string id=index<known.Length?known[index]:"";
    Rect r=SlotAt(outputSlots[n].X,outputSlots[n].Y);
    if(id.Length>0){
     if(selectingOutput&&id==selectedLearned)Highlight(r);
     gui.Icon(r,Sample(id),id);
     if(prices[id].Value>balance)McGui.Fill(r,new(0,0,0,.5f));
     Hover(r,Name(id)+$"\n{prices[id].Value:N0} EMC / 件"+(prices[id].Value>balance?"\nEMC 不足，先消耗库存物品":"\n点击选择，再点击取出物品"));
    }
    if(McGui.Hit(r,id.Length>0&&!Busy)){selectedLearned=id;selectingOutput=true;quantity="1";resources.Play("tock",Volume*.5f);}
   }
   Rect center=SlotAt(158,50);if(selectedLearned.Length>0){gui.Icon(center,Sample(selectedLearned),selectedLearned);Hover(center,"已选择："+Name(selectedLearned));}
   var selected=Chosen;Rect fire=SlotAt(107,97);
   if(selected!=null){gui.Icon(fire,selected,selected.StringTemplateId);Hover(fire,"消耗并学习："+Name(selected.StringTemplateId)+"\n"+ActionProblem(true));}
   else Hover(fire,"消耗槽：先点击下方库存中的物品");
   if(McGui.Hit(fire,!Busy&&selected!=null&&ActionProblem(true).Length==0)){selectingOutput=false;Exchange(true);}
   Rect reset=SlotAt(89,97);Hover(reset,"清除所选物品（不会删除库存）");
   if(McGui.Hit(reset,!Busy)){selectedItem=selectedLearned="";quantity="1";}
   gui.Label(new(ArtX+18,ArtY+91*Pixel,225,26),"EMC",new(.2f,.3f,.15f),17);
   gui.Label(new(ArtX+18,ArtY+101*Pixel,225,26),ready?$"{balance:N0}":"—",new(.15f,.32f,.08f),balance>999999999?13:18);
   var viewport=new Rect(ArtX+35*Pixel,ArtY+117*Pixel,9*54+18,216);
   McGui.Fill(viewport,new(.12f,.12f,.12f));
   bool inventoryInput=viewport.Contains(Event.current.mousePosition);
   inventoryScroll=GUI.BeginScrollView(viewport,inventoryScroll,new Rect(0,0,9*54,Math.Max(216,((filtered.Length+8)/9)*54)),false,true);
   try{int row=(int)(inventoryScroll.y/54);for(int n=row*9;n<Math.Min(filtered.Length,(row+5)*9);n++){var r=new Rect(n%9*54,n/9*54,48,48);McGui.Slot(r);DrawInventorySlot(r,filtered[n],inventoryInput);}}
   finally{GUI.EndScrollView();}
   if(filtered.Length==0){McGui.Fill(new(ArtX+102,ArtY+354,486,148),new(.1f,.1f,.1f,.85f));gui.Wrapped(new(ArtX+120,ArtY+366,448,120),access==null?"请先打开 PMC 角色库存，再点击右侧刷新。":inventoryQuery.Length>0?"没有匹配的库存物品，清空筛选后重试。":"背包、弹挂和口袋中没有物品。\n将物品放进这些容器后即可选择。\n大厅还会读取仓库。",Color.white,16);}
   DrawControls(known.Length,filtered.Length);
   gui.Label(new(8,638,252,28),$"库存 {filtered.Length} 件",Color.white,13);
   gui.Label(new(274,638,54,28),"筛选",Color.white,13);
   string inventorySearch=gui.Field(new(326,638,366,28),inventoryQuery);if(inventorySearch!=inventoryQuery){inventoryQuery=inventorySearch;inventoryScroll=Vector2.zero;}
   if(tooltip.Length>0){var mouse=Event.current.mousePosition;var tr=new Rect(Math.Clamp(mouse.x+16,0,CanvasWidth-330),Math.Clamp(mouse.y+18,0,CanvasHeight-116),330,116);McGui.Fill(tr,new(.08f,.02f,.14f,.98f));McGui.Fill(new(tr.x+2,tr.y+2,tr.width-4,2),new(.4f,.2f,.65f));gui.Wrapped(new(tr.x+10,tr.y+6,tr.width-20,tr.height-12),tooltip,Color.white,14);}
   if(Event.current.type is EventType.MouseDown or EventType.MouseUp or EventType.ScrollWheel or EventType.KeyDown or EventType.KeyUp)Event.current.Use();
  }finally{GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.enabled=oldEnabled;GUI.depth=oldDepth;}
 }
 void DrawInventorySlot(Rect r,Item? item,bool interact=true){
  if(item!=null){
   if(!selectingOutput&&item.Id.ToString()==selectedItem)Highlight(r);
   gui!.Icon(r,item,item.StringTemplateId);string reason=ready?Restriction(item):"等待服务端价格";
   if(reason.Length>0)McGui.Fill(r,new(0,0,0,.4f));
   if(item.StackObjectsCount>1){gui.Label(new(r.x+1,r.y+30,r.width,18),item.StackObjectsCount.ToString(),Color.black,12);gui.Label(new(r.x,r.y+29,r.width,18),item.StackObjectsCount.ToString(),Color.white,12);}
   if(interact)Hover(r,Name(item.StringTemplateId)+" ×"+item.StackObjectsCount+"\n"+(reason.Length>0?reason:$"{EmcRules.Quote(prices[item.StringTemplateId].Value,1,InventoryAccess.Quality(item)):N0} EMC / 件\n点击选择消耗数量"));
  }
  if(McGui.Hit(r,interact&&item!=null&&!Busy)){selectedItem=item!.Id.ToString();selectingOutput=false;quantity="1";resources.Play("tock",Volume*.5f);}
 }
 static void Highlight(Rect r){var c=new Color(1,.9f,.3f);McGui.Fill(new(r.x-2,r.y-2,r.width+4,2),c);McGui.Fill(new(r.x-2,r.y+r.height,r.width+4,2),c);McGui.Fill(new(r.x-2,r.y,2,r.height),c);McGui.Fill(new(r.x+r.width,r.y,2,r.height),c);}
 void Hover(Rect r,string text){if(r.Contains(Event.current.mousePosition))tooltip=text;}
 void DrawControls(int knownCount,int inventoryCount){
  McGui.Panel(new(708,42,274,624));
  gui!.Label(new(724,56,242,28),selectingOutput?"取出已学物品":"消耗物品 · 学习知识",null,17);
  var chosen=Chosen;string id=selectingOutput?selectedLearned:chosen?.StringTemplateId??"";
  McGui.Slot(new(724,96,60,60));if(id.Length>0)gui.Icon(new(730,102,48,48),selectingOutput?Sample(id):chosen,id);
  gui.Wrapped(new(796,94,166,64),id.Length>0?Name(id):"点击库存或法阵\n选择一个物品",null,15);
  string problem=ActionProblem(!selectingOutput);bool valid=problem.Length==0;
  string preview=valid&&prices.TryGetValue(id,out var p)?$"本次{(selectingOutput?"花费":"获得")}\n{EmcRules.Quote(p.Value,int.Parse(quantity),selectingOutput?10000:InventoryAccess.Quality(chosen!)):N0} EMC":problem;
  gui.Wrapped(new(724,166,242,66),preview,valid?new(.12f,.36f,.08f):new(.5f,.18f,.12f),15);
  gui.Label(new(724,236,240,24),"数量",null,14);
  int.TryParse(quantity,out int count);int maximum=Maximum();
  if(gui.Button(new(724,266,32,30),"−",!Busy&&count>1))quantity=(count-1).ToString();
  bool enabled=GUI.enabled;GUI.enabled=!Busy;quantity=gui.Field(new(764,266,82,30),quantity);GUI.enabled=enabled;
  if(gui.Button(new(854,266,32,30),"+",!Busy&&count<maximum))quantity=(Math.Max(0,count)+1).ToString();
  if(gui.Button(new(894,266,72,30),"最大",!Busy&&maximum>0))quantity=maximum.ToString();
  gui.Label(new(724,300,242,24),selectingOutput?$"可取上限 {maximum:N0} 件 / 单个堆叠":$"可选 {maximum:N0} 件 / 当前物品",null,12);
  // Re-evaluate after quantity edits in this same GUI event.
  if(gui.Button(new(724,334,242,40),selectingOutput?"取出物品":"消耗并学习",ActionProblem(!selectingOutput).Length==0))Exchange(!selectingOutput);
  gui.Label(new(724,388,242,24),$"已学 {learned.Count} · 显示 {knownCount}",null,13);
  if(gui.Button(new(724,418,242,28),affordableOnly?"显示：仅买得起的物品":"显示：全部已学物品",!Busy)){affordableOnly=!affordableOnly;knowledgeScroll=0;}
  gui.Wrapped(new(724,454,242,48),access?.Raid==true?"库存来源：背包、弹挂、口袋":"库存来源：背包、弹挂、口袋、仓库",null,13);
  McGui.Fill(new(722,510,246,92),new(.17f,.18f,.2f));gui.Wrapped(new(732,516,226,80),Notice,Color.white,13);
  bool recover=owner.Length>0&&File.Exists(journal);
  if(gui.Button(new(724,614,242,30),recover?"恢复转换":ready?"刷新库存与 EMC":"重新连接",!Busy)){if(recover)Recover();else{Read();Load();}}
 }
 int Maximum(){
  if(!ready)return 0;
  if(selectingOutput)return prices.TryGetValue(selectedLearned,out var p)?EmcRules.MaximumOutput(balance,p.Value,p.Stack):0;
  var item=Chosen;return item!=null&&prices.TryGetValue(item.StringTemplateId,out var input)?Math.Min(item.StackObjectsCount,input.Stack):0;
 }
 string ActionProblem(bool burn){
  if(Busy)return "正在处理，请稍候。";
  if(access==null)return "请先进入 PMC 角色库存。";
  if(!ready)return "等待服务端连接，请点击重新连接。";
  if(File.Exists(journal))return "先点击恢复转换，核对上次操作。";
  if(!int.TryParse(quantity,out int count)||count<1)return "请输入大于零的整数数量。";
  try{
   if(burn){var item=Chosen;if(item==null)return "选择下方库存物品，消耗后获得 EMC 并学习。";string reason=Restriction(item);if(reason.Length>0)return reason;var price=prices[item.StringTemplateId];EmcRules.ValidateCount(count,Math.Min(item.StackObjectsCount,price.Stack));EmcRules.Exchange(balance,EmcRules.Quote(price.Value,count,InventoryAccess.Quality(item)),true);}
   else{if(!learned.Contains(selectedLearned)||!prices.TryGetValue(selectedLearned,out var p))return "选择右侧法阵中的已学物品。";EmcRules.ValidateCount(count,p.Stack);EmcRules.Exchange(balance,EmcRules.Quote(p.Value,count),false);}
  }catch(Exception e) when(e is ArgumentException or OverflowException){return e is OverflowException?"数值过大，请减少数量。":e.Message;}
  return "";
 }
 bool Matches(string id,string search)=>search.Length==0||Name(id).Contains(search,StringComparison.OrdinalIgnoreCase)||id.Contains(search,StringComparison.OrdinalIgnoreCase);
 internal void Clear(){if(Open){Open=false;Cursor.lockState=oldLock;Cursor.visible=oldVisible;}gui?.Clear();samples.Clear();}
}
