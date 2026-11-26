using System.Text.Json;
using System.Text.Json.Nodes;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
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
 static JsonArray CaptureJson(Item item){
  var flat=Singleton<ItemFactory>.Instance.TreeToFlatItems(new Item[]{item});
  var data=RaidStashJson.Normalize(Newtonsoft.Json.JsonConvert.SerializeObject(new Il2CppSystem.Object(flat.Pointer)),out _,out _);
  return data;
 }
 static JsonObject CaptureRecord(Item item)=>CaptureJson(item).OfType<JsonObject>().Single(entry=>entry["_id"]?.GetValue<string>()==item.Id.ToString());
 static void LoadedJson(string text,string revision){
  jsonBaseline=InventoryItemJsonRules.Format(text);jsonDraft=jsonBaseline;jsonRevision=revision;
  jsonScroll=chipScroll=Vector2.zero;chipSource="";jumpCharIndex=-1;notice="";
 }
 static void RebuildChips(){
  if(chipSource==jsonDraft)return;
  chipSource=jsonDraft;jsonChips.Clear();
  try{
   using var doc=JsonDocument.Parse(jsonDraft);
   if(doc.RootElement.ValueKind!=JsonValueKind.Array)return;
   int idx=0;
   foreach(var el in doc.RootElement.EnumerateArray()){
    if(el.ValueKind!=JsonValueKind.Object)continue;
    string id=el.TryGetProperty("_id",out var idProp)&&idProp.ValueKind==JsonValueKind.String?idProp.GetString()??"":"";
    string tpl=el.TryGetProperty("_tpl",out var tplProp)&&tplProp.ValueKind==JsonValueKind.String?tplProp.GetString()??"":"";
    if(id.Length==0)continue;
    string slot=el.TryGetProperty("slotId",out var sProp)&&sProp.ValueKind==JsonValueKind.String?sProp.GetString()??"":"";
    int stack=el.TryGetProperty("upd",out var upd)&&upd.ValueKind==JsonValueKind.Object&&upd.TryGetProperty("StackObjectsCount",out var sc)&&sc.TryGetInt32(out int count)?count:1;
    string itemName=tpl.Length==24?LootDisplay.TemplateName(tpl):tpl;
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
    var baseline=InventoryItemJsonRules.ParseTree(jsonBaseline);
    var live=CaptureJson(current.Item);
    if(!JsonNode.DeepEquals(baseline,live))throw new ArgumentException("物品数据已有更新，请刷新后重试。");
    for(int i=0;i<candidate.Count;i++){
     var onlyUpd=(JsonObject)candidate[i]!.DeepClone();
     if(baseline[i]!.AsObject().ContainsKey("upd"))onlyUpd["upd"]=baseline[i]!["upd"]?.DeepClone();else onlyUpd.Remove("upd");
     if(!JsonNode.DeepEquals(baseline[i],onlyUpd))throw new ArgumentException("战局中只能修改物品的 upd 数据。");
    }
    ApplyJson(current,candidate,true);Close();FeatureToasts.Show("物品参数已应用，随战局正常结算。");return;
   }
   var payload=JsonRequest(current,"inventoryItemTreeJsonSave");payload["preview"]=candidate.ToJsonString();payload["revision"]=jsonRevision;
   saving=CharacterDataPanel.Call("/local/gear/data","characterData",payload);submittedJson=candidate;notice="正在保存物品…";
  }catch(Exception error){notice=error.Message;}
 }
 static void WriteUpd(Item item,JsonObject upd)=>ItemDeserializer.CreateItem(item,new UnparsedData{JToken=JToken.Parse(upd.ToJsonString())});
 static void ApplyJson(Target current,JsonArray candidate,bool validate){
  var factory=Singleton<ItemFactory>.Instance;
  var live=InventoryItemJsonRules.Index(CaptureJson(current.Item));
  var requestedItems=InventoryItemJsonRules.Index(candidate);
  if(live.Count!=requestedItems.Count||live.Keys.Any(id=>!requestedItems.ContainsKey(id)))throw new ArgumentException("物品数据已有更新，请刷新后重试。");
  var baseline=InventoryItemJsonRules.Index(InventoryItemJsonRules.ParseTree(jsonBaseline));
  var items=new Dictionary<string,Item>{{current.Id,current.Item}};
  foreach(var item in current.Item.GetAllItems())items.TryAdd(item.Id.ToString(),item);
  var changes=new List<(Item Item,JsonObject Original,JsonObject Requested,JsonObject Desired)>();
  foreach(var pair in requestedItems){
   if(!items.TryGetValue(pair.Key,out var item)||item.StringTemplateId!=pair.Value["_tpl"]?.GetValue<string>())throw new ArgumentException("物品数据已有更新，请刷新后重试。");
   var requested=InventoryItemJsonRules.Update(pair.Value);
   if(JsonNode.DeepEquals(baseline[pair.Key]["upd"],pair.Value["upd"]))continue;
   var original=InventoryItemJsonRules.Update(live[pair.Key]);
   if(InventoryItemJsonRules.RemovesFields(original,requested))throw new ArgumentException("删除的物品字段需要重新进入角色后生效。");
   var sample=factory.CreateItem(factory.NextId.ToString(),item.StringTemplateId,new UnparsedData{JToken=JToken.Parse(requested.ToJsonString())});
   var desired=InventoryItemJsonRules.Update(CaptureRecord(sample));
   if(validate&&!InventoryItemJsonRules.Matches(requested,desired))throw new ArgumentException("游戏未接受其中一项 JSON 字段，请调整后重试。");
   var replay=factory.CreateItem(factory.NextId.ToString(),item.StringTemplateId,new UnparsedData{JToken=JToken.Parse(original.ToJsonString())});
   WriteUpd(replay,requested);
   if(!JsonNode.DeepEquals(desired,InventoryItemJsonRules.Update(CaptureRecord(replay))))
    throw new ArgumentException("此项 JSON 修改需要重新进入角色后生效。");
   changes.Add((item,original,requested,desired));
  }
  int applied=0;
  try{
   foreach(var change in changes){
    applied++;WriteUpd(change.Item,change.Requested);
    if(!JsonNode.DeepEquals(change.Desired,InventoryItemJsonRules.Update(CaptureRecord(change.Item))))
     throw new ArgumentException("游戏未接受其中一项 JSON 字段，请调整后重试。");
   }
  }catch{
   for(int i=applied-1;i>=0;i--)WriteUpd(changes[i].Item,changes[i].Original);throw;
  }
  foreach(var change in changes)if(change.Item.Pointer!=current.Item.Pointer)ItemFirContext.Refresh(current.Owner,change.Item);
  ItemFirContext.Refresh(current.Owner,current.Item);
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
   jsonDraft=JsonEditorText.Accept(GUI.TextArea(new Rect(0,0,width,height),jsonDraft,InventoryItemJsonRules.TreeLimit,jsonStyle));
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
