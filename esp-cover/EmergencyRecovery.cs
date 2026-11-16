using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
namespace LocalSpt5Esp;
internal static class EmergencyRecovery {
 static Task<string>? backup;static bool stopping,finished;static DateTime deadline;static string result="",localFile="";static IntPtr worldId;
 internal static bool Busy=>!finished&&(backup!=null||stopping)&&DateTime.UtcNow<deadline;
 static void Status(string text){result=text;var host=Plugin.Self?.Host;host?.GetType().GetMethod("SetRecoveryStatus")?.Invoke(host,new object[]{text});}
 internal static string Begin(){
  var world=Singleton<GameWorld>.Instance;var player=world?.MainPlayer;if(!RaidClock.Active||player?.IsYourPlayer!=true){finished=true;return "当前不在战局，无需保存战局物品。";}if(Singleton<AbstractGame>.Instance?.TryCast<LocalGame>()==null){finished=true;return "当前战局不支持本地幸存结算，请导出日志并保留备份。";}
  if(player.HealthController?.IsAlive!=true){finished=true;return "角色已阵亡，不能把已经完成的阵亡结算改为幸存。";}
  deadline=DateTime.UtcNow.AddSeconds(35);worldId=world!.Pointer;finished=stopping=false;
  var root=player.Profile.Inventory.Equipment;var flat=Singleton<ItemFactory>.Instance.TreeToFlatItems(new Item[]{root});string raw=LocalSpt.Shared.RaidStashJson.Normalize(Newtonsoft.Json.JsonConvert.SerializeObject(new Il2CppSystem.Object(flat.Pointer)),out _,out _).ToJsonString();
  string directory=Path.Combine(BepInEx.Paths.PluginPath,"LocalSpt5ESP","recovery");localFile=Path.Combine(directory,DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+"-equipment.json");string profile=player.ProfileId,side=player.Side==EPlayerSide.Savage?"scav":"pmc";
  var remote=CharacterDataPanel.Call("/local/gear/data","characterData",new(){{"action","emergencyBackup"},{"id",profile},{"categories",new[]{side}},{"stashItems",JsonSerializer.Deserialize<JsonElement>(raw)}});
  backup=Task.Run(async()=>{string localError="";try{Directory.CreateDirectory(directory);string temporary=localFile+".next";await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(new{version=1,utc=DateTime.UtcNow,profile,side,items=JsonSerializer.Deserialize<JsonElement>(raw)}));File.Move(temporary,localFile);}catch(Exception e){localError=e.Message;}try{using var doc=JsonDocument.Parse(await remote);var data=CharacterDataPanel.Data(doc.RootElement);if(data.TryGetProperty("ok",out var ok)&&ok.GetBoolean())return "SQLite 救援备份 "+data.GetProperty("backup").GetString();return "服务端救援备份未确认；本地快照："+localFile+" "+localError;}catch(Exception e){return "服务端备份失败："+e.GetBaseException().Message+"；本地快照："+localFile+" "+localError;}});
  return "正在保存当前装备与背包快照，随后请求幸存结算…";
 }
 internal static void Tick(){
  if(finished)return;if(DateTime.UtcNow>=deadline){finished=true;Status(result+"\n救援等待超时；尚不能确认原生结算完成。请保留日志和恢复快照。");return;}
  if(backup?.IsCompleted==true&&!stopping){string saved;try{saved=backup.GetAwaiter().GetResult();}catch(Exception e){saved="救援快照异常："+e.Message;}backup=null;try{if(Singleton<GameWorld>.Instance?.Pointer!=worldId)throw new InvalidOperationException("当前战局已改变。");RaidEnd.ChatExtract();stopping=true;Status(saved+"\n已请求游戏按幸存结算，正在等待退出战局。");}catch(Exception e){finished=true;Status(saved+"\n幸存结算请求失败："+e.GetBaseException().Message);}return;}
  if(stopping){RaidStash.Tick();if(Singleton<GameWorld>.Instance?.Pointer!=worldId||!RaidClock.Active){finished=true;Status(result+"\n游戏已退出战局。最终物品以服务端存档为准，救援快照可用于恢复。");}}
 }
}
