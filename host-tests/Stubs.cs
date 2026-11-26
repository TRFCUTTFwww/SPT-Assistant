// Native boundaries only. Tests execute the production reloader, sequence and
// pause implementation against detached files and a fixture runtime module.
namespace BepInEx {public static class Paths {public static string BepInExRootPath=>AppContext.BaseDirectory;}}
namespace BepInEx.Configuration {public class ConfigEntry<T>{public T Value{get;set;}public ConfigEntry(T value){Value=value;}}public class ConfigFile {public bool SaveOnConfigSet{get;set;}=true;public void Save(){}public void Clear(){}public void Reload(){}public bool TryGetEntry<T>(string s,string k,out ConfigEntry<T> value){value=null!;return false;}}}
namespace BepInEx.Logging {public class ManualLogSource {public void LogWarning(object value)=>Console.WriteLine("WARN "+value);public void LogInfo(object value)=>Console.WriteLine(value);}}
namespace UnityEngine {
 public static class Time {public static float timeScale=0.75f;}
 public static class AudioListener {public static bool pause;}
 public static class Application {public static bool runInBackground;public static void Quit(){}}
 public enum CursorLockMode {None,Locked}
 public static class Cursor {public static CursorLockMode lockState=CursorLockMode.Locked;public static bool visible;}
 public static class Input {public static void ResetInputAxes(){}}
 public record struct Color(float r,float g,float b,float a=1){public static Color white=>new(1,1,1);public static Color Lerp(Color a,Color b,float t)=>a;}
 public struct Rect {public float x,y;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;}}
 public static class Screen {public static int width=1920,height=1080;}
 public class Texture2D {public static Texture2D whiteTexture=new();}
 public enum EventType {Repaint,KeyDown,KeyUp,MouseDown,MouseUp,ScrollWheel}
 public class Event {public static Event current=new();public EventType type=EventType.Repaint;public void Use(){}}
 public class GUIStyle{public int fontSize;public bool wordWrap;public GUIStyle(){}public GUIStyle(GUIStyle other){}}
 public class GUISkin{public GUIStyle label=new();}
 public static class ColorUtility{public static bool TryParseHtmlString(string s,out Color c){c=Color.white;return true;}}
 public static class GUIUtility {public static string systemCopyBuffer="";}
 public static class GUI {public static GUISkin skin=new();public static bool enabled=true;public static void Label(Rect r,string t,GUIStyle s){}public static int depth;public static Color color;public static void DrawTexture(Rect r,Texture2D t){}public static void Box(Rect r,string t){}public static void Label(Rect r,string t){}public static bool Button(Rect r,string t){if(TestState.Click==t){TestState.Click="";return true;}return false;}}
}
namespace Comfort.Common {public static class Singleton<T> {public static T? Instance;}}
namespace Il2CppSystem {
 public record struct DateTime(System.DateTime Value) {public DateTime AddSeconds(double seconds)=>new(Value.AddSeconds(seconds));}
 public class Nullable<T> {public bool HasValue;public T Value;public Nullable(T value){Value=value;HasValue=true;}}
}
namespace EFT {
 public class AbstractGame {public IntPtr Pointer=new(1);public GameTimer? GameTimer;}
 public class GameTimer {public IntPtr Pointer=new(2);public Il2CppSystem.Nullable<Il2CppSystem.DateTime> _start=new(new(System.DateTime.UtcNow.AddSeconds(-100))),_escape=new(new(System.DateTime.UtcNow.AddSeconds(100)));}
}
public static class TestState {
 public static int MainThread,Init,Unload,Updates,Prepares;
 public static bool FailNextInit,FailNextUnload,FailFrameCallbacks,CanReload=true;public static int Rescues;public static string Click="";
}
