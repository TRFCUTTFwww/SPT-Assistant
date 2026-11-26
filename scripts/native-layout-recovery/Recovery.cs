using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.UI;

namespace LocalSpt5.NativeLayoutRecovery;

[BepInPlugin("local.spt5.native-layout-recovery", "Native UI Layout Recovery", "0.1.0")]
public sealed class Plugin : BasePlugin {
 public override void Load() => Recovery.Start();
 public override bool Unload() { Recovery.Stop(); return true; }
}

// EFT 48558 incident recovery: the normal rebuild path left enabled layout
// groups with all participating children at the same position. Native typed
// layout calls restored their positions. Do not adjust fonts or saved settings.
public static class Recovery {
 static GameObject? driver;
 static readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("Native UI Layout Recovery");
 static float nextScan, nextStatus;
 static int scans, repairs, errors;
 static string lastTrigger = "", lastError = "";
 static double peakMs;
 static string Home => Path.Combine(BepInEx.Paths.PluginPath, "NativeLayoutRecovery");
 public static void Start() {
  if (driver != null) return;
  Directory.CreateDirectory(Home);
  ClassInjector.RegisterTypeInIl2Cpp<RecoveryDriver>();
  driver = new GameObject("SPT native UI layout recovery");
  UnityEngine.Object.DontDestroyOnLoad(driver);
  driver.AddComponent<RecoveryDriver>();
  log.LogInfo("Layout recovery active; checking native overlay groups every 0.25 seconds");
  WriteStatus(true);
 }
 public static void Stop() {
  if (driver != null) UnityEngine.Object.Destroy(driver);
  driver = null;
  WriteStatus(false);
 }
 internal static void Tick() {
  if (Time.realtimeSinceStartup < nextScan) return;
  nextScan = Time.realtimeSinceStartup + .25f;
  var clock = System.Diagnostics.Stopwatch.StartNew();
  try {
   scans++;
   var groups = UnityEngine.Object.FindObjectsOfType<LayoutGroup>()
    .Where(Eligible).OrderBy(g => Depth(g.transform)).ToArray();
   var broken = groups.Where(Collapsed).Select(g => PathOf(g.transform)).ToArray();
   if (broken.Length > 0) {
    lastTrigger = string.Join("; ", broken.Take(5));
    // Calculate from leaves upward, then assign from parents downward, as the
    // native layout pipeline does. Two passes settle nested preferred sizes.
    for (int pass = 0; pass < 2; pass++) {
     for (int i = groups.Length - 1; i >= 0; i--) Calculate(groups[i], false);
     foreach (var g in groups) Set(g, false);
     for (int i = groups.Length - 1; i >= 0; i--) Calculate(groups[i], true);
     foreach (var g in groups) Set(g, true);
    }
    Canvas.ForceUpdateCanvases();
    repairs++;
    log.LogInfo("Rebuilt collapsed native layout: " + lastTrigger);
    nextStatus = 0;
   }
  } catch (Exception e) {
   errors++; lastError = e.ToString();
   log.LogWarning("Layout recovery: " + lastError);
   nextScan = Time.realtimeSinceStartup + 5;
  } finally {
   clock.Stop(); peakMs = Math.Max(peakMs, clock.Elapsed.TotalMilliseconds);
   if (Time.realtimeSinceStartup >= nextStatus) { nextStatus = Time.realtimeSinceStartup + 10; WriteStatus(true); }
  }
 }
 static bool Eligible(LayoutGroup g) {
  if (g == null || !g.isActiveAndEnabled) return false;
  string name = g.GetIl2CppType().FullName;
  if (name != "UnityEngine.UI.VerticalLayoutGroup" && name != "UnityEngine.UI.HorizontalLayoutGroup" && name != "UnityEngine.UI.GridLayoutGroup") return false;
  var canvas = g.GetComponentInParent<Canvas>();
  return canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay;
 }
 static bool Collapsed(LayoutGroup g) {
  // Refresh the native list so inactive children and ignoreLayout elements are
  // excluded. Merely collecting children does not assign layout positions.
  Calculate(g, false);
  var children = g.rectChildren;
  if (children == null || children.Count < 2) return false;
  bool vertical = g.TryCast<VerticalLayoutGroup>() != null;
  var linear = g.TryCast<HorizontalOrVerticalLayoutGroup>();
  if (linear != null && linear.spacing < 0) return false;
  RectTransform? previous = null;
  for (int i = 0; i < children.Count; i++) {
   var child = children[i];
   float extent = vertical ? child.rect.height : child.rect.width;
   if (extent <= 1) continue;
   if (previous != null) {
    var a = previous.localPosition; var b = child.localPosition;
    if (linear == null) {
     if (Math.Abs(a.x - b.x) < .5f && Math.Abs(a.y - b.y) < .5f) return true;
    } else if (Math.Abs(vertical ? a.y - b.y : a.x - b.x) < .5f) return true;
   }
   previous = child;
  }
  return false;
 }
 static void Calculate(LayoutGroup g, bool vertical) {
  if (g.TryCast<VerticalLayoutGroup>() is {} v) { if (vertical) v.CalculateLayoutInputVertical(); else v.CalculateLayoutInputHorizontal(); }
  else if (g.TryCast<HorizontalLayoutGroup>() is {} h) { if (vertical) h.CalculateLayoutInputVertical(); else h.CalculateLayoutInputHorizontal(); }
  else if (g.TryCast<GridLayoutGroup>() is {} grid) { if (vertical) grid.CalculateLayoutInputVertical(); else grid.CalculateLayoutInputHorizontal(); }
 }
 static void Set(LayoutGroup g, bool vertical) {
  if (g.TryCast<VerticalLayoutGroup>() is {} v) { if (vertical) v.SetLayoutVertical(); else v.SetLayoutHorizontal(); }
  else if (g.TryCast<HorizontalLayoutGroup>() is {} h) { if (vertical) h.SetLayoutVertical(); else h.SetLayoutHorizontal(); }
  else if (g.TryCast<GridLayoutGroup>() is {} grid) { if (vertical) grid.SetLayoutVertical(); else grid.SetLayoutHorizontal(); }
 }
 static int Depth(Transform t) { int result = 0; for (var p = t.parent; p != null; p = p.parent) result++; return result; }
 static string PathOf(Transform t) { string result = t.name; for (var p = t.parent; p != null; p = p.parent) result = p.name + "/" + result; return result; }
 static void WriteStatus(bool active) {
  try { File.WriteAllText(Path.Combine(Home, "status.json"), JsonSerializer.Serialize(new { utc = DateTime.UtcNow, active, scans, repairs, errors, lastTrigger, lastError, peakMs })); }
  catch (Exception e) { log.LogWarning("Cannot write layout recovery status: " + e.Message); }
 }
}

public sealed class RecoveryDriver : MonoBehaviour {
 public RecoveryDriver(IntPtr pointer) : base(pointer) {}
 public void LateUpdate() => Recovery.Tick();
}
