using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using Microsoft.AspNetCore.Http;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Game;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Profile;
using SPTarkov.Server.Core.Utils;
namespace LocalGearMail;

// Map once, at the HTTP boundary. Native services, caches and async saves then
// receive the actual account ID throughout the request; SaveServer is untouched.
public static class SeasonProfiles {
 const string PatchId="local.spt5.independent-season";
 static SeasonProfileStore store=null!;
 [ThreadStatic] static bool describing;
 public static bool Install(SaveServer saves,JsonUtil json){
  var candidate=new SeasonProfileStore(saves,json,Path.Combine(AppContext.BaseDirectory,"user","mods","LocalGearMail","seasons"));
  Install(candidate);return true;
 }
 public static void Install(SeasonProfileStore candidate){
  var h=new Harmony(PatchId);h.UnpatchSelf();store=candidate;
  h.Patch(AccessTools.Method(typeof(HttpRouter),nameof(HttpRouter.GetResponseObjectAsync)),prefix:new HarmonyMethod(typeof(SeasonProfiles),nameof(Route)));
  h.Patch(AccessTools.Method(typeof(GameController),nameof(GameController.GetGameMode)),prefix:new HarmonyMethod(typeof(SeasonProfiles),nameof(Select)));
  h.Patch(AccessTools.Method(typeof(ProfileController),nameof(ProfileController.GetCharacterSelectionProfiles)),postfix:new HarmonyMethod(typeof(SeasonProfiles),nameof(Describe)));
  h.Patch(AccessTools.Method(typeof(CreateProfileService),nameof(CreateProfileService.CreateProfile)),prefix:new HarmonyMethod(typeof(SeasonProfiles),nameof(Create)));
 }
 static void Route(HttpRequest __0,ref MongoId __1)=>__1=store.Route(__1,__0.Path.Value??"");
 // Keep the native PVE response and local raid pipeline. Only storage changes.
 static void Select(MongoId __0,GameModeRequestData __1)=>store.Select(__0,__1.SessionMode);
 static void Describe(ProfileController __instance,MongoId __0,ref Dictionary<string,CharacterSelectionProfileData> __result){
  if(describing)return;
  var owner=store.Owner(__0);
  try{
   describing=true;
   if(owner!=__0)__result=__instance.GetCharacterSelectionProfiles(owner);
   var target=store.Existing(owner);
   __result["pvp-season"]=target is {} id?__instance.GetCharacterSelectionProfiles(id)["pve"]:new(){Status="empty"};
  }finally{describing=false;}
 }
 // A repeated creation request must never reset an already created season.
 static bool Create(MongoId __0,ref ValueTask<string> __result){
  if(store.IsSeason(__0)&&store.Profile(__0).CharacterData?.PmcData?.Info!=null){__result=ValueTask.FromResult(__0.ToString());return false;}
  return true;
 }
}

public sealed record SeasonLink(string Owner,string Target,bool Active);
public sealed class SeasonProfileStore {
 public const string OwnerMarker="localIndependentSeasonOwner";
 readonly SaveServer saves;readonly JsonUtil json;readonly string directory;
 readonly Action<MongoId> persist;readonly object gate=new();readonly Dictionary<string,SeasonLink> links=[];
 public SeasonProfileStore(SaveServer saves,JsonUtil json,string directory,Action<MongoId>? persist=null){
  this.saves=saves;this.json=json;this.directory=directory;this.persist=persist??(id=>saves.SaveProfileAsync(id).GetAwaiter().GetResult());
  if(Directory.Exists(directory))foreach(var file in Directory.EnumerateFiles(directory,"*.json")){
   var link=JsonSerializer.Deserialize<SeasonLink>(File.ReadAllText(file))??throw new IOException("赛季档案关联无法读取。");
   if(!Valid(link.Owner)||!Valid(link.Target)||link.Owner==link.Target||Path.GetFileNameWithoutExtension(file)!=link.Owner||links.Values.Any(x=>x.Target==link.Target))throw new IOException("赛季档案关联无效。");
   links.Add(link.Owner,link);
  }
 }
 static bool Valid(string value)=>value.Length==24&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
 public SptProfile Profile(MongoId id)=>saves.GetProfile(id);
 public MongoId Owner(MongoId id){lock(gate){var key=id.ToString();return new MongoId(links.Values.FirstOrDefault(x=>x.Target==key)?.Owner??key);}}
 public bool IsSeason(MongoId id){lock(gate)return links.Values.Any(x=>x.Target==id.ToString());}
 void Verify(MongoId owner,MongoId target){
  if(owner==target||!saves.ProfileExists(target))throw new ArgumentException("独立赛季档案缺失，请检查服务端备份。");
  var profile=Profile(target);object? marker=null;
  if(profile.ProfileInfo?.ProfileId!=target||profile.ProfileInfo.ExtensionData?.TryGetValue(OwnerMarker,out marker)!=true||marker?.ToString()!=owner.ToString())throw new ArgumentException("赛季档案身份不匹配，已阻止切换。");
 }
 public MongoId? Existing(MongoId id){lock(gate){var owner=Owner(id);if(!links.TryGetValue(owner.ToString(),out var link))return null;var target=new MongoId(link.Target);Verify(owner,target);return target;}}
 public MongoId Route(MongoId session,string path){
  if(session.IsEmpty)return session;
  lock(gate){
   // Launcher/account operations retain their account identity. Selection must
   // see both slots, regardless of which character is currently loaded.
   if(path.StartsWith("/launcher/",StringComparison.Ordinal))return session;
   if(path is "/client/game/mode" or "/v2/client/game/profiles/")return Owner(session);
   var owner=Owner(session);
   if(!links.TryGetValue(owner.ToString(),out var link)||!link.Active)return owner;
   var target=new MongoId(link.Target);Verify(owner,target);return target;
  }
 }
 static void OutOfRaid(SptProfile profile){var location=profile.InraidData?.Location;if(!string.IsNullOrEmpty(location)&&location is not ("none" or "hideout"))throw new ArgumentException("请退出战局后再切换角色。");}
 void Write(SeasonLink link){Directory.CreateDirectory(directory);string path=Path.Combine(directory,link.Owner+".json");File.WriteAllText(path+".next",JsonSerializer.Serialize(link));File.Move(path+".next",path,true);links[link.Owner]=link;}
 public MongoId Select(MongoId session,string? mode){lock(gate){
  if(mode is not ("pve" or "pvp-season"))return session;
  var owner=Owner(session);var original=Profile(owner);OutOfRaid(original);
  var existing=Existing(owner);if(existing is {} previous)OutOfRaid(Profile(previous));
  if(mode=="pve"){
   if(links.TryGetValue(owner.ToString(),out var link)&&link.Active){persist(new MongoId(link.Target));Write(link with{Active=false});}
   return owner;
  }
  if(existing is {} current){if(!links[owner.ToString()].Active){persist(owner);Write(links[owner.ToString()] with{Active=true});}return current;}
  if(original.ProfileInfo==null)throw new ArgumentException("当前账号尚未就绪。");
  var target=new MongoId(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(owner+":local-season-v1")))[..24]);
  if(target==owner)throw new ArgumentException("赛季角色身份冲突。");
  // A deterministic ID recovers an interrupted first selection without making
  // a second character. Never adopt an unrelated profile at that ID.
  if(saves.ProfileExists(target))Verify(owner,target);
  else{
   Directory.CreateDirectory(Path.Combine(directory,"backups"));
   File.WriteAllText(Path.Combine(directory,"backups",owner+"-before-season.json"),json.Serialize(original,true));
   persist(owner);
   var info=new Info{ProfileId=target,ScavengerId=new MongoId(),Aid=checked(saves.GetProfiles().Values.Max(x=>x.ProfileInfo?.Aid??0)+1),Username=original.ProfileInfo.Username+" (Season)",Edition=original.ProfileInfo.Edition,IsWiped=true,TotalInGame=0};
   info.ExtensionData??=[];info.ExtensionData[OwnerMarker]=owner.ToString();saves.CreateProfile(info);
  }
  persist(target);Write(new(owner.ToString(),target.ToString(),true));return target;
 }}
}
