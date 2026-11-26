using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Local.HotModules;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class FunModuleManager {
 sealed record Panel(string Id,string Title,Type Api,Action<Rect,Action<ConfigEntryBase,Rect,string>,Func<Rect,string,bool>> Draw);
 static readonly List<Panel> panels=[];
 static readonly List<(string Id,string Name,bool Enabled,string Status,bool Removing)> installed=[];
 static string Root=>Path.Combine(BepInEx.Paths.PluginPath,"UniversalModHost");
 static Task<string?>? picker;static Task<string>? importing;static string selected="modules",message="",confirmDelete="";static float next,confirmUntil;static Vector2 listScroll,manageScroll;
 internal static bool CanReload=>picker==null&&importing==null;
 internal static void Tick(){
  if(picker?.IsCompleted==true){try{string? file=picker.GetAwaiter().GetResult();if(file!=null)importing=Task.Run(()=>FunModulePackage.Install(file,Root));}catch(Exception e){message=e.GetBaseException().Message;}picker=null;}
  if(importing?.IsCompleted==true){try{selected=importing.GetAwaiter().GetResult();message="";next=0;}catch(Exception e){message=e.GetBaseException().Message;}importing=null;}
  if(Time.unscaledTime<next)return;next=Time.unscaledTime+1;panels.Clear();installed.Clear();
  var ready=new HashSet<string>(StringComparer.Ordinal);var removing=new HashSet<string>(StringComparer.Ordinal);
  try{using var status=JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,"host-status.json")));if(status.RootElement.GetProperty("processId").GetInt32()==Environment.ProcessId)foreach(var module in status.RootElement.GetProperty("modules").EnumerateArray())if(module.GetProperty("ready").GetBoolean())ready.Add(module.GetProperty("id").GetString()!);}catch{}
  string modules=Path.Combine(Root,"modules");
  if(Directory.Exists(modules))foreach(string home in Directory.GetDirectories(modules).OrderBy(x=>x,StringComparer.Ordinal)){
   string id=Path.GetFileName(home);if(!Regex.IsMatch(id,"^[a-z0-9][a-z0-9._-]{0,63}$"))continue;
   bool deleting=false;
   try{deleting=FunModulePackage.Removing(Root,id);if(deleting){removing.Add(id);if(FunModulePackage.CompleteRemoval(Root,id)){message="模块已删除，配置与存档已保留。";if(selected==id)selected="modules";continue;}}
    var manifest=deleting?FunModulePackage.RemovalManifest(Root,id):FunModulePackage.Manifest(Root,id);if(manifest==null||!FunModulePackage.HasSettings(Root,id,manifest))continue;string title=manifest["name"]?.GetValue<string>()??id;bool enabled=!deleting&&(manifest["enabled"]?.GetValue<bool>()??true);string state=deleting?"正在卸载，等待当前操作完成":ready.Contains(id)?"已加载":enabled?"等待加载":"已关闭";
    string statusFile=Path.Combine(Root,"state",id,"status.json");if(File.Exists(statusFile)){using var status=JsonDocument.Parse(File.ReadAllText(statusFile));var data=status.RootElement.GetProperty("module");if(data.TryGetProperty("error",out var error)&&!string.IsNullOrWhiteSpace(error.GetString()))state=error.GetString()!;else if(data.TryGetProperty("waiting",out var waiting)&&waiting.GetBoolean())state="等待操作完成";}
    installed.Add((id,title,enabled,state,deleting));
   }catch(Exception e){installed.Add((id,id,false,e.GetBaseException().Message,deleting));}
  }
  if(removing.Contains(selected))selected="modules";
  foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Reverse()){
   try{if(!assembly.GetReferencedAssemblies().Any(a=>a.Name=="Local.ModHost.Contracts"))continue;
    foreach(var type in assembly.GetExportedTypes()){
     var idProperty=type.GetProperty("ModuleId",BindingFlags.Public|BindingFlags.Static);if(idProperty?.PropertyType!=typeof(string))continue;
     string? id=idProperty.GetValue(null) as string;if(id==null||removing.Contains(id)||!ready.Contains(id)||panels.Any(p=>p.Id==id)||type.GetProperty("Active")?.GetValue(null) is not true)continue;
     var method=type.GetMethod("DrawSettings",BindingFlags.Public|BindingFlags.Static,null,[typeof(Rect),typeof(Action<ConfigEntryBase,Rect,string>),typeof(Func<Rect,string,bool>)],null);if(method==null)continue;
     panels.Add(new(id,type.GetProperty("DisplayName")?.GetValue(null) as string??id,type,method.CreateDelegate<Action<Rect,Action<ConfigEntryBase,Rect,string>,Func<Rect,string,bool>>>()));
    }
   }catch{}
  }
 }
 internal static void Draw(Rect rect,Action<Rect> laser){
  float width=Math.Min(190,rect.width*.28f);var left=new Rect(rect.x,rect.y,width,rect.height);var right=new Rect(rect.x+width+14,rect.y,Math.Max(40,rect.width-width-14),rect.height);
  listScroll=GUI.BeginScrollView(left,listScroll,new Rect(0,0,width-20,(panels.Count+2)*44));
  try{if(InterfaceMenu.Button(new(0,0,width-24,36),"模块管理",selected=="modules"))selected="modules";if(InterfaceMenu.Button(new(0,44,width-24,36),"镭射眼",selected=="laser"))selected="laser";
   int row=2;foreach(var panel in panels.OrderBy(p=>p.Title,StringComparer.Ordinal)){if(InterfaceMenu.Button(new(0,row++*44,width-24,36),panel.Title,selected==panel.Id))selected=panel.Id;}
  }finally{GUI.EndScrollView();}
  if(selected=="laser"){laser(right);return;}
  var current=panels.FirstOrDefault(p=>p.Id==selected);
  if(selected!="modules"&&current!=null){try{if(current.Api.GetProperty("Active")?.GetValue(null) is true){current.Draw(right,(entry,area,title)=>InterfaceMenu.DrawEntry(entry,area,title),(area,title)=>InterfaceMenu.Button(area,title));return;}}catch(Exception e){message=e.GetBaseException().Message;}selected="modules";}
  if(picker==null&&importing==null&&InterfaceMenu.Button(new(right.x,right.y,180,34),"载入 .module"))picker=WindowsDialogs.PickOpenFile("载入子 mod","Module (*.module)\0*.module\0\0");
  InterfaceMenu.Paragraph(new(right.x,right.y+42,right.width,50),message.Length>0?message:"删除模块会保留配置与存档，再次载入后可继续使用。",null,13);
  var body=new Rect(right.x,right.y+100,right.width,Math.Max(40,right.height-100));manageScroll=GUI.BeginScrollView(body,manageScroll,new Rect(0,0,body.width-22,installed.Count*90));
  try{int i=0;foreach(var module in installed){float y=i++*90;InterfaceMenu.Label(new(0,y,body.width-210,28),module.Name,InterfaceMenu.Accent,17);InterfaceMenu.Paragraph(new(0,y+32,body.width-28,46),module.Status,null,12);
    if(module.Removing){InterfaceMenu.Label(new(body.width-192,y,164,30),"正在卸载…",null,13);continue;}
    if(picker!=null||importing!=null)continue;
    if(InterfaceMenu.Button(new(body.width-192,y,78,30),module.Enabled?"关闭":"启用")){try{FunModulePackage.SetEnabled(Root,module.Id,!module.Enabled);next=0;message="";confirmDelete="";}catch(Exception e){message=e.GetBaseException().Message;}}
    bool confirming=confirmDelete==module.Id&&Time.unscaledTime<confirmUntil;
    if(InterfaceMenu.Button(new(body.width-106,y,78,30),confirming?"确认删除":"删除",confirming)){if(!confirming){confirmDelete=module.Id;confirmUntil=Time.unscaledTime+5;}else try{FunModulePackage.BeginRemoval(Root,module.Id);confirmDelete="";next=0;message="正在卸载，等待当前操作完成";}catch(Exception e){message=e.GetBaseException().Message;}}
   }}finally{GUI.EndScrollView();}
 }
 internal static void Reset(){panels.Clear();installed.Clear();confirmDelete="";next=0;}
 internal static object Diagnostics()=>new{loaded=panels.Select(p=>new{id=p.Id,title=p.Title}),installed=installed.Select(m=>new{id=m.Id,name=m.Name,enabled=m.Enabled,status=m.Status})};
}
