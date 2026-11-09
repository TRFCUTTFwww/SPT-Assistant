using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
namespace LocalSpt5Esp;
internal static partial class InterfaceMenu {
 sealed record SearchItem(string Section,string Page,string Title,string Terms,ConfigEntryBase? Entry=null,bool Scav=false);
 static string globalQuery="",cachedQuery="";
 static bool cachedEnglish;
 static SearchItem[]? searchIndex;
 static SearchItem[] searchResults=Array.Empty<SearchItem>();
 static Vector2 searchScroll;
 static void ResetGlobalSearch(){searchIndex=null;searchResults=Array.Empty<SearchItem>();globalQuery=cachedQuery="";searchScroll=Vector2.zero;visibleEntries=null;}
 static string Bilingual(params string[] values)=>string.Join(" ",values.SelectMany(value=>new[]{value,I18n.EnglishText(value)}));
 static void BuildSearchIndex(){
  var items=new List<SearchItem>();
  foreach(string section in MenuSections.Names)foreach(string page in MenuSections.Pages(section)){
   string extra=(section,page) switch{
    ("杂项",_)=>MiscPanel.SearchTerms,
    ("服务端","配置参数")=>"保险 投保 回保 藏身处 制作 修理 交易 跳蚤 市场 insurance hideout crafting repair trading flea",
    ("服务端","业力与信誉")=>"Fence 声望 好感 信誉 业力 reputation standing karma",
    ("数据","角色管理")=>"藏身处 等级 经验 名称 阵营 hideout level experience profile",
    ("战局与物资","本局物资")=>"仓库 战局仓库 stash inventory loot",
    ("载具与空投","BTR 控制")=>"下一站 设置下一站 立即发车 停留 路线 destination next stop route departure",
    ("剧情","等待时间")=>"跳过 支线 主线 商人任务 冷却 skip quest wait cooldown",
    _=>""
   };
   items.Add(new(section,page,page,Bilingual(section,MenuSections.DisplayName(section),page,MenuSections.Subtitle(section),extra)));
  }
  items.Add(new("数据","角色管理","Scav 装备",Bilingual("Scav 装备","近战武器 弹挂 胸挂 背包 内容物 装备修改 安全箱 scav equipment melee rig backpack contents secure container"),Scav:true));
  foreach(var pair in config){
   var entry=pair.Value;var attributes=Metadata(entry);var source=attributes!=null?I18n.Source(attributes):(entry.Definition.Key,entry.Definition.Section,entry.Description.Description);
   string section=MenuSections.Group(entry.Definition.Section,entry.Definition.Key);string page=MenuSections.Pages(section).FirstOrDefault(candidate=>MenuSections.OnPage(section,candidate,entry.Definition.Section,entry.Definition.Key))??MenuSections.Pages(section)[0];
   if(MenuSections.Number(entry.Definition.Section) is 24 or 32){section="设置与热键";page="主题与布局";}
   items.Add(new(section,page,source.Item1,Bilingual(source.Item1,source.Item2,source.Item3,entry.Definition.Section,entry.Definition.Key,section,page),entry));
  }
  searchIndex=items.ToArray();cachedEnglish=I18n.English;cachedQuery="";
 }
 static void FindGlobalResults(){
  if(searchIndex==null||cachedEnglish!=I18n.English)BuildSearchIndex();
  string query=globalQuery.Trim();if(query==cachedQuery)return;cachedQuery=query;
  var terms=MenuSearchRules.Terms(query);searchResults=searchIndex!.Where(item=>MenuSearchRules.Matches(terms,item.Terms)).OrderBy(item=>item.Entry!=null).ThenByDescending(item=>item.Title.Equals(query,StringComparison.OrdinalIgnoreCase)).ToArray();
 }
 static void DrawGlobalSearchBar(ref Rect content){
  Label(new Rect(content.x,content.y,76,32),"全局搜索",Accent,13);
  string next=Field(new Rect(content.x+84,content.y,Math.Max(40,content.width-158),32),globalQuery);
  if(next!=globalQuery){globalQuery=next;searchScroll=Vector2.zero;capture=null;}
  if(globalQuery.Length==0)Label(new Rect(content.x+96,content.y,Math.Max(20,content.width-182),32),"搜索全部功能、说明或页面…",Color.Lerp(Text,Background,.55f),12);
  if(Button(new Rect(content.xMax-66,content.y,66,32),"清空")){globalQuery="";capture=null;GUI.FocusControl(null);}
  content.y+=46;content.height-=46;
 }
 static void DrawGlobalResults(Rect content){
  FindGlobalResults();Label(new Rect(content.x,content.y,content.width,28),$"全局搜索 · {searchResults.Length} 项结果",Accent,16);
  var view=new Rect(content.x,content.y+38,content.width,Math.Max(30,content.height-38));int columns=view.width>=760?2:1;float width=(view.width-24-12*(columns-1))/columns;const float rowHeight=136;
  searchScroll=GUI.BeginScrollView(view,searchScroll,new Rect(0,0,view.width-20,Math.Max(view.height-1,MathF.Ceiling(searchResults.Length/(float)columns)*rowHeight)));
  SearchItem? navigate=null;
  if(searchResults.Length==0)Paragraph(new Rect(8,8,view.width-36,70),"没有匹配的功能。试试更短的关键词，或清空搜索返回当前页面。",null,13);
  int first=Math.Max(0,(int)(searchScroll.y/rowHeight)*columns-columns),last=Math.Min(searchResults.Length,((int)((searchScroll.y+view.height)/rowHeight)+2)*columns);
  for(int i=first;i<last;i++){
   var item=searchResults[i];var rect=new Rect(4+i%columns*(width+12),i/columns*rowHeight,width,124);
   if(item.Entry!=null){DrawEntry(item.Entry,new Rect(rect.x,rect.y,rect.width,92));Label(new Rect(rect.x+8,rect.y+96,rect.width-16,24),MenuSections.DisplayName(item.Section)+" / "+I18n.Text(Category(item.Entry)),Color.Lerp(Text,Background,.4f),11);}
   else if(WrappedButton(rect,I18n.Text(item.Title)+"\n"+I18n.Text(MenuSections.DisplayName(item.Section))+" / "+I18n.Text(item.Page)+"  ›"))navigate=item;
  }
  GUI.EndScrollView();if(navigate!=null){if(navigate.Scav)ProfileAdminPanel.OpenScav();Navigate(navigate.Section,navigate.Page);GUI.FocusControl(null);}
 }
 internal static object SearchDiagnostics(){
  if(searchIndex==null||cachedEnglish!=I18n.English)BuildSearchIndex();
  var queries=new[]{"仓库","BTR","Scav 装备","违禁品","信誉","制作","无敌","opacity"};
  var index=searchIndex!;return new{settings=index.Count(item=>item.Entry!=null),pages=index.Count(item=>item.Entry==null),queries=queries.Select(query=>new{query,matches=index.Where(item=>MenuSearchRules.Matches(MenuSearchRules.Terms(query),item.Terms)).Select(item=>item.Title).Take(12).ToArray()})};
 }
}
