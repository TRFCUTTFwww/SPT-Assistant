using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.Builds;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LocalSpt.Shared;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LocalSpt5Esp;

internal static class PlayerWeaponPresets {
 sealed record Entry(string Id,string Name,string Weapon,int Parts);
 sealed record ParsedPreset(string Name,string RootId,JsonArray Items);
 static readonly JsonSerializerOptions PrettyJson=new(){WriteIndented=true,NewLine="\n",Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
 static readonly JsonSerializerOptions CompactJson=new(){WriteIndented=false,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
 static readonly List<Entry> entries=new();
 static string search="",notice="",quantity="1",sessionKey="",confirmDeleteId="",importName="",importDraft="";
 static bool importOpen;
 static IntPtr sessionPointer;static float nextRefresh;static Vector2 scroll,importScroll;
 static GUIStyle? pasteStyle;
 static Task<string>? mail;
 static Task<string?>? fileDialog;
 static string dialogAction="",dialogPresetId="";
 static Il2CppSystem.Threading.Tasks.Task<IResult>? buildTask;
 static readonly Queue<(WeaponBuild Build,string Name)> pendingSaves=new();
 static string buildTaskMessage="";
 static WeaponCondition condition;static string currentDurability="100",maximumDurability="100";

 static IEftSession? Session(){var app=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>()??Object.FindObjectOfType<TarkovApplication>();return app?.Session;}
 internal static void Initialize(){try{Refresh();var nativeBuilds=RequireSession().WeaponBuildsStorage?.WeaponBuilds;if(nativeBuilds!=null)foreach(var pair in nativeBuilds)if(pair.Value?.Item?.TryCast<Weapon>() is Weapon gun){WeaponEditor.CheckNativeClone(gun);try{AiEquipmentEditor.CheckNativeClone();}catch(Exception error){Plugin.Self.Error(error);}break;}Plugin.Self.Log.LogInfo("Player weapon preset read-only check: saved builds="+entries.Count+", total components="+entries.Sum(e=>e.Parts));}catch(Exception e){nextRefresh=0;Plugin.Self.Log.LogInfo("Player weapon presets await login: "+e.Message);}}
 static IEftSession RequireSession()=>Session()??throw new ArgumentException("请登录角色后读取枪械预设。");
 static bool Busy=>mail!=null||fileDialog!=null||buildTask!=null||pendingSaves.Count>0;

 static void Refresh(){
  var session=RequireSession();var storage=session.WeaponBuildsStorage?.WeaponBuilds??throw new ArgumentException("枪械预设尚未载入，请稍后刷新。");
  entries.Clear();sessionPointer=session.Pointer;sessionKey=session.GetPhpSessionId();
  foreach(var pair in storage){
   var build=pair.Value;if(build==null||build.FromPreset||build.Item?.TryCast<Weapon>()==null)continue;
   string title=!string.IsNullOrWhiteSpace(build.HandbookName)?build.HandbookName:!string.IsNullOrWhiteSpace(build.ItemIconName)?build.ItemIconName:pair.Key.ToString();
   entries.Add(new(pair.Key.ToString(),title,LootDisplay.ItemName(build.Item),Count(LoadoutPlans.CaptureItem(build.Item))));
  }
  entries.Sort((a,b)=>string.Compare(a.Name,b.Name,StringComparison.OrdinalIgnoreCase));nextRefresh=Time.unscaledTime+3;
 }

 static int Count(ItemAttachmentNode node)=>1+node.Children.Values.Sum(Count)+node.Cartridges.Count;

 static WeaponBuild FindNativeBuild(string id){
  var session=RequireSession();
  var builds=session.WeaponBuildsStorage?.WeaponBuilds;
  if(builds!=null)foreach(var pair in builds)if(pair.Key.ToString()==id&&pair.Value!=null&&!pair.Value.FromPreset&&pair.Value.Item?.TryCast<Weapon>()!=null)return pair.Value;
  throw new ArgumentException("该玩家预设已删除或不可用，请刷新。");
 }

 static JsonObject BuildExportNode(WeaponBuild build){
  var factory=Singleton<ItemFactory>.Instance??throw new ArgumentException("物品工厂尚未就绪。");
  var rootItem=build.Item??throw new ArgumentException("预设武器数据为空。");
  var flat=factory.TreeToFlatItems(new Item[]{rootItem});
  var items=RaidStashJson.Normalize(Newtonsoft.Json.JsonConvert.SerializeObject(new Il2CppSystem.Object(flat.Pointer)),out _,out _);
  string rootId=rootItem.Id.ToString();
  foreach(var entry in items.OfType<JsonObject>())
   if(entry["_id"]?.GetValue<string>()==rootId){entry.Remove("parentId");entry.Remove("slotId");entry.Remove("location");}
  string title=!string.IsNullOrWhiteSpace(build.HandbookName)?build.HandbookName:!string.IsNullOrWhiteSpace(build.ItemIconName)?build.ItemIconName:build.Id.ToString();
  return new JsonObject{["Id"]=build.Id.ToString(),["Name"]=title,["Root"]=rootId,["Items"]=items};
 }

 static string SerializePresetJson(string id,bool indented)=>BuildExportNode(FindNativeBuild(id)).ToJsonString(indented?PrettyJson:CompactJson);
 static string SerializePresetBase64(string id)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(SerializePresetJson(id,false)));
 static string SerializeAllPresetsJson(){
  var arr=new JsonArray();
  foreach(var entry in entries)arr.Add(BuildExportNode(FindNativeBuild(entry.Id)));
  return arr.ToJsonString(PrettyJson);
 }

 static string SafeFileName(string name){
  var invalid=Path.GetInvalidFileNameChars();
  string cleaned=new string((name??"preset").Select(c=>invalid.Contains(c)?'_':c).ToArray()).Trim();
  return cleaned.Length==0?"weapon-preset":cleaned;
 }

 static List<ParsedPreset> ParseImportPayload(string rawInput){
  string text=(rawInput??"").Trim();
  if(text.Length==0)throw new ArgumentException("导入内容为空，请粘贴 JSON / Base64 或选择文件。");
  if(text.StartsWith("base64:",StringComparison.OrdinalIgnoreCase))text=text[7..].Trim();
  if(!text.StartsWith('{')&&!text.StartsWith('[')){
   try{
    string compact=new string(text.Where(c=>!char.IsWhiteSpace(c)).ToArray());
    byte[] decoded=Convert.FromBase64String(compact);
    text=Encoding.UTF8.GetString(decoded).TrimStart('\uFEFF').Trim();
   }catch{throw new ArgumentException("无法识别输入格式：既不是有效 JSON，也不是有效 Base64。");}
  }
  JsonNode? root;
  try{root=JsonNode.Parse(text,documentOptions:new JsonDocumentOptions{AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip,MaxDepth=64});}
  catch(JsonException ex){throw new ArgumentException($"预设 JSON 语法无效（第 {ex.LineNumber.GetValueOrDefault()+1} 行）。");}
  var results=new List<ParsedPreset>();
  if(root is JsonObject obj)results.Add(ParseSingleObject(obj));
  else if(root is JsonArray arr){
   if(arr.Count==0)throw new ArgumentException("导入的 JSON 数组为空。");
   if(arr[0] is JsonObject first&&(HasProp(first,"Items")||HasProp(first,"items"))){
    foreach(var el in arr)if(el is JsonObject child)results.Add(ParseSingleObject(child));
   }else{
    results.Add(ParseFlatItemsArray("",null,arr));
   }
  }else throw new ArgumentException("预设数据必须是 JSON 对象或数组。");
  if(results.Count==0)throw new ArgumentException("未找到可导入的枪械预设。");
  return results;
 }

 static bool HasProp(JsonObject obj,string name)=>obj.Any(p=>string.Equals(p.Key,name,StringComparison.OrdinalIgnoreCase));
 static JsonNode? GetProp(JsonObject obj,string name)=>obj.FirstOrDefault(p=>string.Equals(p.Key,name,StringComparison.OrdinalIgnoreCase)).Value;

 static ParsedPreset ParseSingleObject(JsonObject obj){
  string name=GetProp(obj,"Name")?.GetValue<string>()??GetProp(obj,"HandbookName")?.GetValue<string>()??"";
  string? rootId=GetProp(obj,"Root")?.GetValue<string>();
  var itemsNode=GetProp(obj,"Items") as JsonArray??throw new ArgumentException("预设缺少 Items 物品数组。");
  return ParseFlatItemsArray(name,rootId,itemsNode);
 }

 static ParsedPreset ParseFlatItemsArray(string name,string? rootId,JsonArray itemsNode){
  var normalized=RaidStashJson.Normalize(itemsNode.ToJsonString(),out _,out _);
  if(normalized.Count==0)throw new ArgumentException("预设物品列表为空。");
  var ids=normalized.OfType<JsonObject>().Select(o=>o["_id"]?.GetValue<string>()??"").Where(s=>s.Length>0).ToHashSet(StringComparer.Ordinal);
  if(string.IsNullOrWhiteSpace(rootId)||!ids.Contains(rootId!)){
   rootId=normalized.OfType<JsonObject>().FirstOrDefault(o=>{
    string? p=o["parentId"]?.GetValue<string>();
    return string.IsNullOrWhiteSpace(p)||!ids.Contains(p!);
   })?["_id"]?.GetValue<string>()??throw new ArgumentException("无法定位预设的根武器节点。");
  }
  return new(name.Trim(),rootId!,normalized);
 }

 static void ImportPresets(string rawInput){
  if(ItemSpawner.InRaid())throw new ArgumentException("请退出战局后再导入或修改游戏枪械预设。");
  var session=RequireSession();
  var storage=session.WeaponBuildsStorage??throw new ArgumentException("枪械预设存储尚未就绪。");
  var factory=Singleton<ItemFactory>.Instance??throw new ArgumentException("物品工厂尚未就绪。");
  var templates=factory.ItemTemplates;
  var parsed=ParseImportPayload(rawInput);
  var prepared=new List<(WeaponBuild Build,string Name)>();
  string customName=importName.Trim();

  for(int pIdx=0;pIdx<parsed.Count;pIdx++){
   var spec=parsed[pIdx];
   var idMap=new Dictionary<string,string>(StringComparer.Ordinal);
   foreach(var obj in spec.Items.OfType<JsonObject>()){
    string oldId=obj["_id"]?.GetValue<string>()??throw new ArgumentException("预设物品缺少 _id。");
    string tpl=obj["_tpl"]?.GetValue<string>()??throw new ArgumentException("预设物品缺少 _tpl。");
    if(tpl.Length!=24||templates==null||!templates.ContainsKey(new MongoID(tpl)))
     throw new ArgumentException($"当前游戏不存在配件/武器模板：{tpl}。");
    idMap[oldId]=MongoID.Generate(true).ToString();
   }
   string newRoot=idMap[spec.RootId];
   var flat=new Il2CppReferenceArray<JsonType.FlatItem>(spec.Items.Count);
   int idx=0;
   foreach(var obj in spec.Items.OfType<JsonObject>()){
    string oldId=obj["_id"]!.GetValue<string>();
    string mappedId=idMap[oldId];
    string tpl=obj["_tpl"]!.GetValue<string>();
    var native=new JsonType.FlatItem{_id=new MongoID(mappedId),_tpl=new MongoID(tpl)};
    if(oldId!=spec.RootId&&obj["parentId"] is JsonValue pv&&pv.TryGetValue<string>(out var oldParent)&&!string.IsNullOrWhiteSpace(oldParent)){
     string mappedParent=idMap.TryGetValue(oldParent,out var mp)?mp:oldParent;
     native.parentId=RaidStash.ParentId(mappedParent);
     if(obj["slotId"] is JsonValue sv&&sv.TryGetValue<string>(out var slot))native.slotId=slot;
     if(obj["location"] is JsonNode loc)native.location=new UnparsedData{JToken=JToken.Parse(loc.ToJsonString())};
    }
    if(obj["upd"] is JsonNode upd)native.upd=new UnparsedData{JToken=JToken.Parse(upd.ToJsonString())};
    flat[idx++]=native;
   }
   var tree=factory.FlatItemsToTree(flat,false,null);
   if(tree.DeserializationErrors?.Count>0)throw new ArgumentException("预设物品结构解析失败，请检查配件槽位兼容性。");
   var weapon=tree.Items[newRoot]?.TryCast<Weapon>()??throw new ArgumentException("预设根物品不是有效枪械。");
   string baseTitle=customName.Length>0?(parsed.Count>1?$"{customName} ({pIdx+1})":customName):(spec.Name.Length>0?spec.Name:LootDisplay.ItemName(weapon,false)+" 预设");
   var existing=storage.FindByName(baseTitle);
   var buildId=existing!=null&&!existing.FromPreset?existing.Id:MongoID.Generate(true);
   prepared.Add((new WeaponBuild(buildId,baseTitle,baseTitle,weapon,false),baseTitle));
  }

  foreach(var item in prepared)pendingSaves.Enqueue(item);
  StartNextSave(storage);
 }

 static void StartNextSave(WeaponBuildsStorage storage){
  if(buildTask!=null||pendingSaves.Count==0)return;
  var next=pendingSaves.Dequeue();
  buildTaskMessage="已导入枪械预设："+next.Name+(pendingSaves.Count>0?$"（剩余 {pendingSaves.Count} 项）":"");
  notice="正在保存枪械预设："+next.Name+"…";
  buildTask=storage.SaveBuild(next.Build);
 }

 static void DeletePreset(Entry entry){
  if(ItemSpawner.InRaid())throw new ArgumentException("请退出战局后再删除游戏枪械预设。");
  var storage=RequireSession().WeaponBuildsStorage??throw new ArgumentException("枪械预设存储尚未就绪。");
  confirmDeleteId="";
  buildTaskMessage="已删除枪械预设："+entry.Name;
  notice="正在删除枪械预设："+entry.Name+"…";
  buildTask=storage.RemoveBuild(new MongoID(entry.Id));
 }

 static ItemSpawner.SavedPreset Read(Entry entry,int count,bool keepOriginal=false){
  var session=RequireSession();if(session.Pointer!=sessionPointer||session.GetPhpSessionId()!=sessionKey)throw new ArgumentException("登录角色已改变，请刷新预设列表。");
  var build=FindNativeBuild(entry.Id);
  var node=LoadoutPlans.CaptureItem(build.Item);
  if(!keepOriginal&&condition!=WeaponCondition.Original)ClearDurability(node);
  if(!keepOriginal&&condition==WeaponCondition.Custom){var value=WeaponConditionRules.Parse(currentDurability,maximumDurability);node.Parameters.Durability=value.Current;node.Parameters.MaxDurability=value.Maximum;}
  var p=node.Parameters;var settings=new ItemSpawner.Values(p.Durability.HasValue||p.MaxDurability.HasValue,p.Durability??p.MaxDurability??0,p.MaxDurability??p.Durability??0,false,0,false,0,false,0,false,0,"",0,p);
  return new(entry.Id,entry.Name,node.Template,count,settings,node);
 }

 static void ClearDurability(ItemAttachmentNode node){node.Parameters.Durability=node.Parameters.MaxDurability=null;foreach(var child in node.Children.Values)ClearDurability(child);}
 static void Spawn(Entry entry){
  int count=ItemSpawnRules.Integer(quantity,1,int.MaxValue,"枪械数量");var preset=Read(entry,count);
  if(ItemSpawner.InRaid()){notice=ItemSpawner.SpawnPreset(preset,count);return;}
  mail=GearMailClient.Send(ItemSpawner.PresetMail(preset,count),true);notice="正在将预设枪械发送到邮件…";
 }
 static void Edit(Entry entry){ItemSpawner.EditSavedPreset(Read(entry,1));notice="已载入预设，可调整属性与附件后添加。";}
 static void Action(Action action){try{action();}catch(Exception e){notice=e.Message;Plugin.Self.Log.LogWarning("Player weapon preset: "+e.Message);}}

 internal static void Reset(){entries.Clear();pendingSaves.Clear();sessionPointer=IntPtr.Zero;sessionKey=confirmDeleteId="";nextRefresh=0;mail=null;fileDialog=null;buildTask=null;importOpen=false;pasteStyle=null;}
 internal static void RefreshForAi()=>Refresh();
 internal static (string Id,string Name)[] SceneEntries(){if(entries.Count==0||Time.unscaledTime>=nextRefresh)Refresh();return entries.Select(e=>(e.Id,e.Name)).ToArray();}
 internal static ItemAttachmentNode SceneWeapon(string id){if(entries.Count==0||Time.unscaledTime>=nextRefresh)Refresh();return Read(entries.FirstOrDefault(e=>e.Id==id)??throw new ArgumentException("该玩家预设已不存在，请刷新。"),1,true).Attachments;}

 static void TickTasks(){
  if(mail?.IsCompleted==true){try{notice=mail.GetAwaiter().GetResult();}catch(Exception e){notice=e.Message;}mail=null;}
  if(fileDialog?.IsCompleted==true){
   var done=fileDialog;fileDialog=null;string mode=dialogAction,presetId=dialogPresetId;dialogAction=dialogPresetId="";
   Action(()=>{
    string? path=done.GetAwaiter().GetResult();
    if(string.IsNullOrEmpty(path))return;
    if(mode=="exportOne"){File.WriteAllText(path,SerializePresetJson(presetId,true),new UTF8Encoding(false));notice="已导出枪械预设文件："+path;}
    else if(mode=="exportAll"){File.WriteAllText(path,SerializeAllPresetsJson(),new UTF8Encoding(false));notice="已批量导出全部枪械预设："+path;}
    else if(mode=="importFile"){ImportPresets(File.ReadAllText(path));}
   });
  }
  if(buildTask?.IsCompleted==true){
   var done=buildTask;buildTask=null;
   Action(()=>{
    var res=done.GetAwaiter().GetResult();
    if(res!=null&&res.Failed){pendingSaves.Clear();throw new ArgumentException(string.IsNullOrWhiteSpace(res.Error)?"预设同步服务端失败。":res.Error);}
    if(pendingSaves.Count>0&&Session()?.WeaponBuildsStorage is {} storage){StartNextSave(storage);return;}
    Refresh();
    importDraft="";
    notice=buildTaskMessage;
    FeatureToasts.Show(buildTaskMessage);
   });
  }
 }

 internal static void Draw(Rect rect){
  TickTasks();
  if(!Busy&&Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+3;Action(Refresh);}
  float x=rect.x,y=rect.y,w=rect.width;
  InterfaceMenu.Paragraph(new Rect(x,y,w,40),"管理当前角色在游戏改枪台保存的原生枪械预设：支持生成枪械、导出 JSON 文件／复制 JSON 或 Base64、选择文件或粘贴导入、删除预设。",InterfaceMenu.Text,12);y+=44;

  bool oldGui=GUI.enabled;GUI.enabled=oldGui&&!Busy;
  search=InterfaceMenu.Field(new Rect(x,y,Math.Max(100,w-464),30),search);
  if(search.Length==0)InterfaceMenu.Label(new Rect(x+8,y,Math.Max(80,w-480),30),"搜索预设名称或武器型号…",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.45f),11);
  InterfaceMenu.Label(new Rect(x+w-454,y,40,30),"数量",InterfaceMenu.Text,12);
  quantity=InterfaceMenu.Field(new Rect(x+w-412,y,52,30),quantity);
  if(InterfaceMenu.Button(new Rect(x+w-352,y,110,30),"导入预设",importOpen))importOpen=!importOpen;
  bool canExportAll=entries.Count>0;GUI.enabled=oldGui&&!Busy&&canExportAll;
  if(InterfaceMenu.Button(new Rect(x+w-236,y,114,30),"导出全部")){
   dialogAction="exportAll";
   fileDialog=WindowsDialogs.PickSaveFile(I18n.Text("导出全部枪械预设"),"json","weapon-presets-all.json");
  }
  GUI.enabled=oldGui&&!Busy;
  if(InterfaceMenu.Button(new Rect(x+w-116,y,116,30),"刷新玩家预设"))Action(Refresh);
  y+=38;

  if(importOpen){
   float boxH=172;
   InterfaceMenu.Surface(new Rect(x,y,w,boxH),Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.045f),8);
   float ix=x+10,iy=y+8,iw=w-20;
   importName=InterfaceMenu.Field(new Rect(ix,iy,Math.Max(120,iw-396),28),importName);
   if(importName.Length==0)InterfaceMenu.Label(new Rect(ix+8,iy,Math.Max(100,iw-410),28),"自定义导入名称（留空沿用数据原名；同名将覆盖更新）",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.45f),11);
   if(InterfaceMenu.Button(new Rect(ix+iw-388,iy,124,28),"选择文件导入")){
    dialogAction="importFile";
    fileDialog=WindowsDialogs.PickOpenFile(I18n.Text("选择枪械预设文件"),"JSON / Base64 (*.json;*.txt)\0*.json;*.txt\0All files (*.*)\0*.*\0\0");
   }
   if(InterfaceMenu.Button(new Rect(ix+iw-258,iy,126,28),"从剪贴板导入"))Action(()=>ImportPresets(GUIUtility.systemCopyBuffer??""));
   if(InterfaceMenu.Button(new Rect(ix+iw-126,iy,126,28),"导入下方内容",true))Action(()=>ImportPresets(importDraft));
   iy+=34;
   pasteStyle??=new GUIStyle(GUI.skin.textArea){fontSize=12,wordWrap=true,richText=false,padding=new RectOffset(6,6,6,6)};
   pasteStyle.normal.textColor=pasteStyle.focused.textColor=InterfaceMenu.Text;
   var pasteBox=new Rect(ix,iy,iw,boxH-48);
   InterfaceMenu.Surface(pasteBox,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.075f),6);
   importScroll=GUI.BeginScrollView(pasteBox,importScroll,new Rect(0,0,iw-20,Math.Max(pasteBox.height,180)),false,true);
   try{importDraft=JsonEditorText.Accept(GUI.TextArea(new Rect(0,0,iw-20,Math.Max(pasteBox.height,180)),importDraft,524288,pasteStyle));}
   finally{GUI.EndScrollView();}
   if(importDraft.Length==0)InterfaceMenu.Label(new Rect(ix+8,iy+6,iw-28,24),"在此粘贴预设 JSON 或 Base64 文本（支持单套预设、预设数组或扁平物品数组）…",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.45f),11);
   y+=boxH+8;
  }

  string[] modes={"全新耐久","沿用预设","自定义耐久"};
  int chosenCondition=ClickSelect.Draw(new(x,y,w,28),"weapon-build-condition",modes[(int)condition],modes,(int)condition);
  if(chosenCondition>=0)condition=(WeaponCondition)chosenCondition;
  y+=34;
  if(condition==WeaponCondition.Custom){
   InterfaceMenu.Label(new Rect(x,y,80,28),"当前耐久",InterfaceMenu.Text,12);currentDurability=InterfaceMenu.Field(new Rect(x+82,y,100,28),currentDurability);
   InterfaceMenu.Label(new Rect(x+200,y,80,28),"最大耐久",InterfaceMenu.Text,12);maximumDurability=InterfaceMenu.Field(new Rect(x+282,y,100,28),maximumDurability);
   y+=34;
  }

  var filtered=entries.Where(e=>(e.Name+" "+e.Weapon).Contains(search,StringComparison.OrdinalIgnoreCase)).ToArray();
  var area=new Rect(x,y,w,Math.Max(50,rect.yMax-y-56));
  var layout=new MenuRowLayout();
  layout.Build(filtered.Select(e=>Math.Max(94,InterfaceMenu.ParagraphHeight(e.Name+"\n"+e.Weapon,Math.Max(120,w-380),14)+24)));
  scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,w-20,Math.Max(40,layout.Height)));
  Entry? chosen=null;string chosenOp="";
  var visible=layout.Visible(scroll.y,area.height);
  for(int i=visible.First;i<visible.End;i++){
   var entry=filtered[i];float top=layout.Tops[i],rowH=layout.Heights[i]-6;
   InterfaceMenu.Surface(new Rect(0,top,w-24,rowH),Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.035f),8);
   InterfaceMenu.Paragraph(new Rect(12,top+8,Math.Max(100,w-376),rowH-14),entry.Name+"\n"+entry.Weapon+" · "+entry.Parts+" 个组件",InterfaceMenu.Text,13);
   float rx=w-358,ry1=top+10,ry2=top+46;
   if(InterfaceMenu.Button(new Rect(rx,ry1,110,28),ItemSpawner.InRaid()?"添加到背包":"生成到邮件")){chosen=entry;chosenOp="spawn";}
   if(InterfaceMenu.Button(new Rect(rx+116,ry1,104,28),"载入编辑")){chosen=entry;chosenOp="edit";}
   bool confirming=confirmDeleteId==entry.Id;
   if(InterfaceMenu.Button(new Rect(rx+226,ry1,98,28),confirming?"确认删除":"删除预设",confirming)){chosen=entry;chosenOp=confirming?"deleteConfirm":"deleteAsk";}
   if(InterfaceMenu.Button(new Rect(rx,ry2,110,28),"导出文件")){chosen=entry;chosenOp="exportFile";}
   if(InterfaceMenu.Button(new Rect(rx+116,ry2,104,28),"复制 JSON")){chosen=entry;chosenOp="copyJson";}
   if(InterfaceMenu.Button(new Rect(rx+226,ry2,98,28),"复制 Base64")){chosen=entry;chosenOp="copyBase64";}
  }
  if(filtered.Length==0)InterfaceMenu.Paragraph(new Rect(10,10,w-44,72),entries.Count==0?"当前角色没有玩家枪械预设。可在游戏改枪台保存，或点击上方“导入预设”导入。":"没有匹配的预设。",InterfaceMenu.Text,13);
  GUI.EndScrollView();
  GUI.enabled=oldGui;

  if(chosen!=null)Action(()=>{
   if(chosenOp!="deleteAsk")confirmDeleteId="";
   switch(chosenOp){
    case "spawn":Spawn(chosen);break;
    case "edit":Edit(chosen);break;
    case "deleteAsk":confirmDeleteId=chosen.Id;notice="再次点击“确认删除”以移除预设："+chosen.Name;break;
    case "deleteConfirm":DeletePreset(chosen);break;
    case "exportFile":
     dialogAction="exportOne";dialogPresetId=chosen.Id;
     fileDialog=WindowsDialogs.PickSaveFile(I18n.Text("导出枪械预设"),"json",SafeFileName(chosen.Name)+".json");
     break;
    case "copyJson":
     GUIUtility.systemCopyBuffer=SerializePresetJson(chosen.Id,true);
     notice="已复制预设 JSON："+chosen.Name;FeatureToasts.Show("已复制预设 JSON");
     break;
    case "copyBase64":
     GUIUtility.systemCopyBuffer=SerializePresetBase64(chosen.Id);
     notice="已复制预设 Base64："+chosen.Name;FeatureToasts.Show("已复制预设 Base64");
     break;
   }
  });
  InterfaceMenu.Paragraph(new Rect(x,rect.yMax-50,w,48),notice,InterfaceMenu.Accent,12);
 }
}
