using System;
using System.Collections.Generic;
namespace LocalSpt5Esp;
internal readonly record struct IconLine(float X1,float Y1,float X2,float Y2);
internal static class MenuIconGeometry {
 static readonly Dictionary<string,IconLine[]> icons=new();
 internal static IReadOnlyList<IconLine> For(string name){if(!icons.TryGetValue(name,out var lines))icons[name]=lines=Create(name);return lines;}
 static IconLine[] Create(string name){
  var lines=new List<IconLine>();
  void Line(float x1,float y1,float x2,float y2)=>lines.Add(new(x1,y1,x2,y2));
  void Circle(float x,float y,float r){for(int i=0;i<32;i++){float a=i*MathF.Tau/32,b=(i+1)*MathF.Tau/32;Line(x+r*MathF.Cos(a),y+r*MathF.Sin(a),x+r*MathF.Cos(b),y+r*MathF.Sin(b));}}
  void Curve(float ax,float ay,float bx,float by,float cx,float cy,float dx,float dy){float px=ax,py=ay;for(int i=1;i<=24;i++){float t=i/24f,u=1-t,x=u*u*u*ax+3*u*u*t*bx+3*u*t*t*cx+t*t*t*dx,y=u*u*u*ay+3*u*u*t*by+3*u*t*t*cy+t*t*t*dy;Line(px,py,x,y);px=x;py=y;}}
  switch(name){
   case "射击":Circle(16,16,9);Line(16,2,16,10);Line(16,22,16,30);Line(2,16,10,16);Line(22,16,30,16);break;
   case "人物":Circle(16,9,4.8f);Curve(6,28,6,15,26,15,26,28);break;
   case "视觉":Curve(3,16,10,4,22,4,29,16);Curve(3,16,10,28,22,28,29,16);Circle(16,16,4.2f);break;
   case "信息":Circle(16,16,12);Line(16,8,16,10);Line(16,15,16,24);break;
   default:Line(3,10,8,10);Line(14,10,29,10);Circle(11,10,3);Line(3,22,18,22);Line(24,22,29,22);Circle(21,22,3);break;
  }
  return lines.ToArray();
 }
}
