using System;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class HotkeyPage {
 static string lastScope="",search="";static Vector2 scroll;
 internal sealed record FixedKey(string Name,string Keys,string Scope,string Help);
 internal static readonly FixedKey[] Fixed={
  new("客户端热加载","Ctrl+F1","设置与热键","宿主固定按键；更新功能模块后重新加载。"),
  new("编辑：显示鼠标与框选","Alt + Mouse0","开发工具","编辑模式下按住左／右 Alt，左键点选或拖框。"),
  new("编辑：右键菜单","Mouse1","开发工具","右键打开菜单；移动中右键取消。"),
  new("编辑：菜单选择","↑ / ↓ / Wheel / Enter","开发工具","方向键或滚轮选择，Enter 或左键执行。"),
  new("编辑：放下与取消","Mouse0 / Mouse1 / Esc","开发工具","移动中左键放下，右键或 Esc 取消；滚轮调整距离。"),
  new("关闭／取消","Esc","设置与热键","关闭当前界面或取消按键录入；编辑模式下也可清空选择。"),
  new("聊天：候选与执行","↑ / ↓ / Tab / Enter","开发工具","聊天窗口内选择候选、填入和执行。"),
  new("聊天：历史记录","Ctrl+↑ / Ctrl+↓","开发工具","聊天窗口内浏览已输入的历史。")
 };
 static string Name(ConfigEntryBase entry)=>entry.Description.Tags.OfType<CnConfig.ConfigurationManagerAttributes>().FirstOrDefault()?.DispName??entry.Definition.Key;
 static bool Match(string value)=>search.Length==0||value.Contains(search,StringComparison.OrdinalIgnoreCase);
 internal static void Draw(Rect rect,string? scope=null){
  string currentScope=scope??"全部";
  if(lastScope!=currentScope){lastScope=currentScope;search="";scroll=Vector2.zero;}
  float x=rect.x,y=rect.y,w=rect.width;
  InterfaceMenu.Label(new Rect(x,y,w,30),scope==null?"热键设置":MenuSections.DisplayName(scope)+" · 热键",InterfaceMenu.Accent,21);y+=44;
  search=InterfaceMenu.Field(new Rect(x,y,w,32),search);y+=44;
  var keys=Plugin.Self.Config.Select(p=>p.Value).Where(e=>ShortcutBindings.IsBinding(e.BoxedValue)&&(scope==null||MenuSections.Group(e.Definition.Section,e.Definition.Key)==scope)&&Match(Name(e)+" "+e.Definition.Section+" "+e.Definition.Key+" "+ShortcutBindings.Label(e))).OrderBy(e=>MenuSections.Group(e.Definition.Section,e.Definition.Key)).ThenBy(Name).ToArray();
  var fixedKeys=Fixed.Where(key=>(scope==null||key.Scope==scope)&&Match(I18n.Text(key.Name)+" "+key.Keys+" "+I18n.Text(key.Help))).ToArray();
  var view=new Rect(x,y,w,Math.Max(32,rect.yMax-y-68));
  scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,Math.Max(1,w-20),Math.Max(view.height-1,(keys.Length+fixedKeys.Length)*112)));
  for(int i=0;i<keys.Length;i++){
   var entry=keys[i];float top=i*112;
   InterfaceMenu.DrawEntry(entry,new Rect(4,top,Math.Max(100,w-140),100));
   InterfaceMenu.Label(new Rect(w-130,top+2,104,26),MenuSections.DisplayName(MenuSections.Group(entry.Definition.Section,entry.Definition.Key)),null,11);
   if(InterfaceMenu.Button(new Rect(w-130,top+32,100,28),"清除绑定"))InterfaceMenu.SetKeyBinding(entry,ShortcutBindings.Empty(entry.BoxedValue));
   if(InterfaceMenu.Button(new Rect(w-130,top+66,100,28),"恢复默认"))InterfaceMenu.SetKeyBinding(entry,entry.DefaultValue);
  }
  for(int i=0;i<fixedKeys.Length;i++){
   var key=fixedKeys[i];float top=(keys.Length+i)*112;
   InterfaceMenu.Surface(new Rect(4,top,Math.Max(100,w-28),100),new Color(1,1,1,.035f),9);
   InterfaceMenu.Label(new Rect(17,top+8,Math.Max(1,w-170),26),key.Name,null,13);
   InterfaceMenu.Label(new Rect(w-138,top+8,108,26),"固定操作",InterfaceMenu.Accent,11);
   InterfaceMenu.Label(new Rect(17,top+36,Math.Max(1,w-50),25),key.Keys,InterfaceMenu.Accent,12);
   InterfaceMenu.Paragraph(new Rect(17,top+64,Math.Max(1,w-50),32),key.Help,null,11);
  }
  GUI.EndScrollView();
  InterfaceMenu.Paragraph(new Rect(x,rect.yMax-62,w,62),"点击后按键；组合键请按住 Ctrl／Shift／Alt 再按主键，也支持鼠标键。Esc 取消录入。可清除或恢复默认；固定操作仅供查阅。",null,12);
 }
}
