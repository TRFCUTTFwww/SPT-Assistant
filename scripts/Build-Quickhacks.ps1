param(
    [string]$GameRoot='D:/SPT-5.0.0-48558',
    [string]$ResourceRoot='D:/CP2077_Quickhack_Resources_20261112',
    [string]$BlackwallRoot='D:/CP2077_Blackwall_Supplement_20261112',
    [string]$Dotnet='dotnet',
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [switch]$Install
)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path -Parent $PSScriptRoot
$outputPath=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $outputPath) -and @(Get-ChildItem -LiteralPath $outputPath -Force).Count -gt 0) { throw 'Output directory must be new or empty.' }
& $Dotnet build (Join-Path $repoRoot 'quickhack-mod/Quickhacks.csproj') -c Release "-p:GameRoot=$GameRoot" --nologo
if($LASTEXITCODE -ne 0) { throw 'Quickhack build failed.' }

$files=[ordered]@{}
$iconDir='03_images/base/gameplay/gui/common/icons/quickhacks_icons.inkatlas/2160p'
foreach($name in @('Cyberpsychosis','BlackwallBreach','GlitchScreenBlind')) { $files["$name.png"]=@($ResourceRoot,"$iconDir/$name.png") }
$files['DoorAuthorization.png']=@($ResourceRoot,'03_images/base/gameplay/gui/fullscreen/hacking_minigame/atlas_minigame_programs.inkatlas/2160p/DoorAuthorization.png')
$audioDir='04_audio/WAV_by_event'
$audioMap=[ordered]@{
    'open'='ui_hacking_open.wav'; 'close'='ui_hacking_close.wav'; 'hover'='ui_hacking_hover.wav';
    'press'='ui_hacking_press.wav'; 'fail'='ui_hacking_press_fail.wav'; 'granted'='ui_hacking_access_granted.wav';
    'denied'='ui_hacking_access_denied.wav'; 'scan'='ui_focus_mode_scanning_open.wav';
    'Cyberpsychosis'='quickhack_cyberpsychosis {r}.wav'; 'GlitchScreenBlind'='quickhack_reboot_optics {r}.wav';
    'BlackwallBreach'='quickhack_synapse_burnout {r}.wav'
}
foreach($pair in $audioMap.GetEnumerator()) { $files["$($pair.Key).wav"]=@($ResourceRoot,"$audioDir/$($pair.Value)") }
$files['blackwall.png']=@($BlackwallRoot,'03_images/ep1/fx/blackwall/textures/blackwall_atlas.png')
$files['death.wav']=@($BlackwallRoot,'04_audio/WAV_by_event/w_expl_blackwall_npc_death_gameplay {r}.wav')
$manifests=@{}
foreach($root in @($ResourceRoot,$BlackwallRoot)) {
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
Copy-Item -LiteralPath (Join-Path $repoRoot 'quickhack-mod/bin/Release/net10.0/Local.Cp2077.Quickhacks.dll') -Destination $payload
Copy-Item -LiteralPath (Join-Path $repoRoot 'quickhack-mod/README.zh-CN.md') -Destination (Join-Path $outputPath 'README.zh-CN.md')
$packaged=@()
foreach($file in $files.GetEnumerator()) {
    $source=Join-Path $file.Value[0] $file.Value[1]
    $destination=Join-Path $assets $file.Key
    Copy-Item -LiteralPath $source -Destination $destination
    $packaged+=@{name=$file.Key;bytes=(Get-Item -LiteralPath $destination).Length;sha256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash}
}
@{version='0.1.0';target='EFT 48558 / SPT 5.0';files=$packaged} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $payload 'asset-manifest.json') -Encoding UTF8
& $Dotnet run --project (Join-Path $repoRoot 'quickhack-tests/Tests.csproj') -c Release "-p:GameRoot=$GameRoot" -- $GameRoot $assets
if($LASTEXITCODE -ne 0) { throw 'Quickhack checks failed; package has not been installed.' }
if($Install) {
    $destination=Join-Path $GameRoot 'BepInEx/plugins/CP2077Quickhacks'
    if(Test-Path -LiteralPath $destination) {
        $backup=Join-Path $GameRoot ('mod-backups/quickhacks-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        Copy-Item -LiteralPath $destination -Destination $backup -Recurse
    }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -Path (Join-Path $payload '*') -Destination $destination -Recurse -Force
    Write-Output "Installed standalone Quickhacks: $destination (loads at next client start)"
}
Write-Output "Quickhacks package: $outputPath; $($files.Count) verified assets"
