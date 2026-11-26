using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
namespace LocalGearMail;
// Keep developer inputs available when the running base mail assembly is older.
internal static class LiveNumericEditing {
 internal static void Install(Harmony harmony){
  harmony.Patch(AccessTools.Method(typeof(GearAttributeRules),nameof(GearAttributeRules.Apply)),prefix:new HarmonyMethod(typeof(LiveNumericEditing),nameof(Apply)));
  harmony.Patch(StandingTarget(),transpiler:new HarmonyMethod(typeof(LiveNumericEditing),nameof(StandingLimits)));
 }
 internal static MethodInfo StandingTarget()=>AccessTools.Method(typeof(ProfileAdminService),nameof(ProfileAdminService.Handle)).GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType.GetMethod("MoveNext",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!;
 static IEnumerable<CodeInstruction> StandingLimits(IEnumerable<CodeInstruction> source){
  var code=source.ToList();
  if(!code.Any(i=>i.opcode==OpCodes.Ldstr&&i.operand is string s&&s=="好感请输入 -20～100。"))return code;
  var lower=code.Where(i=>i.opcode==OpCodes.Ldc_R8&&i.operand is double d&&d==-20).ToArray();
  var upper=code.Where(i=>i.opcode==OpCodes.Ldc_R8&&i.operand is double d&&d==100).ToArray();
  if(lower.Length!=1||upper.Length!=1)throw new InvalidOperationException("旧商人好感输入校验与预期不符。");
  lower[0].operand=double.MinValue;upper[0].operand=double.MaxValue;
  foreach(var i in code)if(i.opcode==OpCodes.Ldstr&&i.operand is string s&&s=="好感请输入 -20～100。")i.operand="好感请输入有限数值。";
  return code;
 }
 static bool Apply(Upd __0,GearAttributes? __1,JsonElement __2,string __3){GearAttributeValueRules.Apply(__0,__1,__2,__3);return false;}
}
