# Native UI layout recovery

Standalone incident recovery plugin for EFT 48558 / SPT 5.0.0 IL2CPP. It is not included in the Assistant installer.

On the affected client, graphics settings rows, trader cards and currency labels occupied identical coordinates. Font advances and font face metrics were normal. A generic `LayoutRebuilder.ForceRebuildLayoutImmediate` call did not resolve the collapsed settings rows. Calling the concrete native layout component methods restored separate row positions. The original cause of the missed layout work remains unconfirmed.

The plugin checks active, standard vertical, horizontal and grid layout groups on overlay canvases every 0.25 seconds. It uses the native participating-child list, skips negative spacing, and looks for adjacent children occupying the same position along the layout axis. When it detects this condition, it calculates sizes from children upward and assigns positions from parents downward, using concrete IL2CPP layout component methods. It performs two passes to settle nested preferred sizes. It does not replace fonts or change graphics preferences, language, profiles or game assets.

Build from the repository root with the .NET 10 SDK and matching generated game interop assemblies:

```powershell
dotnet build scripts/native-layout-recovery/NativeLayoutRecovery.csproj -c Release -p:GameRoot=D:/SPT-5.0.0-48558
```

Install `bin/Release/net10.0/Local.Spt5.NativeLayoutRecovery.dll` into `BepInEx/plugins/NativeLayoutRecovery/`. BepInEx loads the plugin at the next game start. `Recovery.Start()` also supports activation through an existing trusted main-thread diagnostic entry point in the running client. Do not load several copies or register the driver type twice in one process.

The plugin writes `status.json` next to its DLL with scan count, repair count, errors, last triggering hierarchy paths and peak scan/repair duration. The first repair can take longer than later checks. It logs each repair to BepInEx. Remove the DLL and restart the client to uninstall; `Recovery.Stop()` removes the current live driver when invoked on the main thread.

Validation on the affected client: compiled against EFT 48558 with no warnings or errors; live activation detected a collapsed graphics-settings content group and repaired it. On 2026-11-11 the user confirmed that closing and reopening Graphics settings and scrolling all displayed normally. A later sample showed 316 scans, nine repairs across UI transitions, zero errors and a peak scan/repair time of 1036.7 ms; repair can cause a noticeable one-time pause. This is an incident workaround, not a fix to the original missed native layout updates. Changing resolution, broader UI compatibility and loading after a full client restart remain unverified. The detector intentionally covers standard overlay layout components only.
