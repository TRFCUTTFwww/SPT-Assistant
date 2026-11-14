param([string]$GameRoot='D:/SPT-5.0.0-48558',[string]$Dotnet='D:/SPT_Mod_Staging_20261027/dotnet-sdk/dotnet.exe')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$hostRoot=Join-Path $GameRoot 'BepInEx/plugins/UniversalModHost'
if(!(Test-Path -LiteralPath (Join-Path $hostRoot 'Local.UniversalModHost.dll'))){throw 'Install the universal host first.'}
foreach($project in @('atm-swords-client/Client.csproj','projecte-client/Client.csproj','projecte-server/Server.csproj')){
 & $Dotnet build (Join-Path $repo $project) -c Release --nologo ('-p:GameRoot='+$GameRoot)
 if($LASTEXITCODE -ne 0){throw ('Build failed: '+$project)}
}
$serverRoot=Join-Path $GameRoot 'SPT_Runtime/user/mods/ProjectE'
New-Item -ItemType Directory -Path $serverRoot -Force | Out-Null
$source=Join-Path $repo 'projecte-server/bin/Release/net10.0/Local.ProjectE.Server.dll'
$target=Join-Path $serverRoot 'Local.ProjectE.Server.dll'
if(Test-Path -LiteralPath $target){
 $backup=Join-Path $GameRoot ('mod-backups/projecte-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
 New-Item -ItemType Directory -Path $backup -Force | Out-Null
 Copy-Item -LiteralPath $target -Destination $backup
}
Copy-Item -LiteralPath $source -Destination $target
if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw 'Server installation hash mismatch'}
& (Join-Path $PSScriptRoot 'Publish-HotModule.ps1') -Id atm-swords -Assembly (Join-Path $repo 'atm-swords-client/bin/Release/net10.0/Local.AllTheModium.HotModule.dll') -EntryType AllTheModium.Client.SwordModule -Version 1.1.2 -Name 'Pixel Swords - AllTheModium and ProjectE' -HostRoot $hostRoot -AssetsDirectory (Join-Path $repo 'atm-swords-client/Assets')
& (Join-Path $PSScriptRoot 'Publish-HotModule.ps1') -Id projecte -Assembly (Join-Path $repo 'projecte-client/bin/Release/net10.0/Local.ProjectE.HotModule.dll') -EntryType ProjectE.Client.ProjectModule -Version 1.0.2 -Name 'ProjectE Transmutation and MC HUD' -HostRoot $hostRoot -AssetsDirectory (Join-Path $repo 'projecte-client/Assets')
Copy-Item -LiteralPath (Join-Path $repo 'docs/projecte.md') -Destination (Join-Path $serverRoot 'README.md')
Copy-Item -LiteralPath (Join-Path $repo 'projecte-client/Assets/ProjectE-LICENSE.txt') -Destination $serverRoot
Write-Output 'Installed ProjectE. Restart the server and client once for the new templates and table endpoint. Later client updates use the universal host.'
