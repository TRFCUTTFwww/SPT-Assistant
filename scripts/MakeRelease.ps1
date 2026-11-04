param([Parameter(Mandatory=$true)][string]$Version,[string]$Dotnet='dotnet',[string]$ServerPayloadVersion='1.32.7')
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid release version'}
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game=Split-Path $repo -Parent
$release=Join-Path $repo ('release-'+$Version)
$payload=Join-Path $release 'payload'
$source=Join-Path $repo 'esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll'
if(-not [Diagnostics.FileVersionInfo]::GetVersionInfo($source).FileVersion.StartsWith($Version+'.')){throw 'Built module version does not match requested release'}
New-Item -ItemType Directory -Path $payload -Force|Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $payload 'Local.Spt5.ESP.Core.module')
Copy-Item -LiteralPath (Join-Path $repo 'esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.pdb') -Destination $payload
foreach($name in @('LocalGearMail.dll','LocalRaidControls.dll')){Copy-Item -LiteralPath (Join-Path $repo ('release-'+$ServerPayloadVersion+'/payload/'+$name)) -Destination $payload}
Copy-Item -LiteralPath (Join-Path $repo ('docs/release-'+$Version+'-zh.txt')) -Destination (Join-Path $payload 'README-zh.txt')
$checksums=[ordered]@{}
Get-ChildItem -LiteralPath $payload -File|Where-Object Name -ne 'checksums.json'|Sort-Object Name|ForEach-Object {$checksums[$_.Name]=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
[IO.File]::WriteAllText((Join-Path $payload 'checksums.json'),($checksums|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
& $Dotnet build (Join-Path $repo ('installer-'+$Version+'/Installer.csproj')) -c Release --nologo -o $release
if($LASTEXITCODE -ne 0){throw 'Installer build failed'}
& $Dotnet (Join-Path $release 'SptControlInstaller.dll') --verify $game
if($LASTEXITCODE -ne 0){throw 'Package verification failed'}
