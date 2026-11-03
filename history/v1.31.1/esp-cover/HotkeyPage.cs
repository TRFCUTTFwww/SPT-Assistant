using System;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class HotkeyPage {
 static string lastScope="",search="";static Vector2 scroll;
 static string Name(ConfigEntryBase entry)=>entry.Description.Tags.OfType<CnConfig.ConfigurationManagerAttributes>().FirstOrDefault()?.DispName??entry.Definition.Key;
 internal static void Draw(Rect rect,string? scope=null){string currentScope=scope??"全部";if(lastScope!=currentScope){lastScope=currentScope;search="";scroll=Vector2.zero;}float x=rect.x,y=rect.y,w=rect.width;InterfaceMenu.Label(new Rect(x,y,w,30),scope==null?"热键设置":MenuSections.DisplayName(scope)+" · 热键",InterfaceMenu.Accent,21);y+=44;search=InterfaceMenu.Field(new Rect(x,y,w,32),search);y+=44;var keys=Plugin.Self.Config.Select(p=>p.Value).Where(e=>e.BoxedValue is KeyCode&&(scope==null||MenuSections.Group(e.Definition.Section,e.Definition.Key)==scope)&&(search.Length==0||(Name(e)+" "+e.Definition.Section).Contains(search,StringComparison.OrdinalIgnoreCase))).OrderBy(e=>MenuSections.Group(e.Definition.Section,e.Definition.Key)).ThenBy(Name).ToArray();var view=new Rect(x,y,w,rect.yMax-y-46);scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,w-20,keys.Length*100));for(int i=0;i<keys.Length;i++){var entry=keys[i];InterfaceMenu.DrawEntry(entry,new Rect(4,i*100,w-140,90));InterfaceMenu.Label(new Rect(w-130,i*100+2,104,26),MenuSections.DisplayName(MenuSections.Group(entry.Definition.Section,entry.Definition.Key)),null,11);if(InterfaceMenu.Button(new Rect(w-130,i*100+40,100,30),"清除绑定"))entry.BoxedValue=KeyCode.None;}GUI.EndScrollView();InterfaceMenu.Paragraph(new Rect(x,rect.yMax-40,w,40),"点击后按键，也支持右键／中键／侧键；Esc 取消。按住类仍按住生效；注意避免重复绑定。",null,12);}
}
