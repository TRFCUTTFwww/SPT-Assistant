param(
 [string]$GameRoot='D:/SPT-5.0.0-48558',
 [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$GameRoot=(Resolve-Path -LiteralPath $GameRoot).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){throw 'OutputDirectory must be new.'}
[IO.Directory]::CreateDirectory($OutputDirectory)|Out-Null
$files=[ordered]@{
 'esp-cover/bin/Release/net10.0/Local.Spt5.ESP.Core.dll'='Local.Spt5.ESP.Core.module'
 'esp-host/bin/Release/net10.0/Local.Spt5.ESP.dll'='Local.Spt5.ESP.dll'
 'gear-mail/bin/Release/net10.0/LocalGearMail.dll'='LocalGearMail.dll'
 'gear-admin-module/bin/Release/net10.0/LocalGearAdmin.dll'='LocalGearAdmin.module'
 'raid-controls-server/bin/Release/net10.0/LocalRaidControls.dll'='LocalRaidControls.dll'
}
$hashes=[ordered]@{}
foreach($entry in $files.GetEnumerator()){
 $source=Join-Path $repo $entry.Key
 if(!(Test-Path -LiteralPath $source -PathType Leaf)){throw ('Build first: '+$entry.Key)}
 $destination=Join-Path $OutputDirectory $entry.Value
 Copy-Item -LiteralPath $source -Destination $destination
 $hashes[$entry.Value]=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
}
$utf8=[Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'checksums.json'),($hashes|ConvertTo-Json),$utf8)
$apply=@'
param([string]$GameRoot='__GAME_ROOT__',[switch]$VerifyOnly)
$ErrorActionPreference='Stop'
$GameRoot=(Resolve-Path -LiteralPath $GameRoot).Path
$entries=[ordered]@{
 'Local.Spt5.ESP.Core.module'='BepInEx/plugins/LocalSpt5ESP/runtime/Local.Spt5.ESP.Core.module'
 'Local.Spt5.ESP.dll'='BepInEx/plugins/LocalSpt5ESP/Local.Spt5.ESP.dll'
 'LocalGearMail.dll'='SPT_Runtime/user/mods/LocalGearMail/LocalGearMail.dll'
 'LocalGearAdmin.module'='SPT_Runtime/user/mods/LocalGearMail/LocalGearAdmin.module'
 'LocalRaidControls.dll'='SPT_Runtime/user/mods/LocalRaidControls/LocalRaidControls.dll'
}
$hashes=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'checksums.json') -Raw|ConvertFrom-Json
foreach($entry in $entries.GetEnumerator()){
 $source=Join-Path $PSScriptRoot $entry.Key
 if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $hashes.($entry.Key)){throw ('Payload checksum mismatch: '+$entry.Key)}
 $target=[IO.Path]::GetFullPath((Join-Path $GameRoot $entry.Value))
 if(!$target.StartsWith($GameRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Destination escapes game root.'}
 if(!(Test-Path -LiteralPath $target -PathType Leaf)){throw ('Existing installation missing: '+$entry.Value)}
}
if($VerifyOnly){Write-Output 'Verified five payload checksums and existing installation paths; no files changed.';exit 0}
if(Get-Process -Name EscapeFromTarkov,SPT.Server -ErrorAction SilentlyContinue){throw 'Close EscapeFromTarkov and SPT.Server before applying this update. No process was stopped.'}
# Preflight every destination before replacing any component.
foreach($entry in $entries.GetEnumerator()){$stream=[IO.File]::Open((Join-Path $GameRoot $entry.Value),[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$stream.Dispose()}
$backup=Join-Path $PSScriptRoot ('backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
[IO.Directory]::CreateDirectory($backup)|Out-Null
foreach($entry in $entries.GetEnumerator()){Copy-Item -LiteralPath (Join-Path $GameRoot $entry.Value) -Destination (Join-Path $backup $entry.Key)}
$changed=[Collections.Generic.List[string]]::new()
try{
 foreach($entry in $entries.GetEnumerator()){
  $destination=Join-Path $GameRoot $entry.Value
  $temporary=$destination+'.editor-update.tmp'
  Copy-Item -LiteralPath (Join-Path $PSScriptRoot $entry.Key) -Destination $temporary
  [IO.File]::Replace($temporary,$destination,(Join-Path $backup ($entry.Key+'.replaced')))
  $changed.Add($entry.Key)
  if((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $hashes.($entry.Key)){throw ('Installed checksum mismatch: '+$entry.Key)}
 }
}catch{
 foreach($name in $changed){Copy-Item -LiteralPath (Join-Path $backup $name) -Destination (Join-Path $GameRoot $entries[$name]) -Force}
 throw
}
Write-Output ('Installed five components. Previous binaries: '+$backup)
Write-Output 'Start the SPT server and game normally. Configuration and profiles were not modified.'
'@
$apply=$apply.Replace('__GAME_ROOT__',$GameRoot.Replace("'","''"))
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'Apply.ps1'),$apply,$utf8)
$launcher="@echo off`r`npowershell.exe -NoProfile -ExecutionPolicy Bypass -File `"%~dp0Apply.ps1`"`r`npause`r`n"
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'Apply.cmd'),$launcher,[Text.Encoding]::ASCII)
Copy-Item -LiteralPath (Join-Path $repo 'docs/world-editor.md') -Destination (Join-Path $OutputDirectory 'world-editor.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs/error-protection-tests.md') -Destination (Join-Path $OutputDirectory 'error-protection-tests.md')
Write-Output ('Staged five-component update: '+$OutputDirectory)
