using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace LocalSpt5Esp;

internal static class LootPrices
{
    private static FleaPriceSnapshot? source;
    private static Task<Dictionary<string, double>>? reading;
    private static Dictionary<string, double> current = new();
    private static float nextRead;
    private static double? bitcoin;

    internal static double Resolve(string id, double local) => FleaPriceSnapshot.Resolve(current, id, local);

    internal static void Tick()
    {
        bool changed = bitcoin != BitcoinControl.Price;
        bitcoin = BitcoinControl.Price;
        if (reading?.IsCompleted == true)
        {
            if (reading.IsCompletedSuccessfully)
            {
                var next = reading.Result;
                if (!ReferenceEquals(current, next))
                {
                    current = next;
                    changed = true;
                    Plugin.Self?.Log.LogInfo($"ESP flea prices updated: mode={source?.Mode}, {current.Count} prices; missing items use local snapshot");
                }
            }
            else _ = reading.Exception;
            reading = null;
        }
        if (changed) { LootDisplay.Reset(); RaidLoot.Invalidate(); }
        if (reading != null || Time.unscaledTime < nextRead) return;
        nextRead = Time.unscaledTime + 5;
        var reader = source ??= new FleaPriceSnapshot(Path.Combine(BepInEx.Paths.GameRootPath,
            "SPT_Runtime", "user", "mods", "LocalGearMail", "flea-prices"));
        reading = Task.Run(reader.Read);
    }

    internal static void Reset()
    {
        // The worker only owns its reader and cannot publish into the next runtime generation.
        source = null;
        reading = null;
        current = new();
        nextRead = 0;
        bitcoin = null;
    }
}
