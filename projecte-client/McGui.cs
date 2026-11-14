using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace ProjectE.Client;
internal sealed class McGui {
 readonly ProjectResources resources;readonly Dictionary<string,ItemIcon> icons=[];
 internal readonly GUIStyle Text=new(){fontSize=14,alignment=TextAnchor.MiddleLeft,wordWrap=false};
 Font? font;
 internal McGui(ProjectResources resources){this.resources=resources;}
 internal void InitializeFont(){if(font!=null)return;font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",16);Text.font=font;Text.normal.textColor=Color.white;}
 internal void Clear(){icons.Clear();if(font!=null)UnityEngine.Object.Destroy(font);font=null;}
 internal static void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
 internal static void Panel(Rect r){Fill(r,Color.black);Fill(new(r.x+2,r.y+2,r.width-4,r.height-4),new(.34f,.34f,.34f));Fill(new(r.x+2,r.y+2,r.width-6,3),Color.white);Fill(new(r.x+2,r.y+2,3,r.height-6),Color.white);Fill(new(r.x+5,r.y+5,r.width-10,r.height-10),new(.77f,.77f,.77f));}
 internal static void Slot(Rect r,bool selected=false){Fill(r,selected?new(1,.95f,.55f):new(.22f,.22f,.22f));Fill(new(r.x+2,r.y+2,r.width-2,r.height-2),Color.white);Fill(new(r.x+2,r.y+2,r.width-4,r.height-4),selected?new(.52f,.52f,.42f):new(.55f,.55f,.55f));}
 internal void Label(Rect r,string text,Color? color=null,int size=14,bool center=false){Text.fontSize=size;Text.alignment=center?TextAnchor.MiddleCenter:TextAnchor.MiddleLeft;Text.normal.textColor=color??new(.22f,.22f,.22f);GUI.Label(r,text,Text);}
 internal bool Button(Rect r,string label,bool enabled=true){bool hover=r.Contains(Event.current.mousePosition);Fill(r,Color.black);Fill(new(r.x+2,r.y+2,r.width-4,r.height-4),hover&&enabled?new(.5f,.55f,.7f):new(.42f,.42f,.42f));Fill(new(r.x+2,r.y+2,r.width-4,2),new(.7f,.7f,.7f));Fill(new(r.x+2,r.y+2,2,r.height-4),new(.7f,.7f,.7f));Label(r,label,enabled?Color.white:new(.6f,.6f,.6f),14,true);return enabled&&GUI.Button(r,"",GUIStyle.none);}
 internal string Field(Rect r,string text){Fill(r,new(.15f,.15f,.15f));Fill(new(r.x+2,r.y+2,r.width-4,r.height-4),Color.black);Text.normal.textColor=Color.white;Text.alignment=TextAnchor.MiddleLeft;Text.fontSize=14;return GUI.TextField(new(r.x+5,r.y+1,r.width-10,r.height-2),text,Text);}
 internal void Icon(Rect r,Item? item,string template){
  var sword=Swords.Find(template);if(sword!=null){GUI.DrawTexture(r,resources.Texture(sword.Key+"_sword"),ScaleMode.ScaleToFit);return;}
  if(item==null){Label(r,"?",new(.8f,.8f,.8f),18,true);return;}
  try{
   if(!icons.TryGetValue(template,out var icon)){
    var creator=Singleton<ItemIconCreator>.Instance;if(creator==null)return;var size=new IntVec2();icon=creator.GetItemIcon(item,ref size,false);if(icon!=null)icons[template]=icon;
   }
   var sprite=icon?.Sprite;if(sprite==null)return;var t=sprite.texture;var tr=sprite.textureRect;float ratio=tr.width/tr.height;var rect=r;if(ratio>r.width/r.height){rect.height=r.width/ratio;rect.y+=(r.height-rect.height)/2;}else{rect.width=r.height*ratio;rect.x+=(r.width-rect.width)/2;}
   GUI.DrawTextureWithTexCoords(rect,t,new(tr.x/t.width,tr.y/t.height,tr.width/t.width,tr.height/t.height));
  }catch{Label(r,"?",null,16,true);}
 }
}
internal sealed partial class Transmutation {
 McGui? gui;bool selectingOutput;
 readonly Dictionary<string,Item> samples=[];
 string query="",quantity="1",selectedItem="",selectedLearned="";
 int backpackPage,knowledgePage;
 internal void Draw(){
  if(!Open)return;gui??=new(resources);gui.InitializeFont();
  var oldMatrix=GUI.matrix;var oldColor=GUI.color;int oldDepth=GUI.depth;GUI.depth=-8000;GUI.color=Color.white;
  float scale=Math.Min(Screen.width/830f,Screen.height/570f);float left=(Screen.width-800*scale)/2,top=(Screen.height-530*scale)/2;
  try{
   McGui.Fill(new(0,0,Screen.width,Screen.height),new(0,0,0,.72f));GUI.matrix=Matrix4x4.TRS(new(left,top,0),Quaternion.identity,new(scale,scale,1));
   McGui.Panel(new(0,0,800,530));gui.Label(new(16,9,500,28),"转换桌  ·  ProjectE",null,19);
   if(gui.Button(new(756,9,30,26),"×",!Busy))SetOpen(false);
   gui.Label(new(16,43,500,28),$"EMC  {balance:N0}    |    1 卢布 = 1 EMC",new(.1f,.35f,.08f),18);
   gui.Label(new(16,77,340,23),access?.Raid==true?"背包 · 弹挂 · 口袋":"背包 · 弹挂 · 口袋 · 仓库");
   gui.Label(new(444,77,330,23),"已学物品  ·  "+learned.Count);
   var filtered=items.Where(i=>Matches(i.StringTemplateId)).ToArray();var known=learned.Where(prices.ContainsKey).Where(Matches).OrderBy(Name).ToArray();
   backpackPage=Math.Clamp(backpackPage,0,Math.Max(0,(filtered.Length-1)/48));knowledgePage=Math.Clamp(knowledgePage,0,Math.Max(0,(known.Length-1)/48));
   string tooltip="";
   for(int n=0;n<48;n++){
    var r=new Rect(16+n%8*43,106+n/8*43,40,40);int index=backpackPage*48+n;var item=index<filtered.Length?filtered[index]:null;McGui.Slot(r,item?.Id.ToString()==selectedItem);
    if(item!=null){gui.Icon(new(r.x+3,r.y+3,34,34),item,item.StringTemplateId);if(item.StackObjectsCount>1)gui.Label(new(r.x,r.y+20,38,19),item.StackObjectsCount.ToString(),Color.white,12,true);if(r.Contains(Event.current.mousePosition))tooltip=Name(item.StringTemplateId)+" ×"+item.StackObjectsCount+"\n"+(Restriction(item) is {Length:>0} reason?reason:$"{prices[item.StringTemplateId].Value:N0} EMC / 件");if(!Busy&&GUI.Button(r,"",GUIStyle.none)){selectedItem=item.Id.ToString();selectingOutput=false;quantity="1";resources.Play("tock",Volume*.5f);}}
    r=new Rect(444+n%8*41,106+n/8*43,38,40);index=knowledgePage*48+n;string id=index<known.Length?known[index]:"";McGui.Slot(r,id.Length>0&&id==selectedLearned);
    if(id.Length>0){if(!samples.TryGetValue(id,out var sample))try{samples[id]=sample=Singleton<ItemFactory>.Instance.CreateItem(Singleton<ItemFactory>.Instance.NextId.ToString(),id,null);}catch{}gui.Icon(new(r.x+3,r.y+3,32,34),sample,id);if(r.Contains(Event.current.mousePosition))tooltip=Name(id)+$"\n{prices[id].Value:N0} EMC / 件";if(!Busy&&GUI.Button(r,"",GUIStyle.none)){selectedLearned=id;selectingOutput=true;quantity="1";resources.Play("tock",Volume*.5f);}}
   }
   GUI.DrawTextureWithTexCoords(new(365,106,72,72),resources.Texture("transmute"),new(.46f,.61f,.36f,.36f));
   GUI.DrawTexture(new Rect(367,183,68,68),resources.Texture("tablet"),ScaleMode.ScaleToFit);
   gui.Label(new(366,261,70,26),"EMC",null,16,true);
   if(gui.Button(new(16,370,45,26),"<",!Busy&&backpackPage>0))backpackPage--;
   gui.Label(new(65,370,230,26),$"{backpackPage+1} / {Math.Max(1,(filtered.Length+47)/48)}",null,13,true);
   if(gui.Button(new(311,370,45,26),">",!Busy&&(backpackPage+1)*48<filtered.Length))backpackPage++;
   if(gui.Button(new(444,370,45,26),"<",!Busy&&knowledgePage>0))knowledgePage--;
   gui.Label(new(493,370,230,26),$"{knowledgePage+1} / {Math.Max(1,(known.Length+47)/48)}",null,13,true);
   if(gui.Button(new(737,370,35,26),">",!Busy&&(knowledgePage+1)*48<known.Length))knowledgePage++;
   gui.Label(new(16,406,50,28),"搜索");string updated=gui.Field(new(65,406,250,28),query);if(updated!=query){query=updated;backpackPage=knowledgePage=0;}
   gui.Label(new(328,406,50,28),"数量");quantity=gui.Field(new(375,406,75,28),quantity);
   if(gui.Button(new(464,406,150,28),"消耗并学习",!Busy&&selectedItem.Length>0&&!File.Exists(journal)))Exchange(true);
   if(gui.Button(new(626,406,150,28),"取出物品",!Busy&&selectedLearned.Length>0&&!File.Exists(journal)))Exchange(false);
   string detail=Preview();
   gui.Label(new(16,443,760,25),detail,null,13);
   gui.Label(new(16,472,760,24),Notice,Busy?new(.3f,.25f,.05f):new(.15f,.28f,.15f),12);
   if(gui.Button(new(16,500,125,22),File.Exists(journal)?"恢复流水":"刷新",!Busy)){if(File.Exists(journal))Recover();else{Read();Load();}}
   gui.Label(new(161,499,610,23),"F10 / Esc 关闭  ·  任务物品与带内容物容器不可消耗",null,12);
   if(tooltip.Length>0){var mouse=Event.current.mousePosition;var tr=new Rect(Math.Min(mouse.x+10,520),Math.Min(mouse.y+14,440),265,62);McGui.Fill(tr,new(.08f,.02f,.14f,.96f));gui.Text.wordWrap=true;gui.Label(new(tr.x+8,tr.y+5,tr.width-16,tr.height-10),tooltip,Color.white,13);gui.Text.wordWrap=false;}
   if(Event.current.type is EventType.MouseDown or EventType.MouseUp or EventType.ScrollWheel or EventType.KeyDown or EventType.KeyUp)Event.current.Use();
  }finally{GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.depth=oldDepth;}
 }
 string Preview(){
  try{
   if(!int.TryParse(quantity,out int count)||count<1)return "请输入有效数量。";
   if(selectingOutput&&prices.TryGetValue(selectedLearned,out var outputPrice)){
    if(count>outputPrice.Stack)return $"单次最多取出 {outputPrice.Stack:N0} 件。";
    long cost=EmcRules.Quote(outputPrice.Value,count);return Name(selectedLearned)+$"  本次花费 {cost:N0} EMC"+(cost>balance?" · 余额不足":"");
   }
   var chosen=items.FirstOrDefault(i=>i.Id.ToString()==selectedItem);if(chosen==null)return "选择左侧物品消耗；右侧选择已学物品取出。";
   string restriction=Restriction(chosen);if(restriction.Length>0)return Name(chosen.StringTemplateId)+"  "+restriction;
   if(count>chosen.StackObjectsCount)return "所选物品数量不足。";
   return Name(chosen.StringTemplateId)+$"  本次获得 {EmcRules.Quote(prices[chosen.StringTemplateId].Value,count,InventoryAccess.Quality(chosen)):N0} EMC";
  }catch(OverflowException){return "本次数值过大，请减少数量。";}
 }
 bool Matches(string id)=>query.Length==0||Name(id).Contains(query,StringComparison.OrdinalIgnoreCase)||id.Contains(query,StringComparison.OrdinalIgnoreCase);
 internal void Clear(){if(Open){Open=false;Cursor.lockState=oldLock;Cursor.visible=oldVisible;}gui?.Clear();samples.Clear();}
}
