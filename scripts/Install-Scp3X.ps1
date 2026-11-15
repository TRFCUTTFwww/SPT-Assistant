param([Parameter(Mandatory=$true)][string]$AssetsDirectory,[string]$GameRoot='D:/SPT-5.0.0-48558',[string]$Dotnet='D:/SPT_Mod_Staging_20261027/dotnet-sdk/dotnet.exe')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$hostRoot=Join-Path $GameRoot 'BepInEx/plugins/UniversalModHost'
if(!(Test-Path -LiteralPath (Join-Path $hostRoot 'Local.UniversalModHost.dll'))){throw 'Install the universal client host first'}
foreach($file in @('weapon.json','icon.png','mesh-2212.bin','mesh-2721.bin','mesh-3128.bin','mesh-3782.bin','3x_single_shot.wav','3x_rapid_shot.wav')){if(!(Test-Path -LiteralPath (Join-Path $AssetsDirectory $file))){throw ('Missing 3-X resource: '+$file)}}
foreach($project in @('scp3x-client/Client.csproj','projecte-server/Server.csproj')){& $Dotnet build (Join-Path $repo $project) -c Release --nologo;if($LASTEXITCODE -ne 0){throw ('Build failed: '+$project)}}
& $Dotnet run --project (Join-Path $repo 'scp3x-tests/Tests.csproj') -c Release
if($LASTEXITCODE -ne 0){throw '3-X boundary checks failed'}
$server=Join-Path $GameRoot 'SPT_Runtime/user/mods/ProjectE'
if(!(Test-Path -LiteralPath (Join-Path $server 'Local.ProjectE.Server.dll'))){throw 'ProjectE stable server hot host is required'}
$source=Join-Path $repo 'projecte-server/bin/Release/net10.0/Local.ProjectE.Server.Logic.dll'
$target=Join-Path $server 'Local.ProjectE.Logic.module'
$backup=Join-Path $GameRoot ('mod-backups/scp3x-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
if(Test-Path -LiteralPath $target){Copy-Item -LiteralPath $target -Destination (Join-Path $backup 'Local.ProjectE.Logic.module')}
Copy-Item -LiteralPath $source -Destination ($target+'.scp3x-new')
if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath ($target+'.scp3x-new')).Hash){throw 'Server module staged hash mismatch'}
if(Test-Path -LiteralPath $target){[IO.File]::Replace(($target+'.scp3x-new'),$target,(Join-Path $backup 'Local.ProjectE.Logic.atomic-backup'))}else{[IO.File]::Move(($target+'.scp3x-new'),$target)}
& (Join-Path $PSScriptRoot 'Publish-HotModule.ps1') -Id scp3x -Assembly (Join-Path $repo 'scp3x-client/bin/Release/net10.0/Local.Scp3X.HotModule.dll') -EntryType Scp3X.Client.DisruptorModule -Version 1.0.2 -Name 'SCP 3-X Particle Disruptor' -HostRoot $hostRoot -AssetsDirectory $AssetsDirectory
Copy-Item -LiteralPath (Join-Path $repo 'docs/scp3x.md') -Destination (Join-Path $server 'SCPSL_3-X_README.md')
Write-Output ('Installed 3-X; prior server module: '+$backup)
