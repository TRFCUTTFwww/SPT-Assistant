// Managed stand-ins let the real controls run through loading, exceptions,
// frame timing and responses without a game process or profile mutations.
namespace UnityEngine {
 public class Object { public static T? FindObjectOfType<T>() where T:class=>null; }
 public static class Time { public static float unscaledTime; }
 public struct Vector2 { }
 public struct Rect(float x,float y,float width,float height) { public float x=x,y=y,width=width,height=height;public float xMax=>x+width; }
 public static class GUI {public static bool enabled=true;public static Vector2 BeginScrollView(Rect a,Vector2 b,Rect c)=>b;public static void EndScrollView(){} }
}
namespace Comfort.Common { public static class Singleton<T> {public static T? Instance;} }
namespace BepInEx.Configuration {public class ConfigEntry<T>(T value){public T Value=value;} }
namespace HarmonyLib {
 public class HarmonyMethod {public HarmonyMethod(Type type,string name){} }
 public static class AccessTools {public static object Method(Type type,string name)=>new();}
}
namespace EFT {
 public class Native { public T? TryCast<T>() where T:class=>this as T; }
 public class AbstractApplication:Native { }
 public class AbstractGame:Native {public bool InRaid;}
 public class HideoutGame:AbstractGame { }
 public class GameWorld:Native {public Player? MainPlayer;}
 public class HideoutGameWorld:GameWorld { }
 public class Player {public Profile? Profile;}
 public class TarkovApplication:AbstractApplication {
  public Menu? _menuOperation;public Hideout.HideoutRepresentation? _hideout;public int UnsafeCalls;
  public Profile? GetProfileForLocalGame(){UnsafeCalls++;throw new NullReferenceException("Native profile is not ready");}
 }
 public class Menu {public Func<QuestController?> Read=()=>null;public int Reads;public QuestController? QuestController {get{Reads++;return Read();}}}
 public class QuestController {public Profile? Profile;}
 public class Profile {public string Id="p";public HideoutData? Hideout=new();}
 public class HideoutData {public Dictionary<string,Hideout.ProductionData> Production=new();}
 public class AreaInfo { public int Type;public int Level;public Il2CppSystem.Nullable<int> CompleteTime;public bool Constructing; }
}
namespace Il2CppSystem { public struct Nullable<T>(T value) where T:struct { public bool HasValue=>true;public T Value=>value; } }
namespace EFT.UI {public class ItemUiContext {public static ItemUiContext? Instance;public EFT.QuestController? QuestController;} }
namespace EFT.Hideout {
 public enum EAreaType { Generator=4, Workbench=10 }
 public class AreaTemplate { public EAreaType Type; }
 public class AreaData { public AreaTemplate? Template;public int CurrentLevel;public int StatusUpdates;public void DecideStatus(int levelToSet)=>StatusUpdates++; }
 public class HideoutRepresentation {public ProductionController? ProductionController;public AreaInfo[]? Areas;public List<AreaData>? AreaDatas;}
 public class ProductionController {public Dictionary<int,List<ItemsProducerBase>> _areaProducers=new();}
 public class ProductionData {public double Progress;public int StartTimestamp,ProductionTime;public bool InProgress,Interrupted,AvailableForFinish;public string RecipeId="";}
 public class ProducingItemController:Native {
  public IntPtr Pointer=new(1);public ProducingProcess? _producingProcess=new();public double Progress;public bool AvailableForFinish;
  public class ProducingProcess {public IntPtr Pointer=new(2);public double BaseProductionTime=100,TimeLeft;public bool IsInProgress=true;}
 }
 public class Storage {public Dictionary<string,int> Counts=new();public int GetItemsCount(string id)=>Counts.GetValueOrDefault(id);}
 public class ItemsProducerBase:Native {
  public string AreaType="CircleOfCultists";public bool HasCompletedItems=>CompleteItemsCount>0;public int CompleteItemsCount=>CompleteItemsStorage!.Counts.Values.Sum();
  public Dictionary<string,object> Schemes=new();public Dictionary<string,ProducingItemController> ProducingItems=new();public Storage? CompleteItemsStorage=new();
  public int Updates;public ProductionData? LastUpdate;
  public bool TryUpdateProduction(string id,ProductionData data){
   Updates++;LastUpdate=data;
   if(ProducingItems.TryGetValue(id,out var item)&&item._producingProcess is {} process){
    process.BaseProductionTime=data.ProductionTime;
    item.Progress=data.Progress/data.ProductionTime;
    process.TimeLeft=data.ProductionTime-data.Progress;
    process.IsInProgress=process.TimeLeft>0;
    item.AvailableForFinish=data.AvailableForFinish;
   }
   return true;
  }
 }
 public class CultistsProducer:ItemsProducerBase {static int nextPointer=10;public IntPtr Pointer=new(++nextPointer);}
}
namespace LocalSpt5Esp {
 internal static class CornerGameData { internal sealed record Cultist(bool Active,bool Ready); }
 internal class Plugin {public static Plugin Self=new();public PatchSet Patches=new();public Logger Log=new();public int Errors;public void Error(Exception error){Errors++;}}
 internal class PatchSet {public void Patch(object method,HarmonyLib.HarmonyMethod? postfix=null){} }
 internal class Logger {public void LogInfo(string text){} }
 internal static class FeatureConfig {
  internal static readonly Dictionary<string,BepInEx.Configuration.ConfigEntry<bool>> Entries=new();
  internal static BepInEx.Configuration.ConfigEntry<bool> Bind(string section,string key,string title,bool value,string description="") {var result=new BepInEx.Configuration.ConfigEntry<bool>(value);Entries[key]=result;return result;}
 }
 internal static class ItemSpawner {internal static bool InRaid(){var game=Comfort.Common.Singleton<EFT.AbstractGame>.Instance;return game!=null&&game.InRaid&&game is not EFT.HideoutGame;}}
 internal static class CharacterDataPanel {
  internal static readonly List<Dictionary<string,object?>> Calls=new();internal static Func<Dictionary<string,object?>,Task<string>> Handler=_=>Task.FromResult("{}");
  internal static Task<string> Call(string route,string name,Dictionary<string,object?> payload){Calls.Add(payload);return Handler(payload);}
  internal static System.Text.Json.JsonElement Data(System.Text.Json.JsonElement root)=>root.GetProperty("data");
 }
 internal static class InterfaceMenu {
  internal static object Accent=new();internal static Func<string,bool> Click=_=>false;
  internal static bool Button(UnityEngine.Rect rect,string text,bool selected=false)=>UnityEngine.GUI.enabled&&Click(text);
  internal static void Label(UnityEngine.Rect rect,string text,object? color=null,int size=12,bool translate=true){}
  internal static void Paragraph(UnityEngine.Rect rect,string text,object? color=null,int size=12){}
 }
 internal static class I18n {internal static string Text(string text)=>text;}
 internal static class LootDisplay {internal static string TemplateName(string tpl)=>tpl;}
}
