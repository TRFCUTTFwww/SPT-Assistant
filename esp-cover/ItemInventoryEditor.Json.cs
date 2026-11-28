using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LocalSpt.Shared;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LocalSpt5Esp;

internal static partial class ItemInventoryEditor {
 sealed record TreeChip(string Id,string Label);
 static string jsonBaseline="",jsonDraft="",jsonRevision="",measuredJson="",chipSource="",jsonSearch="";
 static readonly List<TreeChip> jsonChips=new();
 static Task<string>? reading;
 static JsonArray? submittedJson;
 static Vector2 jsonScroll,chipScroll;
 static string selectedJsonPart="";
 static GUIStyle? jsonStyle;
 static float jsonHeight,jsonWidth;
 static int jsonSearchIndex,jumpCharIndex=-1;
 static Dictionary<string,object?> JsonRequest(Target current,string action)=>new(){
  {"action",action},{"id",current.Id},{"template",current.Template},
  {"categories",new[]{current.Owner.Side==EPlayerSide.Savage?"scav":"pmc"}}
 };
 internal static JsonArray CaptureJson(Item item){
  var flat=Singleton<ItemFactory>.Instance.TreeToFlatItems(new Item[]{item});
  var data=RaidStashJson.Normalize(Newtonsoft.Json.JsonConvert.SerializeObject(new Il2CppSystem.Object(flat.Pointer)),out _,out _);
  return data;
 }
 static void LoadedJson(string text,string revision){
  jsonBaseline=InventoryItemJsonRules.Format(text);jsonDraft=jsonBaseline;jsonRevision=revision;
  jsonScroll=chipScroll=Vector2.zero;chipSource="";jumpCharIndex=-1;notice="";
 }
 static void RebuildChips(){
  if(chipSource==jsonDraft)return;
  chipSource=jsonDraft;jsonChips.Clear();
  try{
   using var doc=JsonDocument.Parse(jsonDraft,new JsonDocumentOptions{AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip,MaxDepth=1024});
   if(doc.RootElement.ValueKind!=JsonValueKind.Array)return;
   int idx=0;
   foreach(var el in doc.RootElement.EnumerateArray()){
    if(el.ValueKind!=JsonValueKind.Object)continue;
    string id=el.TryGetProperty("_id",out var idProp)&&idProp.ValueKind==JsonValueKind.String?idProp.GetString()??"":"";
    string tpl=el.TryGetProperty("_tpl",out var tplProp)&&tplProp.ValueKind==JsonValueKind.String?tplProp.GetString()??"":"";
    if(id.Length==0)continue;
    string slot=el.TryGetProperty("slotId",out var sProp)&&sProp.ValueKind==JsonValueKind.String?sProp.GetString()??"":"";
    int stack=el.TryGetProperty("upd",out var upd)&&upd.ValueKind==JsonValueKind.Object&&upd.TryGetProperty("StackObjectsCount",out var sc)&&sc.TryGetInt32(out int count)?count:1;
    string itemName=tpl.Length==24?ItemSpawner.TemplateName(tpl):tpl;
    string label=idx++==0?$"★ {itemName}":(slot.Length>0?$"{slot}: {itemName}":itemName);
    if(stack>1)label+=$" ×{stack}";
    jsonChips.Add(new(id,label));
   }
  }catch{}
 }
 static void MutateDraftTree(Action<JsonObject> mutator,string doneMessage){
  try{
   var tree=InventoryItemJsonRules.ParseTree(jsonDraft);
   foreach(var node in tree)if(node is JsonObject obj)mutator(obj);
   jsonDraft=InventoryItemJsonRules.Format(tree.ToJsonString());
   notice=doneMessage;
  }catch(Exception error){notice=error.Message;}
 }
 static void ReadJson(Target current){
  try{
   if(!current.IsCurrent())throw new ArgumentException("物品已离开当前库存，请重新打开右键菜单。");
   if(current.IsInRaid)LoadedJson(CaptureJson(current.Item).ToJsonString(),"");
   else{reading=CharacterDataPanel.Call("/local/gear/data","characterData",JsonRequest(current,"inventoryItemTreeJsonRead"));notice="正在读取物品 JSON…";}
  }catch(Exception error){notice=error.Message;}
 }
 static void TickJson(Target current){
  if(reading?.IsCompleted!=true)return;
  try{
   using var document=JsonDocument.Parse(reading.GetAwaiter().GetResult());
   var root=document.RootElement;
   if(root.TryGetProperty("err",out var error)&&error.GetInt32()!=0)
    throw new ArgumentException(root.TryGetProperty("errmsg",out var message)?message.GetString():"读取物品 JSON 失败。");
   if(!current.IsCurrent())throw new ArgumentException("物品已离开当前库存，请重新打开右键菜单。");
   var data=CharacterDataPanel.Data(root);LoadedJson(data.GetProperty("items").GetRawText(),data.GetProperty("revision").GetString()!);
  }catch(Exception error){notice=error.Message;}
  finally{reading=null;}
 }
 static void SaveJson(Target current){
  try{
   if(jsonBaseline.Length==0)throw new ArgumentException("请先读取物品 JSON。");
   if(!current.IsCurrent())throw new ArgumentException("物品已离开当前库存，请重新打开右键菜单。");
   var candidate=InventoryItemJsonRules.ValidateTree(jsonBaseline,jsonDraft);
   if(current.IsInRaid){
    if(ChatFikaSession.Role!=0)throw new ArgumentException("FIKA 战局暂不支持右键编辑库存物品。");
    ApplyJson(current,candidate,true);Close();FeatureToasts.Show("物品 JSON 已应用，随战局正常结算。");return;
   }
   var payload=JsonRequest(current,"inventoryItemTreeJsonSave");payload["preview"]=candidate.ToJsonString();payload["revision"]=jsonRevision;
   saving=CharacterDataPanel.Call("/local/gear/data","characterData",payload);submittedJson=candidate;notice="正在保存物品…";
  }catch(Exception error){notice=error.Message;}
 }
 static void WriteUpd(Item item,JsonObject upd)=>ItemDeserializer.CreateItem(item,new UnparsedData{JToken=JToken.Parse(upd.ToJsonString())});

 internal static Item BuildClientTree(JsonArray items,string rootId){
  var factory=Singleton<ItemFactory>.Instance??throw new ArgumentException("物品工厂尚未就绪。");
  var flat=new Il2CppReferenceArray<JsonType.FlatItem>(items.Count);
  for(int i=0;i<items.Count;i++){
   var obj=items[i] as JsonObject??throw new ArgumentException("物品 JSON 元素必须是对象。");
   string id=obj["_id"]?.GetValue<string>()??throw new ArgumentException("缺少物品 _id。");
   string tpl=obj["_tpl"]?.GetValue<string>()??throw new ArgumentException("缺少物品 _tpl。");
   var native=new JsonType.FlatItem{_id=new MongoID(id),_tpl=new MongoID(tpl)};
   if(obj["parentId"] is JsonValue pv&&pv.TryGetValue<string>(out var parent)&&!string.IsNullOrWhiteSpace(parent)&&id!=rootId)
    native.parentId=RaidStash.ParentId(parent);
   if(obj["slotId"] is JsonValue sv&&sv.TryGetValue<string>(out var slot)&&!string.IsNullOrWhiteSpace(slot)&&id!=rootId)
    native.slotId=slot;
   if(obj["location"] is JsonNode locNode&&id!=rootId)
    native.location=new UnparsedData{JToken=JToken.Parse(locNode.ToJsonString())};
   if(obj["upd"] is JsonObject updNode)
    native.upd=new UnparsedData{JToken=JToken.Parse(updNode.ToJsonString())};
   flat[i]=native;
  }
  var tree=factory.FlatItemsToTree(flat,false,null);
  if(tree?.Items==null||!tree.Items.ContainsKey(rootId))
   throw new ArgumentException("无法根据 JSON 构建物品实例，请检查 _tpl 与子槽位名称。");
  return tree.Items[rootId];
 }

 static bool CanApplyUpdInPlace(JsonArray liveArray,JsonArray candidate){
  var live=InventoryItemJsonRules.Index(liveArray);
  var req=InventoryItemJsonRules.Index(candidate);
  if(live.Count!=req.Count||live.Keys.Any(k=>!req.ContainsKey(k)))return false;
  foreach(var pair in req){
   var before=live[pair.Key];
   var after=pair.Value;
   if(!JsonNode.DeepEquals(before["_tpl"],after["_tpl"])
    ||!JsonNode.DeepEquals(before["parentId"],after["parentId"])
    ||!JsonNode.DeepEquals(before["slotId"],after["slotId"])
    ||!JsonNode.DeepEquals(before["location"],after["location"]))return false;
   var beforeUpd=InventoryItemJsonRules.Update(before);
   var afterUpd=InventoryItemJsonRules.Update(after);
   if(InventoryItemJsonRules.RemovesFields(beforeUpd,afterUpd))return false;
  }
  return true;
 }

 static void ApplyJson(Target current,JsonArray candidate,bool _){
  var liveArray=CaptureJson(current.Item);
  if(CanApplyUpdInPlace(liveArray,candidate)){
   var req=InventoryItemJsonRules.Index(candidate);
   var items=new Dictionary<string,Item>{{current.Id,current.Item}};
   foreach(var item in current.Item.GetAllItems())items.TryAdd(item.Id.ToString(),item);
   foreach(var pair in req){
    if(!items.TryGetValue(pair.Key,out var targetItem))continue;
    WriteUpd(targetItem,InventoryItemJsonRules.Update(pair.Value));
    if(targetItem.Pointer!=current.Item.Pointer)ItemFirContext.Refresh(current.Owner,targetItem);
   }
   ItemFirContext.Refresh(current.Owner,current.Item);
   return;
  }

  // Full live replacement: supports changing _tpl, adding/removing child attachments/ammo,
  // deleting upd fields, or relocating within the container without relogging.
  var rootObj=InventoryItemJsonRules.FindRoot(candidate);
  string newRootId=rootObj["_id"]!.GetValue<string>();
  var rebuilt=BuildClientTree(candidate,newRootId);
  var address=current.Item.CurrentAddress??throw new ArgumentException("无法读取原物品所在容器地址。");
  if(address.TryCast<GridItemAddress>() is {} gridAddr){
   var grid=gridAddr.Grid;
   var loc=gridAddr.LocationInGrid;
   if(rootObj["location"] is JsonObject customLoc){
    int cx=customLoc["x"]?.GetValue<int>()??loc.x;
    int cy=customLoc["y"]?.GetValue<int>()??loc.y;
    bool vert=customLoc["r"] is JsonValue rv&&(rv.ToString() is "1" or "Vertical");
    loc=new LocationInGrid(cx,cy,vert?ItemRotation.Vertical:ItemRotation.Horizontal);
   }
   if(!grid.RemoveWithoutRestrictions(current.Item).Succeeded)
    throw new ArgumentException("无法从当前格位移除旧物品。");
   if(!grid.AddItemWithoutRestrictions(rebuilt,loc).Succeeded){
    grid.AddItemWithoutRestrictions(current.Item,gridAddr.LocationInGrid);
    throw new ArgumentException("替换后的物品无法放入当前格位。");
   }
   GridCellItemCreator.RefreshGridViews(grid,current.Owner,rebuilt);
   return;
  }
  if(address.TryCast<SlotItemAddress>() is {} slotAddr){
   var slot=slotAddr.Slot;
   if(!slot.RemoveItemWithoutRestrictions().Succeeded)
    throw new ArgumentException("无法从当前装备槽移除旧物品。");
   if(!slot.AddWithoutRestrictions(rebuilt).Succeeded){
    slot.AddWithoutRestrictions(current.Item);
    throw new ArgumentException("替换后的物品无法装入当前槽位。");
   }
   ItemFirContext.Refresh(current.Owner,rebuilt);
   return;
  }
  throw new ArgumentException("当前物品容器类型暂不支持原位结构替换，请重登角色生效。");
 }

 static void JumpToText(string needle,int startOffset=0){
  if(string.IsNullOrEmpty(needle)||jsonDraft.Length==0)return;
  int idx=jsonDraft.IndexOf(needle,Math.Clamp(startOffset,0,jsonDraft.Length),StringComparison.OrdinalIgnoreCase);
  if(idx<0&&startOffset>0)idx=jsonDraft.IndexOf(needle,0,StringComparison.OrdinalIgnoreCase);
  if(idx>=0){jumpCharIndex=idx;jsonSearchIndex=idx+needle.Length;}
  else notice="未找到匹配内容。";
 }
 static void DrawJson(Rect rect,Target current){
  RebuildChips();
  float x=rect.x,y=rect.y,w=rect.width;
  int operation=ActionButtons.Draw(new(x,y,w,28),["格式化","复制","粘贴","整树带钩","整树满耐久","丢弃并刷新"],out float actionHeight,3);
  if(operation==0)try{jsonDraft=InventoryItemJsonRules.Format(jsonDraft);}catch(Exception error){notice=error.Message;}
  if(operation==1){GUIUtility.systemCopyBuffer=jsonDraft;notice="已复制物品树 JSON 到剪贴板。";}
  if(operation==2){
   string clip=GUIUtility.systemCopyBuffer??"";
   if(!string.IsNullOrWhiteSpace(clip)){jsonDraft=JsonEditorText.Accept(clip);notice="已从剪贴板粘贴草稿，点击保存生效。";}
  }
  if(operation==3)
   MutateDraftTree(obj=>{var upd=obj["upd"] as JsonObject??new JsonObject();upd["SpawnedInSession"]=true;obj["upd"]=upd;},"已将整棵物品树草稿设为战局发现（带钩），点击保存生效。");
  if(operation==4)
   MutateDraftTree(obj=>{if(obj["upd"] is JsonObject upd&&upd["Repairable"] is JsonObject rep&&rep["MaxDurability"]!=null)rep["Durability"]=rep["MaxDurability"]!.DeepClone();},"已将草稿内有耐久组件的物品恢复满耐久，点击保存生效。");
  if(operation==5)ReadJson(current);
  y+=actionHeight;

  jsonSearch=InterfaceMenu.Field(new(x,y,w-96,26),jsonSearch);
  if(jsonSearch.Length==0)InterfaceMenu.Label(new(x+8,y,w-110,26),"在 JSON 中查找字段、模板或数值…",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.45f),11);
  if(InterfaceMenu.Button(new(x+w-90,y,90,26),"查找下一个"))JumpToText(jsonSearch,jsonSearchIndex);
  y+=32;

  if(jsonChips.Count>1){
   int currentPart=jsonChips.FindIndex(p=>p.Id==selectedJsonPart);
   int part=ClickSelect.Draw(new(x,y,w,28),"item-json-parts",currentPart>=0?jsonChips[currentPart].Label:"选择物品或子组件…",jsonChips.Select(p=>p.Label).ToArray(),currentPart);
   if(part>=0){selectedJsonPart=jsonChips[part].Id;JumpToText("\"_id\": \""+selectedJsonPart+"\"");}
   y+=34;
  }

  var viewport=new Rect(x,y,w,Math.Max(24,rect.yMax-y));
  jsonStyle??=new GUIStyle(GUI.skin.textArea){fontSize=13,wordWrap=true,richText=false,padding=new RectOffset(8,8,8,8)};
  jsonStyle.normal.textColor=jsonStyle.focused.textColor=InterfaceMenu.Text;
  float width=Math.Max(1,viewport.width-24);
  if(measuredJson!=jsonDraft||Math.Abs(jsonWidth-width)>.5f){measuredJson=jsonDraft;jsonWidth=width;jsonHeight=jsonStyle.CalcHeight(new GUIContent(jsonDraft+"\n"),width)+16;}
  float height=Math.Max(viewport.height,jsonHeight);var ev=Event.current;
  if(jumpCharIndex>=0){
   var pos=jsonStyle.GetCursorPixelPosition(new(0,0,width,height),new GUIContent(jsonDraft),Math.Clamp(jumpCharIndex,0,jsonDraft.Length));
   jsonScroll.y=Math.Clamp(pos.y-24,0,Math.Max(0,height-viewport.height));
   jumpCharIndex=-1;
  }
  if(viewport.Contains(ev.mousePosition)&&ev.type==EventType.ScrollWheel){jsonScroll.y=Math.Clamp(jsonScroll.y+ev.delta.y*42,0,Math.Max(0,height-viewport.height));ev.Use();}
  bool caretVisible=GUI.GetNameOfFocusedControl()=="LocalItemJson"&&ev.type==EventType.KeyDown;
  jsonScroll=GUI.BeginScrollView(viewport,jsonScroll,new Rect(0,0,width,height),false,true);
  try{
   GUI.SetNextControlName("LocalItemJson");
   jsonDraft=JsonEditorText.Accept(GUI.TextArea(new Rect(0,0,width,height),jsonDraft,jsonStyle));
   if(caretVisible){
    var text=GUIUtility.GetStateObject(Il2CppInterop.Runtime.Il2CppType.Of<TextEditor>(),GUIUtility.keyboardControl)?.TryCast<TextEditor>();
    if(text!=null){var caret=jsonStyle.GetCursorPixelPosition(new(0,0,width,height),new GUIContent(jsonDraft),Math.Clamp(text.cursorIndex,0,jsonDraft.Length));
     float line=Math.Max(18,jsonStyle.lineHeight);
     if(caret.y<jsonScroll.y+8)jsonScroll.y=Math.Max(0,caret.y-8);
     else if(caret.y+line>jsonScroll.y+viewport.height-8)jsonScroll.y=caret.y+line-viewport.height+8;
    }
   }
  }finally{GUI.EndScrollView();}
 }
 static void ResetJson(){selectedJsonPart="";jsonBaseline=jsonDraft=jsonRevision=measuredJson=chipSource=jsonSearch="";jsonChips.Clear();reading=null;submittedJson=null;jsonStyle=null;jsonScroll=chipScroll=Vector2.zero;jsonSearchIndex=0;jumpCharIndex=-1;}
}
