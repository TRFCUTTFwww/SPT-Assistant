using System.Text.Json;
namespace UnityEngine {
 public class Object {public static object? Scene;public static int Searches;public static T? FindObjectOfType<T>() where T:class{Searches++;return Scene as T;}}
 public struct Vector2 {public static Vector2 zero=>default;}
 public struct Rect(float x,float y,float width,float height){public float x=x,y=y,width=width,height=height;public float xMax=>x+width;public float yMax=>y+height;}
 public struct Color {public Color(float r,float g,float b,float a=1){}public static Color yellow=>new();public static Color Lerp(Color a,Color b,float value)=>a;}
 public static class GUI {public static bool enabled=true;public static Vector2 BeginScrollView(Rect a,Vector2 b,Rect c,bool d,bool e)=>b;public static void EndScrollView(){}}
}
namespace EFT {
 public class Profile {public string Id="";}
 public class AbstractApplication {public T? TryCast<T>() where T:class=>this as T;}
 public class TarkovApplication:AbstractApplication {public ClientBackendSession? Backend,Session;public Menu? _menuOperation;public ClientBackendSession? GetClientBackEndSession()=>Backend;}
 public class ClientBackendSession {public Profile? Profile;public T? TryCast<T>() where T:class=>this as T;}
 public class Menu {public QuestController? QuestController;}
 public class QuestController {public Profile? Profile;}
}
namespace LocalSpt5Esp {
 internal static class InterfaceMenu {
  internal static readonly List<string> Labels=[];internal static UnityEngine.Color Accent=new(),Background=new(),Text=new();
  internal static readonly HashSet<string> Clicks=[];
  internal static bool Button(UnityEngine.Rect rect,string value,bool selected=false)=>UnityEngine.GUI.enabled&&Clicks.Remove(value);
  internal static void Surface(UnityEngine.Rect rect,UnityEngine.Color color,int radius){}
  internal static void Label(UnityEngine.Rect rect,string value,UnityEngine.Color? color=null,int size=14,bool translate=true)=>Labels.Add(value);
  internal static void Paragraph(UnityEngine.Rect rect,string value,UnityEngine.Color? color=null,int size=14)=>Labels.Add(value);
 }
 internal static class CharacterDataPanel {
  internal static int Calls;internal static Func<Task<string>> Response=()=>Task.FromResult("{}");
  internal static Task<string> Call(string route,string capability,Dictionary<string,object?> payload){Calls++;return Response();}
  internal static JsonElement Data(JsonElement root)=>root.TryGetProperty("data",out var data)?data:root;
  internal static string Name(string key,string fallback)=>fallback;
 }
 internal static class RaidRules {internal static string Clock(double seconds)=>TimeSpan.FromSeconds(seconds).ToString();}
 internal static class I18n {internal static string Text(string value)=>value;}
 internal static class AiSpawnCatalog {internal sealed record Option(string Role,string Name);internal static readonly Option[] Options=[];}
}
