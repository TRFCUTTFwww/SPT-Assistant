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
 internal static bool Hit(Rect r,bool enabled=true){bool previous=GUI.enabled;try{GUI.enabled=previous&&enabled;return GUI.Button(r,"",GUIStyle.none);}finally{GUI.enabled=previous;}}
 internal bool Button(Rect r,string label,bool enabled=true){bool hover=r.Contains(Event.current.mousePosition);Fill(r,Color.black);Fill(new(r.x+2,r.y+2,r.width-4,r.height-4),hover&&enabled?new(.5f,.55f,.7f):new(.42f,.42f,.42f));Fill(new(r.x+2,r.y+2,r.width-4,2),new(.7f,.7f,.7f));Fill(new(r.x+2,r.y+2,2,r.height-4),new(.7f,.7f,.7f));Label(r,label,enabled?Color.white:new(.6f,.6f,.6f),14,true);return Hit(r,enabled);}
 internal void Wrapped(Rect r,string text,Color? color=null,int size=14){bool wrap=Text.wordWrap;try{Text.wordWrap=true;Label(r,text,color,size);}finally{Text.wordWrap=wrap;}}
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
