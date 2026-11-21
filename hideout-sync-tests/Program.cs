using System.Reflection;
using System.Text.Json;
using Comfort.Common;
using EFT;
using EFT.Hideout;
using LocalSpt5Esp;
using UnityEngine;

static class Tests {
 static readonly Type Craft=typeof(HideoutCraftControl),Circle=typeof(CultistCircleControl);
 const BindingFlags Flags=BindingFlags.Static|BindingFlags.NonPublic;
 static void Set(Type type,string name,object? value)=>type.GetField(name,Flags)!.SetValue(null,value);
 static T? Get<T>(Type type,string name)=>(T?)type.GetField(name,Flags)!.GetValue(null);
 static void Call(Type type,string name,params object[] args)=>type.GetMethod(name,Flags)!.Invoke(null,args);
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void At(float time,Action tick){Time.unscaledTime=time;tick();}
 static void Option(string name,bool value)=>FeatureConfig.Entries[name].Value=value;
 static TarkovApplication Reset(bool hideout=false){
  HideoutCraftControl.Shutdown();CultistCircleControl.Shutdown();Set(Craft,"draining",false);Set(Circle,"draining",false);
  foreach(var type in new[]{Craft,Circle})Set(type,"next",0f);Set(Circle,"visibleUntil",0f);
  Plugin.Self=new();FeatureConfig.Entries.Clear();HideoutCraftControl.Initialize(Plugin.Self);CultistCircleControl.Initialize(Plugin.Self);
  CharacterDataPanel.Calls.Clear();CharacterDataPanel.Handler=_=>new TaskCompletionSource<string>().Task;
  InterfaceMenu.Click=_=>false;InterfaceMenu.Closes=0;CultistCircleControl.OpenStash=null;CultistCircleControl.IsStashOpen=null;
  GUI.enabled=true;Time.unscaledTime=0;EFT.UI.ItemUiContext.Instance=null;
  var app=new TarkovApplication();Singleton<TarkovApplication>.Instance=app;Singleton<AbstractApplication>.Instance=null;
  Singleton<AbstractGame>.Instance=hideout?new HideoutGame{InRaid=true}:null;return app;
 }
 static Profile Ready(TarkovApplication app,string id="p"){
  var profile=new Profile{Id=id};app._menuOperation=new Menu{Read=()=>new QuestController{Profile=profile}};return profile;
 }
 static CultistsProducer Live(TarkovApplication app){
  var producer=new CultistsProducer();producer.ProducingItems["ritual"]=new();producer.Schemes["ritual"]=new();
  app._hideout=new(){ProductionController=new()};app._hideout.ProductionController._areaProducers[27]=[producer];return producer;
 }
 static string Ritual(bool ready,string owner="p")=>JsonSerializer.Serialize(new{err=0,data=new{profileId=owner,active=true,revision="rev",production=new{id="ritual",startTimestamp=1234,productionTime=100,progress=0,inProgress=!ready,availableForFinish=ready},items=new[]{new{id="reward",tpl="tpl",count=3}}}});
 static string Crafts()=>JsonSerializer.Serialize(new{err=0,data=new{profileId="p",productions=new[]{new{id="ritual",startTimestamp=1234,productionTime=100,progress=100,availableForFinish=true}}}});
 static void Run(string name,Action check){check();Console.WriteLine("PASS "+name);}
 static void Main(){
  Run("hideout world works when the AbstractGame singleton is absent",()=>{
   var app=Reset();Singleton<GameWorld>.Instance=new HideoutGameWorld{MainPlayer=new(){Profile=new(){Id="p"}}};Live(app);
   CharacterDataPanel.Handler=_=>Task.FromResult(Ritual(true));int synced=0;CultistCircleControl.SyncStash=_=>synced++;
   CultistCircleControl.Tick();At(3,CultistCircleControl.Tick);
   Check(CultistCircleControl.InHideout&&HideoutCraftControl.CurrentProfile?.Id=="p"&&synced==1,"Hideout world was not recognized or reward sync did not run");
   Singleton<GameWorld>.Instance=null;CultistCircleControl.SyncStash=null;
  });
  Run("unready character never calls native GetProfileForLocalGame; hideout controller fallback is allowed",()=>{
   var app=Reset();EFT.UI.ItemUiContext.Instance=new(){QuestController=new(){Profile=new(){Id="stale"}}};
   Check(HideoutCraftControl.CurrentProfile==null,"Unloaded menu reused stale UI profile");
   Option("Instant hideout crafts",true);Option("Instant cultist circle",true);
   for(int frame=0;frame<120;frame++)At(frame/60f,()=>{HideoutCraftControl.Tick();CultistCircleControl.Tick();});
   Check(app.UnsafeCalls==0&&Plugin.Self.Errors==0&&CharacterDataPanel.Calls.Count==0,"Unready character threw or submitted work");
   Singleton<AbstractGame>.Instance=new HideoutGame{InRaid=true};Check(HideoutCraftControl.CurrentProfile?.Id=="stale","Hideout could not use its UI quest controller");
   Ready(app);Check(HideoutCraftControl.CurrentProfile?.Id=="p","Menu profile must take priority over stale UI profile");
  });
  Run("cultist exceptions wait ten seconds before any native read, including manual refresh/toggle",()=>{
   var app=Reset();app._menuOperation=new Menu{Read=()=>throw new NullReferenceException("loading")};Option("Instant cultist circle",true);
   CultistCircleControl.Tick();Check(Plugin.Self.Errors==1,"Initial error not reported");
   for(int frame=1;frame<600;frame++)At(frame/60f,()=>{CultistCircleControl.Tick();Call(Circle,"Begin",false);});
   InterfaceMenu.Click=text=>text.StartsWith("仪式圈自动秒完成");CultistCircleControl.Draw(new(0,0,600,600));
   Option("Instant cultist circle",true);CultistCircleControl.Tick();
   Check(app._menuOperation.Reads==1&&Plugin.Self.Errors==1,"Cooldown repeated a native read or logged again");
   At(10,CultistCircleControl.Tick);Check(app._menuOperation.Reads==2&&Plugin.Self.Errors==2,"Retry did not run at ten seconds");
  });
  Run("ordinary craft completion survives temporary profile loss and resumes without another server mutation",()=>{
   var app=Reset();var profile=Ready(app);var producer=Live(app);Option("Instant hideout crafts",true);CharacterDataPanel.Handler=_=>Task.FromResult(Crafts());
   HideoutCraftControl.Tick();Option("Instant hideout crafts",false);app._menuOperation=null;
   At(2,HideoutCraftControl.Tick);Check(Get<Task<string>>(Craft,"request")!=null&&producer.Updates==0,"Lost pending result while profile was loading");
   app._menuOperation=new(){Read=()=>new(){Profile=profile}};At(4,HideoutCraftControl.Tick);
   Check(producer.Updates==1&&CharacterDataPanel.Calls.Count==1,"Deferred craft response was not applied exactly once");
  });
  Run("cultist response and captured process survive temporary character loading",()=>{
   var app=Reset(true);var profile=Ready(app);var producer=Live(app);CharacterDataPanel.Handler=_=>Task.FromResult(Ritual(true));
   CultistCircleControl.Tick();app._menuOperation=null;At(3,CultistCircleControl.Tick);
   Check(Get<Task<string>>(Circle,"request")!=null&&producer.Updates==0,"Lost response or applied without a ready profile");
   app._menuOperation=new(){Read=()=>new(){Profile=profile}};At(5,CultistCircleControl.Tick);Check(producer.Updates==1,"Did not resume matching ritual sync");
  });
  Run("hideout area construction completes and calls DecideStatus",()=>{
   var app=Reset();var profile=Ready(app);var area=new AreaInfo{Type=4,Level=1,Constructing=true,CompleteTime=new((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()+3600)};
   var areaData=new AreaData{Template=new(){Type=EAreaType.Generator},CurrentLevel=1};
   app._hideout=new(){Areas=[area],AreaDatas=[areaData]};
   CharacterDataPanel.Handler=_=>Task.FromResult(JsonSerializer.Serialize(new{err=0,data=new{profileId="p",upgradesCompleted=1,areas=new[]{new{type=4,level=1,constructing=true,completeTime=100}}}}));
   Option("Instant hideout upgrades",true);HideoutCraftControl.Tick();At(2,HideoutCraftControl.Tick);
   Check(area.CompleteTime.Value==100&&areaData.StatusUpdates==1,"Area upgrade sync did not update CompleteTime and DecideStatus");
  });
  Run("natural completion in hideout syncs native reward availability with preview and instant completion off",()=>{
   var app=Reset(true);var profile=Ready(app);var producer=Live(app);Option("Preview cultist rewards",false);CharacterDataPanel.Handler=_=>Task.FromResult(Ritual(true));
   var saved=new ProductionData{StartTimestamp=1234,ProductionTime=120,Progress=20,InProgress=true};profile.Hideout!.Production["ritual"]=saved;
   CultistCircleControl.Tick();At(3,CultistCircleControl.Tick);
   Check(producer.Updates==1&&producer.ProducingItems["ritual"] is {Progress:1,AvailableForFinish:true,_producingProcess.IsInProgress:false},"Native timer remained active after server completion");
   Check(saved is {ProductionTime:100,Progress:0,InProgress:false,AvailableForFinish:true},"Local profile lost the native server completion format");
   At(6,CultistCircleControl.Tick);Check(producer.Updates==1,"Repeated status reapplied the completed ritual");
   Check(CharacterDataPanel.Calls.All(p=>(string)p["action"]! == "cultistStatus"),"Natural completion silently finished or claimed a ritual");
  });
  Run("entering the hideout replaces a captured producer and retries with a fresh capture",()=>{
   var app=Reset();Ready(app);var previous=Live(app);Option("Instant cultist circle",true);
   CharacterDataPanel.Handler=_=>Task.FromResult(Ritual(true));CultistCircleControl.Tick();
   Singleton<AbstractGame>.Instance=new HideoutGame{InRaid=true};var current=Live(app);
   At(3,CultistCircleControl.Tick);Check(previous.Updates==0&&current.Updates==0,"Stale response updated a producer after the transition");
   At(6,CultistCircleControl.Tick);Check(previous.Updates==0&&current.Updates==1,"Fresh status did not update the visible producer");
  });
  Run("a new ritual with the same recipe rejects an old completion",()=>{
   var app=Reset(true);var profile=Ready(app);var producer=Live(app);
   CharacterDataPanel.Handler=_=>Task.FromResult(Ritual(true));CultistCircleControl.Tick();
   profile.Hideout!.Production["ritual"]=new(){StartTimestamp=2345,ProductionTime=100,InProgress=true};
   At(3,CultistCircleControl.Tick);Check(producer.Updates==0,"Completion was applied to a later sacrifice using the same recipe");
  });
  Run("running ritual stays running in hideout unless instant completion is enabled",()=>{
   var app=Reset(true);Ready(app);var producer=Live(app);Option("Preview cultist rewards",false);CharacterDataPanel.Handler=_=>Task.FromResult(Ritual(false));
   CultistCircleControl.Tick();At(3,CultistCircleControl.Tick);Check(producer.Updates==0&&CharacterDataPanel.Calls.All(p=>(string)p["action"]! == "cultistStatus"),"Hideout watching accelerated the ritual");
  });
  Run("hideout switch, raid entry, owner switches and replaced process prevent inappropriate sync",()=>{
   var app=Reset(true);Ready(app);Live(app);Option("Preview cultist rewards",false);Option("Allow cultist rewards in hideout",false);CultistCircleControl.Tick();Check(CharacterDataPanel.Calls.Count==0,"Disabled hideout option still polled");
   app=Reset(true);Ready(app);var producer=Live(app);CharacterDataPanel.Handler=_=>Task.FromResult(Ritual(true));CultistCircleControl.Tick();Ready(app,"other");At(3,CultistCircleControl.Tick);Check(producer.Updates==0,"Cross-profile result accepted");
   app=Reset(true);Ready(app);producer=Live(app);CharacterDataPanel.Handler=_=>Task.FromResult(Ritual(true));CultistCircleControl.Tick();producer.ProducingItems["ritual"]._producingProcess=new(){Pointer=new(999)};At(3,CultistCircleControl.Tick);Check(producer.Updates==0,"Old response updated a replacement process");
   app=Reset(true);Ready(app);producer=Live(app);CharacterDataPanel.Handler=_=>Task.FromResult(Ritual(true));CultistCircleControl.Tick();Singleton<AbstractGame>.Instance=new(){InRaid=true};At(3,CultistCircleControl.Tick);Check(producer.Updates==0&&CharacterDataPanel.Calls.Count==1,"Raid entry synced or requested hideout production");
  });
  Run("cultist sync allows duration mismatch and invokes stash sync callback in hideout",()=>{
   Check(CultistCircleSyncRules.CanApply("p","p",false,1,1,2,2,100.0,1234,5,true,false),"CanApply rejected ritual with overridden server duration");
   var app=Reset(true);Ready(app);var producer=Live(app);int stashSyncs=0;CultistCircleControl.SyncStash=_=>stashSyncs++;
   CharacterDataPanel.Handler=_=>Task.FromResult(Ritual(true));
   CultistCircleControl.Tick();At(3,CultistCircleControl.Tick);
   Check(producer.Updates==1&&stashSyncs==1,"Completed ritual in hideout did not invoke SyncStash");
   CharacterDataPanel.Handler=_=>Task.FromResult(JsonSerializer.Serialize(new{err=0,data=new{profileId="p",active=false,completed=false,revision="",production=(object?)null,items=Array.Empty<object>()}}));
   At(6,CultistCircleControl.Tick);
   Check(stashSyncs==2,"Inactive ritual in hideout with orphaned stash items did not invoke SyncStash");
   CultistCircleControl.SyncStash=null;
  });
  Run("cultist reward transfer opens only on a manual click for the current hideout profile",()=>{
   foreach(var (active,ready,rewards,allowed) in new[]{(true,true,true,true),(false,false,true,true),(true,false,true,false),(false,false,false,false)}){
    var app=Reset(true);Ready(app);Live(app);
    var production=active?new{id="ritual",startTimestamp=1234,productionTime=100,inProgress=!ready,availableForFinish=ready}:null;
    var stashItems=rewards?new[]{new{_id="reward"}}:[];
    CharacterDataPanel.Handler=_=>Task.FromResult(JsonSerializer.Serialize(new{err=0,data=new{profileId="p",active,production,items=Array.Empty<object>(),stashId="stash",stashItems}}));
    int opens=0;
    var native=new Il2CppSystem.Threading.Tasks.Task<bool>();
    CultistCircleControl.OpenStash=data=>{opens++;return data.GetProperty("stashId").GetString()=="stash"?native:null;};
    CultistCircleControl.IsStashOpen=()=>true;
    CultistCircleControl.Tick();At(3,CultistCircleControl.Tick);
    Check(opens==0,"Polling opened the transfer screen");
    InterfaceMenu.Click=text=>text=="打开仪式圈奖励转移";
    CultistCircleControl.Draw(new(0,0,600,600));
    Check(opens==0&&InterfaceMenu.Closes==0,"GUI event opened the screen before Update");
    At(3.1f,CultistCircleControl.Tick);
    int expected=allowed?1:0;
    Check(opens==expected&&InterfaceMenu.Closes==0,"Manual open did not match reward availability");
    if(!allowed)continue;
    CultistCircleControl.Draw(new(0,0,600,600));
    At(3.2f,CultistCircleControl.Tick);
    Check(opens==1&&InterfaceMenu.Closes==0,"A second click replaced the in-flight open");
    native.IsCompleted=true;native.Result=true;
    At(3.3f,CultistCircleControl.Tick);
    Check(InterfaceMenu.Closes==1,"Successful native open did not close the menu");
    Option("Allow cultist rewards in hideout",false);
    Call(Circle,"OpenRewards");At(3.4f,CultistCircleControl.Tick);
    Option("Allow cultist rewards in hideout",true);
    Call(Circle,"OpenRewards");Ready(app,"other");At(3.5f,CultistCircleControl.Tick);
    Ready(app);Call(Circle,"OpenRewards");Singleton<AbstractGame>.Instance=new(){InRaid=true};At(3.6f,CultistCircleControl.Tick);
    Singleton<AbstractGame>.Instance=new HideoutGame{InRaid=true};
    CultistCircleControl.OpenStash=_=>null;
    Call(Circle,"OpenRewards");At(3.7f,CultistCircleControl.Tick);
    Check(opens==expected&&InterfaceMenu.Closes==expected,"A stale request or missing live reward closed the menu");
    Check(CharacterDataPanel.Calls.All(p=>(string)p["action"]! == "cultistStatus"),"Opening the screen submitted a ritual mutation");
   }
  });
  Run("cultist transfer failures keep the menu open and remain visible through polling",()=>{
   var app=Reset(true);Ready(app);Live(app);
   CharacterDataPanel.Handler=_=>Task.FromResult(JsonSerializer.Serialize(new{err=0,data=new{profileId="p",active=false,items=Array.Empty<object>(),stashId="stash",stashItems=new[]{new{_id="reward"}}}}));
   CultistCircleControl.Tick();At(3,CultistCircleControl.Tick);
   var native=new Il2CppSystem.Threading.Tasks.Task<bool>{IsCompleted=true};
   CultistCircleControl.OpenStash=_=>native;
   CultistCircleControl.IsStashOpen=()=>false;
   foreach(var result in new[]{false,true}){
    native.Result=result;
    Call(Circle,"OpenRewards");CultistCircleControl.Tick();
    Check(InterfaceMenu.Closes==0&&Get<string>(Circle,"transferStatus")!.StartsWith("游戏未打开"),"Rejected or unconfirmed transfer closed the menu");
   }
   At(6,CultistCircleControl.Tick);
   Check(Get<string>(Circle,"transferStatus")!.StartsWith("游戏未打开"),"Automatic status refresh cleared the transfer failure");
   native.IsFaulted=true;
   Call(Circle,"OpenRewards");CultistCircleControl.Tick();
   Check(InterfaceMenu.Closes==0&&Get<string>(Circle,"transferStatus")!.StartsWith("无法打开"),"Native transfer fault was not surfaced");
  });
 }
}
