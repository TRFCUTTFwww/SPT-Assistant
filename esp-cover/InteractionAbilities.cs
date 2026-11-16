using System;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class InteractionAbilities {
 internal static void Draw(Rect rect){InterfaceMenu.Label(new(rect.x,rect.y,rect.width,30),"交互能力",InterfaceMenu.Accent,20);InterfaceMenu.Paragraph(new(rect.x,rect.y+40,rect.width,44),"破门强开与门卡破解分别控制，修改立即生效。",null,13);float gap=16;bool narrow=rect.width<580;float width=narrow?rect.width:(rect.width-gap)/2;var breach=new Rect(rect.x,rect.y+102,width,180);var keycard=new Rect(narrow?rect.x:rect.x+width+gap,narrow?breach.yMax+16:breach.y,width,180);InterfaceMenu.DrawEntry(DoorAssist.Enabled,breach,"破门强开");QuickhackSettingsBridge.DrawDoorToggle(keycard);}
}
