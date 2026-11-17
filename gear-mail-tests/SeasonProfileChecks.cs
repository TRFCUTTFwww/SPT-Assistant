using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using LocalGearMail;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Game;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Profile;
using SPTarkov.Server.Core.Utils;
using ProfileInfo=SPTarkov.Server.Core.Models.Eft.Profile.Info;
internal static class SeasonProfileChecks {
 internal static void Run(JsonUtil json){
  void Check(bool value,string message){if(!value)throw new Exception(message);}
  void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Unsafe seasonal operation accepted");}
  string directory=Path.Combine(Path.GetTempPath(),"spt-season-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
  try{
   var saves=new SaveServer(null!,[],json,null!,null!,null!,null!,null!);
   var owner=new MongoId();var unrelated=new MongoId();
   SptProfile Fresh(MongoId id)=>new(){ProfileInfo=new ProfileInfo{ProfileId=id,ScavengerId=new MongoId(),Aid=id==owner?1:2,Edition="Standard",Username="fixture"},CharacterData=new(){PmcData=new(){Id=id,Info=new(){Nickname="fixture",Level=42},Inventory=new(){Items=[]},Quests=[]},ScavData=new(){Id=new MongoId()}},InraidData=new(){Location="none"}};
   saves.AddProfile(Fresh(owner));saves.AddProfile(Fresh(unrelated));
   string original=json.Serialize(saves.GetProfile(owner))!,other=json.Serialize(saves.GetProfile(unrelated))!;
   var saved=new List<MongoId>();
   void Persist(MongoId id){saved.Add(id);File.WriteAllText(Path.Combine(directory,id+".profile"),json.Serialize(saves.GetProfile(id)));}
   var store=new SeasonProfileStore(saves,json,directory,Persist);
   Check(store.Existing(owner)==null&&saves.GetProfiles().Count==2,"Inspecting slots created a real character");
   var target=store.Select(owner,"pvp-season");var season=saves.GetProfile(target);
   Check(target!=owner&&target!=unrelated&&season.ProfileInfo?.ProfileId==target&&season.ProfileInfo.ScavengerId!=saves.GetProfile(owner).ProfileInfo?.ScavengerId,"Season account identity was aliased");
   Check(season.CharacterData?.PmcData?.Info==null&&season.CharacterData?.PmcData?.Inventory==null&&season.DialogueRecords==null&&season.ProfileInfo?.IsWiped==true,"New season inherited existing character progress");
   Check(json.Serialize(saves.GetProfile(owner))==original&&json.Serialize(saves.GetProfile(unrelated))==other,"Season selection changed an existing profile");
   Check(File.Exists(Path.Combine(directory,"backups",owner+"-before-season.json")),"Original profile backup missing");
   Check(store.Select(owner,"pvp-season")==target&&saves.GetProfiles().Count==3,"Repeated selection created duplicate season");
   foreach(string path in new[]{"/client/game/profile/list","/client/game/profile/create","/client/game/profile/items/moving","/client/notifier/channel/create","/local/gear/data"})Check(store.Route(owner,path)==target,"Native route missed independent profile: "+path);
   Check(store.Route(owner,"/v2/client/game/profiles/")==owner&&store.Route(target,"/client/game/mode")==owner&&store.Route(owner,"/launcher/profile/info")==owner&&store.Route(unrelated,"/client/game/profile/list")==unrelated,"Control/launcher/other account routing leaked");
   var captured=store.Route(owner,"/client/game/profile/list");store.Select(target,"pve");
   Check(captured==target&&store.Route(owner,"/client/game/profile/list")==owner&&store.Route(target,"/client/game/profile/list")==owner,"Switch changed a captured request or trapped secondary session");
   // Simulate native template creation in the detached account, then persist and reload.
   season.CharacterData=new(){PmcData=new(){Id=target,Info=new(){Nickname="new-season",Level=1},Inventory=new(){Items=[]},Quests=[]},ScavData=new(){Id=season.ProfileInfo!.ScavengerId}};
   season.InraidData=new(){Location="none"};Persist(target);store.Select(owner,"pvp-season");
   var restored=new SeasonProfileStore(saves,json,directory,Persist);
   Check(restored.Existing(owner)==target&&restored.Route(owner,"/client/game/profile/list")==target,"Persistent season association was lost");
   season.CharacterData.PmcData.Info.Level=2;Persist(target);
   Check(saves.GetProfile(owner).CharacterData!.PmcData!.Info!.Level==42&&json.Serialize(saves.GetProfile(owner))==original,"Season progress leaked into original profile");
   season.InraidData.Location="bigmap";Reject(()=>restored.Select(owner,"pve"));Check(restored.Route(owner,"/client/game/profile/list")==target,"Rejected in-raid switch changed active profile");season.InraidData.Location="none";
   var marker=season.ProfileInfo!.ExtensionData![SeasonProfileStore.OwnerMarker];season.ProfileInfo.ExtensionData[SeasonProfileStore.OwnerMarker]=unrelated.ToString();Reject(()=>restored.Route(owner,"/client/game/profile/list"));season.ProfileInfo.ExtensionData[SeasonProfileStore.OwnerMarker]=marker;
   // Exercise the actual Harmony submission/create hooks on native server types.
   SeasonProfiles.Install(restored);
   var create=(CreateProfileService)RuntimeHelpers.GetUninitializedObject(typeof(CreateProfileService));
   Check(create.CreateProfile(target,new(){Nickname="must-not-reset"}).GetAwaiter().GetResult()==target.ToString()&&season.CharacterData.PmcData.Info.Level==2,"Duplicate native create reset a populated season");
   var game=(GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
   var helper=(HttpServerHelper)RuntimeHelpers.GetUninitializedObject(typeof(HttpServerHelper));
   typeof(HttpServerHelper).GetField("<httpConfig>P",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(helper,json.Deserialize<HttpConfig>(File.ReadAllText("D:/SPT-5.0.0-48558/SPT_Runtime/SPT_Data/configs/http.json"))!);
   typeof(GameController).GetField("<httpServerHelper>P",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(game,helper);
   var response=game.GetGameMode(owner,new GameModeRequestData{SessionMode="pve"});
   Check(response.GameMode=="pve"&&restored.Route(owner,"/client/game/profile/list")==owner,"Native PVE response or return to original profile changed");
   response=game.GetGameMode(owner,new GameModeRequestData{SessionMode="pvp-season"});
   Check(response.GameMode=="pve"&&restored.Route(owner,"/client/game/profile/list")==target,"Native season request did not choose independent PVE storage");
   Check(json.Serialize(saves.GetProfile(owner))==original&&json.Serialize(saves.GetProfile(unrelated))==other,"Native hook modified existing accounts");
   new Harmony("local.spt5.independent-season").UnpatchSelf();
   // Crash between native empty-account save and association publication is recoverable.
   string recovery=Path.Combine(directory,"recovery");var recovering=new SeasonProfileStore(saves,json,recovery,Persist);
   Check(recovering.Select(owner,"pvp-season")==target&&saves.GetProfiles().Count==3,"Interrupted first selection created duplicate account");
   var corrupted=saves.GetProfile(target).ProfileInfo!.ExtensionData!;corrupted[SeasonProfileStore.OwnerMarker]="invalid";Reject(()=>recovering.Select(owner,"pve"));corrupted[SeasonProfileStore.OwnerMarker]=marker;
   Console.WriteLine("PASS independent seasonal account/native hooks, blank start, duplicate creation, profile isolation, HTTP routing, captured requests, persistence, recovery, identity and raid guards");
  }finally{new Harmony("local.spt5.independent-season").UnpatchSelf();Directory.Delete(directory,true);}
 }
}
