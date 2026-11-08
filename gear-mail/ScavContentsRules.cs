using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;

public sealed class ScavContentSpec {
 [JsonPropertyName("id")] public string Id{get;set;}="";
 [JsonPropertyName("template")] public string Template{get;set;}="";
 [JsonPropertyName("count")] public int Count{get;set;}=1;
}
public static class ScavContentsRules {
 public static readonly string[] Slots=["TacticalVest","Backpack"];
 public static List<Item> Tree(List<Item> source,string root){var ids=new HashSet<string>{root};bool added;do{added=false;foreach(var item in source)if(item.ParentId!=null&&ids.Contains(item.ParentId)&&ids.Add(item.Id.ToString()))added=true;}while(added);return source.Where(i=>ids.Contains(i.Id.ToString())).ToList();}
 public static Dictionary<string,ScavContentSpec[]> Describe(LoadoutSnapshot loadout)=>Slots.ToDictionary(slot=>slot,slot=>{
  var container=loadout.Items.SingleOrDefault(i=>i.ParentId==loadout.EquipmentRoot&&i.SlotId==slot);
  return container==null?[]:loadout.Items.Where(i=>i.ParentId==container.Id.ToString()&&i.Location!=null).Select(i=>new ScavContentSpec{Id=i.Id.ToString(),Template=i.Template.ToString(),Count=(int)(i.Upd?.StackObjectsCount??1)}).ToArray();
 });
 public static void Apply(LoadoutSnapshot loadout,Dictionary<string,List<ScavContentSpec>> edits,TemplateTable templates,ItemHelper helper,PresetHelper presets,JsonUtil json){
  // Stage both containers before touching the caller's tree.
  var working=json.Deserialize<LoadoutSnapshot>(json.Serialize(loadout))!;
  foreach(var edit in edits){
   if(!Slots.Contains(edit.Key))throw new ArgumentException("只能编辑弹挂和背包的内容物。");
   if(edit.Value==null||edit.Value.Count>512)throw new ArgumentException("内容物条目过多。");
   var container=working.Items.SingleOrDefault(i=>i.ParentId==working.EquipmentRoot&&i.SlotId==edit.Key);
   if(container==null){if(edit.Value.Count==0)continue;throw new ArgumentException("请先为该槽位选择容器。");}
   var grids=templates.Items[container.Template].Properties.Grids?.Where(g=>g.Properties!=null).ToArray()??[];
   if(grids.Length==0){if(edit.Value.Count==0)continue;throw new ArgumentException("该装备没有可用储物格。");}
   var roots=working.Items.Where(i=>i.ParentId==container.Id.ToString()&&grids.Any(g=>g.Name==i.SlotId)).ToDictionary(i=>i.Id.ToString());
   var seen=new HashSet<string>();var groups=new List<List<Item>>();
   foreach(var spec in edit.Value){
    if(spec.Count<1||!MongoId.IsValidMongoId(spec.Template)||!templates.Items.TryGetValue(new MongoId(spec.Template),out var definition)||definition.Type!="Item")throw new ArgumentException("内容物模板或数量无效。");
    if(spec.Count>Math.Max(1,definition.Properties.StackMaxSize))throw new ArgumentException("数量超过单组上限，请分为多条添加。");
    List<Item> group;Item root;
    if(spec.Id.Length>0){
     if(!seen.Add(spec.Id)||!roots.TryGetValue(spec.Id,out root!)||root.Template.ToString()!=spec.Template)throw new ArgumentException("原内容物已变化，请刷新装备方案。");
     group=Tree(working.Items,spec.Id);
    }else{
     var preset=presets.GetDefaultPreset(new MongoId(spec.Template));
     if(preset?.Items?.Count>0){group=json.Deserialize<List<Item>>(json.Serialize(preset.Items))!;root=group.Single(i=>i.Id==preset.Parent);var ids=group.ToDictionary(i=>i.Id.ToString(),_=>new MongoId());group=group.Select(i=>i with{Id=ids[i.Id.ToString()],ParentId=i.Id==root.Id?container.Id.ToString():i.ParentId!=null&&ids.TryGetValue(i.ParentId,out var p)?p.ToString():throw new ArgumentException("默认预设结构错误。")}).ToList();root=group.Single(i=>i.Id==ids[root.Id.ToString()]);}
     else {root=new(){Id=new MongoId(),Template=new MongoId(spec.Template)};group=[root];}
    }
    root.Upd??=helper.GenerateUpdForItem(definition)??new();root.Upd.StackObjectsCount=spec.Count;
    groups.Add(group);
   }
   var placed=new List<(int Grid,int X,int Y,int W,int H)>();
   foreach(var group in groups){
    var root=group.Single(i=>!group.Any(p=>p.Id.ToString()==i.ParentId));var size=helper.GetItemSize(group,root.Id)??throw new ArgumentException("无法读取内容物尺寸。");bool found=false;
    if(size.Width<1||size.Height<1)throw new ArgumentException("该物品没有有效的库存尺寸。");
    for(int g=0;g<grids.Length&&!found;g++){
     var grid=grids[g];var gridProps=grid.Properties!;if(!Allowed(root.Template,grid,templates))continue;
     for(int r=0;r<2&&!found;r++){int w=r==0?size.Width:size.Height,h=r==0?size.Height:size.Width;
      for(int y=0;y<=gridProps.CellsV-h&&!found;y++)for(int x=0;x<=gridProps.CellsH-w;x++){
       if(placed.Any(p=>p.Grid==g&&x<p.X+p.W&&x+w>p.X&&y<p.Y+p.H&&y+h>p.Y))continue;
       root.ParentId=container.Id.ToString();root.SlotId=grid.Name;root.Location=new ItemLocation{X=x,Y=y,R=(ItemRotation)r,IsSearched=true};placed.Add((g,x,y,w,h));found=true;break;
      }
     }
    }
    if(!found)throw new ArgumentException("内容物放不下或不符合容器限制，请减少物品或更换容器。");
   }
   var remove=roots.Keys.SelectMany(id=>Tree(working.Items,id)).Select(i=>i.Id.ToString()).ToHashSet();working.Items.RemoveAll(i=>remove.Contains(i.Id.ToString()));working.Items.AddRange(groups.SelectMany(g=>g));
  }
  loadout.Items.Clear();loadout.Items.AddRange(working.Items);
 }
 static bool Allowed(MongoId id,Grid grid,TemplateTable templates){
  var ancestry=new HashSet<string>();while(ancestry.Add(id.ToString())&&templates.Items.TryGetValue(id,out var entry))id=entry.Parent;
  return grid.Properties!.Filters?.All(f=>(f.Filter==null||!f.Filter.Any()||f.Filter.Any(v=>ancestry.Contains(v.ToString())))&&(f.ExcludedFilter==null||!f.ExcludedFilter.Any(v=>ancestry.Contains(v.ToString()))))??true;
 }
}
