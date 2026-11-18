// Draw the production IMGUI renderer against a detached GDI canvas. This checks
// geometry and action dispatch and produces review images without running EFT.
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using D=System.Drawing;
namespace UnityEngine {
 public record struct Color(float r,float g,float b,float a=1){public static Color white=>new(1,1,1);public static Color Lerp(Color a,Color b,float t)=>new(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);internal D.Color Ink=>D.Color.FromArgb((int)(Math.Clamp(a,0,1)*255),(int)(Math.Clamp(r,0,1)*255),(int)(Math.Clamp(g,0,1)*255),(int)(Math.Clamp(b,0,1)*255));}
 public record struct Vector2(float x,float y){public static Vector2 zero=>new();}
 public record struct Vector3(float x,float y,float z);
 public record struct Matrix4x4(float scale){public static Matrix4x4 Scale(Vector3 v)=>new(v.x);}
 public struct Rect(float x,float y,float width,float height){public float x=x,y=y,width=width,height=height;public bool Contains(Vector2 p)=>p.x>=x&&p.x<x+width&&p.y>=y&&p.y<y+height;internal D.RectangleF Box=>new(x,y,width,height);}
 public enum FontStyle {Normal,Bold}public enum TextAnchor {UpperLeft,MiddleLeft,MiddleCenter}public enum ScaleMode {StretchToFill}public enum EventType {Repaint,KeyDown,KeyUp,MouseDown,MouseUp,ScrollWheel}
 public class Event {public static Event current=new();public EventType type=EventType.Repaint;public Vector2 mousePosition=new(-100,-100);public void Use(){}}
 public static class Screen {public static int width,height;}
 public class Texture2D {public static Texture2D whiteTexture=new();}
 public class GUIContent(string text){public string text=text;}
 public class GUIStyle {
  public static GUIStyle none=new();public int fontSize=14;public TextAnchor alignment;public bool wordWrap,richText;public FontStyle fontStyle;
  public GUIStyle(){}public GUIStyle(GUIStyle other){}
  internal D.Font Font()=>new("Microsoft YaHei UI",fontSize,fontStyle==FontStyle.Bold?D.FontStyle.Bold:D.FontStyle.Regular,D.GraphicsUnit.Pixel);
  internal D.StringFormat Format(){var format=new D.StringFormat(D.StringFormat.GenericTypographic);if(!wordWrap)format.FormatFlags|=D.StringFormatFlags.NoWrap;format.Alignment=alignment==TextAnchor.MiddleCenter?D.StringAlignment.Center:D.StringAlignment.Near;format.LineAlignment=alignment==TextAnchor.UpperLeft?D.StringAlignment.Near:D.StringAlignment.Center;return format;}
  public float CalcHeight(GUIContent content,float width){using var font=Font();using var format=Format();return (float)Math.Ceiling(Preview.Canvas.MeasureString(content.text,font,new D.SizeF(width,100000),format).Height+2);}
 }
 public class GUISkin {public GUIStyle label=new();}
 public static class GUI {
  public static GUISkin skin=new();public static Color color=Color.white;public static int depth;public static bool enabled=true;static Matrix4x4 current=new(1);
  public static Matrix4x4 matrix {get=>current;set{current=value;Preview.Canvas.ResetTransform();Preview.Canvas.ScaleTransform(value.scale,value.scale);}}
  public static void DrawTexture(Rect r,Texture2D texture,ScaleMode mode,bool alpha,float aspect,Color ink,float border,float radius){if(r.width<=0||r.height<=0)return;using var brush=new D.SolidBrush(ink.Ink);if(radius<1){Preview.Canvas.FillRectangle(brush,r.Box);return;}float d=Math.Min(radius*2,Math.Min(r.width,r.height));using var path=new GraphicsPath();path.AddArc(r.x,r.y,d,d,180,90);path.AddArc(r.x+r.width-d,r.y,d,d,270,90);path.AddArc(r.x+r.width-d,r.y+r.height-d,d,d,0,90);path.AddArc(r.x,r.y+r.height-d,d,d,90,90);path.CloseFigure();Preview.Canvas.FillPath(brush,path);}
  public static void Label(Rect r,string text,GUIStyle style){Preview.LastCaption=text;using var font=style.Font();using var brush=new D.SolidBrush(color.Ink);using var format=style.Format();Preview.Canvas.DrawString(text,font,brush,r.Box,format);}
  public static bool Button(Rect r,string text,GUIStyle style){Preview.Controls.Add((Preview.LastCaption,r,enabled,Preview.ScrollDepth==0));if(enabled&&Preview.LastCaption==Preview.Click){Preview.Click="";return true;}return false;}
  public static Vector2 BeginScrollView(Rect view,Vector2 scroll,Rect content){Preview.States.Push(Preview.Canvas.Save());Preview.Canvas.SetClip(view.Box,CombineMode.Intersect);Preview.Canvas.TranslateTransform(view.x-scroll.x,view.y-scroll.y);Preview.ScrollDepth++;return scroll;}
  public static void EndScrollView(){Preview.ScrollDepth--;Preview.Canvas.Restore(Preview.States.Pop());}
 }
 public static class Preview {
  public static D.Bitmap Image=null!;public static D.Graphics Canvas=null!;public static string LastCaption="",Click="";public static int ScrollDepth;
  public static readonly Stack<GraphicsState> States=new();public static readonly List<(string Caption,Rect Bounds,bool Enabled,bool Fixed)> Controls=new();
  public static void Begin(int w,int h){Canvas?.Dispose();Image?.Dispose();Image=new(w,h);Canvas=D.Graphics.FromImage(Image);Canvas.SmoothingMode=SmoothingMode.AntiAlias;Canvas.TextRenderingHint=D.Text.TextRenderingHint.AntiAliasGridFit;Canvas.Clear(D.Color.FromArgb(39,46,48));Screen.width=w;Screen.height=h;Controls.Clear();ScrollDepth=0;States.Clear();}
  public static void Save(string path)=>Image.Save(path,ImageFormat.Png);
 }
}
