using System.Reflection.Emit;
using System.Text.Json;
using HarmonyLib;
using LocalGearMail;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
internal static class LegacyNumericChecks {
 internal static void Run(){
  var target=LiveNumericEditing.StandingTarget();
  bool legacy=PatchProcessor.GetCurrentInstructions(target).Any(i=>i.opcode==OpCodes.Ldstr&&i.operand is string s&&s=="好感请输入 -20～100。");
  var harmony=new Harmony("assistant.legacy-numeric-tests");
  try{
   LiveNumericEditing.Install(harmony);
   var patched=PatchProcessor.GetCurrentInstructions(target);
   if(legacy&&(!patched.Any(i=>i.opcode==OpCodes.Ldc_R8&&i.operand is double d&&d==double.MinValue)||patched.Any(i=>i.opcode==OpCodes.Ldstr&&i.operand is string s&&s=="好感请输入 -20～100。")))throw new Exception("Legacy trader limits remain active");
   using var props=JsonDocument.Parse("{}");
   var upd=new Upd{Repairable=new(){Durability=50,MaxDurability=100}};
   GearAttributeRules.Apply(upd,new(){Durability=1000000,MaxDurability=-12},props.RootElement);
   if(upd.Repairable.Durability!=1000000||upd.Repairable.MaxDurability!=-12)throw new Exception("Legacy item limits remain active");
   Console.WriteLine("PASS live numeric editing works with "+(legacy?"the deployed legacy":"the current")+" base assembly, including trader and item inputs");
  }finally{harmony.UnpatchSelf();}
 }
}
