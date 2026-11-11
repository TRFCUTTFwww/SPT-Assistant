using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class RaidCommands {
 internal sealed class CommandItemPreset {public string Name{get;set;}="";public string Template{get;set;}="";public JsonElement Options{get;set;}=JsonSerializer.SerializeToElement(new{});}
 static string CommandPresetPath=>Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","command-item-presets.json");
 internal static CommandItemPreset[] CommandPresets()=>PresetStorage.Load<CommandItemPreset>(CommandPresetPath).ToArray();
 static CommandItemPreset FindCommandPreset(string name){var matches=CommandPresets().Where(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();return matches.Length==1?matches[0]:throw new ArgumentException("命令物品预设不存在或重名："+name);}
 static void ExpandCommandPreset(string command,string[] args,ref JsonElement? options){if(command!="give"||args.Length<2||!args[1].StartsWith("preset:",StringComparison.OrdinalIgnoreCase))return;var p=FindCommandPreset(args[1][7..]);args[1]=p.Template;var merged=JsonNode.Parse(p.Options.GetRawText())!.AsObject();if(options is {} o)Merge(merged,JsonNode.Parse(o.GetRawText())!.AsObject());options=JsonSerializer.SerializeToElement(merged);}
 static void Merge(JsonObject target,JsonObject source){foreach(var p in source){if(p.Value is JsonObject child&&target[p.Key] is JsonObject existing)Merge(existing,child);else target[p.Key]=p.Value?.DeepClone();}}
 static bool commandEditor(string command,string[] args){
  if(command!="give"||args.Length<3||!args.Contains("edit"))return false;
  if(args.Count(a=>a=="edit")!=1)throw new ArgumentException("edit 只能出现一次。");args=args.Where(a=>a!="edit").ToArray();var options=ChatNamedArguments.Take(command,ref args);ChatCommandSyntax.ValidateArguments(command,args);ExpandCommandPreset(command,args,ref options);ChatCommandOptions.Validate(command,args,options);var w=World();
  string selector=string.Join("\n",Resolve(w,args[0]).Select(p=>"uuid:"+Id(p)));int count=args.Length>2?Integer(args[2],1,100000):1;ChatItemEditor.Open(w.Pointer,selector,Template(args[1]),count,options);return true;
 }
 internal static string[] EditorWeaponFields(Item item)=>item.TryCast<Weapon>() is {} gun?WeaponEditor.CommandFields(gun.Template):Array.Empty<string>();
 internal static void EditorGive(IntPtr expected,string targets,string template,int count,JsonObject options){var w=World();if(w.Pointer!=expected)throw new ArgumentException("战局已变化，请重新打开物品编辑。");foreach(var target in targets.Split('\n'))Execute("give",new[]{target,template,count.ToString(CultureInfo.InvariantCulture),options.ToJsonString()});}
 internal static void SaveCommandPreset(string name,string template,JsonObject options){name=name.Trim();if(name.Length is <1 or >80)throw new ArgumentException("预设名须为 1～80 字符。");var presets=CommandPresets().Where(p=>!p.Name.Equals(name,StringComparison.OrdinalIgnoreCase)).ToList();presets.Add(new(){Name=name,Template=template,Options=JsonSerializer.SerializeToElement(options)});PresetStorage.Save(CommandPresetPath,presets);}
}
internal static class ChatItemEditor {
 sealed class Node {
  internal readonly string Template;internal string Count="1";internal readonly JsonObject Options;internal JsonObject Placement=new();
  internal readonly List<(string Slot,Node Child)> Attachments=new();internal readonly List<Node> Contents=new(),Ammo=new();
  internal readonly bool HasAttachments,HasContents,HasAmmo;
  internal ItemComponentEditor Components;internal Item Sample;
  internal Node(string template,JsonObject? options=null,int count=1,int depth=0){if(depth>8)throw new ArgumentException("物品配置最多八层。");Template=template;Count=count.ToString();Options=options?.DeepClone().AsObject()??new();var factory=Singleton<ItemFactory>.Instance??throw new ArgumentException("请进入战局。");Sample=factory.CreateItem(factory.NextId.ToString(),template,null)??throw new ArgumentException("模板不可用。");
   if(Options["attachments"] is JsonObject existing&&existing.Count>256)throw new ArgumentException("附件过多。");var parameters=JsonSerializer.Deserialize<ItemComponentParameters>(Options.ToJsonString(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new();Components=new(Sample,parameters);
   HasAttachments=Options.ContainsKey("attachments");HasContents=Options.ContainsKey("contents");HasAmmo=Options.ContainsKey("cartridges");
   if(Options["attachments"] is JsonObject a)foreach(var p in a){if(p.Value==null)continue;Attachments.Add((p.Key,From(p.Value,depth+1)));}
   if(Options["contents"] is JsonArray c)foreach(var p in c)if(p!=null)Contents.Add(From(p,depth+1));
   if(Options["cartridges"] is JsonArray rounds)foreach(var p in rounds)if(p!=null)Ammo.Add(From(p,depth+1));
  }
  static Node From(JsonNode value,int depth){string id=ItemSpawner.CommandItems.Resolve(value["item"]!.GetValue<string>());var node=new Node(id,value["options"]?.AsObject(),value["count"]?.GetValue<int>()??1,depth);foreach(string key in new[]{"grid","x","y","rotation"})if(value[key]!=null)node.Placement[key]=value[key]!.DeepClone();return node;}
  internal JsonObject Spec(){var value=Placement.DeepClone().AsObject();value["item"]=Template;value["count"]=CountValue();value["options"]=Export();return value;}
  internal int CountValue(){double n=ChatCommandSyntax.Number(Count,1,100000);if(n!=Math.Truncate(n))throw new ArgumentException("数量须为整数。");return(int)n;}
  internal JsonObject Export(){var result=Options.DeepClone().AsObject();foreach(var key in new[]{"durability","maxDurability","medicalResource","foodResource","keyRemaining","poisonUses","resource","repairKitResource","toggledOn","foundInRaid"})result.Remove(key);foreach(var p in Components.Read().Attributes())result[p.Key]=JsonSerializer.SerializeToNode(p.Value);
   if(HasAttachments||Attachments.Count>0||Options.ContainsKey("attachments")){var a=Options["attachments"]?.DeepClone().AsObject()??new();foreach(var p in Attachments)a[p.Slot]=p.Child.Spec();result["attachments"]=a;}
   if(HasContents||Contents.Count>0){var a=new JsonArray();foreach(var child in Contents)a.Add(child.Spec());result["contents"]=a;}
   if(HasAmmo||Ammo.Count>0){var a=new JsonArray();foreach(var child in Ammo)a.Add(child.Spec());result["cartridges"]=a;}
   return result;}
 }
 static Node? root;static readonly List<Node> path=new();static IntPtr world;static string targets="",page="参数",notice="",preset="",search="",picker="",slot="";static Vector2 scroll;static bool ignore;static readonly Dictionary<string,string> weaponDrafts=new();static readonly HashSet<string> weaponEnabled=new();
 internal static bool Active=>root!=null;
 internal static void Reset(){root=null;path.Clear();world=IntPtr.Zero;targets="";picker="";weaponDrafts.Clear();weaponEnabled.Clear();}
 internal static void Open(IntPtr w,string target,string template,int count,JsonElement? options){var next=new Node(template,options is {} o?JsonNode.Parse(o.GetRawText())!.AsObject():null,count);Reset();world=w;targets=target;root=next;path.Add(next);page="参数";notice="配置完成后点生成；取消不会发放物品。";preset=search="";scroll=Vector2.zero;}
 static void Action(Action action){try{action();}catch(Exception e){notice=e.Message;}}
 static void Push(Node node){path.Add(node);page="参数";picker="";scroll=Vector2.zero;weaponDrafts.Clear();weaponEnabled.Clear();}
 static void Pick(string kind,string id=""){picker=kind;slot=id;search="";scroll=Vector2.zero;}
 internal static void Draw(Rect rect){if(root==null)return;InterfaceMenu.Panel(rect);float x=rect.x+18,y=rect.y+16,w=rect.width-36;var node=path[^1];
  InterfaceMenu.Label(new Rect(x,y,w-112,28),"物品编辑 · "+ItemSpawner.TemplateName(node.Template),InterfaceMenu.Accent,18);if(InterfaceMenu.Button(new Rect(x+w-100,y,100,30),"取消 / 返回")){Reset();return;}y+=42;
  if(path.Count>1){if(InterfaceMenu.Button(new Rect(x,y,130,30),"返回上一级")){path.RemoveAt(path.Count-1);page="参数";picker="";weaponDrafts.Clear();weaponEnabled.Clear();scroll=Vector2.zero;return;}InterfaceMenu.Label(new Rect(x+144,y,w-144,30),"层级 "+path.Count+" · 子物品修改保留",null,12);y+=40;}
  string[] tabs={"参数","附件","内容物","装弹","枪械"};float cell=(w-24)/5;for(int i=0;i<tabs.Length;i++)if(InterfaceMenu.Button(new Rect(x+i*(cell+6),y,cell,31),tabs[i],page==tabs[i])){page=tabs[i];picker="";scroll=Vector2.zero;weaponDrafts.Clear();}y+=43;
  float footer=path.Count==1?154:70;var body=new Rect(x,y,w,Math.Max(45,rect.yMax-y-footer));GUI.BeginGroup(body);try{if(picker.Length>0)DrawPicker(new Rect(0,0,w,body.height),node);else switch(page){case "参数":scroll=GUI.BeginScrollView(new Rect(0,0,w,body.height),scroll,new Rect(0,0,w-22,node.Components.Height+50));InterfaceMenu.Label(new Rect(0,0,70,30),"数量",null,13);node.Count=InterfaceMenu.Field(new Rect(80,0,130,30),node.Count);node.Components.Draw(w-28,44);GUI.EndScrollView();break;case "附件":DrawAttachments(new Rect(0,0,w,body.height),node);break;case "内容物":DrawChildren(new Rect(0,0,w,body.height),node,false);break;case "装弹":DrawChildren(new Rect(0,0,w,body.height),node,true);break;case "枪械":DrawWeapon(new Rect(0,0,w,body.height),node);break;}}catch(Exception e){notice=e.Message;}finally{GUI.EndGroup();}
  if(path.Count==1){float bottom=rect.yMax-144;InterfaceMenu.Label(new Rect(x,bottom,60,30),"预设名",null,12);preset=InterfaceMenu.Field(new Rect(x+65,bottom,Math.Max(40,w-205),30),preset);if(InterfaceMenu.Button(new Rect(x+w-130,bottom,130,30),"保存预设"))Action(()=>{RaidCommands.SaveCommandPreset(preset,root.Template,root.Export());notice="已保存；可用 /give @s \"preset:"+preset+"\" 1";});bottom+=41;
   if(InterfaceMenu.Button(new Rect(x,bottom,w,36),"生成到命令指定目标"))Action(()=>{RaidCommands.EditorGive(world,targets,root.Template,root.CountValue(),root.Export());notice="已提交生成；可继续修改，或取消返回聊天。";});}
  InterfaceMenu.Paragraph(new Rect(x,rect.yMax-51,w,42),notice,InterfaceMenu.Accent,12);
 }
 static void DrawAttachments(Rect r,Node n){var definitions=ItemSpawner.SlotsFor(n.Template);scroll=GUI.BeginScrollView(r,scroll,new Rect(0,0,r.width-22,Math.Max(r.height,definitions.Count*83)));try{for(int i=0;i<definitions.Count;i++){var d=definitions[i];float y=i*83;var child=n.Attachments.FirstOrDefault(a=>a.Slot==d.Id).Child;InterfaceMenu.Label(new Rect(0,y,r.width-250,28),d.Name+(d.Required?" · 必需":""),InterfaceMenu.Accent,13);InterfaceMenu.Label(new Rect(0,y+30,r.width-250,30),child!=null?ItemSpawner.TemplateName(child.Template):n.Options["attachments"] is JsonObject a&&a.ContainsKey(d.Id)&&a[d.Id]==null?"已移除":"保留原生默认",null,12);
   if(InterfaceMenu.Button(new Rect(r.width-240,y,70,30),"选择")&&!d.Locked)Pick("attachment",d.Id);
   if(InterfaceMenu.Button(new Rect(r.width-162,y,70,30),"编辑"))Action(()=>{if(child==null){string? id=d.DefaultTemplate??n.Sample.TryCast<CompoundItem>()?.Slots?.FirstOrDefault(s=>s.ID==d.Id)?.ContainedItem?.StringTemplateId;if(id==null)throw new ArgumentException("请先选择附件。");child=new(id);n.Attachments.Add((d.Id,child));}Push(child);});
   if(InterfaceMenu.Button(new Rect(r.width-84,y,60,30),"移除")&&!d.Locked&&!d.Required){n.Attachments.RemoveAll(a=>a.Slot==d.Id);if(n.Options["attachments"] is not JsonObject)n.Options["attachments"]=new JsonObject();n.Options["attachments"]![d.Id]=null;}}
   if(definitions.Count==0)InterfaceMenu.Label(new Rect(0,0,r.width-24,40),"该物品没有附件槽。",null,13);
  }finally{GUI.EndScrollView();}}
 static void DrawChildren(Rect r,Node n,bool ammo){var list=ammo?n.Ammo:n.Contents;bool valid=ammo?n.Sample.TryCast<Magazine>()!=null:n.Sample.TryCast<CompoundItem>()?.Grids?.Length>0;if(!valid){InterfaceMenu.Label(r,ammo?"该物品不是弹匣；请在附件里进入弹匣后装弹。":"该物品没有储物格。",null,13);return;}
  if(InterfaceMenu.Button(new Rect(0,0,r.width-5,32),ammo?"添加一组弹药":"添加内容物"))Pick(ammo?"ammo":"contents");var body=new Rect(0,43,r.width,Math.Max(1,r.height-43));scroll=GUI.BeginScrollView(body,scroll,new Rect(0,0,r.width-22,Math.Max(body.height,list.Count*72)));try{Node? remove=null;for(int i=0;i<list.Count;i++){var c=list[i];float y=i*72;InterfaceMenu.Label(new Rect(0,y,r.width-205,30),(ammo?(i+1)+". ":"")+ItemSpawner.TemplateName(c.Template),null,13);c.Count=InterfaceMenu.Field(new Rect(0,y+32,95,29),c.Count);if(InterfaceMenu.Button(new Rect(r.width-190,y,76,30),"编辑"))Push(c);if(InterfaceMenu.Button(new Rect(r.width-106,y,76,30),"删除"))remove=c;}if(remove!=null)list.Remove(remove);}finally{GUI.EndScrollView();}}
 static void DrawPicker(Rect r,Node n){if(InterfaceMenu.Button(new Rect(0,0,90,30),"返回")){picker="";return;}search=InterfaceMenu.Field(new Rect(100,0,r.width-106,30),search);var d=picker=="attachment"?ItemSpawner.SlotsFor(n.Template).First(s=>s.Id==slot):null;var matches=ItemSpawner.CommandItems.Search(search,picker=="ammo").Where(p=>d==null||ItemSpawner.Compatible(p.Id,d.Allowed,d.Excluded)).Take(120).ToArray();var body=new Rect(0,42,r.width,Math.Max(1,r.height-42));scroll=GUI.BeginScrollView(body,scroll,new Rect(0,0,r.width-22,matches.Length*41));try{for(int i=0;i<matches.Length;i++){var p=matches[i];if(InterfaceMenu.Button(new Rect(0,i*41,r.width-26,35),p.Name))Action(()=>{if(path.Count>=8)throw new ArgumentException("最多八层，请返回上级。");var child=new Node(p.Id);if(picker=="attachment"){n.Attachments.RemoveAll(a=>a.Slot==slot);n.Attachments.Add((slot,child));}else if(picker=="ammo")n.Ammo.Add(child);else n.Contents.Add(child);picker="";scroll=Vector2.zero;});}}finally{GUI.EndScrollView();}}
 static void DrawWeapon(Rect r,Node n){var fields=RaidCommands.EditorWeaponFields(n.Sample);if(fields.Length==0){InterfaceMenu.Label(r,"该物品不是枪械。",null,13);return;}if(weaponDrafts.Count==0){foreach(string f in fields)weaponDrafts[f]=n.Options["weapon"]?["values"]?[f]?.ToJsonString()??"0";weaponEnabled.Clear();if(n.Options["weapon"]?["values"] is JsonObject values)foreach(var p in values)weaponEnabled.Add(p.Key);ignore=n.Options["weapon"]?["ignoreAttachments"]?.GetValue<bool>()??false;}
  scroll=GUI.BeginScrollView(r,scroll,new Rect(0,0,r.width-22,fields.Length*39+90));try{if(InterfaceMenu.Button(new Rect(0,0,r.width-26,30),ignore?"忽略配件数值：是":"忽略配件数值：否"))ignore=!ignore;for(int i=0;i<fields.Length;i++){string f=fields[i];float y=40+i*39;if(InterfaceMenu.Button(new Rect(0,y,r.width*.62f,31),(weaponEnabled.Contains(f)?"✓ ":"＋ ")+f,weaponEnabled.Contains(f))){if(!weaponEnabled.Remove(f))weaponEnabled.Add(f);}weaponDrafts[f]=InterfaceMenu.Field(new Rect(r.width*.64f,y,r.width*.36f-26,31),weaponDrafts[f]);}if(InterfaceMenu.Button(new Rect(0,fields.Length*39+45,r.width-26,32),"保存枪械数值到当前配置"))Action(()=>{var values=new JsonObject();foreach(string f in weaponEnabled)values[f]=ChatCommandSyntax.Number(weaponDrafts[f],-float.MaxValue,float.MaxValue);n.Options["weapon"]=new JsonObject{["values"]=values,["ignoreAttachments"]=ignore};notice="已保存到当前配置；仅生成后的本局实例生效。";});}finally{GUI.EndScrollView();}}
}
