using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Path=System.IO.Path;
using RaidSettings=LocalRaidShared.RaidSettings;
using Patches=LocalRaidControls.Patches;
using HarmonyLib;
using LocalRaidControls;
using LocalRaidShared;
using LocalSpt5Esp;
using Mono.Cecil;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.InRaid;
int checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
void Reject(Action action,string label){try{action();}catch(ArgumentException){Check(true,label);return;}throw new Exception(label);}
Check(TrainTimeRules.Parse("1","2","3")==3723,"train HMS input preserves hour/minute/second units");
Check(TrainTimeRules.Parse("168","0","0")==604800&&TrainTimeRules.Parse("0","0","0")==0,"train duration retains original zero and seven-day bounds");
foreach(var value in new[]{("0","60","0"),("0","0","60"),("-1","0","0"),("1.5","0","0"),("168","0","1")})Reject(()=>TrainTimeRules.Parse(value.Item1,value.Item2,value.Item3),"train rejects invalid HMS input "+value);
Check(TrainTimeRules.Clock(3661.1)=="01:01:02"&&TrainTimeRules.Clock(-1)=="00:00:00"&&TrainTimeRules.Clock(double.NaN)=="--:--:--","train display keeps total hours, rounds remaining seconds up and handles missing timings");
Check(MenuSections.Group("4. AI ignore","Enabled")=="人物与移动"&&MenuSections.OnPage("人物与移动","生存防护","4. AI ignore","Enabled"),"AI ignore routes to character protection");
Check(MenuSections.Group("3. Keys","AI ignore toggle")=="人物与移动"&&MenuSections.OnPage("人物与移动","生存防护","3. Keys","AI ignore toggle"),"AI ignore hotkey follows the same character page");
Check(MenuSections.Group("17. Force doors")=="人物与移动"&&MenuSections.OnPage("人物与移动","交互能力","17. Force doors","Enabled"),"forced breach routes to character interaction abilities");
Check(MenuSections.Group("18. BTR","Stop marker enabled")=="镜头与画面"&&MenuSections.OnPage("镜头与画面","BTR 站点","18. BTR","Stop marker enabled"),"BTR stop visibility routes to visual settings");
Check(MenuSections.Pages("数据").Contains("存档备份")&&MenuSections.Pages("载具与空投").Contains("装甲列车"),"backup and armored-train pages are navigable");
Check(UpdateCheckRules.Endpoint.EndsWith("/releases?per_page=20")&&UpdateCheckRules.Releases.EndsWith("/releases"),"update checks use the experimental-capable release list");
Reject(()=>UpdateCheckRules.Proxy("http://user:secret@localhost:7890"),"proxy credentials belong in separate fields");
Check(UpdateCheckRules.Parse("v2.3")==UpdateCheckRules.Parse("2.3.0"),"equivalent GitHub tag formats do not offer the same release again");
Check(UpdateCheckRules.IsNewer("2.3","v2.3.1")&&UpdateCheckRules.IsNewer("2.3","v2.10")&&!UpdateCheckRules.IsNewer("2.3","v2.2.9"),"patch/minor updates compare numerically and never downgrade");
Reject(()=>UpdateCheckRules.Parse("v2.4-beta"),"prerelease tags are not presented as stable updates");
foreach(string value in new[]{"file:///C:/secret","javascript:alert(1)","http://localhost:7890/path","not a proxy"})Reject(()=>UpdateCheckRules.Proxy(value),"reject malformed or unsupported proxy: "+value);
foreach(string value in new[]{"http://127.0.0.1:7890","https://localhost:7890","socks5://127.0.0.1:1080"})Check(UpdateCheckRules.Proxy(value).Port>0,"supported proxy: "+value);
var edges=new[]{("A","B"),("B","C"),("C","D")};
Check(BtrRouteRules.Reachable("A","D",edges)&&!BtrRouteRules.Reachable("D","A",edges),"BTR dispatch respects directed roads");
Check(BtrRouteRules.Plan("A","D",edges).SequenceEqual(new[]{"B","C","D"})&&BtrRouteRules.Plan("A","A",edges).Length==0,"multi-leg dispatch and already-at-stop behavior");
Check(!new RaidSettings().InstantBtrDelivery,"instant delivery defaults off for existing configurations");
var generationField=typeof(Patches).GetField("generation",BindingFlags.NonPublic|BindingFlags.Static)!;
var finishLocation=typeof(Patches).GetMethod("FinishLocation",BindingFlags.NonPublic|BindingFlags.Static)!;
typeof(Patches).GetField("Log",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,DispatchProxy.Create<ISptLogger<LocalRaidControls.Startup>,QuietLog>());
var nativeLocation=(SPTarkov.Server.Core.Models.Eft.Common.LocationBase)Activator.CreateInstance(typeof(SPTarkov.Server.Core.Models.Eft.Common.LocationBase))!;nativeLocation=nativeLocation with{Id="woods",EscapeTimeLimit=40,EscapeTimeLimitCoop=40,EscapeTimeLimitPVE=40};
try{generationField.SetValue(null,new RaidSettings{NextRaidMinutes=90.5});object?[] state={"woods",nativeLocation};finishLocation.Invoke(null,state);var changed=(SPTarkov.Server.Core.Models.Eft.Common.LocationBase)state[1]!;Check(changed.EscapeTimeLimit==90.5&&changed.EscapeTimeLimitCoop==91&&changed.EscapeTimeLimitPVE==91&&nativeLocation.EscapeTimeLimit==40,"pre-raid duration changes detached native map duration for all raid modes");generationField.SetValue(null,new RaidSettings());object?[] stock={"woods",nativeLocation};finishLocation.Invoke(null,stock);Check(((SPTarkov.Server.Core.Models.Eft.Common.LocationBase)stock[1]!).EscapeTimeLimit==40,"zero next-raid minutes restores stock map duration");}finally{generationField.SetValue(null,null);}
string repo=Path.GetFullPath(args[0]),game=args[1];
using(var core=AssemblyDefinition.ReadAssembly(Path.Combine(repo,"esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll"))){
 Check(core.MainModule.Types.Any(t=>t.FullName=="AllTheModium.Client.SwordModule")&&core.MainModule.Types.Any(t=>t.FullName=="ProjectE.Client.ProjectModule"),"both Minecraft clients are compiled into Assistant");
 Check(!core.MainModule.AssemblyReferences.Any(a=>a.Name is "Local.ModHost.Contracts" or "Local.ProjectE.HotModule" or "Local.AllTheModium.HotModule"),"integrated Minecraft has no dependency on independent hot-module assemblies");
 var draw=core.MainModule.Types.Single(t=>t.FullName=="ProjectE.Client.MinecraftHud").Methods.Single(m=>m.Name=="Draw");Check(draw.Parameters.Count==6&&draw.Parameters.Skip(1).All(p=>p.ParameterType.FullName=="System.Boolean"),"HUD exposes independent hearts/hunger/hydration/experience/hotbar visibility");
 Check(core.Name.Version.Major==2&&core.Name.Version.Minor==3,"client binary release version is 2.3");
}
using(var mail=AssemblyDefinition.ReadAssembly(Path.Combine(repo,"gear-mail/bin/Release/net10.0/LocalGearMail.dll"))){
 Check(mail.MainModule.Types.Count(t=>t.Interfaces.Any(i=>i.InterfaceType.Name=="IModMetadata"))==1,"integrated server has exactly one mod metadata registration");
 Check(mail.MainModule.Types.Any(t=>t.FullName=="LocalGearMail.MinecraftRouter")&&!mail.MainModule.Types.Any(t=>t.Name=="Scp3XRegistration"),"Minecraft router is built in and 3-X registration is absent");
 Check(mail.MainModule.Types.Where(t=>t.FullName is "ProjectE.Server.TableRouter" or "ProjectE.Server.TerminalRouter" or "ProjectE.Server.Startup" or "ProjectE.Server.DeveloperTrader").All(t=>!t.CustomAttributes.Any(a=>a.AttributeType.Name=="InjectableAttribute")),"old MC routers/startups cannot register twice");
}
using(var native=AssemblyDefinition.ReadAssembly(Path.Combine(game,"BepInEx/interop/Assembly-CSharp.dll"))){
 var transfer=native.MainModule.Types.Single(t=>t.FullName=="EFT.TransferItemsController");Check(transfer.Methods.Any(m=>m.Name=="GetGridSizeForProfileId"&&m.ReturnType.Name=="IntVec2"),"expanded grid patch matches this EFT build");
 var btr=native.MainModule.Types.Single(t=>t.FullName=="EFT.BtrTransferItemsController");Check(btr.Methods.Count(m=>m.Name=="GetGridItemsPrice"&&m.Parameters.Count==1)==1&&btr.Methods.Single(m=>m.Name=="GetDeliveryPriceForProfile").ReturnType.FullName=="System.Double","zero-fee patches match concrete native BTR price boundaries");
}
// Exercise the actual native queue, insurance cleanup and package removal, with mail intercepted.
var harmony=new Harmony("assistant.release23.tests");var btrPatch=typeof(Patches).Assembly.GetType("LocalRaidControls.BtrDelivery",true)!;
btrPatch.GetMethod("Install",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{harmony});
harmony.Patch(AccessTools.Method(typeof(BtrDeliveryService),"GetBTRDeliveryReturnTimestamp"),prefix:new HarmonyMethod(typeof(DeliverySpy),nameof(DeliverySpy.Timestamp)));
harmony.Patch(AccessTools.Method(typeof(BtrDeliveryService),"SendBTRDelivery"),prefix:new HarmonyMethod(typeof(DeliverySpy),nameof(DeliverySpy.Send)));
var saves=(SaveServer)RuntimeHelpers.GetUninitializedObject(typeof(SaveServer));
var session=new MongoId();var old=new BtrDelivery{Id=new MongoId(),Items=[new(){Id=new MongoId(),Template=new MongoId()}],ScheduledTime=int.MaxValue};
var profile=new SptProfile{CharacterData=new(){PmcData=new(){InsuredItems=[]}},BtrDeliveryList=[old]};
typeof(SaveServer).GetField("profiles",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(saves,new ConcurrentDictionary<MongoId,SptProfile>(new[]{new KeyValuePair<MongoId,SptProfile>(session,profile)}));
var service=(BtrDeliveryService)RuntimeHelpers.GetUninitializedObject(typeof(BtrDeliveryService));
foreach(var field in typeof(BtrDeliveryService).GetFields(BindingFlags.NonPublic|BindingFlags.Instance)){if(field.FieldType==typeof(SaveServer))field.SetValue(service,saves);if(field.FieldType==typeof(ISptLogger<BtrDeliveryService>))field.SetValue(service,DispatchProxy.Create<ISptLogger<BtrDeliveryService>,QuietLog>());}
var item=new Item{Id=new MongoId(),Template=new MongoId()};var transit=new Item{Id=new MongoId(),Template=new MongoId()};
string settings=Path.Combine(AppContext.BaseDirectory,"user/mods/LocalRaidControls/settings.json");Directory.CreateDirectory(Path.GetDirectoryName(settings)!);File.WriteAllText(settings,JsonSerializer.Serialize(new RaidSettings{InstantBtrDelivery=true}));
var begin=btrPatch.GetMethod("Begin",BindingFlags.NonPublic|BindingFlags.Static)!;var end=btrPatch.GetMethod("End",BindingFlags.NonPublic|BindingFlags.Static)!;
object?[] scope={new EndLocalRaidRequestData{TransferItems=new(){{"656f0f98d80a697f855d34b1_BtrItemsDelivery",new[]{item}},{"656f0f98d80a697f855d34b1_TransitItemsDelivery",new[]{transit}}}},null};
begin.Invoke(null,scope);
try{
 AccessTools.Method(typeof(BtrDeliveryService),"HandleTransferItemDelivery").Invoke(service,new object[]{session,new[]{item}});
 Check(DeliverySpy.Sent.Count==1&&DeliverySpy.Sent.Single()==item.Id&&profile.BtrDeliveryList!.Count==1&&profile.BtrDeliveryList[0].Id==old.Id,"native BTR queue immediately sends only the new package and preserves prior deliveries");
 AccessTools.Method(typeof(BtrDeliveryService),"HandleTransferItemDelivery").Invoke(service,new object[]{session,new[]{transit}});
 Check(DeliverySpy.Sent.Count==1&&profile.BtrDeliveryList!.Count==2,"transit packages retain native delivery timing");
}finally{end.Invoke(null,new[]{scope[1]});}
File.WriteAllText(settings,JsonSerializer.Serialize(new RaidSettings()));
object?[] disabled={scope[0],null};begin.Invoke(null,disabled);try{AccessTools.Method(typeof(BtrDeliveryService),"HandleTransferItemDelivery").Invoke(service,new object[]{session,new[]{item}});Check(DeliverySpy.Sent.Count==1&&profile.BtrDeliveryList!.Count==3,"disabling instant delivery restores native queue behavior");}finally{end.Invoke(null,new[]{disabled[1]});}
harmony.UnpatchSelf();
Console.WriteLine($"Passed {checks} release 2.3 checks.");
public static class DeliverySpy {
 public static readonly List<MongoId> Sent=[];
 public static bool Timestamp(ref double __result){__result=int.MaxValue;return false;}
 public static bool Send(IEnumerable<Item> __1){Sent.AddRange(__1.Select(i=>i.Id));return false;}
}
public class QuietLog:DispatchProxy{protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.ReturnType==typeof(bool)?false:null;}
