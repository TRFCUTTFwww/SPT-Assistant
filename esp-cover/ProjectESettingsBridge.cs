using System;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class ProjectESettingsBridge {
 static Type? api;static Func<bool>? active;static Vector2 scroll;static float nextLookup;
 static void Find(){if(active?.Invoke()==true)return;api=null;active=null;if(Time.unscaledTime<nextLookup)return;nextLookup=Time.unscaledTime+1;api=typeof(ProjectE.Client.ProjectEApi);active=api?.GetProperty("Active")?.GetMethod?.CreateDelegate<Func<bool>>();}
 static string Name(ConfigEntryBase e)=>e.Definition.Key switch{"Enabled"=>"原版 MC HUD 与同步快捷栏","Scale"=>"底部 HUD 缩放","Show hearts"=>"显示 MC 爱心","Show hunger"=>"显示 MC 饱食度","Show hydration"=>"显示 MC 口渴条","Show experience"=>"显示 MC 经验与等级","Show hotbar"=>"显示 MC 物品快捷栏","Volume"=>"ProjectE 音效音量","Open key"=>"打开转换桌快捷键",_=>e.Definition.Key};
 internal static void Draw(Rect rect){
  try{
   Find();InterfaceMenu.Label(new(rect.x,rect.y,rect.width,30),"ProjectE · 转换桌与 MC HUD",InterfaceMenu.Accent,20);
   if(api?.GetMethod("GetSettings")?.Invoke(null,null) is not ConfigEntryBase[] entries){InterfaceMenu.Paragraph(new(rect.x,rect.y+44,rect.width,80),"内置 MC 功能尚未就绪，请检查辅助 MOD 的 MC 资源包。");return;}
   InterfaceMenu.Paragraph(new(rect.x,rect.y+38,rect.width,55),"F10 打开 MC 风格转换桌。1 卢布 = 1 EMC，物品按手册卢布价转换；爱心、饱食度和经验条读取实际角色数据。",null,13);
   if(InterfaceMenu.Button(new(rect.x,rect.y+98,220,34),"打开 / 关闭转换桌")){InterfaceMenu.CloseForExternalPanel();api.GetMethod("ToggleTable")?.Invoke(null,null);}
   var area=new Rect(rect.x,rect.y+146,rect.width,Math.Max(40,rect.height-146));scroll=GUI.BeginScrollView(area,scroll,new(0,0,rect.width-22,entries.Length*102));
   try{for(int i=0;i<entries.Length;i++)InterfaceMenu.DrawEntry(entries[i],new(0,i*102,rect.width-28,92),Name(entries[i]));}finally{GUI.EndScrollView();}
  }catch(Exception e){api=null;active=null;InterfaceMenu.Paragraph(new(rect.x,rect.y+44,rect.width,80),"ProjectE 设置暂不可用："+e.GetBaseException().Message);}
 }
}
