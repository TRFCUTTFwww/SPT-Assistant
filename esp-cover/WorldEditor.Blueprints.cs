using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BepInEx;
using Comfort.Common;
using EFT;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class WorldEditor {
 sealed record BlueprintActor(float X,float Y,float Z,SceneTemplate Template);
 sealed record BlueprintContainer(string Template,float X,float Y,float Z,float Qx,float Qy,float Qz,float Qw,bool Opened,string Name,ItemCopy[] Contents);
 sealed record BlueprintItem(float X,float Y,float Z,ItemCopy Item);
 sealed record BlueprintDocument(int Version,string Name,string Map,string Created,BlueprintActor[] Actors,BlueprintContainer[] Containers,BlueprintItem[] Items);
 static readonly List<BlueprintDocument> blueprints=new();
 static readonly JsonSerializerOptions blueprintJson=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
 static string BlueprintPath=>Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","world-blueprints.json");
 static bool blueprintsLoaded,showingBlueprints;
 static string blueprintName="",blueprintStatus="";
 static Vector2 blueprintScroll;
 static Task<string?>? blueprintPick;
 static string blueprintPickMode="";
 static void LoadBlueprints(){if(blueprintsLoaded)return;blueprintsLoaded=true;try{blueprints.Clear();blueprints.AddRange(PresetStorage.Load<BlueprintDocument>(BlueprintPath));}catch(Exception e){blueprintStatus="蓝图读取失败，原文件保留："+e.Message;}}
 static void SaveBlueprints()=>PresetStorage.Save(BlueprintPath,blueprints);
 static void ResetBlueprints(){blueprintPick=null;blueprintPickMode="";}
 static BlueprintDocument CaptureCurrentBlueprint(string title){
  var current=Current();
  var actors=new List<BlueprintActor>();
  foreach(var candidate in current.AllAlivePlayersList)if(Editable(candidate))actors.Add(new(candidate.Position.x,candidate.Position.y,candidate.Position.z,SceneTemplates.Capture(candidate,candidate.Profile.Nickname)));
  var containers=new List<BlueprintContainer>();
  foreach(var c in ContainerScene.Placed(current)){var root=ContainerScene.Root(c);var copy=CaptureContainer(c);containers.Add(new(copy.Template,root.position.x,root.position.y,root.position.z,copy.Rotation.x,copy.Rotation.y,copy.Rotation.z,copy.Rotation.w,copy.Opened,copy.Name,copy.Contents));}
  var items=new List<BlueprintItem>();
  var all=current.LootItems;
  for(int i=0;i<all.Count;i++){var lootItem=all.GetByIndex(i);if(lootItem?.Item==null)continue;string id=lootItem.Item.Id.ToString();if(!WorldItemPlacement.IsPlaced(id))continue;items.Add(new(lootItem.transform.position.x,lootItem.transform.position.y,lootItem.transform.position.z,CaptureItem(lootItem.Item)));}
  string clean=string.IsNullOrWhiteSpace(title)?"蓝图 "+DateTime.Now.ToString("MMdd-HHmmss"):title.Trim();
  if(clean.Length>80)clean=clean[..80];
  return new(1,clean,current.LocationId,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),actors.ToArray(),containers.ToArray(),items.ToArray());
 }
 static void ApplyBlueprint(BlueprintDocument doc){
  var current=Current();
  if(!string.Equals(doc.Map,current.LocationId,StringComparison.OrdinalIgnoreCase))throw new ArgumentException($"该蓝图属于地图：{doc.Map}");
  int cCount=0,iCount=0,aCount=0;
  foreach(var c in doc.Containers){var copy=new ContainerCopy(c.Contents,c.Template,new(c.Qx,c.Qy,c.Qz,c.Qw),c.Opened,c.Name);PlaceContainerCopy(copy,new(c.X,c.Y,c.Z));cCount++;}
  foreach(var item in doc.Items){PlaceItem(CloneItem(item.Item),new(item.X,item.Y,item.Z));iCount++;}
  foreach(var a in doc.Actors){SceneTemplates.Spawn(current,new(a.X,a.Y,a.Z),a.Template with{Id=Guid.NewGuid().ToString("N"),Count=1});aCount++;}
  blueprintStatus=$"已应用蓝图：{doc.Name}（容器 {cCount} · 物品 {iCount} · AI {aCount}）";
  FeatureToasts.Show(blueprintStatus);
 }
 static void BeginExportBlueprint(BlueprintDocument doc){
  if(blueprintPick!=null)return;
  blueprintPickMode="export:"+doc.Name+"|"+doc.Created;
  blueprintPick=WindowsDialogs.PickFolder("选择场景蓝图导出目录");
 }
 static void BeginImportBlueprint(){
  if(blueprintPick!=null)return;
  blueprintPickMode="import";
  blueprintPick=WindowsDialogs.PickOpenFile("导入场景蓝图","场景蓝图 (*.json)\0*.json\0所有文件 (*.*)\0*.*\0");
 }
 static void PollBlueprintDialog(){
  if(blueprintPick is not {IsCompleted:true} done)return;
  string mode=blueprintPickMode;blueprintPick=null;blueprintPickMode="";
  try{
   string? path=done.GetAwaiter().GetResult();
   if(string.IsNullOrWhiteSpace(path))return;
   if(mode.StartsWith("export:",StringComparison.Ordinal)){
    string key=mode[7..];
    var doc=blueprints.FirstOrDefault(b=>(b.Name+"|"+b.Created)==key)??throw new ArgumentException("未找到要导出的蓝图。");
    string safeName=string.Concat(doc.Name.Select(ch=>Path.GetInvalidFileNameChars().Contains(ch)?'_':ch));
    if(string.IsNullOrWhiteSpace(safeName))safeName="blueprint";
    string file=Path.Combine(path,$"{safeName}-{DateTime.Now:yyyyMMdd-HHmmss}.blueprint.json");
    File.WriteAllText(file,JsonSerializer.Serialize(doc,blueprintJson));
    blueprintStatus="已导出到："+file;FeatureToasts.Show(blueprintStatus);
   }else if(mode=="import"){
    var doc=JsonSerializer.Deserialize<BlueprintDocument>(File.ReadAllText(path),blueprintJson)??throw new InvalidDataException("蓝图文件为空。");
    if(doc.Version!=1||string.IsNullOrWhiteSpace(doc.Name)||string.IsNullOrWhiteSpace(doc.Map))throw new InvalidDataException("蓝图格式无效。");
    blueprints.Add(doc with{Created=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")});
    SaveBlueprints();
    blueprintStatus=$"已导入蓝图：{doc.Name}（{doc.Map}）";FeatureToasts.Show(blueprintStatus);
   }
  }catch(Exception e){blueprintStatus="蓝图文件操作失败："+e.GetBaseException().Message;FeatureToasts.Show(blueprintStatus);}
 }
 static void DrawBlueprints(Rect rect,Action? back=null){
  LoadBlueprints();PollBlueprintDialog();
  float x=rect.x,y=rect.y,w=rect.width;
  if(back!=null){if(InterfaceMenu.Button(new(x,y,140,30),"返回设置"))back();InterfaceMenu.Label(new(x+152,y,w-152,30),"场景蓝图（容器 · 物品 · AI）",InterfaceMenu.Accent,17);y+=38;}
  InterfaceMenu.Paragraph(new(x,y,w,40),"保存当前战局内已放置容器、已生成物品与可编辑 AI；支持导出到 Windows 文件夹或从文件导入。",null,12);y+=44;
  blueprintName=InterfaceMenu.Field(new(x,y,Math.Max(120,w-248),30),blueprintName);
  if(InterfaceMenu.Button(new(x+w-240,y,116,30),"保存当前战局"))Run(()=>{var doc=CaptureCurrentBlueprint(blueprintName);blueprints.Add(doc);SaveBlueprints();blueprintName="";blueprintStatus=$"已保存：{doc.Name}";});
  if(InterfaceMenu.Button(new(x+w-116,y,116,30),blueprintPick!=null?"选择中…":"导入文件"))BeginImportBlueprint();y+=38;
  if(blueprintStatus.Length>0){InterfaceMenu.Label(new(x,y,w,26),blueprintStatus,InterfaceMenu.Accent,12);y+=30;}
  float viewH=Math.Max(120,rect.yMax-y-6);
  blueprintScroll=GUI.BeginScrollView(new(x,y,w,viewH),blueprintScroll,new(0,0,w-22,Math.Max(viewH,blueprints.Count*78+16)));
  try{
   float rowY=0,cw=w-28;
   for(int i=blueprints.Count-1;i>=0;i--){
    var bp=blueprints[i];int idx=i;
    InterfaceMenu.Label(new(0,rowY,cw,24),$"{bp.Name} · {bp.Map} · 容器 {bp.Containers.Length} / 物品 {bp.Items.Length} / AI {bp.Actors.Length}",null,13);
    float bw=Math.Max(70,(cw-18)/3f);
    if(InterfaceMenu.Button(new(0,rowY+28,bw,32),"还原"))Run(()=>ApplyBlueprint(bp));
    if(InterfaceMenu.Button(new(bw+9,rowY+28,bw,32),"导出"))BeginExportBlueprint(bp);
    if(InterfaceMenu.Button(new((bw+9)*2,rowY+28,bw,32),"删除")){blueprints.RemoveAt(idx);SaveBlueprints();blueprintStatus="已删除蓝图："+bp.Name;break;}
    rowY+=74;
   }
  }finally{GUI.EndScrollView();}
 }
}
