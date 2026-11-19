param(
    [string]$GameRoot='D:/SPT-5.0.0-48558',
    [string]$ResourceRoot='D:/CP2077_Quickhack_Resources_20261112',
    [string]$BlackwallRoot='D:/CP2077_Blackwall_Supplement_20261112',
    [string]$UiSupplementRoot='D:/aaa/CP2077_Collaboration_20261113/CP2077_Upload_UI_Fonts',
    [string]$Python='D:/aaa/build/alpha-tools/python/cpython-3.12.16-windows-x86_64-none/python.exe',
    [string]$PythonModulePath='D:/SPT_Mod_Staging_20261027/quickhack-video-tools',
    [string]$Dotnet='dotnet',
    [string]$HostReleaseRevision='',
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [switch]$Install
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$outputPath=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $outputPath) -and @(Get-ChildItem -LiteralPath $outputPath -Force).Count -gt 0) { throw 'Output directory must be new or empty.' }
& $Dotnet build (Join-Path $repoRoot 'quickhack-mod/Quickhacks.csproj') -c Release "-p:GameRoot=$GameRoot" --nologo
if($LASTEXITCODE -ne 0) { throw 'Quickhack build failed.' }
# Reproduce the released loader's informational metadata for a core-only update.
# Real loader code changes still change its hash and require a client restart.
if($HostReleaseRevision) {
    if($HostReleaseRevision -notmatch '^[a-fA-F0-9]{40}$') { throw 'HostReleaseRevision must be a full Git revision.' }
    & $Dotnet build (Join-Path $repoRoot 'quickhack-host/Host.csproj') -c Release "-p:GameRoot=$GameRoot" "-p:SourceRevisionId=$HostReleaseRevision" --nologo
    if($LASTEXITCODE -ne 0) { throw 'Released quickhack host build failed.' }
}
& $Dotnet build (Join-Path $repoRoot 'quickhack-host-tests/Fixture/Fixture.csproj') -c Release --nologo
if($LASTEXITCODE -ne 0) { throw 'Quickhack host test build failed.' }
& $Dotnet run --project (Join-Path $repoRoot 'quickhack-host-tests/Tests.csproj') -c Release --no-build -- (Join-Path $repoRoot 'quickhack-host-tests/Fixture/bin/Release/net10.0/Fixture.dll')
if($LASTEXITCODE -ne 0) { throw 'Quickhack host checks failed.' }

$files=[ordered]@{}
$iconDir='03_images/base/gameplay/gui/common/icons/quickhacks_icons.inkatlas/2160p'
foreach($name in @('Cyberpsychosis','BlackwallBreach','GlitchScreenBlind','WeaponMalfunction','LocomotionMalfunction','MemoryWipe','Overheat','BrainMelt')) { $files["$name.png"]=@($ResourceRoot,"$iconDir/$name.png") }
$files['DoorAuthorization.png']=@($ResourceRoot,'03_images/base/gameplay/gui/fullscreen/hacking_minigame/atlas_minigame_programs.inkatlas/2160p/DoorAuthorization.png')
$breachAtlas='03_images/base/gameplay/gui/fullscreen/hacking_minigame/atlas_minigame.inkatlas/2160p'
foreach($name in @('buffer_empty','buffer_activated','fluff_1','fluff_2','fluff_3','fluff_barcode1','fluff_barcode2','fluff_code1','fluff_header1','fluff_protocol2','fluff_text2','fluff_text6','fluff_pettern','frame_glow_small','ico_programs','logo_nettech')) { $files["breach_$name.png"]=@($ResourceRoot,"$breachAtlas/$name.png") }
$scannerAtlas='03_images/base/gameplay/gui/widgets/scanning/atlas_scanner.inkatlas/2160p'
foreach($name in @('outer_brackets','inner_crosshair','ruller','fluff_corner_text','fluff_corner_text2','fluff_top_right','fluff_left','fluff_bottom')) { $files["scan_$name.png"]=@($ResourceRoot,"$scannerAtlas/$name.png") }
$quickAtlas='03_images/base/gameplay/gui/widgets/quickhacks/atlas_quickhacks.inkatlas/2160p'
foreach($name in @('memory_small_bg','memory_small_fg','frame_qh_bg','frame_qh_fg','seal','arrow')) { $files["qh_$name.png"]=@($ResourceRoot,"$quickAtlas/$name.png") }
$quickUi='03_images/base/gameplay/gui/widgets/quickhacks/quickhacks.inkatlas/2160p'
foreach($name in @('selection_plate','selection_fluff','quickhacks_handle','warning_tiles')) { $files["qh_$name.png"]=@($ResourceRoot,"$quickUi/$name.png") }
$files['qh_selection_glow.png']=@($ResourceRoot,'03_images/base/gameplay/gui/widgets/quickhacks/texture_mask.inkatlas/2160p/glow_plate_2.png')
$audioDir='04_audio/WAV_by_event'
$audioMap=[ordered]@{
    'open'='ui_hacking_open.wav'; 'close'='ui_hacking_close.wav'; 'hover'='ui_hacking_hover.wav';
    'press'='ui_hacking_press.wav'; 'fail'='ui_hacking_press_fail.wav'; 'granted'='ui_hacking_access_granted.wav';
    'denied'='ui_hacking_access_denied.wav'; 'scan'='ui_focus_mode_scanning_open.wav';
    'Cyberpsychosis'='quickhack_cyberpsychosis {r}.wav'; 'GlitchScreenBlind'='quickhack_reboot_optics {r}.wav';
    'WeaponMalfunction'='quickhack_weapon_malfunction {r}.wav'; 'LocomotionMalfunction'='quickhack_locomotion_malfunction {r}.wav';
    'MemoryWipe'='quickhack_memory_wipe {r}.wav';
    'Overheat'='quickhack_overheat {r}.wav'; 'BrainMelt'='quickhack_synapse_burnout {r}.wav'
}
foreach($pair in $audioMap.GetEnumerator()) { $files["$($pair.Key).wav"]=@($ResourceRoot,"$audioDir/$($pair.Value)") }
$files['blackwall.png']=@($BlackwallRoot,'03_images/ep1/fx/blackwall/textures/blackwall_atlas.png')
$files['BlackwallBreach.wav']=@($BlackwallRoot,'04_audio/WAV_by_event/q306_sc_08_blackwall_burnout_a {r}.wav')
$files['death.wav']=@($BlackwallRoot,'04_audio/WAV_by_event/w_expl_blackwall_npc_death_gameplay {r}.wav')
$manifests=@{}
foreach($root in @($ResourceRoot,$BlackwallRoot,$UiSupplementRoot)) {
    $lookup=@{}
    foreach($record in (Import-Csv -LiteralPath (Join-Path $root 'RESOURCE_MANIFEST.csv'))) { $lookup[$record.Path.Replace('\','/')]=$record.SHA256 }
    $manifests[$root]=$lookup
}
# Verify all source assets before writing an installable package.
foreach($file in $files.GetEnumerator()) {
    $root=$file.Value[0]; $relative=$file.Value[1]; $source=Join-Path $root $relative
    if(!(Test-Path -LiteralPath $source)) { throw "Missing asset: $relative" }
    $expected=$manifests[$root][$relative]
    if(!$expected -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $expected) { throw "Asset hash mismatch: $relative" }
}
$payload=Join-Path $outputPath 'CP2077Quickhacks'
$assets=Join-Path $payload 'Assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $payload 'runtime') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'quickhack-host/bin/Release/net10.0/Local.Cp2077.Quickhacks.dll') -Destination $payload
Copy-Item -LiteralPath (Join-Path $repoRoot 'quickhack-mod/bin/Release/net10.0/Local.Cp2077.Quickhacks.Core.dll') -Destination (Join-Path $payload 'runtime/Local.Cp2077.Quickhacks.Core.module')
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts/Install-Quickhacks.ps1') -Destination (Join-Path $outputPath 'Install.ps1')
Copy-Item -LiteralPath (Join-Path $repoRoot 'quickhack-mod/README.zh-CN.md') -Destination (Join-Path $outputPath 'README.zh-CN.md')
$packaged=@()
foreach($file in $files.GetEnumerator()) {
    $source=Join-Path $file.Value[0] $file.Value[1]
    $destination=Join-Path $assets $file.Key
    Copy-Item -LiteralPath $source -Destination $destination
    $packaged+=@{name=$file.Key;bytes=(Get-Item -LiteralPath $destination).Length;sha256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash}
}
& $Python (Join-Path $repoRoot 'scripts/Rasterize-QuickhackFonts.py') --ui-root $UiSupplementRoot --source-root (Join-Path $repoRoot 'quickhack-mod') --output $assets --module-path $PythonModulePath
if($LASTEXITCODE -ne 0) { throw 'Original font rasterization failed.' }
foreach($generated in (Get-ChildItem -LiteralPath $assets -File -Filter 'font_*')) {
    $packaged+=@{name=$generated.Name;bytes=$generated.Length;sha256=(Get-FileHash -LiteralPath $generated.FullName -Algorithm SHA256).Hash}
}
@{version='0.6.2';target='EFT 48558 / SPT 5.0';files=$packaged} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $payload 'asset-manifest.json') -Encoding UTF8
@{
    version='0.6.2'
    hostSha256=(Get-FileHash -LiteralPath (Join-Path $payload 'Local.Cp2077.Quickhacks.dll') -Algorithm SHA256).Hash
    coreSha256=(Get-FileHash -LiteralPath (Join-Path $payload 'runtime/Local.Cp2077.Quickhacks.Core.module') -Algorithm SHA256).Hash
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'release-manifest.json') -Encoding UTF8
& $Dotnet run --project (Join-Path $repoRoot 'quickhack-tests/Tests.csproj') -c Release "-p:GameRoot=$GameRoot" -- $GameRoot $assets
if($LASTEXITCODE -ne 0) { throw 'Quickhack checks failed; package has not been installed.' }
if($Install) {
    & (Join-Path $outputPath 'Install.ps1') -GameRoot $GameRoot
}
Write-Output "Quickhacks package: $outputPath; $($packaged.Count) verified assets"
