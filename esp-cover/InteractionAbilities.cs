using UnityEngine;
namespace LocalSpt5Esp;
internal static class InteractionAbilities {
 internal static void Draw(Rect rect){
  InterfaceMenu.Label(new(rect.x,rect.y,rect.width,30),"交互能力",InterfaceMenu.Accent,20);
  InterfaceMenu.Paragraph(new(rect.x,rect.y+36,rect.width,44),"控制原生门的破门强开能力，修改立即生效。",null,13);
  InterfaceMenu.DrawEntry(DoorAssist.Enabled,new(rect.x,rect.y+88,Math.Min(256,rect.width),82),"破门强开");
 }
}
