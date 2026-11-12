param(
 [string]$GameRoot='D:/SPT-5.0.0-48558',
 [Parameter(Mandatory=$true)][string]$ResourceRoot,
 [string]$Dotnet='dotnet'
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$GameRoot=(Resolve-Path -LiteralPath $GameRoot).Path
$ResourceRoot=(Resolve-Path -LiteralPath $ResourceRoot).Path
if(Get-Process EscapeFromTarkov -ErrorAction SilentlyContinue){throw 'Close the game client before installing a new BepInEx plugin.'}
if(!(Test-Path -LiteralPath (Join-Path $GameRoot 'EscapeFromTarkov.exe'))){throw 'Wrong game root'}
# Only copy the small subset used by this local prototype. Proprietary assets are
# supplied by the user's export, never embedded in source or a public release.
$assets=[ordered]@{
 '09_station_arms_glb/pma_q306.anims.glb'='station-arms.glb'
 '03_images/ep1/fx/blackwall/textures/blackwall_atlas.png'='blackwall.png'
 '03_images/base/fx/_textures/masks/noise/fx_digital_noise_01_d.png'='noise.png'
 '04_audio/WAV_by_event/q306_sc_08_blackwall_burnout_a {r}.wav'='cast.wav'
 '04_audio/WAV_by_event/w_expl_blackwall_npc_death_gameplay {r}.wav'='death.wav'
}
$manifest=Import-Csv -LiteralPath (Join-Path $ResourceRoot 'RESOURCE_MANIFEST.csv') -Encoding UTF8
foreach($path in $assets.Keys){
 $source=Join-Path $ResourceRoot $path
 $entry=$manifest | Where-Object Path -eq $path
 if(!$entry -or !(Test-Path -LiteralPath $source) -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.SHA256){throw ('Resource check failed: '+$path)}
}
foreach($project in @('blackwall-pulse/BlackwallPulse.csproj','esp-cover/Esp.csproj')){
 & $Dotnet build (Join-Path $repo $project) -c Release --nologo ('-p:GameRoot='+$GameRoot) -p:EftBuild=48558
 if($LASTEXITCODE -ne 0){throw ('Build failed: '+$project)}
}
$plugin=Join-Path $GameRoot 'BepInEx/plugins/BlackwallPulse'
$runtime=Join-Path $GameRoot 'BepInEx/plugins/LocalSpt5ESP/runtime'
if(!(Test-Path -LiteralPath (Join-Path $GameRoot 'BepInEx/plugins/LocalSpt5ESP/Local.Spt5.ESP.dll'))){throw 'Install Assistant first for its menu and shortcut integration.'}
$backup=Join-Path $GameRoot ('mod-backups/blackwall-prototype-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmssfff'))
[IO.Directory]::CreateDirectory($backup)|Out-Null
foreach($item in @($plugin,(Join-Path $runtime 'Local.Spt5.ESP.Core.module'))){if(Test-Path -LiteralPath $item){Copy-Item -LiteralPath $item -Destination $backup -Recurse}}
[IO.Directory]::CreateDirectory((Join-Path $plugin 'Assets'))|Out-Null
[IO.Directory]::CreateDirectory($runtime)|Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'blackwall-pulse/bin/Release/net10.0/Local.BlackwallPulse.dll') -Destination $plugin
foreach($path in $assets.Keys){Copy-Item -LiteralPath (Join-Path $ResourceRoot $path) -Destination (Join-Path $plugin ('Assets/'+$assets[$path]))}
Copy-Item -LiteralPath (Join-Path $repo 'esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll') -Destination (Join-Path $runtime 'Local.Spt5.ESP.Core.module')
[IO.File]::WriteAllText((Join-Path $runtime 'reload.request'),[DateTime]::UtcNow.ToString('O'))
$verification=@{}
Get-ChildItem -LiteralPath $plugin -Recurse -File | Where-Object Name -ne 'installed-checksums.json' | ForEach-Object {$verification[$_.FullName.Substring($plugin.Length+1).Replace('\','/')]=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
[IO.File]::WriteAllText((Join-Path $plugin 'installed-checksums.json'),($verification|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Write-Output ('Installed Blackwall Pulse 0.1.0 and Assistant bridge. Backup: '+$backup)
Write-Output 'Start the client. Assistant > Model & Camera > Blackwall Pulse. F11: cast; Alt+F11: harmless preview.'
