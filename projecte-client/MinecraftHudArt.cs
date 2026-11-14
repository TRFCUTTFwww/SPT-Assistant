using System.Text.Json;
using Local.ModHost;
using UnityEngine;
namespace ProjectE.Client;

// Native-size Minecraft 1.8.9 atlas coordinates. All positions use MC GUI pixels.
internal sealed class MinecraftHudArt {
 readonly Texture2D icons,widgets,font,thirst;
 readonly int[] advances;
 internal MinecraftHudArt(ModuleContext context,ProjectResources resources){
  icons=resources.Texture("mc_icons");widgets=resources.Texture("mc_widgets");font=resources.Texture("mc_ascii");thirst=resources.Texture("thirst_icons");
  using var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(context.ModuleDirectory,"Assets","mc-hud-provenance.json")));
  advances=source.RootElement.GetProperty("glyphAdvances").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
 }
 internal static void Blit(Texture2D texture,Rect target,float x,float y,float width,float height)=>GUI.DrawTextureWithTexCoords(target,texture,new(x/texture.width,1-(y+height)/texture.height,width/texture.width,height/texture.height));
 internal void Symbol(float x,float y,bool hunger,float units){
  float row=hunger?27:0;Blit(icons,new(x,y,9,9),16,row,9,9);
  if(units>0)Blit(icons,new(x,y,9,9),units>1?52:61,row,9,9);
 }
 internal void Experience(float x,float y,float progress){
  Blit(icons,new(x,y,182,5),0,64,182,5);
  int fill=Math.Clamp((int)(progress*183),0,182);if(fill>0)Blit(icons,new(x,y,fill,5),0,69,fill,5);
 }
 internal void Hydration(float x,float y,float units){Blit(thirst,new(x,y,9,9),0,0,9,9);if(units>0)Blit(thirst,new(x,y,9,9),units>1?16:8,0,9,9);}
 internal void Hotbar(int slots){
  int original=Math.Min(slots,9)*20+1;
  Blit(widgets,new(0,0,original,22),0,0,original,22);
  for(int i=9;i<slots;i++)Blit(widgets,new(1+i*20,0,20,22),81,0,20,22);
  Blit(widgets,new(1+slots*20,0,1,22),181,0,1,22);
 }
 internal void Selected(int slot)=>Blit(widgets,new(slot*20-1,-1,24,22),0,22,24,22);
 internal float Width(string text)=>text.Sum(c=>c<advances.Length?advances[c]:advances['?']);
 internal void Text(float x,float y,string text,Color color,bool shadow=false){
  if(shadow)Glyphs(x+1,y+1,text,new(0,0,0,color.a));Glyphs(x,y,text,color);
 }
 internal void FittedText(float x,float y,float width,string text,Color color,bool right=false){
  float length=Width(text);if(length<=0)return;float scale=Math.Min(1,width/length);var old=GUI.matrix;
  try{GUI.matrix=old*Matrix4x4.TRS(new(x+(right?width-length*scale:0),y,0),Quaternion.identity,new(scale,scale,1));Text(0,0,text,color,true);}finally{GUI.matrix=old;}
 }
 internal void Level(float center,float y,int level){
  string text=level.ToString();float x=center-Width(text)/2;
  Glyphs(x+1,y,text,Color.black);Glyphs(x-1,y,text,Color.black);Glyphs(x,y+1,text,Color.black);Glyphs(x,y-1,text,Color.black);Glyphs(x,y,text,new(128/255f,1,32/255f));
 }
 void Glyphs(float x,float y,string text,Color color){
  var old=GUI.color;GUI.color=color;
  foreach(char value in text){int c=value<128?value:'?';Blit(font,new(x,y,8,8),c%16*8,c/16*8,8,8);x+=advances[c];}
  GUI.color=old;
 }
 internal static void Sprite(Rect rect,Sprite sprite){
  var tex=sprite.texture;var uv=sprite.textureRect;float size=Math.Min(rect.width/uv.width,rect.height/uv.height);
  var target=new Rect(rect.center.x-uv.width*size/2,rect.center.y-uv.height*size/2,uv.width*size,uv.height*size);
  GUI.DrawTextureWithTexCoords(target,tex,new(uv.x/tex.width,uv.y/tex.height,uv.width/tex.width,uv.height/tex.height));
 }
}
