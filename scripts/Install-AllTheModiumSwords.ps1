param([string]$GameRoot='D:/SPT-5.0.0-48558',[string]$Dotnet='D:/SPT_Mod_Staging_20261027/dotnet-sdk/dotnet.exe')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$hostRoot=Join-Path $GameRoot 'BepInEx/plugins/UniversalModHost'
if(!(Test-Path -LiteralPath (Join-Path $hostRoot 'Local.UniversalModHost.dll'))){throw 'Install the universal host before installing swords.'}
foreach($project in @('atm-swords-client/Client.csproj','atm-swords-server/Server.csproj')){
 & $Dotnet build (Join-Path $repo $project) -c Release --nologo ('-p:GameRoot='+$GameRoot)
 if($LASTEXITCODE -ne 0){throw ('Build failed: '+$project)}
}
$serverRoot=Join-Path $GameRoot 'SPT_Runtime/user/mods/AllTheModiumSwords'
New-Item -ItemType Directory -Force $serverRoot | Out-Null
$source=Join-Path $repo 'atm-swords-server/bin/Release/net10.0/Local.AllTheModium.Server.dll'
$target=Join-Path $serverRoot 'Local.AllTheModium.Server.dll'
if(Test-Path -LiteralPath $target){
 $backup=Join-Path $GameRoot ('mod-backups/atm-swords-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
 New-Item -ItemType Directory -Force $backup | Out-Null
 Copy-Item -LiteralPath $target -Destination $backup
}
Copy-Item -LiteralPath $source -Destination $target
if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw 'Server installation hash mismatch'}
& (Join-Path $PSScriptRoot 'Publish-HotModule.ps1') -Id atm-swords -Assembly (Join-Path $repo 'atm-swords-client/bin/Release/net10.0/Local.AllTheModium.HotModule.dll') -EntryType AllTheModium.Client.SwordModule -Version 1.0.1 -Name 'AllTheModium Swords' -HostRoot $hostRoot -AssetsDirectory (Join-Path $repo 'atm-swords-client/Assets')
Copy-Item -LiteralPath (Join-Path $repo 'docs/allthemodium-swords.md') -Destination (Join-Path $serverRoot 'README.md')
Write-Output 'Installed four independent swords. Restart the server and client to receive new item templates. Later model updates use the universal hot loader.'
