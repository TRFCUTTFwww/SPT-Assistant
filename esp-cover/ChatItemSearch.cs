using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
namespace LocalSpt5Esp;

internal sealed class ChatItemSearch {
 internal sealed record Item(string Id,string Name,string EnglishName,bool IsAmmo,string[] Aliases) {
  internal string Title=>string.IsNullOrWhiteSpace(EnglishName)?Name:EnglishName;
 }
 sealed record Indexed(Item Item,string[] Names);
 readonly Indexed[] entries;
 readonly Dictionary<string,int> nameCounts;
 internal ChatItemSearch(IEnumerable<Item> items){entries=items.Select(item=>new Indexed(item,new[]{item.Name,item.EnglishName}.Concat(item.Aliases).Where(s=>!string.IsNullOrWhiteSpace(s)).Select(Normalize).Distinct().ToArray())).ToArray();nameCounts=entries.SelectMany(e=>e.Names).GroupBy(n=>n).ToDictionary(g=>g.Key,g=>g.Count());}
 internal static string Normalize(string value){var result=new StringBuilder();foreach(char c in value)if(c is '×' or '*')result.Append('x');else if(char.IsLetterOrDigit(c))result.Append(char.ToLowerInvariant(c));return result.ToString();}
 static string Key(string value)=>value.StartsWith("item:",StringComparison.OrdinalIgnoreCase)?value[5..].Trim():value.Trim();
 static int Rank(Indexed entry,string key,string normalized,string[] terms){
  if(entry.Item.Id.Equals(key,StringComparison.OrdinalIgnoreCase))return 0;
  if(entry.Names.Contains(normalized))return 1;
  if(entry.Item.Id.StartsWith(key,StringComparison.OrdinalIgnoreCase))return 2;
  if(entry.Names.Any(n=>n.StartsWith(normalized,StringComparison.Ordinal)))return 3;
  return terms.All(term=>entry.Names.Any(n=>n.Contains(term,StringComparison.Ordinal)))?4:int.MaxValue;
 }
 internal Item[] Search(string value,bool ammoOnly=false){
  string key=Key(value);string[] terms=key.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(Normalize).Where(s=>s.Length>0).ToArray();
  if(key.Length>0&&terms.Length==0)return Array.Empty<Item>();
  string normalized=Normalize(key);
  return entries.Where(e=>!ammoOnly||e.Item.IsAmmo).Select(e=>(e.Item,Rank:key.Length==0?5:Rank(e,key,normalized,terms))).Where(e=>e.Rank<int.MaxValue).OrderBy(e=>e.Rank).ThenBy(e=>e.Item.Title,StringComparer.OrdinalIgnoreCase).ThenBy(e=>e.Item.Id,StringComparer.Ordinal).Select(e=>e.Item).ToArray();
 }
 internal string Resolve(string value){
  string key=Key(value);if(key.Length==0)throw new ArgumentException("请输入物品名称，例如 salewa、IFAK 或 比特币。");
  var exactId=entries.FirstOrDefault(e=>e.Item.Id.Equals(key,StringComparison.OrdinalIgnoreCase));if(exactId!=null)return exactId.Item.Id;
  string normalized=Normalize(key);var exact=entries.Where(e=>e.Names.Contains(normalized)).Select(e=>e.Item).ToArray();var matches=exact.Length>0?exact:Search(key);
  if(matches.Length==1)return matches[0].Id;
  if(matches.Length==0)throw new ArgumentException("没有找到物品："+key+"。可输入中文、英文名、简称或模板 ID。");
  throw new ArgumentException("匹配到 "+matches.Length+" 种物品，请用 ↑↓ 选择候选，再按 Tab 填入："+string.Join(" / ",matches.Take(3).Select(p=>p.Title)));
 }
 internal string Insert(Item item){string title=item.Title;return nameCounts.GetValueOrDefault(Normalize(title))==1?title:"item:"+item.Id;}
 internal static ChatItemSearch Read(JsonElement templates,IReadOnlyDictionary<string,string> names,IReadOnlyDictionary<string,string> english,IReadOnlyDictionary<string,string> chinese){
  string Name(IReadOnlyDictionary<string,string> map,string id,string suffix)=>map.TryGetValue(id+suffix,out var value)?value:"";
  var items=new List<Item>();foreach(var entry in templates.EnumerateObject()){
   if(!entry.Value.TryGetProperty("_type",out var type)||type.GetString()!="Item")continue;
   string local=names.TryGetValue(entry.Name,out var label)?label:Name(chinese,entry.Name," Name"),en=Name(english,entry.Name," Name");
   string raw=entry.Value.TryGetProperty("_name",out var rawName)?rawName.GetString()??"":"";
   if(local.Length==0)local=en.Length>0?en:raw.Length>0?raw:entry.Name;
   bool ammo=entry.Value.TryGetProperty("_props",out var props)&&props.TryGetProperty("PenetrationPower",out _);
   items.Add(new(entry.Name,local,en,ammo,new[]{Name(chinese,entry.Name," Name"),Name(chinese,entry.Name," ShortName"),Name(english,entry.Name," ShortName"),raw}));
  }
  return new(items);
 }
}
