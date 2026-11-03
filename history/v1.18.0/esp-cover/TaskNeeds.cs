using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using EFT.Quests;
using UnityEngine;
namespace LocalSpt5Esp;
internal static class TaskNeeds {
 internal sealed record Need(string Source,string[] Targets,int Remaining,bool Fir,string Note);
 internal static readonly List<Need> Needs=new();
 static readonly Dictionary<string,Need?> matchCache=new();
 internal static ConfigEntry<bool> MarkNeeds=null!;
 static readonly Dictionary<string,(long All,long Fir)> inventory=new();
 static JsonDocument? hideout;
 static float nextRead;
 static Vector2 scroll;
 static string status="",search="";
 internal static void Initialize(){MarkNeeds=FeatureConfig.Bind("27. 搜刮筛选","Mark task needs","物品标签提示任务与藏身处需求",true);}
 static Need? Match(string template){if(!matchCache.TryGetValue(template,out var need)){need=Needs.FirstOrDefault(n=>n.Remaining>0&&n.Targets.Any(t=>ItemCatalog.IsChildOf(template,t)));matchCache[template]=need;}return need;}
 internal static bool Needed(string template)=>Match(template)!=null;
 internal static string Hint(string template,bool fir){if(!MarkNeeds.Value)return "";var need=Match(template);return need==null?"":" · "+need.Source+" 需"+need.Remaining+(need.Fir?(fir?" [战局发现符合]":" [要求战局发现]"):"");}
 internal static void Reset(){matchCache.Clear();Needs.Clear();inventory.Clear();nextRead=0;}
 internal static Dictionary<string,long> InventoryCounts(Profile profile){var result=new Dictionary<string,long>();var seen=new HashSet<IntPtr>();foreach(var item in profile.Inventory.AllRealPlayerItems){if(item==null||!seen.Add(item.Pointer))continue;result.TryGetValue(item.StringTemplateId,out var old);result[item.StringTemplateId]=old+Math.Max(1,item.StackObjectsCount);}return result;}
 internal static void Tick(Player? player){if(player==null||!RaidClock.Active){Reset();return;}if(Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+5;matchCache.Clear();Needs.Clear();inventory.Clear();try{
  var profile=player.Profile;if(profile==null)return;var seen=new HashSet<IntPtr>();
  foreach(var item in profile.Inventory.AllRealPlayerItems){if(item==null||!seen.Add(item.Pointer))continue;inventory.TryGetValue(item.StringTemplateId,out var old);long count=Math.Max(1,item.StackObjectsCount);inventory[item.StringTemplateId]=(old.All+count,old.Fir+(item.SpawnedInSession?count:0));}
  var activeQuests=new List<Quest>();var questSeen=new HashSet<IntPtr>();if(player.QuestController?.Quests!=null)foreach(var quest in player.QuestController.Quests)if(quest!=null&&quest.QuestStatus==EQuestStatus.Started&&questSeen.Add(quest.Pointer))activeQuests.Add(quest);if(profile.QuestsData!=null)foreach(var data in profile.QuestsData)if(data!=null&&data.Status==EQuestStatus.Started&&data.Quest!=null&&questSeen.Add(data.Quest.Pointer))activeQuests.Add(data.Quest);
  foreach(var quest in activeQuests){if(!quest.Conditions.TryGetValue(EQuestStatus.AvailableForFinish,out var conditions))continue;
   var candidates=new List<ConditionItem>();foreach(var condition in conditions){var item=condition?.TryCast<ConditionItem>();if(item!=null&&(condition!.TryCast<ConditionHandoverItem>()!=null||condition.TryCast<ConditionFindItem>()!=null))candidates.Add(item);}
   foreach(var condition in candidates){var targets=condition.target?.ToArray()??Array.Empty<string>();if(targets.Length==0)continue;if(condition.TryCast<ConditionFindItem>()!=null&&candidates.Any(c=>c.TryCast<ConditionHandoverItem>()!=null&&c.target.ToArray().Intersect(targets).Any()))continue;
    if(quest.IsConditionDone(condition))continue;var progress=quest.CalculateConditionProgress(condition);int remaining=Math.Max(0,(int)Math.Ceiling(condition.value-progress.Item1));if(remaining==0)continue;
    string note=condition.minDurability>0?"最低耐久 "+condition.minDurability+"%":"";Needs.Add(new("任务 · "+quest.Name,targets,remaining,condition.onlyFoundInRaid,note));
   }
  }
  hideout??=JsonDocument.Parse(File.ReadAllText(Path.Combine(BepInEx.Paths.GameRootPath,"SPT_Runtime","SPT_Data","database","hideout","areas.json")));
  if(profile.Hideout?.Areas!=null)foreach(var area in profile.Hideout.Areas){if(area==null||area.Constructing)continue;foreach(var definition in hideout.RootElement.EnumerateArray()){if(definition.GetProperty("type").GetInt32()!=area.Type||!definition.TryGetProperty("stages",out var stages)||!stages.TryGetProperty((area.Level+1).ToString(),out var stage))continue;foreach(var requirement in stage.GetProperty("requirements").EnumerateArray()){if(requirement.GetProperty("type").GetString()!="Item")continue;string id=requirement.GetProperty("templateId").GetString()!;int count=(int)requirement.GetProperty("count").GetDouble();bool fir=requirement.TryGetProperty("isSpawnedInSession",out var flag)&&flag.ValueKind==JsonValueKind.True;inventory.TryGetValue(id,out var held);int missing=(int)Math.Max(0,count-(fir?held.Fir:held.All));if(missing>0)Needs.Add(new("藏身处 · "+area.AreaType+" Lv."+(area.Level+1),new[]{id},missing,fir,"按当前已加载库存计算"));}}
  }
  status="只读当前已接任务和藏身处下一等级；任务条目为未完成数量，库存只统计当前已加载物品。";
 }catch(Exception e){status="需求读取不完整："+e.Message;Plugin.Self.Log.LogWarning(status);}}
 internal static void Draw(Rect rect){
  InterfaceMenu.Label(new Rect(rect.x,rect.y,rect.width,28),"任务与藏身处物资",InterfaceMenu.Accent,18);search=InterfaceMenu.Field(new Rect(rect.x,rect.y+40,rect.width-150,30),search);if(InterfaceMenu.Button(new Rect(rect.xMax-138,rect.y+40,138,30),"定位所有需求",RaidLoot.TrackNeeds))RaidLoot.TrackNeeds=!RaidLoot.TrackNeeds;
  var rows=Needs.Where(n=>RaidRules.Match(n.Source+" "+string.Join(" / ",n.Targets.Select(ItemSpawner.TemplateName)),"",search)).ToList();var view=new Rect(rect.x,rect.y+84,rect.width,Math.Max(60,rect.height-150));scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,view.width-18,Math.Max(60,rows.Count*100)));int first=Math.Max(0,(int)(scroll.y/100)),end=Math.Min(rows.Count,first+(int)(view.height/100)+2);
  for(int i=first;i<end;i++){var n=rows[i];float y=i*100;string label=string.Join(" / ",n.Targets.Select(ItemSpawner.TemplateName));InterfaceMenu.Paragraph(new Rect(0,y,view.width-190,90),n.Source+"\n"+label+" · 剩余 "+n.Remaining+(n.Fir?" · 要求战局发现":"")+"\n"+n.Note,null,13);if(InterfaceMenu.Button(new Rect(view.width-178,y+12,150,32),"高亮此需求"))RaidLoot.TrackTemplates(n.Targets);}
  if(rows.Count==0)InterfaceMenu.Label(new Rect(0,0,view.width-24,40),RaidClock.Active?"没有匹配需求，或当前角色没有已接任务。":"进入战局后读取任务与物资需求。",null,13);GUI.EndScrollView();InterfaceMenu.Paragraph(new Rect(rect.x,rect.yMax-55,rect.width,50),status,null,12);
 }
}
