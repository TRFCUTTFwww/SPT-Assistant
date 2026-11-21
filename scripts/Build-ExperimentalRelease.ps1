param(
 [Parameter(Mandatory=$true)][string]$GameRoot,
 [Parameter(Mandatory=$true)][string]$OutputDirectory,
 [string]$Dotnet='dotnet',
 [string]$BlackwallAssetRoot
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$GameRoot=(Resolve-Path -LiteralPath $GameRoot).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $OutputDirectory) -and (Get-ChildItem -LiteralPath $OutputDirectory -Force | Select-Object -First 1)){throw 'OutputDirectory must be new or empty'}
[IO.Directory]::CreateDirectory($OutputDirectory)|Out-Null
$version=([IO.File]::ReadAllText((Join-Path $repo 'VERSION'))).Trim()
if($version -ne '2.3.5'){throw 'Review packaging for the new release version'}
foreach($project in @('esp-cover/Esp.csproj','gear-admin-module/AdminModule.csproj','raid-controls-server/RaidControls.csproj','quickhack-mod/Quickhacks.csproj')){
 & $Dotnet build (Join-Path $repo $project) -c Release --nologo ('-p:GameRoot='+$GameRoot) -p:EftBuild=48558
 if($LASTEXITCODE -ne 0){throw ('Build failed: '+$project)}
}
& (Join-Path $PSScriptRoot 'Build-CrashMonitor.ps1')
$stage=Join-Path $OutputDirectory 'package'
$payload=Join-Path $stage 'payload'
[IO.Directory]::CreateDirectory($payload)|Out-Null
$components=@{
 'crash-monitor/bin/Assistant.CrashMonitor.exe'='Assistant.CrashMonitor.exe'
 'quickhack-mod/bin/Release/net10.0/Local.Cp2077.Quickhacks.Core.dll'='Quickhacks.Core.module'
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
[IO.Directory]::CreateDirectory((Join-Path $stage 'docs/licenses'))|Out-Null
foreach($name in @('INSTALL.md','profile-backup.md','release-2.3.5.md','validation-2.3.5.json','release-2.3.4.md','validation-2.3.4.json','release-2.3.3.md','validation-2.3.3.json','release-2.3.2.md','release-2.3.1.md','validation-2.3.1.json','release-2.3.md','validation-2.3.json','installer-validation-2.3.json','hotkeys.md','world-editor.md','blackwall-pulse.md','blackwall-assets.json','minecraft-assets.json','CHAT.md','COMMAND-OPTIONS.md','corner-version-scripts.md','raid-parameters.md','raid-history-20261118.md','universal-mod-host.md','licenses/Jint.txt','licenses/Acornima.txt')){Copy-Item -LiteralPath (Join-Path $repo ('docs/'+$name)) -Destination (Join-Path $stage ('docs/'+$name))}
# Package only the explicitly inventoried Blackwall runtime resources.
if(!$BlackwallAssetRoot){
 $BlackwallAssetRoot=Join-Path $GameRoot 'BepInEx/plugins/LocalSpt5ESP/Blackwall/Assets'
 if(!(Test-Path -LiteralPath $BlackwallAssetRoot)){$BlackwallAssetRoot=Join-Path $GameRoot 'BepInEx/plugins/BlackwallPulse/Assets'}
}
$resourceManifest=Get-Content -LiteralPath (Join-Path $repo 'docs/blackwall-assets.json') -Raw -Encoding UTF8|ConvertFrom-Json
$assetDestination=Join-Path $payload 'Blackwall/Assets'
[IO.Directory]::CreateDirectory($assetDestination)|Out-Null
foreach($entry in $resourceManifest.files){
 if($entry.name -notmatch '^[a-zA-Z0-9_-]+\.(glb|png|wav)$'){throw 'Invalid Blackwall resource name'}
 $inputAsset=Join-Path $BlackwallAssetRoot $entry.name
 if(!(Test-Path -LiteralPath $inputAsset -PathType Leaf) -or (Get-FileHash -LiteralPath $inputAsset -Algorithm SHA256).Hash -ne $entry.sha256){throw ('Missing or modified Blackwall resource: '+$entry.name)}
 Copy-Item -LiteralPath $inputAsset -Destination (Join-Path $assetDestination $entry.name)
}
$minecraftManifest=Get-Content -LiteralPath (Join-Path $repo 'docs/minecraft-assets.json') -Raw -Encoding UTF8|ConvertFrom-Json
foreach($entry in $minecraftManifest.files){
 $asset=Join-Path $repo $entry.source
 if(!(Test-Path -LiteralPath $asset -PathType Leaf) -or (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash -ne $entry.sha256){throw ('Invalid MC resource: '+$entry.source)}
 $destination=Join-Path $payload $entry.payload
 [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))|Out-Null
 Copy-Item -LiteralPath $asset -Destination $destination
}
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
& (Join-Path $PSScriptRoot 'Build-Setup.ps1') -PackageDirectory $stage -OutputDirectory (Join-Path $OutputDirectory 'setup')
$setupName='SPT-Assistant-'+$version+'-Setup.exe'
Copy-Item -LiteralPath (Join-Path $OutputDirectory ('setup/'+$setupName)) -Destination (Join-Path $OutputDirectory $setupName)
$lines=@(foreach($name in @([IO.Path]::GetFileName($archive),$setupName)){(Get-FileHash -LiteralPath (Join-Path $OutputDirectory $name) -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$name})
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'SHA256SUMS.txt'),(($lines -join [Environment]::NewLine)+[Environment]::NewLine),$utf8)
Write-Output ('Built experimental package: '+$archive)
