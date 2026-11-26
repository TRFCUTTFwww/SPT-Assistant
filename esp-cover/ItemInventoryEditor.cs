using System.Globalization;
using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using LocalSpt.Shared;
using UnityEngine;

namespace LocalSpt5Esp;

internal static partial class ItemInventoryEditor {
 sealed record Target(Profile Owner,Item Item,string Id,string Template,IntPtr Raid,IntPtr Screen){
  internal bool IsInRaid=>Raid!=IntPtr.Zero;

  internal bool IsCurrent(){
   var context=ItemUiContext.Instance;
   if(context?._profile?.Pointer!=Owner.Pointer||Raid!=CurrentRaid()||Screen!=CurrentScreen())return false;
   return Owner.Inventory?.AllRealPlayerItems?.Any(item=>item!=null&&item.Id==Id
    &&item.Pointer==Item.Pointer&&item.StringTemplateId==Template)==true;
  }
 }

 enum EditTab{Properties,Weapon,Json}
 static EditTab activeTab;
 static readonly MenuInputGate input=new();
 static Target? target;
 static ItemComponentEditor? editor;
 static Task<string>? saving;
 static ItemComponentParameters? submitted;
 static DogtagEdit? submittedDogtag;
 static Vector2 scroll;
 static string notice="";
 internal static bool Open=>target!=null;
 internal static bool InputBlocked=>input.Blocked;
 internal static bool Saving=>saving!=null||reading!=null;

 static IntPtr CurrentRaid(){
  var game=Singleton<AbstractGame>.Instance;
  return game!=null&&game.InRaid&&game.TryCast<HideoutGame>()==null?game.Pointer:IntPtr.Zero;
 }

 static IntPtr CurrentScreen()=>EFT.UI.Screens.EftScreenManager.Instance?.CurrentScreenController?.Pointer??IntPtr.Zero;

 internal static void Show(Profile owner,Item item){
  if(Open||Saving)return;
  var current=new Target(owner,item,item.Id,item.StringTemplateId,CurrentRaid(),CurrentScreen());
  if(!current.IsCurrent())throw new ArgumentException("物品已离开当前库存，请重新打开右键菜单。");
  if(current.IsInRaid&&ChatFikaSession.Role!=0)
   throw new ArgumentException("FIKA 战局暂不支持右键编辑库存物品。");

  editor=new ItemComponentEditor(item,new ItemComponentParameters(),editingExisting:true);
  activeTab=EditTab.Properties;
  ResetJson();
  ResetWeapon(item);
  target=current;
  notice="";
  scroll=Vector2.zero;
  input.SetOpen(true,Time.frameCount);
 }

 static void Save(){
  if(target is not Target current||editor==null||Saving)return;
  if(activeTab==EditTab.Json){SaveJson(current);return;}
  if(activeTab==EditTab.Weapon){ApplyWeaponTab(current);return;}
  try{
   if(!current.IsCurrent())throw new ArgumentException("物品已离开当前库存，请重新打开右键菜单。");
   var requested=editor.Read();
   var dogtag=editor.ReadDogtag();
   var attributes=requested.Attributes();
   if(attributes.Count==0&&dogtag==null)throw new ArgumentException("请至少选择一项可修改的物品参数。");

   // Validate combinations against a fresh copy before writing to the live item or server.
   var factory=Singleton<ItemFactory>.Instance??throw new ArgumentException("物品工厂尚未就绪。");
   var sample=factory.CreateItem(factory.NextId.ToString(),current.Template,null);
   LoadoutPlans.Parameters(current.Item).Apply(sample);
   requested.Apply(sample);
   if(dogtag!=null){
    var component=RequireDogtag(sample);
    var expected=ReadDogtag(component).With(dogtag);
    expected.Write(component,dogtag);
    if(ReadDogtag(component)!=expected)throw new ArgumentException("游戏未接受其中一项狗牌参数，请调整后重试。");
   }
   var actual=LoadoutPlans.Parameters(sample).Attributes();
   if(attributes.Any(pair=>!actual.TryGetValue(pair.Key,out var value)||!Equals(pair.Value,value)))
    throw new ArgumentException("游戏未接受其中一项物品参数，请调整数值。");

   if(current.IsInRaid){
    if(ChatFikaSession.Role!=0)throw new ArgumentException("FIKA 战局暂不支持右键编辑库存物品。");
    Apply(current,requested,dogtag);
    Close();
    FeatureToasts.Show("物品参数已应用，随战局正常结算。");
    return;
   }

   // Round-trip Float32 values as doubles so the wire value obeys the server's storage limit.
   foreach(var key in attributes.Keys.ToArray())if(attributes[key] is float number)attributes[key]=(double)number;
   var payload=new Dictionary<string,object?>{
    {"action",dogtag==null?"editInventoryItem":"editInventoryDogtag"},{"id",current.Id},{"template",current.Template},
    {"attributes",attributes},{"categories",new[]{current.Owner.Side==EPlayerSide.Savage?"scav":"pmc"}}
   };
   if(dogtag!=null)payload["dogtag"]=dogtag;
   saving=CharacterDataPanel.Call("/local/gear/data","characterData",payload);
   submitted=requested;
   submittedDogtag=dogtag;
   notice="正在保存物品…";
  }catch(Exception error){notice=error.Message;}
 }

 static DogtagComponent RequireDogtag(Item item)=>item.GetItemComponent<DogtagComponent>()??throw new ArgumentException("只有狗牌可以修改狗牌参数。");

 readonly record struct DogtagState(string? Nickname,long Time,EPlayerSide Side,int Level,string? Status,string? WeaponName){
  internal DogtagState With(DogtagEdit edit)=>new(
   edit.Nickname??Nickname,
   edit.Time==null?Time:DateTime.ParseExact(edit.Time,"O",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind).ToBinary(),
   edit.Side==null?Side:edit.Side=="Usec"?EPlayerSide.Usec:EPlayerSide.Bear,
   edit.Level??Level,
   edit.Status??Status,
   edit.WeaponName??WeaponName);

  internal void Write(DogtagComponent tag,DogtagEdit edit){
   if(edit.Nickname!=null)tag.Nickname=Nickname;
   if(edit.Time!=null)tag.Time=Il2CppSystem.DateTime.FromBinary(Time);
   if(edit.Side!=null)tag.Side=Side;
   if(edit.Level!=null)tag.Level=Level;
   if(edit.Status!=null)tag.Status=Status;
   if(edit.WeaponName!=null)tag.WeaponName=WeaponName;
  }
 }

 static DogtagState ReadDogtag(DogtagComponent tag)=>new(tag.Nickname,tag.Time.ToBinary(),tag.Side,tag.Level,tag.Status,tag.WeaponName);

 static void Apply(Target current,ItemComponentParameters requested,DogtagEdit? dogtag){
  var original=LoadoutPlans.Parameters(current.Item);
  var tag=dogtag==null?null:RequireDogtag(current.Item);
  var originalTag=tag==null?default:ReadDogtag(tag);
  try{
   requested.Apply(current.Item);
   if(tag!=null&&dogtag!=null)originalTag.With(dogtag).Write(tag,dogtag);
  }catch{
   if(tag!=null&&dogtag!=null)originalTag.Write(tag,dogtag);
   original.Apply(current.Item);
   throw;
  }
  ItemFirContext.Refresh(current.Owner,current.Item);
 }

 internal static void Tick(){
  input.Tick(Time.frameCount,Input.GetMouseButton(0)||Input.GetMouseButton(1)||Input.GetMouseButton(2));
  if(target is not Target current)return;
  TickJson(current);

  if(saving?.IsCompleted==true){
   bool persisted=false;
   try{
    using var doc=JsonDocument.Parse(saving.GetAwaiter().GetResult());
    var response=doc.RootElement;
    if(response.TryGetProperty("err",out var err)&&err.GetInt32()!=0){
     string? message=response.TryGetProperty("errmsg",out var errorMessage)?errorMessage.GetString():null;
     if(submittedDogtag!=null&&message=="未知数据操作。")message="请重载服务模块后保存狗牌参数。";
     throw new ArgumentException(message??"保存失败。");
    }
    var data=CharacterDataPanel.Data(response);
    persisted=data.TryGetProperty("ok",out var ok)&&ok.ValueKind==JsonValueKind.True;
    if(!persisted)throw new ArgumentException("保存结果无法确认，请重新进入角色刷新库存。");
    if(!current.IsCurrent())throw new InvalidOperationException();
    if(submittedJson!=null)ApplyJson(current,submittedJson,false);
    else Apply(current,submitted!,submittedDogtag);
    Close();
    FeatureToasts.Show("物品参数已保存到角色存档。");
   }catch(Exception error){
    if(persisted){
     Close();
     FeatureToasts.Show("物品已保存，请重新进入角色刷新库存。");
    }else notice=error.Message;
   }finally{saving=null;submitted=null;submittedDogtag=null;submittedJson=null;}
  }

  if(Open&&!Saving&&(!current.IsCurrent()||Input.GetKeyDown(KeyCode.Escape)&&!ClickSelect.IsOpen))Close();
 }

 internal static void Draw(){
  if(target is not Target current||editor==null)return;
  var previous=GUI.color;
  var active=GUI.enabled;
  try{
   GUI.color=new Color(0,0,0,.55f);
   GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
   GUI.color=previous;
   bool wide=activeTab!=EditTab.Properties;
   float width=Math.Min(wide?840:520,Screen.width-16),height=Math.Min(wide?750:600,Screen.height-16);
   var bounds=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);
   InterfaceMenu.Panel(bounds);
   float left=bounds.x+18,top=bounds.y+14,inner=width-36;
   InterfaceMenu.Label(new Rect(left,top,inner,30),"编辑物品",InterfaceMenu.Accent,18);
   InterfaceMenu.Label(new Rect(left,top+30,inner,26),LootDisplay.ItemName(current.Item,false),translate:false);

   GUI.enabled=active&&!Saving;
   bool isWeapon=current.Item.TryCast<Weapon>()!=null;
   if(isWeapon){
    float tabWidth=(inner-16)/3;
    if(InterfaceMenu.Button(new(left,top+62,tabWidth,30),"参数",activeTab==EditTab.Properties))activeTab=EditTab.Properties;
    if(InterfaceMenu.Button(new(left+tabWidth+8,top+62,tabWidth,30),"枪械数值",activeTab==EditTab.Weapon))activeTab=EditTab.Weapon;
    if(InterfaceMenu.Button(new(left+2*(tabWidth+8),top+62,tabWidth,30),"JSON",activeTab==EditTab.Json)){activeTab=EditTab.Json;if(jsonBaseline.Length==0)ReadJson(current);}
   }else{
    if(activeTab==EditTab.Weapon)activeTab=EditTab.Properties;
    float tabWidth=(inner-8)/2;
    if(InterfaceMenu.Button(new(left,top+62,tabWidth,30),"参数",activeTab==EditTab.Properties))activeTab=EditTab.Properties;
    if(InterfaceMenu.Button(new(left+tabWidth+8,top+62,tabWidth,30),"JSON",activeTab==EditTab.Json)){activeTab=EditTab.Json;if(jsonBaseline.Length==0)ReadJson(current);}
   }
   var viewport=new Rect(left,top+100,inner,height-223);
   if(activeTab==EditTab.Json)DrawJson(viewport,current);
   else if(activeTab==EditTab.Weapon)DrawWeapon(viewport,current);
   else{
    scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,inner-22,editor.Height));
    try{editor.Draw(inner-24);}finally{GUI.EndScrollView();}
   }

   InterfaceMenu.Paragraph(new Rect(left,bounds.yMax-102,inner,49),notice,InterfaceMenu.Accent,12);
   float buttonWidth=(inner-10)/2;
   string saveLabel=activeTab==EditTab.Json?"保存 JSON":activeTab==EditTab.Weapon?"应用勾选数值":"保存物品参数";
   if(InterfaceMenu.Button(new Rect(left,bounds.yMax-47,buttonWidth,32),"取消编辑物品"))Close();
   else if(InterfaceMenu.Button(new Rect(left+buttonWidth+10,bounds.yMax-47,buttonWidth,32),saveLabel,true))Save();
  }finally{
   GUI.enabled=active;
   GUI.color=previous;
   var ev=Event.current;
   if(ev!=null&&(ev.isMouse||ev.type==EventType.ScrollWheel))ev.Use();
  }
 }

 static void Close(){
  target=null;
  editor=null;
  activeTab=EditTab.Properties;
  ResetJson();
  ResetWeapon(null);
  input.SetOpen(false,Time.frameCount);
 }

 internal static void Reset(){
  target=null;
  editor=null;
  saving=null;
  submitted=null;
  submittedDogtag=null;
  activeTab=EditTab.Properties;
  ResetJson();
  ResetWeapon(null);
  input.Reset();
 }
}
