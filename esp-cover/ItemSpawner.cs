using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BepInEx;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;

namespace LocalSpt5Esp;
internal static class ItemSpawner {
 sealed record Entry(string Id,string Name,string SearchText,bool IsAmmo,int StackMax,HashSet<string> Allowed,HashSet<string> Excluded,ItemDetails.Detail[] Details);
 static List<Entry> catalog=new(),matches=new();
 static ChatItemSearch commandItems=new(Array.Empty<ChatItemSearch.Item>());
 internal static ChatItemSearch CommandItems { get { Load();return commandItems; } }
 static readonly Dictionary<string,string> parents=new(StringComparer.Ordinal);
 static readonly Dictionary<string,List<AttachmentSlot>> attachmentSlots=new(StringComparer.Ordinal);
 static List<Entry> compatibleAmmo=new();
 static bool loaded;
 static string query="",previousQuery="",quantity="1",selected="",status="";
 static Vector2 scroll,propertyScroll;
 static string inspected="",ammoQuery="",ammoId="",durability="",maxDurability="",medResource="",foodResource="",keyUses="0",rounds="0";
 static bool changeDurability,changeMed,changeFood,changeKey,loadMagazine;
 static int fir;
 static Schema? schema;
 static Task<string>? mailTask;
 static ItemComponentEditor? extraEditor;
 static Item? inspectedSample;
 static bool attachmentPage;
 static bool tagsPage,tagsLoaded,tagsError,playerBuildsPage;
 static string tagName="";
 static List<SavedPreset> presets=new();
 internal sealed record SavedPreset(string Id,string Name,string Template,int Count,Values Settings,ItemAttachmentNode Attachments);
 static string PresetPath=>Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","item-presets.json");
 static readonly MenuRowLayout itemRows=new();
 static float rowsWidth=-1;
 internal static IEnumerable<(string Id,string Name)> FindAmmo(string search){Load();string normalized=ItemSpawnRules.NormalizeSearch(search);return catalog.Where(item=>item.IsAmmo&&item.SearchText.Contains(normalized,StringComparison.Ordinal)).Select(item=>(item.Id,item.Name));} internal static IEnumerable<(string Id,string Name)> FindTemplates(string search){Load();string normalized=ItemSpawnRules.NormalizeSearch(search);return catalog.Where(item=>item.SearchText.Contains(normalized,StringComparison.Ordinal)).Select(item=>(item.Id,item.Name));}
 internal static IEnumerable<(string Id,string Name)> FindEquipment(string search,HashSet<string> allowed,HashSet<string> excluded){Load();string normalized=ItemSpawnRules.NormalizeSearch(search);return catalog.Where(item=>item.SearchText.Contains(normalized,StringComparison.Ordinal)&&ItemSpawnRules.MatchesFilter(item.Id,id=>parents.TryGetValue(id,out var parent)?parent:null,allowed,excluded)).Select(item=>(item.Id,item.Name));}
 internal static string TemplateName(string id){Load();return catalog.FirstOrDefault(item=>item.Id==id)?.Name??id;}
 internal static ItemDetails.Detail[] TemplateDetails(string id){Load();return catalog.FirstOrDefault(item=>item.Id==id)?.Details??Array.Empty<ItemDetails.Detail>();}
 internal static IReadOnlyList<AttachmentSlot> SlotsFor(string template){Load();return attachmentSlots.TryGetValue(template,out var slots)?slots:Array.Empty<AttachmentSlot>();}
 internal static bool Compatible(string template,HashSet<string> allowed,HashSet<string> excluded){Load();return ItemSpawnRules.MatchesFilter(template,id=>parents.TryGetValue(id,out var parent)?parent:null,allowed,excluded);}
 sealed record Schema(bool Repairable,float Durability,float MaxDurability,bool Medical,float MedMaximum,bool Food,float FoodMaximum,bool Key,int KeyMaximum,int MagazineCapacity);
 internal sealed record Values(bool Repairable,float Durability,float MaxDurability,bool Medical,float MedicalAmount,bool Food,float FoodAmount,bool Key,int KeyRemaining,bool LoadMagazine,int Rounds,string AmmoId,int Fir,ItemComponentParameters Extra);
 static string Format(float value)=>value.ToString("0.##",CultureInfo.InvariantCulture);
 static void Load(){
  if(loaded)return;loaded=true;
  try{
   string namesPath=Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","esp5-names.json");
   var names=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(namesPath))??new();
   string templates=Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","SPT_Data","database","templates","items.json");
   using var items=JsonDocument.Parse(File.ReadAllText(templates));
   Dictionary<string,string> Locale(string language){string path=Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","SPT_Data","database","locales","global",language+".json");return File.Exists(path)?JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(path))??new():new();}
   commandItems=ChatItemSearch.Read(items.RootElement,names,Locale("en"),Locale("ch"));
   foreach(var item in items.RootElement.EnumerateObject()){
    if(item.Value.TryGetProperty("_parent",out var parent))parents[item.Name]=parent.GetString()??"";
    if(!item.Value.TryGetProperty("_type",out var kind)||kind.GetString()!="Item")continue;
    var name=names.TryGetValue(item.Name,out var full)?full:item.Value.TryGetProperty("_name",out var raw)?raw.GetString()??item.Name:item.Name;
    var allowed=new HashSet<string>(StringComparer.Ordinal);var excluded=new HashSet<string>(StringComparer.Ordinal);
    var props=item.Value.GetProperty("_props");
    var slots=new List<AttachmentSlot>();
    foreach(string slotType in new[]{"Slots","Chambers"})if(props.TryGetProperty(slotType,out var definitions)&&definitions.ValueKind==JsonValueKind.Array)foreach(var slot in definitions.EnumerateArray()){
     string id=slot.GetProperty("_name").GetString()!;var settings=slot.GetProperty("_props");var included=new HashSet<string>(StringComparer.Ordinal);var denied=new HashSet<string>(StringComparer.Ordinal);string? defaultTemplate=null;
     if(settings.TryGetProperty("filters",out var slotFilters))foreach(var filter in slotFilters.EnumerateArray()){
      if(filter.TryGetProperty("Filter",out var values))foreach(var value in values.EnumerateArray())included.Add(value.GetString()!);
      if(filter.TryGetProperty("ExcludedFilter",out var values2))foreach(var value in values2.EnumerateArray())denied.Add(value.GetString()!);
      if(filter.TryGetProperty("Plate",out var plate)&&plate.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(plate.GetString()))defaultTemplate=plate.GetString();
     }
     bool locked=settings.TryGetProperty("locked",out var lockValue)&&lockValue.ValueKind==JsonValueKind.True;bool required=slot.TryGetProperty("_required",out var requiredValue)&&requiredValue.ValueKind==JsonValueKind.True;
     string lower=id.ToLowerInvariant();string slotName=lower switch{"mod_nvg"=>"夜视仪 / 转接座","mod_mount"=>"安装座","front_plate"=>"前胸防弹板","back_plate"=>"后背防弹板","left_side_plate"=>"左侧防弹板","right_side_plate"=>"右侧防弹板",_=>lower.StartsWith("mod_equipment",StringComparison.Ordinal)?"头盔附加装备 · "+id:id.Replace('_',' ')};
     slots.Add(new(id,slotName,locked,required,defaultTemplate,included,denied));
    }
    if(slots.Count>0)attachmentSlots[item.Name]=slots;
    if(props.TryGetProperty("Cartridges",out var cartridges)&&cartridges.ValueKind==JsonValueKind.Array)
     foreach(var slot in cartridges.EnumerateArray())if(slot.TryGetProperty("_props",out var settings)&&settings.TryGetProperty("filters",out var filters))
      foreach(var filter in filters.EnumerateArray()){
       if(filter.TryGetProperty("Filter",out var list))foreach(var id in list.EnumerateArray())if(id.GetString() is string value)allowed.Add(value);
       if(filter.TryGetProperty("ExcludedFilter",out var blocked))foreach(var id in blocked.EnumerateArray())if(id.GetString() is string value)excluded.Add(value);
      }
    int stackMax=props.TryGetProperty("StackMaxSize",out var stack)?Math.Max(1,(int)stack.GetDouble()):1;
    catalog.Add(new(item.Name,name,ItemSpawnRules.NormalizeSearch(name)+"|"+item.Name,props.TryGetProperty("PenetrationPower",out _),stackMax,allowed,excluded,ItemDetails.Read(props)));
   }
   catalog=catalog.OrderBy(item=>item.Name,StringComparer.OrdinalIgnoreCase).ToList();Search();
  }catch(Exception error){status="物品目录读取失败："+error.Message;Plugin.Self.Error(error);}
 }
 static void Search(){previousQuery=query;string normalized=ItemSpawnRules.NormalizeSearch(query);matches=catalog.Where(item=>item.SearchText.Contains(normalized,StringComparison.Ordinal)).ToList();scroll=Vector2.zero;rowsWidth=-1;}
 internal static void Draw(Rect rect){
  Load();
  LoadPresets();
  if(mailTask?.IsCompleted==true){status=mailTask.GetAwaiter().GetResult();mailTask=null;}
  bool raid=InRaid();
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width-390,28),playerBuildsPage?"玩家枪械预设":tagsPage?"快捷物品标签":raid?"添加物品到当前背包":"添加物品到游戏邮件",InterfaceMenu.Accent,18);
  if(InterfaceMenu.Button(new Rect(rect.xMax-370,rect.y,106,30),"物品目录",!tagsPage&&!playerBuildsPage)){tagsPage=false;playerBuildsPage=false;}
  if(InterfaceMenu.Button(new Rect(rect.xMax-256,rect.y,106,30),"快捷标签",tagsPage)){tagsPage=true;playerBuildsPage=false;}
  if(InterfaceMenu.Button(new Rect(rect.xMax-142,rect.y,142,30),"玩家枪械预设",playerBuildsPage))playerBuildsPage=true;
  if(playerBuildsPage){PlayerWeaponPresets.Draw(new Rect(rect.x,rect.y+48,rect.width,rect.height-48));return;}
  if(tagsPage){DrawPresets(new Rect(rect.x,rect.y+48,rect.width,rect.height-48));return;}
  InterfaceMenu.Label(new Rect(rect.x,rect.y+32,rect.width,22),"搜索中文名称或模板 ID；战局内放入背包，战局外发送邮件。",null,12);
  query=InterfaceMenu.Field(new Rect(rect.x,rect.y+66,rect.width,30),query);if(query!=previousQuery)Search();
  float leftWidth=Math.Max(160,(rect.width-16)*.48f);
  InterfaceMenu.Label(new Rect(rect.x,rect.y+102,leftWidth,22),$"搜索结果 {matches.Count} / {catalog.Count} · 滚动查看全部",InterfaceMenu.Accent,12);
  var list=new Rect(rect.x,rect.y+130,leftWidth,Math.Max(60,rect.height-282));
  if(rowsWidth!=list.width){rowsWidth=list.width;itemRows.Build(matches.Select(item=>InterfaceMenu.ButtonHeight(item.Name,list.width-24)));}
  scroll=GUI.BeginScrollView(list,scroll,new Rect(0,0,list.width-18,itemRows.Height));
  var visible=itemRows.Visible(scroll.y,list.height);
  for(int i=visible.First;i<visible.End;i++){
   var item=matches[i];var row=new Rect(0,itemRows.Tops[i],list.width-24,itemRows.Heights[i]);
   if(InterfaceMenu.WrappedButton(row,item.Name,selected==item.Id)){selected=item.Id;Inspect(item);}
  }
  GUI.EndScrollView();
  float bottom=rect.yMax-140;
  var chosen=catalog.FirstOrDefault(item=>item.Id==selected);
  var properties=new Rect(list.xMax+16,list.y,Math.Max(90,rect.width-leftWidth-16),list.height);
  if(chosen!=null){
   if(inspected!=chosen.Id)Inspect(chosen);
   DrawProperties(properties,chosen);
  }else InterfaceMenu.Label(properties,"选中物品后可设置属性",InterfaceMenu.Accent);
  InterfaceMenu.Label(new Rect(rect.x,bottom,45,30),"数量");quantity=InterfaceMenu.Field(new Rect(rect.x+50,bottom,90,30),quantity);
  if(InterfaceMenu.Button(new Rect(rect.x+154,bottom,180,32),mailTask!=null?"正在发送…":raid?"应用属性并添加":"应用属性并发邮件",chosen!=null)&&chosen!=null&&mailTask==null)Spawn(chosen);
  tagName=InterfaceMenu.Field(new Rect(rect.x,bottom+46,Math.Max(100,rect.width-170),31),tagName);
  if(InterfaceMenu.Button(new Rect(rect.xMax-158,bottom+46,158,31),"保存为快捷标签")&&chosen!=null)SavePreset(chosen);
  InterfaceMenu.Paragraph(new Rect(rect.x,bottom+88,rect.width,45),status,null,12);
 }
 internal static IReadOnlyList<SavedPreset> SavedPresets(){Load();LoadPresets();if(tagsError)throw new ArgumentException("物品标签文件读取失败。");return presets;}
 internal static List<Dictionary<string,object?>> PresetMail(SavedPreset preset,int count){Load();var entry=catalog.FirstOrDefault(e=>e.Id==preset.Template)??throw new ArgumentException("模板已不存在。");return MailItems(entry,count,preset.Settings,preset.Attachments);}
 internal static string SpawnPreset(SavedPreset preset,int count){LoadPreset(preset);quantity=count.ToString();Spawn(catalog.First(e=>e.Id==preset.Template));return status;}
 internal static void EditSavedPreset(SavedPreset preset){Load();LoadPreset(preset);playerBuildsPage=false;}
 static void LoadPresets(){if(tagsLoaded)return;tagsLoaded=true;try{presets=PresetStorage.Load<SavedPreset>(PresetPath);}catch(Exception error){tagsError=true;status="标签文件读取失败，原文件保留："+error.Message;}}
 static void SavePreset(Entry entry){
  try{if(tagsError)throw new ArgumentException("标签文件读取失败，请先修复文件，避免覆盖原有标签。");string label=tagName.Trim();if(label.Length is <1 or >80)throw new ArgumentException("请填写 1～80 个字符的标签名。");int count=ItemSpawnRules.Integer(quantity,1,int.MaxValue,"总数量");var settings=ReadValues();var attachments=ItemAttachments.Capture();var previous=presets.FirstOrDefault(item=>item.Name.Equals(label,StringComparison.OrdinalIgnoreCase));var updated=new List<SavedPreset>(presets);if(previous!=null)updated.Remove(previous);updated.Add(new(previous?.Id??Guid.NewGuid().ToString("N"),label,entry.Id,count,settings,attachments));PresetStorage.Save(PresetPath,updated);presets=updated;status="已保存标签："+label;}
  catch(Exception error){status="标签未保存："+error.Message;}
 }
 static void LoadPreset(SavedPreset preset){
  var entry=catalog.FirstOrDefault(item=>item.Id==preset.Template)??throw new ArgumentException("此标签的物品模板已不存在。");selected=entry.Id;Inspect(entry);var v=preset.Settings;
  changeDurability=v.Repairable;durability=Format(v.Durability);maxDurability=Format(v.MaxDurability);changeMed=v.Medical;medResource=Format(v.MedicalAmount);changeFood=v.Food;foodResource=Format(v.FoodAmount);changeKey=v.Key;keyUses=v.KeyRemaining.ToString();loadMagazine=v.LoadMagazine;rounds=v.Rounds.ToString();ammoId=v.AmmoId;fir=v.Fir;
  if(inspectedSample!=null)extraEditor=new ItemComponentEditor(inspectedSample,v.Extra??new(),false);
  ItemAttachments.Restore(preset.Attachments);quantity=preset.Count.ToString();tagName=preset.Name;tagsPage=false;
 }
 static void DrawPresets(Rect rect){
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width,28),"保存当前物品、参数和附件组合后，可在这里直接添加。",null,12);
  var list=presets.OrderBy(item=>item.Name,StringComparer.OrdinalIgnoreCase).ToList();float width=rect.width-24;
  var layout=new MenuRowLayout();layout.Build(list.Select(item=>Math.Max(76,InterfaceMenu.ParagraphHeight(item.Name+"\n"+TemplateName(item.Template)+" ×"+item.Count,width-300,14)+12)));
  var view=new Rect(rect.x,rect.y+40,rect.width,Math.Max(40,rect.height-92));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,Math.Max(40,layout.Height)));
  SavedPreset? action=null;int command=0;
  if(list.Count==0)InterfaceMenu.Paragraph(new Rect(0,0,width,60),"还没有标签。返回物品目录，配置参数与附件后，在底部填写标签名并保存。");
  var range=layout.Visible(scroll.y,view.height);
  for(int i=range.First;i<range.End;i++){var item=list[i];float y=layout.Tops[i];InterfaceMenu.Paragraph(new Rect(0,y,width-300,layout.Heights[i]),item.Name+"\n"+TemplateName(item.Template)+" ×"+item.Count);
   if(InterfaceMenu.Button(new Rect(width-288,y+8,88,34),mailTask==null?"快捷添加":"发送中…",true)&&mailTask==null){action=item;command=1;}
   if(InterfaceMenu.Button(new Rect(width-192,y+8,88,34),"载入编辑")){action=item;command=2;}
   if(InterfaceMenu.Button(new Rect(width-96,y+8,88,34),"删除")){action=item;command=3;}
  }GUI.EndScrollView();
  if(action!=null)try{if(command==3){var updated=presets.Where(item=>item.Id!=action.Id).ToList();PresetStorage.Save(PresetPath,updated);presets=updated;status="已删除标签："+action.Name;}else{LoadPreset(action);if(command==1){Spawn(catalog.First(item=>item.Id==action.Template));tagsPage=true;}else status="已载入标签，可继续修改参数。";}}catch(Exception error){status=error.Message;}
  InterfaceMenu.Paragraph(new Rect(rect.x,rect.yMax-42,rect.width,40),status,null,12);
 }
 static void Inspect(Entry entry){
  inspected=entry.Id;schema=null;extraEditor=null;inspectedSample=null;attachmentPage=false;ItemAttachments.Reset(entry.Id);propertyScroll=Vector2.zero;changeDurability=changeMed=changeFood=changeKey=loadMagazine=false;fir=0;ammoQuery="";ammoId="";status="";
  compatibleAmmo=catalog.Where(item=>item.IsAmmo&&ItemSpawnRules.MatchesFilter(item.Id,id=>parents.TryGetValue(id,out var parent)?parent:null,entry.Allowed,entry.Excluded)).ToList();
  if(compatibleAmmo.Count>0)ammoId=compatibleAmmo[0].Id;
  try{
   var factory=Singleton<ItemFactory>.Instance;if(factory==null){status="物品工厂尚未就绪，进入战局后重新选择物品。";return;}
   var sample=factory.CreateItem(factory.NextId.ToString(),entry.Id,null);
   if(sample==null){status="无法读取此物品的属性。";return;}
   inspectedSample=sample;extraEditor=new ItemComponentEditor(sample,new ItemComponentParameters(),false);
   var repair=sample.GetItemComponent<RepairableComponent>();var med=sample.GetItemComponent<MedKitComponent>();
   var food=sample.GetItemComponent<FoodDrinkComponent>();var key=sample.GetItemComponent<KeyComponent>();
   var magazine=sample.TryCast<Magazine>();
   schema=new(repair!=null,repair?.Durability??0,repair?.MaxDurability??0,med!=null&&med.MaxHpResource>0,med?.MaxHpResource??0,food!=null&&food.MaxResource>0,food?.MaxResource??0,key!=null,key?.Template?.MaximumNumberOfUsage??0,magazine?.Cartridges?.MaxCount??0);
   durability=Format(schema.Durability);maxDurability=Format(schema.MaxDurability);
   medResource=Format(med?.HpResource??0);foodResource=Format(food?.HpPercent??0);keyUses=key!=null?Math.Max(0,(long)(key.Template?.MaximumNumberOfUsage??0)-key.NumberOfUsages).ToString():"0";rounds=schema.MagazineCapacity.ToString();
  }catch(Exception error){status="属性读取失败："+error.Message;Plugin.Self.Error(error);}
 }
 static void DrawProperties(Rect rect,Entry entry){
  string heading="属性 · "+entry.Name;float headingHeight=Math.Max(27,InterfaceMenu.ParagraphHeight(heading,rect.width,15));
  InterfaceMenu.Paragraph(new Rect(rect.x,rect.y,rect.width,headingHeight),heading,InterfaceMenu.Accent,15);
  InterfaceMenu.Label(new Rect(rect.x,rect.y+headingHeight+4,rect.width,22),entry.Id,null,11);
  float headerHeight=headingHeight+30;
  if(InterfaceMenu.Button(new Rect(rect.x,rect.y+headerHeight,(rect.width-8)/2,31),"主体参数",!attachmentPage)){ItemAttachments.Capture();attachmentPage=false;}
  if(InterfaceMenu.Button(new Rect(rect.x+(rect.width+8)/2,rect.y+headerHeight,(rect.width-8)/2,31),"附件与组件",attachmentPage))attachmentPage=true;
  headerHeight+=43;
  var view=new Rect(rect.x,rect.y+headerHeight,rect.width,Math.Max(25,rect.height-headerHeight));
  if(attachmentPage){ItemAttachments.Draw(view);return;}
  float width=view.width-24;
  string normalizedAmmo=ItemSpawnRules.NormalizeSearch(ammoQuery);
  var ammoMatches=compatibleAmmo.Where(item=>item.SearchText.Contains(normalizedAmmo,StringComparison.Ordinal)).ToList();
  string selectedAmmo="已选："+(catalog.FirstOrDefault(item=>item.Id==ammoId)?.Name??"请选择弹药");
  float selectedHeight=Math.Max(28,InterfaceMenu.ParagraphHeight(selectedAmmo,width,12));
  var ammoHeights=ammoMatches.Select(item=>InterfaceMenu.ButtonHeight(item.Name,width,36)).ToArray();
  var detailLines=entry.Details.Select(row=>I18n.Text(row.Label)+"："+I18n.Text(row.Value)+(row.Unit.Length>0?" "+row.Unit:"")).ToArray();
  var detailHeights=detailLines.Select(line=>Math.Max(24,InterfaceMenu.ParagraphHeight(line,width,12))).ToArray();
  float detailsHeight=64+detailHeights.Sum()+detailHeights.Length*4;
  float contentHeight=55+detailsHeight;
  if(schema is Schema s){
   contentHeight+=(s.Repairable?43+(changeDurability?78:0):0)+(s.Medical?43+(changeMed?39:0):0)+(s.Food?43+(changeFood?39:0):0)+(s.Key?43+(changeKey?39:0):0);
   if(s.MagazineCapacity>0)contentHeight+=43+(loadMagazine?39+28+36+selectedHeight+8+28+(ammoMatches.Count==0?28:ammoHeights.Sum()+ammoMatches.Count*4):0);
  }
  if(extraEditor?.HasOptions==true)contentHeight+=extraEditor.Height;
  propertyScroll=GUI.BeginScrollView(view,propertyScroll,new Rect(0,0,view.width-18,contentHeight));
  float y=0;
  InterfaceMenu.Label(new Rect(0,y,width,26),"物品详情（模板基础值）",InterfaceMenu.Accent,14);y+=32;
  for(int i=0;i<detailLines.Length;i++){InterfaceMenu.Paragraph(new Rect(0,y,width,detailHeights[i]),detailLines[i],null,12);y+=detailHeights[i]+4;}
  InterfaceMenu.Label(new Rect(0,y,width,24),"生成属性",InterfaceMenu.Accent,14);y+=32;
  InterfaceMenu.Label(new Rect(0,y,100,30),"搜刮标记");
  if(InterfaceMenu.Button(new Rect(106,y,Math.Max(60,width-106),30),fir==0?"模板默认":fir==1?"有搜刮标记":"无搜刮标记",fir!=0))fir=(fir+1)%3;
  y+=43;
  if(schema is Schema data){
   if(data.Repairable){Switch("修改耐久",ref changeDurability,width,ref y);if(changeDurability){Number("当前耐久",ref durability,width,ref y);Number("最大耐久",ref maxDurability,width,ref y);}}
   if(data.Medical){Switch("修改医疗使用量",ref changeMed,width,ref y);if(changeMed)Number($"剩余 / {data.MedMaximum:0.##}",ref medResource,width,ref y);}
   if(data.Food){Switch("修改食物使用量",ref changeFood,width,ref y);if(changeFood)Number($"剩余 / {data.FoodMaximum:0.##}",ref foodResource,width,ref y);}
   if(data.Key){Switch("修改钥匙剩余次数",ref changeKey,width,ref y);if(changeKey)Number(data.KeyMaximum>0?$"剩余（常规 {data.KeyMaximum}）":"无限钥匙",ref keyUses,width,ref y);}
   if(data.MagazineCapacity>0){
    Switch("预装弹药",ref loadMagazine,width,ref y);
    if(loadMagazine){
     Number($"发数 / {data.MagazineCapacity}",ref rounds,width,ref y);
     InterfaceMenu.Label(new Rect(0,y,width,26),"只列出此弹匣兼容的弹药",null,12);y+=28;
     ammoQuery=InterfaceMenu.Field(new Rect(0,y,width,29),ammoQuery);y+=36;
     InterfaceMenu.Paragraph(new Rect(0,y,width,selectedHeight),selectedAmmo,InterfaceMenu.Accent,12);y+=selectedHeight+8;
     InterfaceMenu.Label(new Rect(0,y,width,24),$"兼容 {compatibleAmmo.Count} 种 · 匹配 {ammoMatches.Count} 种 · 滚动查看全部",InterfaceMenu.Accent,12);y+=28;
     for(int i=0;i<ammoMatches.Count;i++){
      var candidate=ammoMatches[i];float height=ammoHeights[i];
      if(InterfaceMenu.WrappedButton(new Rect(0,y,width,height),candidate.Name,ammoId==candidate.Id))ammoId=candidate.Id;y+=height+4;
     }
     if(ammoMatches.Count==0)InterfaceMenu.Label(new Rect(0,y,width,28),compatibleAmmo.Count==0?"未找到兼容弹药":"没有匹配项，请修改搜索词。",null,12);
    }
   }
  }
  if(extraEditor?.HasOptions==true)extraEditor.Draw(width,y);
  GUI.EndScrollView();
 }
 static void Switch(string label,ref bool value,float width,ref float y){if(InterfaceMenu.Button(new Rect(0,y,width,31),label+(value?" · 已启用":" · 默认"),value))value=!value;y+=43;}
 static void Number(string label,ref string value,float width,ref float y){float fieldWidth=Math.Min(105,width*.44f);InterfaceMenu.Label(new Rect(0,y,width-fieldWidth-6,30),label,null,12);value=InterfaceMenu.Field(new Rect(width-fieldWidth,y,fieldWidth,30),value);y+=39;}
 static Values ReadValues(){
  float current=0,max=0,medical=0,food=0;int used=0,ammunition=0;
  var s=schema;
  bool repair=changeDurability&&s?.Repairable==true,med=changeMed&&s?.Medical==true,meal=changeFood&&s?.Food==true,key=changeKey&&s?.Key==true,mag=loadMagazine&&s?.MagazineCapacity>0;
  if(repair){max=ItemSpawnRules.Number(maxDurability,.01f,ItemComponentParameters.StorageMaximum,"最大耐久");current=ItemSpawnRules.Number(durability,0,max,"当前耐久");}
  if(med)medical=ItemSpawnRules.Number(medResource,0,ItemComponentParameters.StorageMaximum,"医疗剩余使用量");
  if(meal)food=ItemSpawnRules.Number(foodResource,0,ItemComponentParameters.StorageMaximum,"食物剩余使用量");
  if(key)used=ItemSpawnRules.Integer(keyUses,0,int.MaxValue,"钥匙剩余次数");
  if(mag){ammunition=ItemSpawnRules.Integer(rounds,0,s!.MagazineCapacity,"装填发数");if(ammunition>0&&!compatibleAmmo.Any(item=>item.Id==ammoId))throw new ArgumentException("请选择兼容的弹药。");}
  return new(repair,current,max,med,medical,meal,food,key,used,mag,ammunition,ammoId,fir,extraEditor?.Read()??new());
 }
 static void Spawn(Entry entry){
  int added=0;
  try{
   int count=ItemSpawnRules.Integer(quantity,1,int.MaxValue,"总数量");
   var values=ReadValues();
   var attachments=ItemAttachments.Capture();
   if(!InRaid()){mailTask=GearMailClient.Send(MailItems(entry,count,values,attachments),true);status="正在发送邮件…";return;}
   var world=Singleton<GameWorld>.Instance;var player=world?.MainPlayer;
   if(player==null||!player.IsYourPlayer||player.HealthController==null||!player.HealthController.IsAlive){status="需要进入战局后添加。";return;}
   var equipment=player.Profile?.Inventory?.Equipment;
   var controller=player.InventoryController;var factory=Singleton<ItemFactory>.Instance;
   if(controller==null||factory==null){status="库存尚未就绪。";return;}
   int stacks=0;
   while(added<count&&stacks++<100){
    var item=factory.CreateItem(factory.NextId.ToString(),entry.Id,null);
    if(item==null)break;
    item.StackObjectsCount=Math.Min(count-added,Math.Max(1,item.StackMaxSize));
    ApplyProperties(item,values,factory);
    ItemAttachments.Apply(item,factory,attachments,values.Fir==0?null:values.Fir==1);
    ItemAddress? destination=null;
    var backpack=equipment?.GetSlot(EquipmentSlot.Backpack)?.ContainedItem?.TryCast<CompoundItem>();
    if(backpack?.Grids!=null)foreach(var grid in backpack.Grids){
      if(grid==null)continue;
      var candidate=grid.FindLocationForItem(item);if(candidate==null)continue;
      var check=ItemManipulator.Add(item,candidate,controller,true);
      if(check.Succeeded){destination=candidate;break;}
     }
    if(destination==null&&equipment!=null)foreach(var name in new[]{EquipmentSlot.Backpack,EquipmentSlot.Headwear,EquipmentSlot.ArmorVest,EquipmentSlot.TacticalVest}){
     var slot=equipment.GetSlot(name);if(slot==null||slot.ContainedItem!=null)continue;
     var candidate=slot.CreateItemAddress();
     // Native simulation retains item filters, armor/rig conflicts and slot locks.
     if(ItemManipulator.Add(item,candidate,controller,true).Succeeded){destination=candidate;break;}
    }
    if(destination==null)break;
    controller.AddAndRaiseEvents(item,destination);
    if(item.CurrentAddress==null)break;
    added+=item.StackObjectsCount;
   }
   equipment?.RaiseRefreshEvent(false,true);
   status=added==count?$"已添加 {entry.Name} ×{added}":$"已添加 {added}/{count}：背包空间或空装备槽不足、物品限制或单次数量上限。";
   Plugin.Self.Log.LogInfo($"Item spawn: template={entry.Id}, requested={count}, added={added}");
  }catch(ArgumentException error){status=error.Message;}
  catch(Exception error){status=$"已添加 {added}；操作中止：{error.Message}";Plugin.Self.Error(error);}
 }
 internal static bool InRaid(){var game=Singleton<AbstractGame>.Instance;return game!=null&&game.InRaid&&game.TryCast<HideoutGame>()==null;}
 static List<Dictionary<string,object?>> MailItems(Entry entry,int count,Values values,ItemAttachmentNode attachments){
  var attributes=new Dictionary<string,object?>();
  if(values.Repairable){attributes["durability"]=values.Durability;attributes["maxDurability"]=values.MaxDurability;}
  if(values.Medical)attributes["medicalResource"]=values.MedicalAmount;
  if(values.Food)attributes["foodResource"]=values.FoodAmount;
  if(values.Key)attributes["keyRemaining"]=values.KeyRemaining;
  foreach(var extra in values.Extra.Attributes())attributes[extra.Key]=extra.Value;
  if(values.Fir!=0)attributes["foundInRaid"]=values.Fir==1;
  var result=new List<Dictionary<string,object?>>();int root=0;
  while(count>0){
   int chunk=Math.Min(count,entry.StackMax);string id="item_"+root++;
   var spec=new Dictionary<string,object?>{{"ref",id},{"template",entry.Id},{"count",chunk}};
   if(attributes.Count>0)spec["attributes"]=attributes;
   result.Add(spec);count-=chunk;
   ItemAttachments.AppendMail(result,id,attachments,values.Fir==0?null:values.Fir==1);
   if(values.LoadMagazine&&values.Rounds>0){
    var ammo=catalog.First(item=>item.Id==values.AmmoId);int left=values.Rounds,position=0;
    while(left>0){int amount=Math.Min(left,ammo.StackMax);var cartridge=new Dictionary<string,object?>{{"ref",id+"_ammo_"+position},{"template",ammo.Id},{"parent",id},{"slot","cartridges"},{"location",position++},{"count",amount}};if(values.Fir!=0)cartridge["attributes"]=new Dictionary<string,object?>{{"foundInRaid",values.Fir==1}};result.Add(cartridge);left-=amount;}
   }
   if(result.Count>100)throw new ArgumentException("邮件单次最多 100 个物品条目（含弹匣内弹药），请减少数量。");
  }
  return result;
 }
 static void ApplyProperties(Item item,Values values,ItemFactory factory){
  if(values.Repairable){var component=item.GetItemComponent<RepairableComponent>()??throw new InvalidOperationException("物品不支持耐久属性。");component.MaxDurability=values.MaxDurability;component.Durability=values.Durability;}
  if(values.Medical){var component=item.GetItemComponent<MedKitComponent>()??throw new InvalidOperationException("物品不支持医疗使用量。");component.HpResource=values.MedicalAmount;}
  if(values.Food){var component=item.GetItemComponent<FoodDrinkComponent>()??throw new InvalidOperationException("物品不支持食物使用量。");component.HpPercent=values.FoodAmount;}
  if(values.Key){var component=item.GetItemComponent<KeyComponent>()??throw new InvalidOperationException("物品不支持钥匙次数。");component.NumberOfUsages=checked(component.Template.MaximumNumberOfUsage-values.KeyRemaining);}
  values.Extra.Apply(item);
  if(values.Fir!=0)SetFir(item,values.Fir==1,0);
  if(values.LoadMagazine){
   var magazine=item.TryCast<Magazine>()??throw new InvalidOperationException("物品不是弹匣。");
   var slot=magazine.Cartridges??throw new InvalidOperationException("弹匣没有装填位置。");
   if(slot.Count!=0)throw new InvalidOperationException("此模板自带弹药，未执行预装覆盖。");
   int remaining=values.Rounds;
   while(remaining>0){
    var ammo=factory.CreateItem(factory.NextId.ToString(),values.AmmoId,null)?.TryCast<Ammo>()??throw new InvalidOperationException("无法生成所选弹药。");
    int chunk=Math.Min(remaining,Math.Max(1,ammo.StackMaxSize));ammo.StackObjectsCount=chunk;
    if(values.Fir!=0)ammo.SpawnedInSession=values.Fir==1;
    var result=slot.Add(ammo,false);
    if(!result.Succeeded)throw new InvalidOperationException("装填失败：弹药不兼容或容量不足。");
    remaining-=chunk;
   }
   if(slot.Count!=values.Rounds)throw new InvalidOperationException("装填数量校验失败。");
  }
 }
 internal static void SetCommandFir(Item item,bool value)=>SetFir(item,value,0);
 static void SetFir(Item item,bool value,int depth){if(depth>12)return;item.SpawnedInSession=value;foreach(var child in LootDisplay.Children(item))SetFir(child,value,depth+1);}
}
