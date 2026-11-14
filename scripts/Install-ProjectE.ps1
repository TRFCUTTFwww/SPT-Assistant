param([string]$GameRoot='D:/SPT-5.0.0-48558',[string]$Dotnet='D:/SPT_Mod_Staging_20261027/dotnet-sdk/dotnet.exe')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$hostRoot=Join-Path $GameRoot 'BepInEx/plugins/UniversalModHost'
if(!(Test-Path -LiteralPath (Join-Path $hostRoot 'Local.UniversalModHost.dll'))){throw 'Install the universal host first.'}
function Install-ServerAssembly([string]$Source,[string]$Target){
 # A running .NET server maps the DLL read-only. Replace its directory entry
 # atomically; its current mapping stays alive until the user restarts it.
 $staged=$Target+'.new-'+[Guid]::NewGuid().ToString('N')
 try{
  Copy-Item -LiteralPath $Source -Destination $staged
  if((Get-FileHash -LiteralPath $Source).Hash -ne (Get-FileHash -LiteralPath $staged).Hash){throw 'Staged server assembly hash mismatch'}
  if(Test-Path -LiteralPath $Target){
   $rollback=Join-Path $GameRoot ('mod-backups/server-replace-'+[Guid]::NewGuid().ToString('N'))
   New-Item -ItemType Directory -Path $rollback -Force | Out-Null
   [IO.File]::Replace($staged,$Target,(Join-Path $rollback ([IO.Path]::GetFileName($Target))))
  }else{[IO.File]::Move($staged,$Target)}
  if((Get-FileHash -LiteralPath $Source).Hash -ne (Get-FileHash -LiteralPath $Target).Hash){throw 'Installed server assembly hash mismatch'}
 }finally{if(Test-Path -LiteralPath $staged){Remove-Item -LiteralPath $staged -Force}}
}
foreach($project in @('atm-swords-client/Client.csproj','atm-swords-server/Server.csproj','projecte-client/Client.csproj','projecte-server/Server.csproj','projecte-server-host/ServerHost.csproj')){
 & $Dotnet build (Join-Path $repo $project) -c Release --nologo ('-p:GameRoot='+$GameRoot)
 if($LASTEXITCODE -ne 0){throw ('Build failed: '+$project)}
}
$serverRoot=Join-Path $GameRoot 'SPT_Runtime/user/mods/ProjectE'
New-Item -ItemType Directory -Path $serverRoot -Force | Out-Null
$source=Join-Path $repo 'projecte-server-host/bin/Release/net10.0/Local.ProjectE.Server.dll'
$target=Join-Path $serverRoot 'Local.ProjectE.Server.dll'
if(Test-Path -LiteralPath $target){
 $backup=Join-Path $GameRoot ('mod-backups/projecte-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
 New-Item -ItemType Directory -Path $backup -Force | Out-Null
 Copy-Item -LiteralPath $target -Destination $backup
}
Install-ServerAssembly $source $target
Install-ServerAssembly (Join-Path $repo 'projecte-server/bin/Release/net10.0/Local.ProjectE.Server.Logic.dll') (Join-Path $serverRoot 'Local.ProjectE.Logic.module')
New-Item -ItemType Directory -Path (Join-Path $serverRoot 'Assets') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'projecte-server/Assets/developer.png') -Destination (Join-Path $serverRoot 'Assets/developer.png')
if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw 'Server installation hash mismatch'}
$atmRoot=Join-Path $GameRoot 'SPT_Runtime/user/mods/AllTheModiumSwords'
New-Item -ItemType Directory -Path $atmRoot -Force | Out-Null
$atmSource=Join-Path $repo 'atm-swords-server/bin/Release/net10.0/Local.AllTheModium.Server.dll'
$atmTarget=Join-Path $atmRoot 'Local.AllTheModium.Server.dll'
if(Test-Path -LiteralPath $atmTarget){$atmBackup=Join-Path $GameRoot ('mod-backups/atm-two-cell-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'));New-Item -ItemType Directory -Path $atmBackup -Force | Out-Null;Copy-Item -LiteralPath $atmTarget -Destination $atmBackup}
Install-ServerAssembly $atmSource $atmTarget
if((Get-FileHash -LiteralPath $atmSource).Hash -ne (Get-FileHash -LiteralPath $atmTarget).Hash){throw 'ATM server installation hash mismatch'}
& (Join-Path $PSScriptRoot 'Publish-HotModule.ps1') -Id atm-swords -Assembly (Join-Path $repo 'atm-swords-client/bin/Release/net10.0/Local.AllTheModium.HotModule.dll') -EntryType AllTheModium.Client.SwordModule -Version 1.3.0 -Name 'Pixel Swords and Portable Tools' -HostRoot $hostRoot -AssetsDirectory (Join-Path $repo 'atm-swords-client/Assets')
& (Join-Path $PSScriptRoot 'Publish-HotModule.ps1') -Id projecte -Assembly (Join-Path $repo 'projecte-client/bin/Release/net10.0/Local.ProjectE.HotModule.dll') -EntryType ProjectE.Client.ProjectModule -Version 1.2.0 -Name 'ProjectE Transmutation, Tools and Abilities' -HostRoot $hostRoot -AssetsDirectory (Join-Path $repo 'projecte-client/Assets')
Copy-Item -LiteralPath (Join-Path $repo 'docs/projecte.md') -Destination (Join-Path $serverRoot 'README.md')
Copy-Item -LiteralPath (Join-Path $repo 'projecte-client/Assets/ProjectE-LICENSE.txt') -Destination $serverRoot
Write-Output 'Installed ProjectE. Initial stable-server-host installation needs one server restart. Later logic modules reload live. Client F9 refreshes templates, handbook, locales and traders without relogin.'
