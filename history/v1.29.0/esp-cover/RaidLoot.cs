using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace LocalSpt5Esp;
internal static class RaidLoot {
 internal sealed record Entry(string Key,Item Item,Component Anchor,Vector3 Position,string Name,string Source,LootCategory Category,double Value,double PerCell,int Count,string Template,bool Fir);
 static ConfigEntry<bool> enabled=null!,applyEsp=null!,favoritesOnly=null!,needsOnly=null!,tintSelected=null!;
 static ConfigEntry<int> minimum=null!,minimumCell=null!;
 static ConfigEntry<string> query=null!,favorites=null!;
 static ConfigEntry<LootCategory> category=null!;
 static readonly List<Entry> entries=new(),pending=new();
 static readonly HashSet<string> trackedTemplates=new(StringComparer.Ordinal);
 static HashSet<string> favoriteSet=new(StringComparer.Ordinal);
 static string favoritesRaw="",selected="",status="",minDraft="0",cellDraft="0";
 static bool trackFilter;
 internal static bool TrackNeeds;
 static float nextScan,nextContainerScan,nextError;
 static IntPtr worldPointer;
 static IEnumerator<Entry>? scanner;
 static LootableContainer[] containers=Array.Empty<LootableContainer>();
 static Vector2 scroll;
 static Material? material;
 static Component? tintedOwner;
 static readonly List<(Renderer Renderer,Il2CppReferenceArray<Material> Original)> tinted=new();
 internal static void Initialize(){
  enabled=FeatureConfig.Bind("27. 搜刮筛选","Scan raid loot","本局物品列表",true);
  applyEsp=FeatureConfig.Bind("27. 搜刮筛选","Filter loose ESP","将筛选条件应用到地面物品透视",false);
  query=FeatureConfig.Bind("27. 搜刮筛选","Search","物品名称或模板 ID 筛选","");category=FeatureConfig.Bind("27. 搜刮筛选","Category","物品类别",LootCategory.全部);
  minimum=FeatureConfig.Bind("27. 搜刮筛选","Minimum value","最低总估值（卢布）",0,"",new AcceptableValueRange<int>(0,1000000000));minimumCell=FeatureConfig.Bind("27. 搜刮筛选","Minimum cell value","最低每格估值（卢布）",0,"",new AcceptableValueRange<int>(0,1000000000));
  favoritesOnly=FeatureConfig.Bind("27. 搜刮筛选","Favorites only","只显示收藏物品",false);needsOnly=FeatureConfig.Bind("27. 搜刮筛选","Needs only","只显示任务与藏身处需求",false);tintSelected=FeatureConfig.Bind("27. 搜刮筛选","Highlight selected model","选中物品或所在容器模型高亮",true);
  favorites=FeatureConfig.Bind("27. 搜刮筛选","Favorites","收藏物品模板 ID（分号分隔）","");minDraft=minimum.Value.ToString();cellDraft=minimumCell.Value.ToString();
 }
 static void ReadFavorites(){if(favoritesRaw==favorites.Value)return;favoritesRaw=favorites.Value;favoriteSet=favoritesRaw.Split(new[]{';',',',' ','\n','\r'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);}
 static bool Matches(string template,string name,LootCategory kind,double value,double perCell){ReadFavorites();return RaidRules.Match(name,template,query.Value)&&(category.Value==LootCategory.全部||category.Value==kind)&&value>=minimum.Value&&perCell>=minimumCell.Value&&(!favoritesOnly.Value||favoriteSet.Contains(template))&&(!needsOnly.Value||TaskNeeds.Needed(template));}
 internal static bool AllowEsp(Item item){if(!applyEsp.Value)return true;double value=LootDisplay.Value(item);return Matches(item.StringTemplateId,LootDisplay.ItemName(item),ItemCatalog.Category(item.StringTemplateId),value,value/Math.Max(1,item.Width*item.Height));}
 static bool Matches(Entry e)=>Matches(e.Template,e.Name,e.Category,e.Value,e.PerCell);
 internal static IReadOnlyList<Entry> TeleportEntries=>entries;
 internal static bool TryTeleportTarget(string key,out Vector3 position,out string name){position=default;name="";if(!TryNavigationTarget(key,out _,out _))return false;var entry=entries.FirstOrDefault(e=>e.Key==key);if(entry==null||entry.Item==null)return false;Item? root=entry.Anchor.TryCast<LootableContainer>()?.ItemOwner?.RootItem??entry.Anchor.TryCast<Corpse>()?.ItemOwner?.RootItem??entry.Anchor.TryCast<LootItem>()?.Item;bool present=root?.Pointer==entry.Item.Pointer;if(!present&&root?.TryCast<ContainerCollection>() is ContainerCollection collection)present=collection.TryFindItem(entry.Item.Id,out var found)&&found?.Pointer==entry.Item.Pointer;return present&&TryNavigationTarget(key,out position,out name);}
 internal static Entry? NavigationSelection=>entries.FirstOrDefault(e=>e.Key==selected);
 internal static void SelectNavigationTarget(string key){selected=key;trackFilter=TrackNeeds=false;trackedTemplates.Clear();RestoreTint();}
 internal static bool TryNavigationTarget(string key,out Vector3 position,out string name){position=default;name="";var entry=entries.FirstOrDefault(e=>e.Key==key);if(entry==null||entry.Anchor==null)return false;try{position=entry.Anchor.TryCast<LootableContainer>() is LootableContainer c?ContainerAnchor.Position(c):entry.Anchor.TryCast<Corpse>() is Corpse body?CorpseDisplay.Position(body):entry.Anchor.transform.position;name=entry.Name;return entry.Anchor.gameObject.activeInHierarchy;}catch{return false;}}
 internal static void TrackTemplates(IEnumerable<string> targets){trackedTemplates.Clear();foreach(string id in targets)trackedTemplates.Add(id);trackFilter=TrackNeeds=false;selected="";RestoreTint();status="已高亮此需求的已加载物品。";}
 internal static void ClearTracking(){trackedTemplates.Clear();trackFilter=TrackNeeds=false;selected="";RestoreTint();}
 internal static void Reset(){scanner?.Dispose();scanner=null;entries.Clear();pending.Clear();containers=Array.Empty<LootableContainer>();worldPointer=IntPtr.Zero;nextScan=nextContainerScan=0;ClearTracking();}
 internal static void Shutdown(){Reset();if(material!=null)Object.Destroy(material);material=null;}
 internal static void Tick(GameWorld? world,Player? player){try{
  if(!RaidClock.Active||world==null||player==null||!enabled.Value){if(worldPointer!=IntPtr.Zero)Reset();return;}
  if(worldPointer!=world.Pointer){Reset();worldPointer=world.Pointer;}
  if(scanner==null&&Time.unscaledTime>=nextScan){pending.Clear();if(Time.unscaledTime>=nextContainerScan){containers=Object.FindObjectsOfType<LootableContainer>().ToArray();nextContainerScan=Time.unscaledTime+20;}scanner=Scan(world).GetEnumerator();}
  if(scanner!=null){int budget=120;while(budget-->0){if(scanner.MoveNext()){pending.Add(scanner.Current);continue;}entries.Clear();entries.AddRange(pending);pending.Clear();scanner.Dispose();scanner=null;nextScan=Time.unscaledTime+3;if(selected.Length>0&&!entries.Any(e=>e.Key==selected)){selected="";RestoreTint();status="定位物品已被拿走或移出场景，已取消高亮。";}break;}}
  var entry=entries.FirstOrDefault(e=>e.Key==selected);if(tintSelected.Value&&entry!=null)Tint(entry.Anchor);else RestoreTint();
 }catch(Exception e){scanner?.Dispose();scanner=null;pending.Clear();nextScan=Time.unscaledTime+5;Warn(e);}}
 static IEnumerable<Entry> Scan(GameWorld world){
  var seen=new HashSet<IntPtr>();var loot=world.LootItems;
  if(loot!=null)for(int i=0;i<loot.Count;i++){var root=loot.GetByIndex(i);if(root==null||root.Item==null)continue;var body=root.TryCast<Corpse>();if(body!=null){var item=body.ItemOwner?.RootItem;var secure=item?.TryCast<InventoryEquipment>()?.GetSlot(EquipmentSlot.SecuredContainer)?.ContainedItem;if(item!=null)foreach(var child in LootDisplay.Children(item)){if(secure!=null&&child.Pointer==secure.Pointer)continue;foreach(var entry in Walk(child,body,"尸体 · "+body.Side,seen,0))yield return entry;}}else foreach(var entry in Walk(root.Item,root,"地面",seen,0))yield return entry;}
  foreach(var container in containers){if(container==null||!container.IsInitialized||container.ItemOwner?.RootItem==null)continue;string source="容器 · "+(container.ItemOwner.ContainerName??container.TypeKey??"储物箱");foreach(var child in LootDisplay.Children(container.ItemOwner.RootItem))foreach(var entry in Walk(child,container,source,seen,0))yield return entry;}
 }
 static IEnumerable<Entry> Walk(Item item,Component anchor,string source,HashSet<IntPtr> seen,int depth){
  if(item==null||anchor==null||depth>12||!seen.Add(item.Pointer))yield break;Entry? row=null;
  try{var position=anchor.TryCast<LootableContainer>() is LootableContainer c?ContainerAnchor.Position(c):anchor.TryCast<Corpse>() is Corpse body?CorpseDisplay.Position(body):anchor.transform.position;double value=LootDisplay.Value(item);row=new(item.Id.ToString(),item,anchor,position,LootDisplay.ItemName(item,false),source,ItemCatalog.Category(item.StringTemplateId),value,value/Math.Max(1,item.Width*item.Height),Math.Max(1,item.StackObjectsCount),item.StringTemplateId,item.SpawnedInSession);}catch(Exception e){Warn(e);}
  if(row!=null)yield return row;foreach(var child in LootDisplay.Children(item))foreach(var entry in Walk(child,anchor,source,seen,depth+1))yield return entry;
 }
 internal static void Draw(Rect rect){
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width-170,28),$"本局物品 · {entries.Count} 个已加载条目",InterfaceMenu.Accent,18);if(InterfaceMenu.Button(new Rect(rect.xMax-155,rect.y,155,30),"立即重新扫描"))nextScan=nextContainerScan=0;
  query.Value=InterfaceMenu.Field(new Rect(rect.x,rect.y+40,Math.Max(120,rect.width-175),30),query.Value);if(InterfaceMenu.Button(new Rect(rect.xMax-163,rect.y+40,163,30),"类别："+category.Value))category.Value=(LootCategory)(((int)category.Value+1)%Enum.GetValues<LootCategory>().Length);
  float col=(rect.width-24)/3;
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y+82,col,30),favoritesOnly.Value?"只看收藏 · 开":"只看收藏",favoritesOnly.Value))favoritesOnly.Value=!favoritesOnly.Value;
  if(InterfaceMenu.Button(new Rect(rect.x+col+12,rect.y+82,col,30),needsOnly.Value?"只看需求 · 开":"只看需求",needsOnly.Value))needsOnly.Value=!needsOnly.Value;
  if(InterfaceMenu.Button(new Rect(rect.x+(col+12)*2,rect.y+82,col,30),trackFilter?"正在定位筛选结果":"定位筛选结果",trackFilter)){trackFilter=!trackFilter;TrackNeeds=false;trackedTemplates.Clear();selected="";}
  InterfaceMenu.Label(new Rect(rect.x,rect.y+124,78,28),"最低总价",null,12);minDraft=InterfaceMenu.Field(new Rect(rect.x+82,rect.y+124,90,28),minDraft);InterfaceMenu.Label(new Rect(rect.x+186,rect.y+124,88,28),"最低每格价",null,12);cellDraft=InterfaceMenu.Field(new Rect(rect.x+278,rect.y+124,90,28),cellDraft);
  if(InterfaceMenu.Button(new Rect(rect.x+380,rect.y+124,70,28),"应用"))try{minimum.Value=ItemSpawnRules.Integer(minDraft,0,1000000000,"最低总价");minimumCell.Value=ItemSpawnRules.Integer(cellDraft,0,1000000000,"最低每格价");}catch(Exception e){status=e.Message;}
  if(InterfaceMenu.Button(new Rect(rect.xMax-120,rect.y+124,120,28),"清除定位"))ClearTracking();
  var origin=Comfort.Common.Singleton<GameWorld>.Instance?.MainPlayer?.Position??Vector3.zero;
  var rows=entries.Where(Matches).OrderBy(e=>Vector3.Distance(e.Position,origin)).ToList();var view=new Rect(rect.x,rect.y+168,rect.width,Math.Max(60,rect.height-245));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,Math.Max(40,rows.Count*88)));int first=Math.Max(0,(int)(scroll.y/88)),end=Math.Min(rows.Count,first+(int)(view.height/88)+2);ReadFavorites();
  for(int i=first;i<end;i++){var row=rows[i];float y=i*88;string id=row.Template;bool favorite=favoriteSet.Contains(id);InterfaceMenu.Paragraph(new Rect(0,y,view.width-300,83),row.Name+" ×"+row.Count+" · "+Vector3.Distance(row.Position,origin).ToString("F0")+"米\n"+row.Source+" · "+row.Category+" · "+LootDisplay.Money(row.Value)+" / 每格 "+LootDisplay.Money(row.PerCell)+TaskNeeds.Hint(id,row.Fir),TaskNeeds.Needed(id)?new Color(.5f,1f,.65f):InterfaceMenu.Text,12);
   if(InterfaceMenu.Button(new Rect(view.width-288,y+10,124,31),selected==row.Key?"取消定位":"高亮定位",selected==row.Key)){selected=selected==row.Key?"":row.Key;trackFilter=TrackNeeds=false;trackedTemplates.Clear();RestoreTint();}
   if(InterfaceMenu.Button(new Rect(view.width-154,y+10,126,31),"路面导航"))GroundNavigation.ToLoot(row.Key,row.Name);
   if(InterfaceMenu.Button(new Rect(view.width-288,y+49,124,31),"传送到物品"))TeleportControl.ToLoot(row.Key);
   if(InterfaceMenu.Button(new Rect(view.width-154,y+49,126,31),favorite?"已收藏":"收藏",favorite)){if(favorite)favoriteSet.Remove(id);else favoriteSet.Add(id);favorites.Value=string.Join(";",favoriteSet.OrderBy(x=>x));favoritesRaw=favorites.Value;}
  }
  if(rows.Count==0)InterfaceMenu.Paragraph(new Rect(0,0,view.width-24,65),!RaidClock.Active?"请进入战局后查看物品。":scanner!=null?"正在扫描已加载物品…":"没有匹配物品，可清空搜索或调整筛选。",null,13);GUI.EndScrollView();
  InterfaceMenu.Paragraph(new Rect(rect.x,rect.yMax-67,rect.width,64),(scanner!=null?"扫描中 · ":"匹配 "+rows.Count+" · ")+"物品被拿走后自动移除；容器内物品定位到所在箱体，尸体内物品定位到尸体。\n"+status,null,12);
 }
 internal static void DrawMarkers(Camera? camera,Player? player){if(camera==null||player==null||!enabled.Value||!RaidClock.Active)return;try{
  var visible=entries.Where(e=>e.Key==selected||(TrackNeeds&&TaskNeeds.Needed(e.Template))||(trackFilter&&Matches(e))||trackedTemplates.Any(t=>ItemCatalog.IsChildOf(e.Template,t))).OrderBy(e=>Vector3.Distance(e.Position,player.Position)).Take(24);
  int count=0;foreach(var entry in visible){if(entry.Anchor==null)continue;var position=entry.Anchor.TryCast<LootableContainer>() is LootableContainer c?ContainerAnchor.Position(c):entry.Anchor.TryCast<Corpse>() is Corpse body?CorpseDisplay.Position(body):entry.Anchor.transform.position;DrawMarker(camera,position,entry.Name,Vector3.Distance(position,player.Position),new Color(1f,.84f,.12f),count++==0);}
 }catch(Exception e){Warn(e);}}
 internal static void DrawMarker(Camera camera,Vector3 position,string name,float distance,Color color,bool line){
  var projected=camera.WorldToScreenPoint(position);var center=new Vector2(Screen.width/2f,Screen.height/2f);var point=new Vector2(projected.x,Screen.height-projected.y);bool off=projected.z<=0||point.x<25||point.x>Screen.width-25||point.y<35||point.y>Screen.height-70;
  if(off){var delta=point-center;if(projected.z<=0)delta=-delta;if(delta.sqrMagnitude<.001f)delta=Vector2.up;float scale=Math.Min((center.x-35)/Math.Max(.001f,Math.Abs(delta.x)),(center.y-75)/Math.Max(.001f,Math.Abs(delta.y)));point=center+delta*scale;var direction=delta.normalized;var side=new Vector2(-direction.y,direction.x);Line(point,point-direction*16+side*9,color,3);Line(point,point-direction*16-side*9,color,3);}
  else{float size=13+MathF.Sin(Time.unscaledTime*5)*2;Line(point+new Vector2(-size,-size),point+new Vector2(size,-size),color,2);Line(point+new Vector2(size,-size),point+new Vector2(size,size),color,2);Line(point+new Vector2(size,size),point+new Vector2(-size,size),color,2);Line(point+new Vector2(-size,size),point+new Vector2(-size,-size),color,2);if(line)Line(new Vector2(center.x,Screen.height-80),point,color,1);}
  float x=Math.Clamp(point.x-125,5,Math.Max(5,Screen.width-255)),y=Math.Clamp(point.y+18,5,Screen.height-55);var rect=new Rect(x,y,250,34);InterfaceMenu.Panel(rect,.7f);InterfaceMenu.Label(new Rect(x+6,y+3,238,28),name+" ["+distance.ToString("F0")+"米]",color,12);
 }
 internal static void Line(Vector2 a,Vector2 b,Color color,float width){var matrix=GUI.matrix;var old=GUI.color;try{GUI.color=color;var delta=b-a;GUIUtility.RotateAroundPivot(MathF.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);GUI.DrawTexture(new Rect(a.x,a.y-width/2,delta.magnitude,width),Texture2D.whiteTexture);}finally{GUI.matrix=matrix;GUI.color=old;}}
 static void Tint(Component owner){if(tintedOwner!=null&&tintedOwner.Pointer==owner.Pointer)return;RestoreTint();if(material==null){var shader=new[]{"Hidden/Internal-Colored","Unlit/Color","Sprites/Default"}.Select(Shader.Find).FirstOrDefault(s=>s!=null&&s.isSupported);if(shader==null)return;material=new Material(shader){hideFlags=HideFlags.HideAndDontSave,renderQueue=4000};material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetInt("_Cull",(int)CullMode.Off);material.SetInt("_ZWrite",0);material.SetInt("_ZTest",(int)CompareFunction.Always);material.color=new Color(1f,.8f,.05f,.8f);}
  tintedOwner=owner;foreach(var renderer in owner.GetComponentsInChildren<Renderer>(true)){if(renderer==null||renderer.TryCast<ParticleSystemRenderer>()!=null||renderer.TryCast<TrailRenderer>()!=null)continue;var original=renderer.sharedMaterials;if(original==null||original.Length==0)continue;tinted.Add((renderer,original));var replacement=new Il2CppReferenceArray<Material>(original.Length);for(int i=0;i<replacement.Length;i++)replacement[i]=material;renderer.sharedMaterials=replacement;}
 }
 static void RestoreTint(){foreach(var entry in tinted)try{if(entry.Renderer!=null)entry.Renderer.sharedMaterials=entry.Original;}catch{}tinted.Clear();tintedOwner=null;}
 static void Warn(Exception e){if(Time.unscaledTime<nextError)return;nextError=Time.unscaledTime+20;Plugin.Self.Error(e);}
}
