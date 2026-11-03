using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using LocalRaidShared;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
namespace LocalRaidControls;
public static partial class Patches {
 // The native parent loop decrements its slot counter on false. Consume skipped slots
 // under independent rules so a forbidden/exhausted candidate pool cannot loop forever.
 static void FinishWeightedAir(ref bool __result){if(airdrop?.HasItemWeights==true)__result=true;}
 static double AirFactor(string template,double price)=>airdrop!.HasItemWeights&&airdrop.ItemWeights.TryGetValue(template,out var value)?value:airdrop.AirdropEnabled&&price>=airdrop.ValueThreshold?airdrop.AirdropWeight:1;
 static void AirWeightPatches(Harmony harmony){foreach(string name in new[]{"FindAndAddRandomItemToLoot","FindAndAddRandomPresetToLoot"})harmony.Patch(AccessTools.Method(typeof(LootGenerator),name),transpiler:new HarmonyMethod(typeof(Patches),nameof(AirWeightBoundary)));}
 static IEnumerable<CodeInstruction> AirWeightBoundary(IEnumerable<CodeInstruction> input){int found=0;foreach(var code in input){if(code.operand is MethodInfo method&&method.DeclaringType==typeof(RandomUtil)&&method.Name=="GetArrayValue"&&method.IsGenericMethod){var type=method.GetGenericArguments().Single();if(type!=typeof(TemplateItem)&&type!=typeof(Preset))throw new InvalidOperationException("Unexpected air loot candidate type");found++;code.opcode=OpCodes.Call;code.operand=AccessTools.Method(typeof(Patches),type==typeof(TemplateItem)?nameof(DrawAirItem):nameof(DrawAirPreset));}yield return code;}if(found!=1)throw new InvalidOperationException("Airdrop random draw layout changed");}
 static TemplateItem DrawAirItem(RandomUtil random,IEnumerable<TemplateItem> source){if(airdrop?.HasItemWeights!=true)return random.GetArrayValue(source);var items=source.ToArray();var weights=LootWeightMath.Scale(items.Select(i=>(1d,AirFactor(i.Id.ToString(),Price(i.Id.ToString())))).ToArray());int chosen=LootWeightMath.Pick(weights,random.GetDouble(0,1));return chosen<0?throw new InvalidOperationException("Empty weighted airdrop pool"):items[chosen];}
 static Preset DrawAirPreset(RandomUtil random,IEnumerable<Preset> source){if(airdrop?.HasItemWeights!=true)return random.GetArrayValue(source);var items=source.ToArray();var weights=LootWeightMath.Scale(items.Select(p=>(1d,AirFactor(p.Encyclopedia?.ToString()??"",(p.Items??[]).Sum(i=>Price(i.Template.ToString()))))).ToArray());int chosen=LootWeightMath.Pick(weights,random.GetDouble(0,1));return chosen<0?throw new InvalidOperationException("Empty weighted airdrop preset pool"):items[chosen];}
}
