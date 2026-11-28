using System.Reflection;
using BepInEx.Configuration;
using LocalSpt5Esp;
using Mono.Cecil;

int checks = 0;
void Check(bool condition, string text) { if (!condition) throw new Exception(text); checks++; Console.WriteLine("PASS " + text); }
Array Pending() {
 var queue = typeof(FeatureToasts).GetField("pending", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
 return (Array)queue.GetType().GetMethod("ToArray")!.Invoke(queue, null)!;
}
string Message(int index) { var value = Pending().GetValue(index)!; return (string)value.GetType().GetProperty("Text")!.GetValue(value)!; }
string home = Path.Combine(Path.GetTempPath(), "spt-feature-toast-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(home);
try {
 string path = Path.Combine(home, "saved.cfg");
 File.WriteAllText(path, "[53. 击杀特效]\nConfirmed firearm kill effects = false\nKill center marker = false\n\n[72. 事件通知]\nEnabled = false\n");
 var config = new ConfigFile(path, false) { SaveOnConfigSet = false };
 int restoreEvents = 0;
 config.SettingChanged += (_, _) => restoreEvents++;
 // Use the real BepInEx config parser to reproduce stored false values overriding true defaults.
 var effect = config.Bind("53. 击杀特效", "Confirmed firearm kill effects", true);
 var center = config.Bind("53. 击杀特效", "Kill center marker", true);
 var raids = config.Bind("72. 事件通知", "Enabled", true);
 Check(!effect.Value && !center.Value && !raids.Value && restoreEvents >= 3, "saved startup values emit BepInEx change events");
 FeatureToasts.Initialize(new Plugin(config));
 Check(Pending().Length == 0, "saved startup values stay silent when notifications start after initialization");
 effect.Value = true;
 Check(Pending().Length == 1 && Message(0) == "Confirmed firearm kill effects · 已开启", "manual enable produces one notification");
 FeatureToasts.Initialize(new Plugin(config));
 center.Value = true;
 Check(Pending().Length == 2, "repeated initialization retains only one subscription");
 var other = new ConfigFile(Path.Combine(home, "other.cfg"), false) { SaveOnConfigSet = false };
 var otherEntry = other.Bind("53. 击杀特效", "Confirmed firearm kill effects", false);
 FeatureToasts.Initialize(new Plugin(other));
 effect.Value = false;
 Check(Pending().Length == 2, "switching config detaches the old subscription");
 otherEntry.Value = true;
 otherEntry.Value = false;
 Check(Pending().Length == 4 && Message(3).EndsWith("已关闭"), "manual disable still notifies on the current config");
 FeatureToasts.Shutdown();
 otherEntry.Value = true;
 Check(Pending().Length == 0, "shutdown clears queued notices and detaches the listener");
 FeatureToasts.Initialize(new Plugin(config));
 center.Value = false;
 Check(Pending().Length == 1, "reload can subscribe again without duplicate notices");
 using var core = AssemblyDefinition.ReadAssembly(args[0]);
 var runtime = core.MainModule.Types.Single(t => t.Name == "RuntimeModule");
 var iterator = runtime.NestedTypes.Single(t => t.Name.StartsWith("<RawInitializeSteps>"));
 var move = iterator.Methods.Single(m => m.Name == "MoveNext");
 string[] calls = move.Body.Instructions.Where(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Ldftn).Select(i => (MethodReference)i.Operand)
  .SelectMany(m => m.Name.StartsWith("<RawInitializeSteps>")
   ? m.Resolve().Body.Instructions.Select(i => i.Operand).OfType<MethodReference>() : new[] { m })
  .Select(m => m.DeclaringType.Name + "." + m.Name).ToArray();
 int subscribe = Array.IndexOf(calls, "FeatureToasts.Initialize");
 string[] before = { "KillEffects.Initialize", "RaidNotifications.Initialize", "I18n.Initialize", "Overlay..ctor" };
 Check(before.All(name => Array.IndexOf(calls, name) >= 0 && subscribe > Array.IndexOf(calls, name)), "compiled startup subscribes only after late config bindings and overlay construction");
 var kill = core.MainModule.Types.Single(t => t.Name == "KillEffects").Methods.Single(m => m.Name == "Initialize");
 var instructions = kill.Body.Instructions;
 foreach (string name in new[] { "枪械击杀特效", "准星击杀反馈" }) {
  var index = Enumerable.Range(0, instructions.Count).Single(i => instructions[i].Operand as string == name);
  Check(instructions[index + 1].OpCode.Code == Mono.Cecil.Cil.Code.Ldc_I4_0, "compiled default is off: " + name);
 }
 Console.WriteLine($"Passed {checks} feature notification checks.");
} finally { FeatureToasts.Shutdown(); Directory.Delete(home, true); }
