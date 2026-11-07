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
  name=name switch{"战斗与枪械"=>"射击","人物与移动"=>"人物","AI 工作台"=>"AI 测试","玩具枪工具"=>"枪械","镜头与画面"=>"视觉","战局与物资"=>"战局","载具与空投"=>"空投","传送与导航"=>"传送","物品与装备"=>"装备与预设","设置与热键"=>"其他",_=>name};switch(name){
   case "剧情":Line(3,5,16,8);Line(16,8,29,5);Line(3,5,3,26);Line(29,5,29,26);Line(3,26,16,29);Line(16,29,29,26);Line(16,8,16,29);break;
   case "数据":Line(5,4,27,4);Line(27,4,27,28);Line(27,28,5,28);Line(5,28,5,4);Line(10,22,10,17);Line(16,22,16,10);Line(22,22,22,14);break;
   case "射击":Circle(16,16,9);Line(16,2,16,10);Line(16,22,16,30);Line(2,16,10,16);Line(22,16,30,16);break;
   case "枪械":Line(3,12,24,12);Line(24,12,24,18);Line(24,18,14,18);Line(14,18,11,27);Line(11,27,6,27);Line(6,27,8,18);Line(8,18,3,18);Line(3,18,3,12);Line(24,14,30,14);Line(17,18,17,23);break;
   case "人物":Circle(16,9,4.8f);Curve(6,28,6,15,26,15,26,28);break;
   case "娱乐":Curve(4,11,1,30,10,31,14,23);Curve(28,11,31,30,22,31,18,23);Line(4,11,28,11);Line(14,23,18,23);Line(7,17,13,17);Line(10,14,10,20);Circle(22,16,1.5f);Circle(25,20,1.5f);break;
   case "视觉":Curve(3,16,10,4,22,4,29,16);Curve(3,16,10,28,22,28,29,16);Circle(16,16,4.2f);break;
   case "战局":Line(5,5,27,5);Line(27,5,27,27);Line(27,27,5,27);Line(5,27,5,5);Circle(16,16,6);Line(16,8,16,13);Line(16,19,16,24);Line(8,16,13,16);Line(19,16,24,16);break;
   case "AI 测试":Line(7,7,25,7);Line(25,7,25,25);Line(25,25,7,25);Line(7,25,7,7);Circle(12,15,2);Circle(21,15,2);Line(11,21,21,21);break;
   case "物资刷新":Line(5,10,16,4);Line(16,4,27,10);Line(27,10,27,25);Line(27,25,5,25);Line(5,25,5,10);Line(5,10,16,16);Line(16,16,27,10);Line(16,16,16,25);break;
   case "空投":Curve(3,12,3,-1,29,-1,29,12);Line(3,12,29,12);Line(3,12,13,25);Line(29,12,19,25);Line(16,12,16,25);Line(11,25,21,25);Line(11,25,11,30);Line(21,25,21,30);Line(11,30,21,30);break;
   case "装备与预设":Line(4,10,28,10);Line(28,10,28,27);Line(28,27,4,27);Line(4,27,4,10);Line(11,10,11,5);Line(11,5,21,5);Line(21,5,21,10);Line(8,18,24,18);break;
   case "传送":Circle(8,16,5);Circle(24,16,5);Line(7,5,25,5);Line(25,5,21,2);Line(25,5,21,8);Line(25,27,7,27);Line(7,27,11,24);Line(7,27,11,30);break;
   case "Commando":Line(3,5,29,5);Line(29,5,29,27);Line(29,27,3,27);Line(3,27,3,5);Line(8,12,13,16);Line(13,16,8,20);Line(17,21,24,21);break;
   case "信息":Circle(16,16,12);Line(16,8,16,10);Line(16,15,16,24);break;
   default:Line(3,10,8,10);Line(14,10,29,10);Circle(11,10,3);Line(3,22,18,22);Line(24,22,29,22);Circle(21,22,3);break;
  }
  return lines.ToArray();
 }
}
