using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LocalSpt5Esp;
internal static class InterfaceMenu {
 static ConfigFile config=null!;
 static ConfigEntry<string> background=null!,text=null!,accent=null!;
 static ConfigEntry<float> opacity=null!;
 static ConfigEntry<KeyCode> menuKey=null!;
 static readonly Dictionary<string,Slot> slots=new();
 static readonly Dictionary<ConfigEntryBase,string> drafts=new();
 static GUIStyle? label,button,field,wrappedButton,wrappedLabel;
 static Rect window;
 static Vector2 scroll,dragOffset,resizeOrigin;
 static Vector2 sidebarScroll;
 static string category="战斗与枪械",otherPage="射击辅助",filter="",drag="",hex="";
 static int colorRole;
 static bool oldVisible,editing,windowDrag;
 static CursorLockMode oldLock;
 static float nextError;
 static readonly MenuInputGate inputGate=new();
 internal static bool Open=>inputGate.Open;
 internal static bool TeleportPage=>Open&&(category=="传送与导航"||category=="AI 工作台"&&otherPage=="位置传送");
 internal static void ShowTeleport(){Navigate(category=="AI 工作台"?"AI 工作台":"传送与导航",category=="AI 工作台"?"位置传送":"角色传送");}
 internal static bool AiPage=>Open&&category=="AI 工作台";
 internal static bool AirdropPage=>Open&&category=="载具与空投"&&otherPage=="空投控制";
 internal static bool BuffPage=>Open&&category=="人物与移动"&&otherPage=="Buff 状态";
 internal static bool InputBlocked=>inputGate.Blocked||FloatingStatus.Capturing;
 internal static bool Editing=>Open&&editing;
 sealed class Slot {
  internal string Name="";
  internal ConfigEntry<float> X=null!,Y=null!,Width=null!,Height=null!;
  internal Rect Bounds;
  internal Rect Initial;
 }
 internal static Color Background=>Parse(background.Value,new Color(.05f,.07f,.1f));
 internal static Color Text=>Parse(text.Value,Color.white);
 internal static Color Accent=>Parse(accent.Value,Color.cyan);
 static Color Parse(string value,Color fallback)=>ColorUtility.TryParseHtmlString(value,out var color)?color:fallback;
 static ConfigEntry<T> Bind<T>(string key,string name,T value,AcceptableValueBase? range=null)=>config.Bind("24. Interface",key,value,new ConfigDescription("",range,new CnConfig.ConfigurationManagerAttributes{DispName=name,Category="24. 界面与主题"}));
 internal static void Initialize(Plugin plugin){
  config=plugin.Config;
  menuKey=Bind("Menu key","主菜单按键",KeyCode.RightShift);
  background=Bind("Panel background","面板背景色","#121826");
  text=Bind("Panel text","面板文字色","#E5EDF5");
  accent=Bind("Panel accent","主题强调色","#77D9C7");
  opacity=Bind("Panel opacity","面板不透明度",.94f,new AcceptableValueRange<float>(.15f,1f));
  if(background.Value=="#111827")background.Value="#121826";if(accent.Value=="#38BDF8")accent.Value="#77D9C7";if(Math.Abs(opacity.Value-.86f)<.001f)opacity.Value=.94f;
  AddSlot("noclip","穿墙速度",new Rect(Screen.width*.5f-130,Screen.height-110,260,32));
  AddSlot("damagepreview","准星伤害",new Rect(Screen.width*.5f-240,Screen.height*.5f+22,480,48));
  AddSlot("status","功能状态",new Rect(Math.Max(12,Screen.width-622),12,610,132));
  AddSlot("raidclock","战局时间",RaidClock.DefaultBounds());
  AddSlot("navigation","路面导航",new Rect(Math.Max(12,Screen.width/2-190),Math.Max(12,Screen.height-172),380,72));
  AddSlot("vitals","生存状态",new Rect(12,110,382,28));
  AddSlot("magazine","弹夹信息",new Rect(12,145,310,130));
  AddSlot("aggro","仇恨追踪",new Rect(12,Screen.height/2-70,340,140));
  AddSlot("armor","头甲分析",new Rect(Screen.width-397,165,385,115));
  AddSlot("exfil","撤离信息",new Rect(Screen.width-452,Screen.height-300,440,280));
  AddSlot("population","局内人数",new Rect(12,12,Math.Min(880,Math.Max(350,Screen.width-660)),48));
  AddSlot("incoming","受击来源",new Rect(12,Screen.height*.65f,460,104));
  AddSlot("hits","命中提示",new Rect(Screen.width-672,Screen.height*.26f,660,160));
  AddSlot("allkills","全战局击杀",new Rect(Screen.width-652,Screen.height*.49f,640,180));
  try{
   var type=typeof(GamePlayerOwner);
   plugin.Patches.Patch(type.GetMethod("TranslateAxes"),prefix:new HarmonyMethod(typeof(InterfaceMenu),nameof(BlockAxes)));
   plugin.Patches.Patch(type.GetMethod("TranslateCommand"),prefix:new HarmonyMethod(typeof(InterfaceMenu),nameof(BlockCommand)));
   plugin.Patches.Patch(typeof(InputManager).GetMethod("Update"),prefix:new HarmonyMethod(typeof(InterfaceMenu),nameof(AllowGameInput)));
   plugin.Patches.Patch(AccessTools.Method(typeof(EventSystem),"Update"),prefix:new HarmonyMethod(typeof(InterfaceMenu),nameof(AllowGameInput)));
   plugin.Patches.Patch(typeof(Player.FirearmController).GetMethod("SetTriggerPressed",new[]{typeof(bool)}),prefix:new HarmonyMethod(typeof(InterfaceMenu),nameof(AllowTrigger)));
  }catch(Exception error){plugin.Log.LogError("Main menu input hooks unavailable: "+error);}
  plugin.Log.LogInfo("Main menu initialized: key="+menuKey.Value+", layout slots="+slots.Count);
 }
 static void AddSlot(string id,string name,Rect bounds){slots[id]=new Slot{Name=name,Bounds=bounds,Initial=bounds,Width=Bind(id+" width",name+"宽度",-1f,new AcceptableValueRange<float>(-1,10000)),Height=Bind(id+" height",name+"高度",-1f,new AcceptableValueRange<float>(-1,10000)),X=Bind(id+" position x",name+"横向位置",-1f,new AcceptableValueRange<float>(-1,1)),Y=Bind(id+" position y",name+"纵向位置",-1f,new AcceptableValueRange<float>(-1,1))};}
 static bool IsLocal(PlayerOwner owner)=>owner!=null&&owner.Player!=null&&owner.Player.IsYourPlayer;
 static bool BlockAxes(GamePlayerOwner __instance)=>(!InputBlocked&&!DirectorStage.FreeActive)||!IsLocal(__instance);
 static bool BlockCommand(GamePlayerOwner __instance,ref InputNode.ETranslateResult __result){if((!InputBlocked&&!DirectorStage.FreeActive)||!IsLocal(__instance))return true;__result=InputNode.ETranslateResult.BlockAll;return false;}
 static bool AllowGameInput()=>!InputBlocked;
 static bool AllowTrigger(Player.FirearmController __instance,bool __0)=>(!InputBlocked&&!DirectorStage.FreeActive)||!__0||__instance._player==null||!__instance._player.IsYourPlayer;
 internal static void KeepCursor(){if(Open){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
 internal static void Update(){
  if(capture==null&&Input.GetKeyDown(menuKey.Value))SetOpen(!Open);
  else if(Open&&Input.GetKeyDown(KeyCode.Escape))SetOpen(false);
  inputGate.Tick(Time.frameCount,Input.GetMouseButton(0)||Input.GetMouseButton(1)||Input.GetMouseButton(2));
  KeepCursor();
 }
 static void SetOpen(bool value){
  if(Open==value)return;
  if(value){oldLock=Cursor.lockState;oldVisible=Cursor.visible;inputGate.SetOpen(true,Time.frameCount);
   try{Comfort.Common.Singleton<GameWorld>.Instance?.MainPlayer?.HandsController?.TryCast<Player.FirearmController>()?.SetTriggerPressed(false);}catch(Exception error){Plugin.Self.Log.LogWarning("Unable to release trigger for menu: "+error.Message);}
  }
  else{RaidEnd.CancelConfirmation();inputGate.SetOpen(false,Time.frameCount);editing=false;capture=null;drag="";windowDrag=false;Cursor.lockState=oldLock;Cursor.visible=oldVisible;config.Save();}
  Plugin.Self.Log.LogInfo("Main menu "+(value?"opened":"closed"));
 }
 internal static void Shutdown(){SetOpen(false);inputGate.Reset();MenuIcons.Shutdown();slots.Clear();drafts.Clear();}
 internal static Rect Place(string id,Rect initial){
  if(!slots.TryGetValue(id,out var slot))return initial;
  slot.Initial=initial;if(slot.Width.Value>0)initial.width=Math.Clamp(slot.Width.Value,60,Math.Max(60,Screen.width));if(slot.Height.Value>0)initial.height=Math.Clamp(slot.Height.Value,24,Math.Max(24,Screen.height));
  if(id=="raidclock"){initial.width=Math.Max(180,initial.width);initial.height=Math.Max(70,initial.height);}
  var position=new Vector2(slot.X.Value,slot.Y.Value);
  if(position.x>=0&&position.y>=0){initial.x=position.x*Math.Max(0,Screen.width-initial.width);initial.y=position.y*Math.Max(0,Screen.height-initial.height);}
  initial.x=Math.Clamp(initial.x,0,Math.Max(0,Screen.width-initial.width));initial.y=Math.Clamp(initial.y,0,Math.Max(0,Screen.height-initial.height));
  slot.Bounds=initial;return initial;
 }
 internal static Color Tint(Color semantic){
  if(semantic.r>.9f&&semantic.g>.9f&&semantic.b>.9f)return Text;
  if(semantic.g>.7f&&semantic.b>.7f&&semantic.r<.6f)return Accent;
  return semantic;
 }
 internal static void Surface(Rect rect,Color color,float radius=8){if(Event.current.type!=EventType.Repaint)return;var old=GUI.color;GUI.color=Color.white;GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);GUI.color=old;}
 internal static void Panel(Rect rect,float alpha=1){var shadow=new Rect(rect.x+2,rect.y+5,rect.width,rect.height);Surface(shadow,new Color(0,0,0,.2f*alpha),12);var border=Color.Lerp(Background,Text,.14f);border.a=opacity.Value*alpha;Surface(rect,border,12);var color=Background;color.a=opacity.Value*alpha;Surface(new Rect(rect.x+1,rect.y+1,rect.width-2,rect.height-2),color,11);}
 internal static void MoveSlot(string id,Vector2 position,Rect bounds){if(!slots.TryGetValue(id,out var slot))return;slot.X.Value=Math.Clamp(position.x/Math.Max(1,Screen.width-bounds.width),0,1);slot.Y.Value=Math.Clamp(position.y/Math.Max(1,Screen.height-bounds.height),0,1);}
 internal static void Close()=>SetOpen(false);
 internal static void BeginLayout(){editing=true;drag="";}
 internal static void Save()=>config.Save();
 internal static void ClearDrafts()=>drafts.Clear();
 static void Styles(){
  if(label!=null)return;
  label=new GUIStyle(GUI.skin.label){fontSize=14,alignment=TextAnchor.MiddleLeft,richText=false,wordWrap=false,clipping=TextClipping.Clip};label.normal.textColor=Color.white;
  button=new GUIStyle(label){alignment=TextAnchor.MiddleCenter};button.hover.textColor=Color.white;button.active.textColor=Color.white;
  wrappedButton=new GUIStyle(button){alignment=TextAnchor.MiddleLeft,wordWrap=true,padding=new RectOffset(12,12,7,7)};
  wrappedLabel=new GUIStyle(label){alignment=TextAnchor.UpperLeft,wordWrap=true,clipping=TextClipping.Clip};
  field=new GUIStyle(GUI.skin.textField){fontSize=14,padding=new RectOffset(8,8,5,5)};field.normal.textColor=Color.white;field.focused.textColor=Color.white;
  field.normal.background=null;field.focused.background=null;field.hover.background=null;field.active.background=null;
 }
 static void Fill(Rect rect,Color color){if(Event.current.type!=EventType.Repaint)return;var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
 internal static void Label(Rect rect,string value,Color? color=null,int size=14){Styles();var old=GUI.color;GUI.color=color??Text;label!.fontSize=size;GUI.Label(rect,value,label);GUI.color=old;}
 internal static bool Button(Rect rect,string value,bool selected=false){
  Styles();Surface(rect,selected?new Color(Accent.r,Accent.g,Accent.b,.19f):new Color(1,1,1,rect.Contains(Event.current.mousePosition)?.10f:.055f),7);
  var old=GUI.color;GUI.color=selected?Accent:Text;bool clicked=GUI.Button(rect,value,button);GUI.color=old;return clicked;
 }
 internal static string Field(Rect rect,string value){Styles();Surface(rect,Color.Lerp(Background,Text,.085f),6);var old=GUI.color;GUI.color=Text;var result=GUI.TextField(rect,value,field);GUI.color=old;return result;}
 internal static float ButtonHeight(string value,float width,float minimum=44){Styles();return Math.Max(minimum,wrappedButton!.CalcHeight(new GUIContent(value),Math.Max(1,width)));}
 internal static float ParagraphHeight(string value,float width,int size=14){Styles();wrappedLabel!.fontSize=size;return wrappedLabel.CalcHeight(new GUIContent(value),Math.Max(1,width));}
 internal static void Paragraph(Rect rect,string value,Color? color=null,int size=14){Styles();wrappedLabel!.fontSize=size;var old=GUI.color;GUI.color=color??Text;GUI.Label(rect,value,wrappedLabel);GUI.color=old;}
 internal static bool WrappedButton(Rect rect,string value,bool selected=false){Styles();Fill(rect,selected?new Color(Accent.r,Accent.g,Accent.b,.24f):new Color(1,1,1,rect.Contains(Event.current.mousePosition)?.1f:.045f));var old=GUI.color;GUI.color=selected?Accent:Text;bool clicked=GUI.Button(rect,value,wrappedButton);GUI.color=old;return clicked;}
 static float Slider(Rect rect,float value,float min,float max){
  var old=GUI.color;GUI.color=Accent;value=GUI.HorizontalSlider(rect,value,min,max);GUI.color=old;return value;
 }
 static readonly Dictionary<string,string> rememberedPages=new();static readonly Dictionary<string,(Vector2 Scroll,string Filter)> pageState=new();static bool contextKeys;static ConfigEntryBase[]? visibleEntries;
 static string PageKey=>category+"/"+otherPage;
 internal static void Navigate(string section,string? page=null){
  pageState[PageKey]=(scroll,filter);rememberedPages[category]=otherPage;if(category=="AI 工作台"&&(section!=category||page!=otherPage))AiWorkspace.CloseEditors();category=section;var choices=MenuSections.Pages(section);otherPage=page??(rememberedPages.TryGetValue(section,out var previous)&&choices.Contains(previous)?previous:choices[0]);if(!choices.Contains(otherPage))otherPage=choices[0];var state=pageState.GetValueOrDefault(PageKey);scroll=state.Scroll;filter=state.Filter??"";contextKeys=false;
 }
 internal static string Tabs(Rect rect,string[] choices,string selected,out float height){int columns=Math.Min(choices.Length,MenuWorkspaceLayout.Columns(rect.width));float width=(rect.width-8*(columns-1))/columns;height=MenuWorkspaceLayout.Height(choices.Length,rect.width)+10;for(int i=0;i<choices.Length;i++)if(Button(new Rect(rect.x+i%columns*(width+8),rect.y+i/columns*40,width,32),choices[i],selected==choices[i]))selected=choices[i];return selected;}
 internal static void Draw(){
  if(!Open)return;KeepCursor();try{
   if(editing){DrawLayout();return;}float width=Math.Min(1320,Screen.width-32),height=Math.Min(900,Screen.height-32);if(window.width!=width||window.height!=height)window=new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height);
   var e=Event.current;var header=new Rect(window.x,window.y,window.width-250,74);if(e.type==EventType.MouseDown&&e.button==0&&header.Contains(e.mousePosition)){windowDrag=true;dragOffset=e.mousePosition-window.position;e.Use();}if(e.type==EventType.MouseDrag&&windowDrag){window.position=e.mousePosition-dragOffset;window.x=Math.Clamp(window.x,0,Math.Max(0,Screen.width-window.width));window.y=Math.Clamp(window.y,0,Math.Max(0,Screen.height-window.height));e.Use();}if(e.type==EventType.MouseUp)windowDrag=false;
   Panel(window);Surface(new Rect(window.x+10,window.y+10,202,window.height-20),Color.Lerp(Background,Color.black,.18f),10);Surface(new Rect(window.x+28,window.y+28,34,34),new Color(Accent.r,Accent.g,Accent.b,.14f),9);MenuIcons.Draw(new Rect(window.x+34,window.y+34,22,22),"战局",Accent);Label(new Rect(window.x+68,window.y+22,132,28),"SPT Assistant",Text,15);Label(new Rect(window.x+73,window.y+49,122,19),"控制中心  /  1.32.0",Color.Lerp(Text,Background,.5f),11);
   if(Button(new Rect(window.xMax-70,window.y+20,48,30),"关闭")){SetOpen(false);return;}if(Button(new Rect(window.xMax-224,window.y+20,68,30),"布局")){editing=true;drag="";}if(Button(new Rect(window.xMax-148,window.y+20,68,30),contextKeys?"返回":"热键"))contextKeys=!contextKeys;
   Label(new Rect(window.x+236,window.y+18,Math.Max(100,window.width-500),31),MenuSections.DisplayName(category),Text,23);Label(new Rect(window.x+236,window.y+53,window.width-480,22),MenuSections.Subtitle(category),Color.Lerp(Text,Background,.4f),12);
   var sidebar=new Rect(window.x+22,window.y+100,184,Math.Max(140,window.height-210));sidebarScroll.x=0;float sideWidth=sidebar.width-8;sidebarScroll=GUI.BeginScrollView(sidebar,sidebarScroll,new Rect(0,0,sideWidth,MenuSections.Names.Length*49),false,false,GUIStyle.none,GUIStyle.none);sidebarScroll.x=0;
   for(int i=0;i<MenuSections.Names.Length;i++){string name=MenuSections.Names[i];var row=new Rect(0,i*49,sideWidth-4,41);if(Button(row,"",category==name))Navigate(name);if(category==name)Surface(new Rect(row.x+1,row.y+10,3,21),Accent,1);MenuIcons.Draw(new Rect(row.x+12,row.y+10,21,21),name,category==name?Accent:Color.Lerp(Text,Background,.35f));Label(new Rect(row.x+43,row.y,row.width-46,row.height),MenuSections.DisplayName(name),category==name?Accent:Text,14);}GUI.EndScrollView();Label(new Rect(window.x+30,window.yMax-82,165,22),RaidClock.Active?"●  战局进行中":"●  待命",RaidClock.Active?Accent:Color.Lerp(Text,Background,.4f),12);Label(new Rect(window.x+30,window.yMax-52,165,22),"右 Shift 开关菜单",Color.Lerp(Text,Background,.5f),11);
   var content=new Rect(window.x+236,window.y+99,window.width-260,window.height-123);
   if(contextKeys){HotkeyPage.Draw(content,category);return;}
   var pages=MenuSections.Pages(category);if(!pages.Contains(otherPage))otherPage=pages[0];string next=Tabs(content,pages,otherPage,out float tabsHeight);if(next!=otherPage)Navigate(category,next);content.y+=tabsHeight;content.height-=tabsHeight;
   if(category=="AI 工作台"){AiWorkspace.Draw(content,otherPage);return;}if(category=="队友"){CompanionPlans.Draw(content,otherPage);return;}if(category=="玩具枪工具"){ToyDeveloper.DrawWorkspace(content,otherPage);return;}
   switch((category,otherPage)){
    case ("战斗与枪械","枪械参数"):WeaponEditor.Draw(content);break;
    case ("战斗与枪械","微自瞄"):SoftAimControl.Draw(content);break;
    case ("战斗与枪械","瞄准排除"):AimTargetFilter.Draw(content);break;
    case ("战斗与枪械","命中音效"):HitSounds.Draw(content);break;
    case ("人物与移动","Buff 状态"):CharacterBuffs.Draw(content);break;
    case ("人物与移动","动作锁定"):ModelPoseLock.Draw(content);break;
    case ("人物与移动","死亡保留"):DeathInventory.Draw(content);break;
    case ("人物与移动","角色管理"):ProfileAdminPanel.Draw(content);break;
    case ("镜头与画面","镜头控制"):DirectorStage.Draw(content);break;
    case ("镜头与画面","界面布局"):DrawAppearance(content);break;
    case ("战局与物资","时间与环境"):scroll=GUI.BeginScrollView(content,scroll,new Rect(0,0,content.width-20,980));RaidClock.DrawPanel(new Rect(4,4,content.width-32,964));GUI.EndScrollView();break;
    case ("战局与物资","本局物资"):RaidLoot.Draw(content);break;
    case ("战局与物资","刷新规则"):NextRaidLoot.Draw(content);break;
    case ("战局与物资","任务物资"):TaskNeeds.Draw(content);break;
    case ("载具与空投","BTR 控制"):BtrControl.Draw(content);break;
    case ("载具与空投","空投控制"):AirdropControl.Draw(content);break;
    case ("传送与导航","角色传送"):TeleportControl.Draw(content);break;
    case ("传送与导航","路面导航"):GroundNavigation.Draw(content);break;
    case ("物品与装备","添加物品"):ItemSpawner.Draw(content);break;
    case ("物品与装备","玩家枪械预设"):PlayerWeaponPresets.Draw(content);break;
    case ("物品与装备","整套装备"):LoadoutPlans.Draw(content);break;
    case ("设置与热键","全部热键"):HotkeyPage.Draw(content);break;
    case ("设置与热键","配置方案"):ConfigProfiles.Draw(content);break;
    case ("设置与热键","主题与布局"):DrawAppearance(content);break;
    case ("设置与热键","命令控制台"):CommandoConsole.Draw(content);break;
    default:DrawSettings(content);break;
   }
  }catch(Exception error){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+10;Plugin.Self.Error(error);}}finally{var e=Event.current;if(InputBlocked&&e!=null&&(e.isMouse||e.type==EventType.ScrollWheel))e.Use();}
 }
 static void DrawAppearance(Rect content){scroll=GUI.BeginScrollView(content,scroll,new Rect(0,0,content.width-20,850));Appearance(new Rect(0,0,content.width-26,850));GUI.EndScrollView();}
 static void DrawSettings(Rect content){
  filter=Field(new Rect(content.x,content.y,content.width,30),filter);if(filter.Length==0)Label(new Rect(content.x+12,content.y,content.width-24,30),"搜索本页功能名称…",Color.Lerp(Text,Background,.55f),12);
  visibleEntries??=config.Select(pair=>pair.Value).Where(e=>e.Definition.Section!="24. Interface"&&e.Definition.Section!="25. Aim exclusions"&&e.Definition.Section!="32. 状态悬浮窗").ToArray();
  var groups=visibleEntries.Where(e=>MenuSections.OnPage(category,otherPage,e.Definition.Section,e.Definition.Key)&&(filter.Length==0||Name(e).Contains(filter,StringComparison.OrdinalIgnoreCase)||e.Definition.Key.Contains(filter,StringComparison.OrdinalIgnoreCase))).GroupBy(Category).OrderBy(g=>MenuSections.Number(g.Key)).ToArray();
  var view=new Rect(content.x,content.y+44,content.width,content.height-44);int columns=view.width>=660?2:1;float cardWidth=(view.width-24-12*(columns-1))/columns;float total=groups.Sum(g=>MathF.Ceiling(g.Count()/(float)columns)*102+44)+76;
  scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-20,Math.Max(view.height-1,total)));float y=0;foreach(var group in groups){Label(new Rect(4,y,view.width-28,28),MenuSections.Heading(group.Key),Accent,14);y+=38;int n=0;foreach(var entry in group){DrawEntry(entry,new Rect(4+n%columns*(cardWidth+12),y+n/columns*102,cardWidth,92));n++;}y+=MathF.Ceiling(group.Count()/(float)columns)*102+6;}
  if(groups.Length==0)Paragraph(new Rect(8,8,view.width-36,60),"没有匹配的功能。可清空搜索，或用右上角“热键”查看本类快捷键。",null,13);
  if(category=="镜头与画面"&&otherPage=="战斗信息"&&Button(new Rect(4,y,240,32),"预览受击方向（不损血）")){IncomingDamage.Preview();Close();}if(category=="战斗与枪械")AutoTrigger.DrawStatus(new Rect(4,y,view.width-28,56));if(category=="人物与移动"&&otherPage=="生存防护")SurvivalAssist.DrawActions(new Rect(4,y,view.width-28,56));GUI.EndScrollView();
 }
 static CnConfig.ConfigurationManagerAttributes? Metadata(ConfigEntryBase entry)=>entry.Description.Tags.OfType<CnConfig.ConfigurationManagerAttributes>().FirstOrDefault();
 static string Category(ConfigEntryBase entry)=>Metadata(entry)?.Category??entry.Definition.Section;
 static string Name(ConfigEntryBase entry)=>Metadata(entry)?.DispName??entry.Definition.Key;
 internal static void DrawEntry(ConfigEntryBase entry,Rect rect){
  Surface(rect,Color.Lerp(Background,Text,.035f),9);rect=new Rect(rect.x+13,rect.y+9,rect.width-26,rect.height-18);
  Label(new Rect(rect.x,rect.y,rect.width,25),Name(entry),null,13);
  rect.y+=34;rect.height=30;
  var value=entry.BoxedValue;
  if(value is bool flag){if(Button(new Rect(rect.x,rect.y,108,28),flag?"●  已开启":"○  已关闭",flag))entry.BoxedValue=!flag;}
  else if(value is KeyCode key){
   if(Button(rect,key.ToString()+"  · 点击后按键",capture==entry)){capture=entry;}
   if(capture==entry&&Event.current.type==EventType.KeyDown){if(Event.current.keyCode!=KeyCode.Escape)entry.BoxedValue=Event.current.keyCode;capture=null;Event.current.Use();}
   if(capture==entry)for(int button=1;button<=6;button++)if(Input.GetMouseButtonDown(button)){entry.BoxedValue=(KeyCode)((int)KeyCode.Mouse0+button);capture=null;if(Event.current.type is not (EventType.Repaint or EventType.Layout))Event.current.Use();break;}
  }
  else if(value.GetType().IsEnum){var values=value is AimMode?(Array)AimModes.All:Enum.GetValues(value.GetType());int current=Array.IndexOf(values.Cast<object>().ToArray(),value);if(Button(rect,EnumName(value)+"   ›"))entry.BoxedValue=values.GetValue((current+1)%values.Length)!;}
  else if(value is int||value is float){
   double low=0,high=value is int?100:5;
   var range=entry.Description.AcceptableValues;
   if(range!=null){var type=range.GetType();var min=type.GetProperty("MinValue")?.GetValue(range);var max=type.GetProperty("MaxValue")?.GetValue(range);if(min!=null&&max!=null){low=Convert.ToDouble(min);high=Convert.ToDouble(max);}}
   var sliderBounds=entry.Description.Tags.OfType<ConfigLimits.SliderBounds>().FirstOrDefault();if(sliderBounds!=null){low=sliderBounds.Minimum;high=sliderBounds.Maximum;}
   double number=Convert.ToDouble(value);string raw=drafts.TryGetValue(entry,out var draft)?draft:number.ToString("0.##",CultureInfo.InvariantCulture);
   raw=Field(new Rect(rect.x,rect.y,100,30),raw);drafts[entry]=raw;
   if(Button(new Rect(rect.x+108,rect.y,55,30),"应用")){object? requested=null;if(value is int&&int.TryParse(raw,NumberStyles.Integer,CultureInfo.InvariantCulture,out int integer))requested=integer;else if(value is float&&float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out float real)&&float.IsFinite(real))requested=real;if(requested!=null&&(range==null||range.IsValid(requested))){entry.BoxedValue=requested;drafts.Remove(entry);}else FeatureToasts.Show("数值无效或超出该参数的实际约束，请检查输入。 ");}
   float chosen=Slider(new Rect(rect.x+180,rect.y+8,Math.Max(30,rect.width-180),20),(float)Math.Clamp(number,low,high),(float)low,(float)high);
   if(Math.Abs(chosen-Math.Clamp(number,low,high))>Math.Max(.0001,(high-low)/2000)){entry.BoxedValue=value is int?(object)(int)Math.Round(chosen):chosen;drafts.Remove(entry);}
  }else if(value is string str){var raw=Field(new Rect(rect.x,rect.y,rect.width-70,30),drafts.TryGetValue(entry,out var draft)?draft:str);drafts[entry]=raw;if(Button(new Rect(rect.xMax-62,rect.y,62,30),"应用"))entry.BoxedValue=raw;}
  else Label(rect,value.ToString()??"");
 }
 static ConfigEntryBase? capture;
 static string EnumName(object value)=>value switch{BossNameLanguage language=>language==BossNameLanguage.English?"英文":"俄文",AimMode mode=>CnConfig.AimName(mode),ExfilPanelMode mode=>mode==ExfilPanelMode.Off?"关闭":mode==ExfilPanelMode.Hold?"按住显示":"常开",ExfilMarkerMode mode=>mode==ExfilMarkerMode.Off?"关闭":mode==ExfilMarkerMode.Nearest?"最近":"全部",AimTargetPart part=>new[]{"头部","胸部","腹部","左臂","右臂","左腿","右腿"}[(int)part],_=>value.ToString()??""};
 static void Appearance(Rect rect){
  Label(new Rect(rect.x,rect.y,rect.width,30),"选择主题",Text,18);
  var names=new[]{"深海蓝","石墨绿","暖琥珀","浅色"};
  var colors=new[]{new[]{"#111827","#E5EDF5","#38BDF8"},new[]{"#171C19","#E8EEE9","#86CFA5"},new[]{"#221B17","#F4E9DB","#F0B866"},new[]{"#E5EAF1","#192536","#1672B8"}};
  for(int i=0;i<4;i++)if(Button(new Rect(rect.x+i*(rect.width/4),rect.y+40,rect.width/4-8,36),names[i])){background.Value=colors[i][0];text.Value=colors[i][1];accent.Value=colors[i][2];hex="";}
  Label(new Rect(rect.x,rect.y+98,rect.width,24),"自定义颜色");
  var roles=new[]{"背景","文字","强调色"};var entries=new[]{background,text,accent};
  for(int i=0;i<3;i++)if(Button(new Rect(rect.x+i*100,rect.y+130,92,30),roles[i],colorRole==i)){colorRole=i;hex="";}
  if(hex.Length==0)hex=entries[colorRole].Value;
  hex=Field(new Rect(rect.x,rect.y+174,150,30),hex);
  if(Button(new Rect(rect.x+160,rect.y+174,72,30),"应用")&&ColorUtility.TryParseHtmlString(hex,out _))entries[colorRole].Value=hex;
  var swatches=new[]{"#38BDF8","#86CFA5","#F0B866","#C4A6FF","#F58AA7","#FFFFFF","#111827","#E5EAF1"};
  for(int i=0;i<swatches.Length;i++){var r=new Rect(rect.x+i*42,rect.y+220,34,30);Fill(r,Parse(swatches[i],Color.white));if(GUI.Button(r,"",GUIStyle.none)){entries[colorRole].Value=swatches[i];hex=swatches[i];}}
  Label(new Rect(rect.x,rect.y+272,rect.width,24),$"面板不透明度  {opacity.Value:P0}");
  float next=Slider(new Rect(rect.x,rect.y+306,rect.width,22),opacity.Value,.15f,1f);if(Math.Abs(next-opacity.Value)>.005f)opacity.Value=next;
  var preview=new Rect(rect.x,rect.y+350,rect.width,80);Panel(preview);Label(new Rect(preview.x+15,preview.y+8,preview.width-30,24),"实时预览  /  弹夹信息",Accent,16);Label(new Rect(preview.x+15,preview.y+39,preview.width-30,25),"5.56×45 mm  ·  30 / 30  ·  穿透 40");
  if(Button(new Rect(rect.x,rect.y+450,170,36),"拖动布局")){editing=true;drag="";}
  if(Button(new Rect(rect.x+180,rect.y+450,170,36),"恢复默认位置"))ResetPositions();
  Label(new Rect(rect.x,rect.y+500,rect.width,22),"设置自动保存 · 右 Shift / Esc 关闭",Text,12);
  DrawEntry(menuKey,new Rect(rect.x,rect.y+540,rect.width,92));
  FloatingStatus.DrawSettings(new Rect(rect.x,rect.y+658,rect.width,160));
 }
 static void DrawLayout(){
  var e=Event.current;
  var toolbar=new Rect(Math.Max(12,Screen.width-392),12,380,112);Panel(toolbar);Label(new Rect(toolbar.x+14,toolbar.y+5,350,26),"布局编辑 · 拖动信息框",Accent,17);
  if(Button(new Rect(toolbar.x+14,toolbar.y+42,160,30),"完成并返回")){editing=false;drag="";config.Save();return;}
  if(Button(new Rect(toolbar.x+186,toolbar.y+42,178,30),"恢复默认位置"))ResetPositions();Label(new Rect(toolbar.x+14,toolbar.y+78,350,24),"各信息框：拖动右下角调整大小",Text,12);
  foreach(var pair in slots){var slot=pair.Value;var initial=pair.Key=="raidclock"?RaidClock.DefaultBounds():slot.Initial;var bounds=Place(pair.Key,initial);Panel(bounds,.7f);Label(new Rect(bounds.x+8,bounds.y,Math.Max(40,bounds.width-16),26),slot.Name+" · 拖动",Accent);
   {
    var grip=new Rect(bounds.xMax-21,bounds.yMax-21,21,21);for(int i=0;i<3;i++)RaidLoot.Line(new Vector2(grip.x+7+i*4,grip.yMax-3),new Vector2(grip.xMax-3,grip.y+7+i*4),Accent,1.5f);
    Label(new Rect(bounds.x+8,bounds.y+25,Math.Max(30,bounds.width-34),22),$"{bounds.width:F0} × {bounds.height:F0}",Text,11);
    if(e.type==EventType.MouseDown&&e.button==0&&grip.Contains(e.mousePosition)&&!toolbar.Contains(e.mousePosition)){drag="resize:"+pair.Key;resizeOrigin=bounds.position;dragOffset=bounds.size-(e.mousePosition-bounds.position);e.Use();}
    if(e.type==EventType.MouseDrag&&drag=="resize:"+pair.Key){var size=e.mousePosition-resizeOrigin+dragOffset;slot.Width.Value=Math.Clamp(size.x,60,Math.Max(60,Screen.width));slot.Height.Value=Math.Clamp(size.y,24,Math.Max(24,Screen.height));MoveSlot(pair.Key,resizeOrigin,new Rect(resizeOrigin,new Vector2(slot.Width.Value,slot.Height.Value)));e.Use();}
   }
   if(e.type==EventType.MouseDown&&e.button==0&&bounds.Contains(e.mousePosition)&&!toolbar.Contains(e.mousePosition)){drag=pair.Key;dragOffset=e.mousePosition-bounds.position;e.Use();}
   if(e.type==EventType.MouseDrag&&drag==pair.Key){MoveSlot(pair.Key,e.mousePosition-dragOffset,bounds);e.Use();}
  }
  if(e.type==EventType.MouseUp){drag="";config.Save();}
 }
 static void ResetPositions(){foreach(var slot in slots.Values){slot.X.Value=-1;slot.Y.Value=-1;slot.Width.Value=-1;slot.Height.Value=-1;}}
}
