using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
using Object=UnityEngine.Object;
namespace LocalSpt5Esp;
internal static class PlayerWeaponPresets {
 sealed record Entry(string Id,string Name,string Weapon,int Parts);
 static readonly List<Entry> entries=new();
 static string search="",notice="",quantity="1",sessionKey="";
 static IntPtr sessionPointer;static float nextRefresh;static Vector2 scroll;
 static Task<string>? mail;static WeaponCondition condition;static string currentDurability="100",maximumDurability="100";
 static IEftSession? Session(){var app=Singleton<TarkovApplication>.Instance??Singleton<AbstractApplication>.Instance?.TryCast<TarkovApplication>()??Object.FindObjectOfType<TarkovApplication>();return app?.Session;}
 internal static void Initialize(){try{Refresh();var nativeBuilds=RequireSession().WeaponBuildsStorage?.WeaponBuilds;if(nativeBuilds!=null)foreach(var pair in nativeBuilds)if(pair.Value?.Item?.TryCast<Weapon>() is Weapon gun){WeaponEditor.CheckNativeClone(gun);try{AiEquipmentEditor.CheckNativeClone();}catch(Exception error){Plugin.Self.Error(error);}break;}Plugin.Self.Log.LogInfo("Player weapon preset read-only check: saved builds="+entries.Count+", total components="+entries.Sum(e=>e.Parts));}catch(Exception e){nextRefresh=0;Plugin.Self.Log.LogInfo("Player weapon presets await login: "+e.Message);}}
 static IEftSession RequireSession()=>Session()??throw new ArgumentException("请登录角色后读取枪械预设。");
 static void Refresh(){var session=RequireSession();var storage=session.WeaponBuildsStorage?.WeaponBuilds??throw new ArgumentException("枪械预设尚未载入，请稍后刷新。");
  entries.Clear();sessionPointer=session.Pointer;sessionKey=session.GetPhpSessionId();
  foreach(var pair in storage){var build=pair.Value;if(build==null||build.FromPreset||build.Item?.TryCast<Weapon>()==null)continue;
   string title=!string.IsNullOrWhiteSpace(build.HandbookName)?build.HandbookName:!string.IsNullOrWhiteSpace(build.ItemIconName)?build.ItemIconName:pair.Key.ToString();
   entries.Add(new(pair.Key.ToString(),title,LootDisplay.ItemName(build.Item),Count(LoadoutPlans.CaptureItem(build.Item))));
  }
  entries.Sort((a,b)=>string.Compare(a.Name,b.Name,StringComparison.OrdinalIgnoreCase));nextRefresh=Time.unscaledTime+3;
 }
 static int Count(ItemAttachmentNode node)=>1+node.Children.Values.Sum(Count)+node.Cartridges.Count;
 static ItemSpawner.SavedPreset Read(Entry entry,int count){var session=RequireSession();if(session.Pointer!=sessionPointer||session.GetPhpSessionId()!=sessionKey)throw new ArgumentException("登录角色已改变，请刷新预设列表。");
  EFT.UI.Builds.WeaponBuild? build=null;var builds=session.WeaponBuildsStorage?.WeaponBuilds;if(builds!=null)foreach(var pair in builds)if(pair.Key.ToString()==entry.Id){build=pair.Value;break;}
  if(build==null||build.FromPreset||build.Item?.TryCast<Weapon>()==null)throw new ArgumentException("该玩家预设已删除或不可用，请刷新。");
  var node=LoadoutPlans.CaptureItem(build.Item);
  if(condition!=WeaponCondition.Original)ClearDurability(node);if(condition==WeaponCondition.Custom){var value=WeaponConditionRules.Parse(currentDurability,maximumDurability);node.Parameters.Durability=value.Current;node.Parameters.MaxDurability=value.Maximum;}
  var p=node.Parameters;var settings=new ItemSpawner.Values(p.Durability.HasValue||p.MaxDurability.HasValue,p.Durability??p.MaxDurability??0,p.MaxDurability??p.Durability??0,false,0,false,0,false,0,false,0,"",0,p);
  return new(entry.Id,entry.Name,node.Template,count,settings,node);
 }
 static void ClearDurability(ItemAttachmentNode node){node.Parameters.Durability=node.Parameters.MaxDurability=null;foreach(var child in node.Children.Values)ClearDurability(child);}
 static void Spawn(Entry entry){int count=ItemSpawnRules.Integer(quantity,1,int.MaxValue,"枪械数量");var preset=Read(entry,count);
  if(ItemSpawner.InRaid()){notice=ItemSpawner.SpawnPreset(preset,count);return;}
  mail=GearMailClient.Send(ItemSpawner.PresetMail(preset,count),true);notice="正在将预设枪械发送到邮件…";
 }
 static void Edit(Entry entry){ItemSpawner.EditSavedPreset(Read(entry,1));notice="已载入预设，可调整属性与附件后添加。";}
 static void Action(Action action){try{action();}catch(Exception e){notice=e.Message;Plugin.Self.Log.LogWarning("Player weapon preset: "+e.Message);}}
 internal static void Reset(){entries.Clear();sessionPointer=IntPtr.Zero;sessionKey="";nextRefresh=0;mail=null;}
 internal static (string Id,string Name)[] SceneEntries(){if(entries.Count==0||Time.unscaledTime>=nextRefresh)Refresh();return entries.Select(e=>(e.Id,e.Name)).ToArray();}
 internal static ItemAttachmentNode SceneWeapon(string id){if(entries.Count==0||Time.unscaledTime>=nextRefresh)Refresh();return Read(entries.FirstOrDefault(e=>e.Id==id)??throw new ArgumentException("该玩家预设已不存在，请刷新。"),1).Attachments;}
 internal static void Draw(Rect rect){
  if(mail?.IsCompleted==true){try{notice=mail.GetAwaiter().GetResult();}catch(Exception e){notice=e.Message;}mail=null;}
  if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+3;Action(Refresh);}
  float x=rect.x,y=rect.y,w=rect.width;InterfaceMenu.Paragraph(new Rect(x,y,w,46),"读取当前登录角色在游戏改枪界面保存的预设。保留配件和弹药；默认恢复出厂耐久，可沿用预设或自定义。",InterfaceMenu.Text,12);y+=54;
  search=InterfaceMenu.Field(new Rect(x,y,w-270,30),search);InterfaceMenu.Label(new Rect(x+w-258,y,48,30),"数量",InterfaceMenu.Text,12);quantity=InterfaceMenu.Field(new Rect(x+w-210,y,62,30),quantity);
  if(InterfaceMenu.Button(new Rect(x+w-134,y,134,30),"刷新玩家预设"))Action(Refresh);y+=40;
  string[] modes={"全新耐久","沿用预设","自定义耐久"};float cell=(w-16)/3;for(int i=0;i<3;i++)if(InterfaceMenu.Button(new Rect(x+i*(cell+8),y,cell,30),modes[i],(int)condition==i))condition=(WeaponCondition)i;y+=38;
  if(condition==WeaponCondition.Custom){InterfaceMenu.Label(new Rect(x,y,80,28),"当前耐久",InterfaceMenu.Text,12);currentDurability=InterfaceMenu.Field(new Rect(x+82,y,100,28),currentDurability);InterfaceMenu.Label(new Rect(x+200,y,80,28),"最大耐久",InterfaceMenu.Text,12);maximumDurability=InterfaceMenu.Field(new Rect(x+282,y,100,28),maximumDurability);y+=38;}
  var filtered=entries.Where(e=>(e.Name+" "+e.Weapon).Contains(search,StringComparison.OrdinalIgnoreCase)).ToArray();var area=new Rect(x,y,w,Math.Max(50,rect.yMax-y-64));var layout=new MenuRowLayout();layout.Build(filtered.Select(e=>Math.Max(80,InterfaceMenu.ParagraphHeight(e.Name+"\n"+e.Weapon,w-300,14)+16)));scroll=GUI.BeginScrollView(area,scroll,new Rect(0,0,w-20,Math.Max(40,layout.Height)));
  Entry? action=null;bool edit=false;var visible=layout.Visible(scroll.y,area.height);
  for(int i=visible.First;i<visible.End;i++){var entry=filtered[i];float top=layout.Tops[i];InterfaceMenu.Surface(new Rect(0,top,w-24,layout.Heights[i]-6),Color.Lerp(InterfaceMenu.Background,InterfaceMenu.Text,.035f),8);InterfaceMenu.Paragraph(new Rect(12,top+8,w-310,layout.Heights[i]-14),entry.Name+"\n"+entry.Weapon+" · "+entry.Parts+" 个组件",InterfaceMenu.Text,13);
   var old=GUI.enabled;GUI.enabled=old&&mail==null;if(InterfaceMenu.Button(new Rect(w-286,top+20,124,32),ItemSpawner.InRaid()?"添加到背包":"生成到邮件"))action=entry;if(InterfaceMenu.Button(new Rect(w-152,top+20,116,32),"载入编辑")){action=entry;edit=true;}GUI.enabled=old;
  }
  if(filtered.Length==0)InterfaceMenu.Paragraph(new Rect(10,10,w-44,72),entries.Count==0?"当前角色没有玩家枪械预设。先在游戏改枪界面保存一套，再点击刷新。":"没有匹配的预设。",InterfaceMenu.Text,13);
  GUI.EndScrollView();if(action!=null)Action(()=>{if(edit)Edit(action);else Spawn(action);});
  InterfaceMenu.Paragraph(new Rect(x,rect.yMax-56,w,52),notice,InterfaceMenu.Accent,12);
 }
}
