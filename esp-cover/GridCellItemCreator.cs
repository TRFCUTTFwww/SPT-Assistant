using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using LocalSpt.Shared;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LocalSpt5Esp;

internal static class GridCellItemCreator {
 sealed record GridTarget(
  Profile? Owner,
  GridView View,
  EFT.InventoryLogic.Grid Grid,
  ItemController? Controller,
  Item ParentItem,
  int CellX,
  int CellY,
  IntPtr Raid,
  IntPtr Screen);

 sealed record PendingPlacement(Item BuiltRoot,LocationInGrid Location);
 sealed record CatalogRow(string Id,string Name,string EnglishName,LootCategory Category,string SearchBlob);

 enum AddMode{Catalog,Json}

 static readonly MenuInputGate input=new();
 static GridTarget? contextTarget,modalTarget;
 static Vector2 catalogScroll,propScroll,jsonScroll;
 static SimpleContextMenu? nativeMenu;
 static AddMode mode;
 static string searchQuery="",quantityText="1",selectedTemplate="",cachedFilterKey="",notice="";
 static int categoryIndex;
 static bool firEnabled=true,useDefaultPreset=true,verticalRotation,ignoreRestrictions=true,autoFit=true;
 static ItemComponentEditor? componentEditor;
 static string componentTemplate="";
 static string jsonDraft="",measuredJson="";
 static float measuredJsonWidth=-1,measuredJsonHeight;
 static GUIStyle? jsonStyle;
 static Task<string>? savingTask;
 static List<PendingPlacement>? pendingPlacements;
 static List<CatalogRow> allCatalog=new();
 static List<CatalogRow> filteredCatalog=new();

 static readonly (string Label,LootCategory? Category)[] CategoryTabs=[
  ("全部",null),
  ("武器",LootCategory.武器),
  ("弹药",LootCategory.弹药),
  ("配件",LootCategory.配件),
  ("防具装备",LootCategory.防具装备),
  ("医疗",LootCategory.医疗),
  ("食物饮料",LootCategory.食物饮料),
  ("钥匙",LootCategory.钥匙),
  ("交易材料",LootCategory.交易材料),
  ("任务物品",LootCategory.任务物品),
  ("其他",LootCategory.其他)
 ];

 internal static bool ContextOpen=>contextTarget!=null;
 internal static bool ModalOpen=>modalTarget!=null;
 internal static bool Open=>ContextOpen||ModalOpen;
 internal static bool InputBlocked=>input.Blocked;
 internal static bool Saving=>savingTask!=null;
 internal static void NativeItemMenuOpened(){contextTarget=null;nativeMenu=null;}

 static IntPtr CurrentRaid(){
  var game=Singleton<AbstractGame>.Instance;
  return game!=null&&game.InRaid&&game.TryCast<HideoutGame>()==null?game.Pointer:IntPtr.Zero;
 }

 static IntPtr CurrentScreen()=>EFT.UI.Screens.EftScreenManager.Instance?.CurrentScreenController?.Pointer??IntPtr.Zero;

 static bool IsTargetValid(GridTarget t){
  if(t.View==null||!t.View.isActiveAndEnabled||t.Grid==null||t.ParentItem==null)return false;
  if(t.Raid!=CurrentRaid()||t.Screen!=CurrentScreen())return false;
  return t.View.Grid?.Pointer==t.Grid.Pointer;
 }

 static bool CellOccupied(EFT.InventoryLogic.Grid grid,int x,int y){
  var collection=grid.ItemCollection;
  if(collection==null)return false;
  foreach(var kv in collection){
   var item=kv.Key;var loc=kv.Value;
   if(item==null||loc==null)continue;
   var sz=item.CalculateCellSize();
   int w=loc.r==ItemRotation.Horizontal?sz.X:sz.Y;
   int h=loc.r==ItemRotation.Horizontal?sz.Y:sz.X;
   if(x>=loc.x&&x<loc.x+w&&y>=loc.y&&y<loc.y+h)return true;
  }
  return false;
 }

 static bool RectOverlaps(EFT.InventoryLogic.Grid grid,int x,int y,int w,int h){
  var collection=grid.ItemCollection;
  if(collection==null)return false;
  foreach(var kv in collection){
   var item=kv.Key;var loc=kv.Value;
   if(item==null||loc==null)continue;
   var sz=item.CalculateCellSize();
   int iw=loc.r==ItemRotation.Horizontal?sz.X:sz.Y;
   int ih=loc.r==ItemRotation.Horizontal?sz.Y:sz.X;
   if(x<loc.x+iw&&x+w>loc.x&&y<loc.y+ih&&y+h>loc.y)return true;
  }
  return false;
 }

 static bool TryFindPlacement(EFT.InventoryLogic.Grid grid,Item item,int startX,int startY,ItemRotation preferred,bool ignoreRules,bool allowAutoSearch,out LocationInGrid result){
  var sz=item.CalculateCellSize();
  int gw=Math.Max(1,grid.GridWidth),gh=Math.Max(1,grid.GridHeight);
  bool FitsAt(int cx,int cy,ItemRotation rot){
   int w=rot==ItemRotation.Horizontal?sz.X:sz.Y;
   int h=rot==ItemRotation.Horizontal?sz.Y:sz.X;
   if(cx<0||cy<0||cx+w>gw||cy+h>gh)return false;
   if(RectOverlaps(grid,cx,cy,w,h))return false;
   return ignoreRules||grid.CheckCompatibility(item);
  }

  ItemRotation alt=preferred==ItemRotation.Horizontal?ItemRotation.Vertical:ItemRotation.Horizontal;
  if(FitsAt(startX,startY,preferred)){result=new LocationInGrid(startX,startY,preferred);return true;}
  if(allowAutoSearch&&FitsAt(startX,startY,alt)){result=new LocationInGrid(startX,startY,alt);return true;}
  if(allowAutoSearch){
   foreach(var rot in new[]{preferred,alt}){
    for(int y=startY;y<gh;y++)
     for(int x=y==startY?startX:0;x<gw;x++)
      if(FitsAt(x,y,rot)){result=new LocationInGrid(x,y,rot);return true;}
    for(int y=0;y<=startY&&y<gh;y++)
     for(int x=0;x<gw;x++)
      if(FitsAt(x,y,rot)){result=new LocationInGrid(x,y,rot);return true;}
   }
  }
  result=new LocationInGrid(startX,startY,preferred);
  return false;
 }

 static bool IsPersistentOutOfRaidTarget(GridTarget t){
  if(t.Raid!=IntPtr.Zero||t.Owner?.Inventory==null)return false;
  string parentId=t.ParentItem.Id.ToString();
  string sortingId=t.Owner.Inventory.SortingTable?.Id.ToString()??"";
  if(sortingId.Length>0&&parentId==sortingId)return false;
  var cur=t.ParentItem;
  if(cur.Id==t.Owner.Inventory.Stash?.Id||cur.Id==t.Owner.Inventory.Equipment?.Id)return true;
  var all=t.Owner.Inventory.AllRealPlayerItems;
  if(all==null)return false;
  foreach(var item in all){
   if(item==null||item.Id!=t.ParentItem.Id)continue;
   var walk=item.CurrentAddress?.Container?.ParentItem;
   int guard=0;
   while(walk!=null&&guard++<32){
    if(sortingId.Length>0&&walk.Id.ToString()==sortingId)return false;
    if(walk.Id==t.Owner.Inventory.Stash?.Id||walk.Id==t.Owner.Inventory.Equipment?.Id)return true;
    walk=walk.CurrentAddress?.Container?.ParentItem;
   }
   return true;
  }
  return false;
 }

 internal static void RefreshGridViews(EFT.InventoryLogic.Grid grid,Profile? owner,Item item){
  try{
   item.RaiseRefreshEvent(true,true);
   grid.ParentItem?.RaiseRefreshEvent(true,true);
   owner?.Inventory?.Equipment?.RaiseRefreshEvent(false,true);
   owner?.Inventory?.Stash?.RaiseRefreshEvent(false,true);
   foreach(var gv in UnityEngine.Object.FindObjectsOfType<GridView>(false)){
    try{
     if(gv==null||!gv.isActiveAndEnabled||gv.Grid?.Pointer!=grid.Pointer)continue;
     if(gv.ItemViews!=null&&gv.ItemViews.ContainsKey(item.Id))continue;
     if(gv.SourceContext!=null&&gv._itemController!=null&&gv._itemUiContext!=null){
      var g=gv.Grid;var ctx=gv.SourceContext;var ctrl=gv._itemController;var ui=gv._itemUiContext;var fp=gv._filterPanel;bool mag=gv.IsMagnified;
      gv.Close();
      gv.Show(g,ctx,ctrl,ui,fp,mag);
     }
    }catch{}
   }
   foreach(var view in UnityEngine.Object.FindObjectsOfType<ItemView>(true))
    try{if(view?.Item?.Id==item.Id){view.RefreshIcon();view.SetQuestItemViewPanel();}}catch{}
  }catch{}
 }

 static void TryDetectEmptyCellRightClick(){
  if(!InventoryContextSettings.Add)return;
  if(ModalOpen||Saving||ItemInventoryEditor.Open||ItemInventoryEditor.Saving||FleaOfferEditor.Open||ScopeSticker.Editing||InterfaceMenu.Open||RaidChat.Open)return;
  if(!Cursor.visible||!Input.GetMouseButtonUp(1))return;
  var es=EventSystem.current;
  if(es==null)return;

  var pointer=new PointerEventData(es){position=Input.mousePosition};
  var results=new Il2CppSystem.Collections.Generic.List<RaycastResult>();
  es.RaycastAll(pointer,results);
  for(int i=0;i<results.Count;i++){
   var go=results[i].gameObject;
   if(go==null)continue;
   if(go.GetComponentInParent<SimpleContextMenu>()!=null)return;
   if(go.GetComponentInParent<ItemView>()!=null)return;
   var gv=go.GetComponentInParent<GridView>();
   if(gv==null||!gv.isActiveAndEnabled||gv._nonInteractable||gv.Grid==null)continue;
   var grid=gv.Grid;
   var parentItem=grid.ParentItem;
   if(parentItem==null||grid.GridWidth<=0||grid.GridHeight<=0)continue;
   var rt=gv._rectTransform!=null?gv._rectTransform:gv.GetComponent<RectTransform>();
   if(rt==null)continue;
   var canvas=gv.GetComponentInParent<Canvas>();
   var cam=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
   if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt,Input.mousePosition,cam,out var local))continue;
   var rect=rt.rect;
   if(rect.width<=4f||rect.height<=4f)continue;
   float px=local.x-rect.xMin;
   float py=rect.yMax-local.y;
   if(px<0||py<0||px>=rect.width||py>=rect.height)continue;
   int cx=Mathf.Clamp(Mathf.FloorToInt(px/(rect.width/grid.GridWidth)),0,grid.GridWidth-1);
   int cy=Mathf.Clamp(Mathf.FloorToInt(py/(rect.height/grid.GridHeight)),0,grid.GridHeight-1);
   if(CellOccupied(grid,cx,cy))return;

   var uiCtx=gv._itemUiContext??ItemUiContext.Instance;
   uiCtx?.ContextMenu?.Close();
   var owner=uiCtx?._profile;
   var controller=gv._itemController??uiCtx?._itemController??uiCtx?._inventoryController;
   var target=new GridTarget(owner,gv,grid,controller,parentItem,cx,cy,CurrentRaid(),CurrentScreen());
   try{ShowNativeMenu(target,uiCtx);}catch(Exception error){CloseAll();Plugin.Self.Log.LogWarning("Inventory grid context menu: "+error);FeatureToasts.Show(error.Message);}
   return;
  }
 }

 static void OpenModal(GridTarget target,AddMode initialMode){
  CloseNativeMenu();
  modalTarget=target;
  mode=initialMode;
  notice="";
  EnsureCatalogLoaded();
  if(selectedTemplate.Length==0&&filteredCatalog.Count>0)SelectCatalogItem(filteredCatalog[0].Id);
  if(jsonDraft.Length==0)FillTemplateJsonExample();
  input.SetOpen(true,Time.frameCount);
 }

 static void CloseAll(){
  CloseNativeMenu();
  modalTarget=null;
  notice="";
  if(!Saving)input.SetOpen(false,Time.frameCount);
 }

 internal static void Reset(){
  CloseNativeMenu();
  modalTarget=null;
  savingTask=null;
  pendingPlacements=null;
  componentEditor=null;
  componentTemplate="";
  notice="";
  jsonStyle=null;
  input.Reset();
 }

 static void EnsureCatalogLoaded(){
  if(allCatalog.Count==0){
   try{
    var items=ItemSpawner.CommandItems.Search("");
    allCatalog=items.Select(it=>{
     LootCategory cat=LootCategory.其他;
     try{cat=ItemCatalog.Category(it.Id);}catch{}
     string en=string.IsNullOrWhiteSpace(it.EnglishName)?it.Name:it.EnglishName;
     string blob=$"{it.Name} {en} {string.Join(' ',it.Aliases)} {it.Id}";
     return new CatalogRow(it.Id,it.Name,en,cat,blob);
    }).OrderBy(e=>e.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
    cachedFilterKey="\0";
   }catch{}
  }
  RefreshCatalogFilter();
 }

 static void RefreshCatalogFilter(){
  string key=categoryIndex+"|"+searchQuery+"|"+I18n.English;
  if(key==cachedFilterKey)return;
  cachedFilterKey=key;
  var cat=CategoryTabs[Math.Clamp(categoryIndex,0,CategoryTabs.Length-1)].Category;
  var terms=MenuSearchRules.Terms(searchQuery.Trim());
  filteredCatalog=allCatalog.Where(e=>{
   if(cat.HasValue&&e.Category!=cat.Value)return false;
   if(terms.Length==0)return true;
   return MenuSearchRules.Matches(terms,e.SearchBlob);
  }).ToList();
 }

 static void SelectCatalogItem(string tpl){
  selectedTemplate=tpl;
  if(componentTemplate==tpl&&componentEditor!=null)return;
  componentTemplate=tpl;
  componentEditor=null;
  try{
   var factory=Singleton<ItemFactory>.Instance;
   var sample=factory?.CreateItem(factory.NextId.ToString(),tpl,null);
   if(sample!=null)componentEditor=new ItemComponentEditor(sample,new ItemComponentParameters(),includeBasic:true,editingExisting:false);
  }catch{}
 }

 static void FillTemplateJsonExample(){
  string tpl=selectedTemplate.Length==24?selectedTemplate:"59faff1d86f7746c51718c9c";
  try{
   var factory=Singleton<ItemFactory>.Instance;
   if(factory!=null){
    Item? sample=useDefaultPreset?factory.GetPresetItem(tpl):null;
    sample??=factory.CreateItem(factory.NextId.ToString(),tpl,null);
    if(sample!=null){
     sample.SpawnedInSession=firEnabled;
     var arr=ItemInventoryEditor.CaptureJson(sample);
     jsonDraft=InventoryItemJsonRules.Format(arr.Count==1?arr[0]!.ToJsonString():arr.ToJsonString());
     return;
    }
   }
  }catch{}
  jsonDraft="{\n  \"_tpl\": \""+tpl+"\",\n  \"upd\": {\n    \"StackObjectsCount\": 1,\n    \"SpawnedInSession\": true\n  }\n}";
 }

 static void AddFromCatalog(GridTarget target){
  if(selectedTemplate.Length!=24)throw new ArgumentException("请先在左侧选择要添加的物品。");
  int totalCount=ItemSpawnRules.Integer(quantityText,1,int.MaxValue,"数量");
  var factory=Singleton<ItemFactory>.Instance??throw new ArgumentException("物品工厂尚未就绪。");
  var probe=factory.CreateItem(factory.NextId.ToString(),selectedTemplate,null)??throw new ArgumentException("无法创建该物品实例。");
  int stackMax=Math.Max(1,probe.StackMaxSize);
  var extraParams=componentEditor?.Read()??new ItemComponentParameters();

  var combined=new JsonArray();
  int remaining=totalCount;
  int safety=0;
  while(remaining>0&&safety++<512){
   int stack=stackMax>1?Math.Min(remaining,stackMax):1;
   Item? instance=useDefaultPreset?factory.GetPresetItem(selectedTemplate):null;
   instance??=factory.CreateItem(factory.NextId.ToString(),selectedTemplate,null)??throw new ArgumentException("无法创建该物品实例。");
   instance.StackObjectsCount=stack;
   extraParams.Apply(instance,firEnabled);
   ItemSpawner.SetCommandFir(instance,extraParams.FoundInRaid??firEnabled);
   var treeJson=ItemInventoryEditor.CaptureJson(instance);
   var (_,remapped)=InventoryItemJsonRules.NormalizeForGridAdd(treeJson.ToJsonString());
   foreach(var node in remapped)combined.Add(node!.DeepClone());
   remaining-=stack;
   if(!autoFit&&remaining>0)break;
  }
  SubmitNormalizedJson(target,combined.ToJsonString());
 }

 static void SubmitNormalizedJson(GridTarget target,string rawJson){
  if(!InventoryContextSettings.Add)throw new ArgumentException("右键添加物品已关闭。");
  if(!IsTargetValid(target))throw new ArgumentException("目标容器界面已关闭或发生变化，请重新右键空格子。");
  if(target.Controller==null)throw new ArgumentException("库存控制器尚未就绪，无法添加物品。");
  if(target.Raid!=IntPtr.Zero&&ChatFikaSession.Role!=0)
   throw new ArgumentException("FIKA 联机战局暂不支持直接往格子写入物品。");

  var (rootIds,normalized)=InventoryItemJsonRules.NormalizeForGridAdd(rawJson);
  var byId=InventoryItemJsonRules.Index(normalized);
  var childrenLookup=normalized.OfType<JsonObject>()
   .Where(o=>o["parentId"] is JsonValue pv&&pv.TryGetValue<string>(out var p)&&!string.IsNullOrEmpty(p))
   .ToLookup(o=>o["parentId"]!.GetValue<string>(),StringComparer.Ordinal);

  JsonArray CollectSubtree(string rootId){
   var arr=new JsonArray();
   var queue=new Queue<string>();
   queue.Enqueue(rootId);
   while(queue.Count>0){
    string cur=queue.Dequeue();
    var obj=byId[cur];
    arr.Add(obj.DeepClone());
    foreach(var child in childrenLookup[cur])queue.Enqueue(child["_id"]!.GetValue<string>());
   }
   return arr;
  }

  ItemRotation preferred=verticalRotation?ItemRotation.Vertical:ItemRotation.Horizontal;
  var placements=new List<PendingPlacement>();
  var finalPayload=new JsonArray();
  var tempAdded=new List<Item>();

  try{
   for(int i=0;i<rootIds.Count;i++){
    string rootId=rootIds[i];
    var subJson=CollectSubtree(rootId);
    var built=ItemInventoryEditor.BuildClientTree(subJson,rootId);
    int startX=i==0?target.CellX:0;
    int startY=i==0?target.CellY:0;
    if(!TryFindPlacement(target.Grid,built,startX,startY,preferred,ignoreRestrictions,i>0||autoFit,out var loc))
     throw new ArgumentException($"目标格位 ({target.CellX}, {target.CellY}) 空间不足或受容器限制，可勾选「自动寻位」或「忽略容器限制」。");

    if(!target.Grid.AddItemWithoutRestrictions(built,loc).Succeeded)
     throw new ArgumentException("无法将物品放入所选格位。");
    tempAdded.Add(built);
    placements.Add(new PendingPlacement(built,loc));

    var rootObj=(JsonObject)subJson[0]!;
    rootObj["parentId"]=target.ParentItem.Id.ToString();
    rootObj["slotId"]=target.Grid.ID;
    rootObj["location"]=new JsonObject{
     ["x"]=loc.x,
     ["y"]=loc.y,
     ["r"]=loc.r==ItemRotation.Vertical?1:0,
     ["isSearched"]=true
    };
    foreach(var node in subJson)finalPayload.Add(node!.DeepClone());
   }
  }finally{
   for(int i=tempAdded.Count-1;i>=0;i--)
    target.Grid.RemoveWithoutRestrictions(tempAdded[i]);
  }

  if(IsPersistentOutOfRaidTarget(target)){
   string side=target.Owner!.Side==EPlayerSide.Savage?"scav":"pmc";
   var payload=new Dictionary<string,object?>{
    {"action","inventoryGridAddItems"},
    {"preview",finalPayload.ToJsonString()},
    {"categories",new[]{side}}
   };
   pendingPlacements=placements;
   modalTarget=target;
   contextTarget=null;
   savingTask=CharacterDataPanel.Call("/local/gear/data","characterData",payload);
   input.SetOpen(true,Time.frameCount);
   notice="正在保存物品到角色存档…";
   return;
  }

  ApplyClientPlacements(target,placements);
  CloseAll();
  FeatureToasts.Show($"已向格位 ({target.CellX}, {target.CellY}) 添加 {placements.Count} 件物品。");
 }

 static void ApplyClientPlacements(GridTarget target,List<PendingPlacement> placements){
  var controller=target.Controller??throw new ArgumentException("库存控制器尚未就绪，无法添加物品。");
  var operations=new List<AddResult>();
  try{
   foreach(var entry in placements){
    var addr=target.Grid.CreateItemAddress(entry.Location);
    var op=ItemManipulator.Add(entry.BuiltRoot,addr,controller,false,ignoreRestrictions);
    if(!op.Succeeded)throw new ArgumentException("无法将物品放入所选格位："+op.Error);
    operations.Add(op.Value);
    if(entry.BuiltRoot.CurrentAddress?.Container?.Pointer!=target.Grid.Pointer)
     throw new ArgumentException("物品未能绑定到目标格位。");
   }
  }catch{
   for(int i=operations.Count-1;i>=0;i--)operations[i].RollBack();
   throw;
  }
  // Match ItemController.AddAndRaiseEvents, while preserving ignoreRestrictions.
  // Begin creates native grid views; Succeed finalizes them and makes new loot known.
  var owner=controller.Cast<IItemOwner>();
  foreach(var operation in operations){
   operation.RaiseEvents(owner,CommandStatus.Begin);
   operation.RaiseEvents(owner,CommandStatus.Succeed);
  }
  foreach(var entry in placements){
   RaidItemPoolGuard.Warm(entry.BuiltRoot);
   RefreshGridViews(target.Grid,target.Owner,entry.BuiltRoot);
  }
 }

 internal static void Tick(){
  input.Tick(Time.frameCount,Input.GetMouseButton(0)||Input.GetMouseButton(1)||Input.GetMouseButton(2));

  if(savingTask?.IsCompleted==true){
   var done=savingTask;
   var target=modalTarget;
   var placements=pendingPlacements;
   savingTask=null;
   pendingPlacements=null;
   try{
    using var doc=JsonDocument.Parse(done.GetAwaiter().GetResult());
    var root=doc.RootElement;
    if(root.TryGetProperty("err",out var err)&&err.GetInt32()!=0){
     string? msg=root.TryGetProperty("errmsg",out var em)?em.GetString():null;
     if(msg=="未知数据操作。")msg="请先点击重载服务模块后再使用空格子添加存档物品。";
     throw new ArgumentException(msg??"保存失败。");
    }
    if(target!=null&&placements!=null&&IsTargetValid(target))
     ApplyClientPlacements(target,placements);
    CloseAll();
    FeatureToasts.Show("已添加物品到格位并保存到存档。");
   }catch(Exception error){
    notice=error.Message;
    if(modalTarget==null)FeatureToasts.Show(error.Message);
   }
  }

  if(contextTarget is {} ct){
   if(!InventoryContextSettings.Add||!IsTargetValid(ct)||nativeMenu==null||!nativeMenu.gameObject.activeInHierarchy||Input.GetKeyDown(KeyCode.Escape)){CloseAll();return;}
  }
  if(modalTarget is {} mt&&!Saving){
   if(!InventoryContextSettings.Add||!IsTargetValid(mt)||(Input.GetKeyDown(KeyCode.Escape)&&!ClickSelect.IsOpen)){CloseAll();return;}
  }

  TryDetectEmptyCellRightClick();
 }

 internal static void Draw(){
  if(modalTarget is {} mt)DrawModal(mt);
 }

 static void CloseNativeMenu(){
  contextTarget=null;
  var menu=nativeMenu;nativeMenu=null;
  if(menu!=null)menu.Close();
 }

 static void ShowNativeMenu(GridTarget target,ItemUiContext? ui){
  CloseNativeMenu();
  var menu=ui?.ContextMenu??throw new ArgumentException("游戏原生右键菜单尚未就绪。");
  // Item actions are injected directly into the shared button pool, outside
  // the native dynamic-interaction lifecycle. Release them before grid Show.
  ItemFirContext.ClearButtons(menu._interactionButtonsContainer);
  // EmptyContextInteractions hardcodes HasInteractions=false and is never shown.
  // BaseEmptyContextInteractions exposes dynamic actions using its empty enum.
  var interactions=new BaseEmptyContextInteractions(ui);
  void Add(string id,string caption,Action action)=>interactions.AddDynamicInteraction(id,I18n.Text(caption),(Action)(()=>{
   if(!InventoryContextSettings.Add||!IsTargetValid(target)){CloseAll();return;}
   action();
  }),null,null,null);
  Add("assistant.grid.catalog","在此格添加物品…",()=>OpenModal(target,AddMode.Catalog));
  Add("assistant.grid.json","在此格用 JSON 添加…",()=>OpenModal(target,AddMode.Json));
  Add("assistant.grid.clipboard","粘贴剪贴板 JSON 到此格",()=>{
    CloseNativeMenu();
    string clip=GUIUtility.systemCopyBuffer??"";
    try{
     if(string.IsNullOrWhiteSpace(clip))throw new ArgumentException("剪贴板为空，请先复制物品或预设 JSON。");
     SubmitNormalizedJson(target,clip);
    }catch(Exception error){
     OpenModal(target,AddMode.Json);
     if(!string.IsNullOrWhiteSpace(clip))jsonDraft=JsonEditorText.Accept(clip);
     notice=error.Message;
    }
  });
  contextTarget=target;
  nativeMenu=menu;
  // The native EventSystem must remain enabled while this menu is open.
  // Only the catalog/JSON modal uses the assistant's input gate.
  if(!interactions.HasInteractions)throw new InvalidOperationException("原生右键菜单没有可用操作。");
  menu.Show<BaseEmptyContextInteractions.EEmptyEnum>(Input.mousePosition,interactions,null,(Item)null!);
  if(!menu.gameObject.activeInHierarchy)throw new InvalidOperationException("原生右键菜单未能显示。");
 }

 static void DrawModal(GridTarget target){
  EnsureCatalogLoaded();
  var prevColor=GUI.color;
  bool prevEnabled=GUI.enabled;
  int oldDepth=GUI.depth;GUI.depth=-850;
  try{
   GUI.color=new Color(0,0,0,.55f);
   GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
   GUI.color=prevColor;

   float w=Math.Min(880,Screen.width-20),h=Math.Min(720,Screen.height-20);
   var bounds=new Rect((Screen.width-w)/2,(Screen.height-h)/2,w,h);
   InterfaceMenu.Panel(bounds);
   float left=bounds.x+16,top=bounds.y+12,inner=w-32;
   string containerName=LootDisplay.ItemName(target.ParentItem,false);
   InterfaceMenu.Label(new Rect(left,top,inner,28),I18n.Text($"在格位 ({target.CellX}, {target.CellY}) 添加物品")+"  ·  "+containerName,InterfaceMenu.Accent,17,translate:false);
   top+=34;

   GUI.enabled=prevEnabled&&!Saving;
   float tabW=(inner-8)/2;
   if(InterfaceMenu.Button(new Rect(left,top,tabW,30),"目录选择添加",mode==AddMode.Catalog))mode=AddMode.Catalog;
   if(InterfaceMenu.Button(new Rect(left+tabW+8,top,tabW,30),"JSON / 预设添加",mode==AddMode.Json))mode=AddMode.Json;
   top+=38;

   float optW=(inner-18)/4;
   if(InterfaceMenu.Button(new Rect(left,top,optW,26),verticalRotation?"朝向 · 竖放":"朝向 · 横放",verticalRotation))verticalRotation=!verticalRotation;
   if(InterfaceMenu.Button(new Rect(left+(optW+6),top,optW,26),firEnabled?"战局勾 FIR · 开":"战局勾 FIR · 关",firEnabled))firEnabled=!firEnabled;
   if(InterfaceMenu.Button(new Rect(left+2*(optW+6),top,optW,26),ignoreRestrictions?"忽略容器限制 · 开":"忽略容器限制 · 关",ignoreRestrictions))ignoreRestrictions=!ignoreRestrictions;
   if(InterfaceMenu.Button(new Rect(left+3*(optW+6),top,optW,26),autoFit?"自动寻位 · 开":"自动寻位 · 关",autoFit))autoFit=!autoFit;
   top+=34;

   var body=new Rect(left,top,inner,Math.Max(120,bounds.yMax-top-92));
   if(mode==AddMode.Catalog)DrawCatalogTab(body);
   else DrawJsonTab(body);

   InterfaceMenu.Paragraph(new Rect(left,bounds.yMax-84,inner,36),notice,InterfaceMenu.Accent,12);
   float btnW=(inner-10)/2;
   if(InterfaceMenu.Button(new Rect(left,bounds.yMax-42,btnW,32),"取消"))CloseAll();
   else if(InterfaceMenu.Button(new Rect(left+btnW+10,bounds.yMax-42,btnW,32),mode==AddMode.Json?"按 JSON 添加到此格":"添加所选物品到此格",true)){
    try{
     if(mode==AddMode.Catalog)AddFromCatalog(target);
     else SubmitNormalizedJson(target,jsonDraft);
    }catch(Exception error){notice=error.Message;}
   }
  }finally{
   GUI.enabled=prevEnabled;
   GUI.color=prevColor;
   GUI.depth=oldDepth;
   var ev=Event.current;
   if(ev!=null&&(ev.isMouse||ev.type==EventType.ScrollWheel))ev.Use();
  }
 }

 static void DrawCatalogTab(Rect r){
  float leftW=MathF.Floor(r.width*.54f),rightW=r.width-leftW-12;
  var leftRect=new Rect(r.x,r.y,leftW,r.height);
  var rightRect=new Rect(r.x+leftW+12,r.y,rightW,r.height);
  InterfaceMenu.Surface(leftRect,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.04f),7);
  InterfaceMenu.Surface(rightRect,Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.04f),7);

  float lx=leftRect.x+8,ly=leftRect.y+8,lw=leftRect.width-16;
  string nextSearch=InterfaceMenu.Field(new Rect(lx,ly,lw-62,28),searchQuery);
  if(nextSearch!=searchQuery){searchQuery=nextSearch;catalogScroll=Vector2.zero;RefreshCatalogFilter();}
  if(searchQuery.Length==0)InterfaceMenu.Label(new Rect(lx+6,ly,lw-72,28),"搜索名称、简称或模板 ID…",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.42f),11);
  if(InterfaceMenu.Button(new Rect(lx+lw-56,ly,56,28),"清空")){searchQuery="";catalogScroll=Vector2.zero;RefreshCatalogFilter();}
  ly+=34;

  int pickedCat=ClickSelect.Draw(new Rect(lx,ly,lw,28),"grid-add-cat",CategoryTabs[categoryIndex].Label,CategoryTabs.Select(c=>c.Label).ToArray(),categoryIndex);
  if(pickedCat>=0&&pickedCat!=categoryIndex){categoryIndex=pickedCat;catalogScroll=Vector2.zero;RefreshCatalogFilter();}
  ly+=34;

  var listView=new Rect(lx,ly,lw,Math.Max(40,leftRect.yMax-ly-8));
  const float rowH=32f;
  catalogScroll=GUI.BeginScrollView(listView,catalogScroll,new Rect(0,0,lw-18,Math.Max(listView.height,filteredCatalog.Count*rowH)));
  try{
   int from=Math.Max(0,(int)(catalogScroll.y/rowH)-1);
   int to=Math.Min(filteredCatalog.Count,(int)((catalogScroll.y+listView.height)/rowH)+2);
   for(int i=from;i<to;i++){
    var entry=filteredCatalog[i];
    bool sel=entry.Id==selectedTemplate;
    string label=I18n.English?entry.EnglishName:entry.Name;
    if(InterfaceMenu.Button(new Rect(0,i*rowH,lw-20,rowH-2),label,sel))SelectCatalogItem(entry.Id);
   }
  }finally{GUI.EndScrollView();}

  float rx=rightRect.x+10,ry=rightRect.y+10,rw=rightRect.width-20;
  var selectedRow=allCatalog.FirstOrDefault(e=>e.Id==selectedTemplate);
  string selectedName=selectedRow!=null?(I18n.English?selectedRow.EnglishName:selectedRow.Name):(selectedTemplate.Length==24?ItemSpawner.TemplateName(selectedTemplate):I18n.Text("未选择物品"));
  InterfaceMenu.Label(new Rect(rx,ry,rw,24),selectedName,InterfaceMenu.Accent,14,translate:false);ry+=24;
  InterfaceMenu.Label(new Rect(rx,ry,rw,20),selectedTemplate,Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.4f),11,translate:false);ry+=26;

  InterfaceMenu.Label(new Rect(rx,ry,72,28),"添加数量",size:12);
  quantityText=InterfaceMenu.Field(new Rect(rx+76,ry,rw-76,28),quantityText);ry+=34;

  if(InterfaceMenu.Button(new Rect(rx,ry,rw,28),useDefaultPreset?"默认配件预设 · 开启":"默认配件预设 · 关闭",useDefaultPreset))
   useDefaultPreset=!useDefaultPreset;
  ry+=34;

  if(InterfaceMenu.Button(new Rect(rx,ry,rw,26),"转为 JSON 编辑此物品")){
   FillTemplateJsonExample();
   mode=AddMode.Json;
   return;
  }
  ry+=34;

  if(componentEditor!=null){
   var propView=new Rect(rx,ry,rw,Math.Max(40,rightRect.yMax-ry-8));
   propScroll=GUI.BeginScrollView(propView,propScroll,new Rect(0,0,rw-18,componentEditor.Height));
   try{componentEditor.Draw(rw-22);}finally{GUI.EndScrollView();}
  }
 }

 static void DrawJsonTab(Rect r){
  float x=r.x,y=r.y,w=r.width;
  int op=ActionButtons.Draw(new Rect(x,y,w,28),["格式化","粘贴剪贴板","填入所选模板示例","整树带钩","整树满耐久","清空"],out float barH,3);
  if(op==0)try{jsonDraft=InventoryItemJsonRules.Format(jsonDraft);notice="已格式化 JSON。";}catch(Exception e){notice=e.Message;}
  if(op==1){string clip=GUIUtility.systemCopyBuffer??"";if(!string.IsNullOrWhiteSpace(clip)){jsonDraft=JsonEditorText.Accept(clip);notice="已从剪贴板粘贴 JSON（支持单物品、物品树数组、改枪预设或 Base64）。";}}
  if(op==2){FillTemplateJsonExample();notice="已生成示例物品 JSON。";}
  if(op==3){
   try{
    var (_,arr)=InventoryItemJsonRules.NormalizeForGridAdd(jsonDraft);
    foreach(var n in arr)if(n is JsonObject o){var u=o["upd"] as JsonObject??new JsonObject();u["SpawnedInSession"]=true;o["upd"]=u;}
    jsonDraft=InventoryItemJsonRules.Format(arr.Count==1?arr[0]!.ToJsonString():arr.ToJsonString());
    notice="已为草稿内所有物品添加战局发现（FIR）标记。";
   }catch(Exception e){notice=e.Message;}
  }
  if(op==4){
   try{
    var (_,arr)=InventoryItemJsonRules.NormalizeForGridAdd(jsonDraft);
    foreach(var n in arr)if(n is JsonObject o&&o["upd"] is JsonObject u&&u["Repairable"] is JsonObject rep&&rep["MaxDurability"]!=null)rep["Durability"]=rep["MaxDurability"]!.DeepClone();
    jsonDraft=InventoryItemJsonRules.Format(arr.Count==1?arr[0]!.ToJsonString():arr.ToJsonString());
    notice="已将耐久组件恢复满耐久。";
   }catch(Exception e){notice=e.Message;}
  }
  if(op==5)jsonDraft="";
  y+=barH+4;

  InterfaceMenu.Paragraph(new Rect(x,y,w,24),"支持单物品 {\"_tpl\":\"...\"}、扁平物品树 [...]、武器预设 {\"Items\":[...]} 或 Base64，自动生成全新唯一 _id。",Color.Lerp(InterfaceMenu.Text,InterfaceMenu.Background,.35f),11);
  y+=26;

  var viewport=new Rect(x,y,w,Math.Max(40,r.yMax-y));
  InterfaceMenu.Surface(viewport,Color.Lerp(InterfaceMenu.Background,Color.black,.22f),7);
  jsonStyle??=new GUIStyle(GUI.skin.textArea){fontSize=13,wordWrap=true,richText=false,padding=new RectOffset(8,8,8,8)};
  jsonStyle.normal.textColor=jsonStyle.focused.textColor=InterfaceMenu.Text;
  float textW=Math.Max(1,viewport.width-24);
  if(measuredJson!=jsonDraft||Math.Abs(measuredJsonWidth-textW)>.5f){
   measuredJson=jsonDraft;measuredJsonWidth=textW;
   measuredJsonHeight=jsonStyle.CalcHeight(new GUIContent(jsonDraft+"\n"),textW)+16;
  }
  float textH=Math.Max(viewport.height,measuredJsonHeight);
  var ev=Event.current;
  if(viewport.Contains(ev.mousePosition)&&ev.type==EventType.ScrollWheel){
   jsonScroll.y=Math.Clamp(jsonScroll.y+ev.delta.y*42f,0,Math.Max(0,textH-viewport.height));
   ev.Use();
  }
  jsonScroll=GUI.BeginScrollView(viewport,jsonScroll,new Rect(0,0,textW,textH),false,true);
  try{
   GUI.SetNextControlName("LocalGridAddJson");
   jsonDraft=JsonEditorText.Accept(GUI.TextArea(new Rect(0,0,textW,textH),jsonDraft,jsonStyle));
  }finally{GUI.EndScrollView();}
 }
}
