using System;
namespace AssistantSafetyUi;
// Keep the action area visible; long explanations scroll independently.
internal readonly struct SafetyLayout {
 internal readonly float Scale,ScreenWidth,ScreenHeight,Width,Height,X,Y,Padding,HeaderHeight,FooterHeight,ContentHeight;
 internal readonly bool Stacked;
 internal SafetyLayout(float width,float height){
  Scale=Math.Max(.75f,Math.Min(1,width/1180f));ScreenWidth=width/Scale;ScreenHeight=height/Scale;
  Width=Math.Max(1,Math.Min(1060,ScreenWidth-32));Height=Math.Max(1,Math.Min(850,ScreenHeight-32));X=(ScreenWidth-Width)/2;Y=(ScreenHeight-Height)/2;
  Padding=Width<780?20:32;Stacked=Width<780;HeaderHeight=Stacked?154:132;FooterHeight=Stacked?258:164;ContentHeight=Math.Max(1,Height-HeaderHeight-FooterHeight);
 }
}
