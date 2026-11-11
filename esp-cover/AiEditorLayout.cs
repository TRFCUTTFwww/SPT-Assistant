using System;
namespace LocalSpt5Esp;
internal readonly record struct AiEditorFrame(float Width,float Height,bool Scroll);
internal static class AiEditorLayout {
 internal static int EquipmentColumns(float width)=>width>=872?8:width>=432?4:width>=212?2:1;
 internal static float EquipmentHeader(float width,bool embedded)=>(embedded?0:50)+(8+EquipmentColumns(width)-1)/EquipmentColumns(width)*44+10;
 // Reserve the longest equipment path: title, slots, item, action, parts,
 // page tabs, a useful parameter viewport, and a separate action/notice footer.
 internal static float EquipmentHeight(float width,bool embedded)=>EquipmentHeader(width,embedded)+44+48+78+48+120+124;
 internal static AiEditorFrame Frame(float width,float height,string page,bool embedded=false){
  float Minimum(float w)=>page switch{"equipment"=>EquipmentHeight(w,embedded),"weapon"=>620,"storage"=>600,_=>throw new ArgumentException("Unknown AI editor page.")};
  float minWidth=page switch{"equipment"=>432,"weapon"=>600,"storage"=>640,_=>throw new ArgumentException("Unknown AI editor page.")};
  bool scroll=width<minWidth||height<Minimum(width);float contentWidth=Math.Max(minWidth,width-(scroll?22:0));
  // Reserving the scrollbar may wrap the slot row. Re-evaluate the minimum
  // using the width the controls actually receive, including threshold widths.
  if(!scroll&&height<Minimum(contentWidth)){scroll=true;contentWidth=Math.Max(minWidth,width-22);}
  return new(contentWidth,Math.Max(height,Minimum(contentWidth)),scroll);
 }
}
