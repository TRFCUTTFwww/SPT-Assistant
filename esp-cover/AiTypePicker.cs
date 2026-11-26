using System;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class AiTypePicker {
 internal static float Height(float width){int columns=width>=740?6:width>=400?3:2;return MathF.Ceiling(Enum.GetValues<AiTypeFilter>().Length/(float)columns)*40+8;}
 internal static void Options(Rect rect,ref AiTypeFilter selected,ref bool open,ref Vector2 scroll){int columns=rect.width>=740?6:rect.width>=400?3:2;float cell=(rect.width-24-(columns-1)*8)/columns;var types=Enum.GetValues<AiTypeFilter>();scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,rect.width-18,Height(rect.width)));for(int i=0;i<types.Length;i++)if(InterfaceMenu.Button(new Rect(i%columns*(cell+8),i/columns*40,cell,32),AiTypeRules.Name(types[i]),selected==types[i])){selected=types[i];open=false;}GUI.EndScrollView();}
}
