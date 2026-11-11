using System;
using System.Linq;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class RaidChat {
 static Vector2 settingsScroll;
 internal static void DrawSettings(Rect rect){settingsScroll=GUI.BeginScrollView(rect,settingsScroll,new Rect(0,0,rect.width-22,Math.Max(rect.height,480+(client?.Peers.Length??0)*95)));try{DrawSettingsContent(new Rect(0,0,rect.width-22,rect.height));}finally{GUI.EndScrollView();}}
 static void DrawSettingsContent(Rect rect){float x=rect.x,y=rect.y,w=rect.width;InterfaceMenu.Label(new(x,y,w,28),"聊天与命令",InterfaceMenu.Accent,18);y+=38;InterfaceMenu.DrawEntry(hotkey!,new(x,y,w,80));y+=90;InterfaceMenu.DrawEntry(enabled!,new(x,y,w,80));y+=90;InterfaceMenu.Paragraph(new(x,y,w,56),"T 打开聊天 · / 输入命令 · ↑↓ 选择候选 · Tab 填入 · Enter 执行 · Ctrl+↑↓ 历史 · Esc 关闭。",null,13);y+=65;
  if(InterfaceMenu.Button(new(x,y,150,30),"打开聊天")){InterfaceMenu.Close();SetOpen(true);focus=true;return;}y+=42;
  if(client==null){InterfaceMenu.Label(new(x,y,w,40),"登录角色后连接聊天。需要安装服务端组件。",null,13);return;}
  InterfaceMenu.Label(new(x,y,w,26),(I18n.English?"Profile ID: ":"档案 ID：")+session,null,13,translate:false);y+=30;InterfaceMenu.Label(new(x,y,w-90,26),client.Fingerprint,null,12,translate:false);if(InterfaceMenu.Button(new(x+w-86,y,86,27),"复制指纹"))GUIUtility.systemCopyBuffer=client.Fingerprint;y+=36;
  InterfaceMenu.Paragraph(new(x,y,w,55),"通过其他可信渠道核对完整指纹，再输入 /identity verify 档案ID 指纹。双方都须核验；指纹改变会拒绝连接。",InterfaceMenu.Accent,13);y+=64;var peers=client.Peers;string? selected=null;for(int i=0;i<peers.Length;i++){var p=peers[i];string description=client.Describe(p);InterfaceMenu.Paragraph(new(x+8,y+i*95,w-16,58),description,null,12);if(InterfaceMenu.Button(new(x+8,y+i*95+60,160,30),"填入核验命令"))selected=p.Profile;}if(selected!=null){InterfaceMenu.Close();SetOpen(true);input="/identity verify "+ChatCommandSyntax.Quote(selected)+" ";focus=true;}
 }
}
