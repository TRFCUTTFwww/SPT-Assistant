using BepInEx.Configuration;

namespace LocalSpt5Esp {
 internal sealed class Plugin(ConfigFile config) { internal ConfigFile Config { get; } = config; }
 internal static class CnConfig {
  internal sealed class ConfigurationManagerAttributes { public string DispName = ""; }
 }
 internal static class DirectorStage { internal static bool ModNotifications = true, HudHidden = false; }
 internal static class RaidNotifications { internal static IntPtr VisibleRaid => IntPtr.Zero; }
 internal static class I18n { internal static bool English => false; internal static string Text(string text) => text; }
 internal static class InterfaceMenu {
  internal static float ParagraphHeight(string text, float width, int size) => 20;
  internal static void Panel(UnityEngine.Rect rect, float alpha) { }
  internal static void Surface(UnityEngine.Rect rect, UnityEngine.Color color, int radius) { }
  internal static void Paragraph(UnityEngine.Rect rect, string text, UnityEngine.Color color, int size) { }
 }
}
namespace UnityEngine {
 internal static class Time { internal static float unscaledTime = 0; }
 internal static class Screen { internal static float width = 1920, height = 1080; }
 internal readonly struct Rect(float x, float y, float width, float height) {
  internal readonly float x = x, y = y, width = width, height = height;
 }
 internal readonly struct Color(float r, float g, float b, float a) { internal readonly float r = r, g = g, b = b, a = a; }
}
