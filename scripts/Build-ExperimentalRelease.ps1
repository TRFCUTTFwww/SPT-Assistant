param(
 [Parameter(Mandatory=$true)][string]$GameRoot,
 [Parameter(Mandatory=$true)][string]$OutputDirectory,
 [string]$Dotnet='dotnet'
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$GameRoot=(Resolve-Path -LiteralPath $GameRoot).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $OutputDirectory) -and (Get-ChildItem -LiteralPath $OutputDirectory -Force | Select-Object -First 1)){throw 'OutputDirectory must be new or empty'}
[IO.Directory]::CreateDirectory($OutputDirectory)|Out-Null
$version=([IO.File]::ReadAllText((Join-Path $repo 'VERSION'))).Trim()
if($version -ne '2.0.6'){throw 'Review packaging for the new release version'}
foreach($project in @('esp-cover/Esp.csproj','gear-admin-module/AdminModule.csproj','raid-controls-server/RaidControls.csproj')){
 & $Dotnet build (Join-Path $repo $project) -c Release --nologo ('-p:GameRoot='+$GameRoot) -p:EftBuild=48558
 if($LASTEXITCODE -ne 0){throw ('Build failed: '+$project)}
}
$stage=Join-Path $OutputDirectory 'package'
$payload=Join-Path $stage 'payload'
[IO.Directory]::CreateDirectory($payload)|Out-Null
$components=@{
 'esp-host/bin/Release/net10.0/Local.Spt5.ESP.dll'='Local.Spt5.ESP.dll'
 'esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll'='Local.Spt5.ESP.Core.module'
 'gear-mail/bin/Release/net10.0/LocalGearMail.dll'='LocalGearMail.dll'
 'gear-admin-module/bin/Release/net10.0/LocalGearAdmin.dll'='LocalGearAdmin.module'
 'raid-controls-server/bin/Release/net10.0/LocalRaidControls.dll'='LocalRaidControls.dll'
}
$core=Join-Path $repo 'esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll'
if(-not([Diagnostics.FileVersionInfo]::GetVersionInfo($core).FileVersion.StartsWith($version+'.'))){throw 'Core version differs from VERSION'}
foreach($entry in $components.GetEnumerator()){Copy-Item -LiteralPath (Join-Path $repo $entry.Key) -Destination (Join-Path $payload $entry.Value)}
Copy-Item -LiteralPath (Join-Path $repo 'scripts/Install-Experimental.ps1') -Destination (Join-Path $stage 'Install.ps1')
foreach($name in @('README.md','README.zh-CN.md','LICENSE')){Copy-Item -LiteralPath (Join-Path $repo $name) -Destination $stage}
[IO.Directory]::CreateDirectory((Join-Path $stage 'docs'))|Out-Null
foreach($name in @('INSTALL.md','release-2.0.6.md','validation-2.0.6.json')){Copy-Item -LiteralPath (Join-Path $repo ('docs/'+$name)) -Destination (Join-Path $stage 'docs')}
$checks=[ordered]@{}
Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object {
 $relative=$_.FullName.Substring($stage.Length+1).Replace('\','/')
 $checks[$relative]=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
}
$utf8=[Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $stage 'checksums.json'),($checks|ConvertTo-Json -Depth 3),$utf8)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=Join-Path $OutputDirectory ('SPT-Assistant-'+$version+'-experimental.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$archive,[IO.Compression.CompressionLevel]::Optimal,$false)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'SHA256SUMS.txt'),((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($archive)+[Environment]::NewLine),$utf8)
Write-Output ('Built experimental package: '+$archive)
