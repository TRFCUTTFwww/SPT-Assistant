using System;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class SwordSettingsBridge {
 static Vector2 scroll;
 static Type? api;
 static Func<bool>? active;
 static float nextLookup;
 static void FindApi(){
  if(active?.Invoke()==true)return;
  api=null;active=null;if(Time.unscaledTime<nextLookup)return;nextLookup=Time.unscaledTime+1;
  api=typeof(AllTheModium.Client.SwordSettingsApi);
  active=api?.GetProperty("Active")?.GetMethod?.CreateDelegate<Func<bool>>();
 }
 static string Name(ConfigEntryBase entry)=>entry.Definition.Key switch{"Enabled"=>"启用右键格挡","Block bullets"=>"格挡子弹","Damage reduction percent"=>"格挡减伤（%）","Size percent"=>"剑模型大小（%）","Brightness percent"=>"剑模型亮度（%）","Opacity percent"=>"剑模型不透明度（%）",_=>entry.Definition.Key};
 internal static void Draw(Rect rect){
  try{
   FindApi();InterfaceMenu.Label(new(rect.x,rect.y,rect.width,30),"像素剑",InterfaceMenu.Accent,20);
   if(api?.GetMethod("GetSettings")?.Invoke(null,null) is not ConfigEntryBase[] entries){InterfaceMenu.Paragraph(new(rect.x,rect.y+42,rect.width,80),"内置像素剑尚未就绪，请检查辅助 MOD 的 MC 资源包。",null,14);return;}
   InterfaceMenu.Paragraph(new(rect.x,rect.y+38,rect.width,56),"按住右键格挡，左键挥砍。减伤仅作用于正面命中；关闭格挡子弹后只防近战。修改立即生效并自动保存。",null,13);
   var area=new Rect(rect.x,rect.y+106,rect.width,Math.Max(40,rect.height-106));scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,rect.width-22,entries.Length*102));
   try{for(int i=0;i<entries.Length;i++)InterfaceMenu.DrawEntry(entries[i],new Rect(0,i*102,rect.width-28,92),Name(entries[i]));}finally{GUI.EndScrollView();}
  }catch(Exception e){api=null;active=null;InterfaceMenu.Paragraph(new(rect.x,rect.y+42,rect.width,80),"像素剑设置暂不可用："+e.GetBaseException().Message,null,14);}
 }
}
