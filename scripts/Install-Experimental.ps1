param(
 [Parameter(Mandatory=$true)][string]$GameRoot,
 [ValidateSet('ch','en')][string]$Language='en',
 [switch]$ClientOnly,
 [switch]$VerifyOnly
)
$ErrorActionPreference='Stop'
$utf8=[Text.UTF8Encoding]::new($false)
$package=$PSScriptRoot
$GameRoot=[IO.Path]::GetFullPath($GameRoot)
$prefix=$GameRoot.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
function Target([string]$relative){
 $resolved=[IO.Path]::GetFullPath((Join-Path $GameRoot $relative))
 if(-not $resolved.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Path outside target game directory'}
 return $resolved
}
$gameExe=Target 'EscapeFromTarkov.exe'
if(-not(Test-Path -LiteralPath $gameExe -PathType Leaf)){throw 'EscapeFromTarkov.exe was not found in GameRoot'}
$gameVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($gameExe)
if(($gameVersion.ProductVersion+' '+$gameVersion.FileVersion) -notmatch '48558'){throw 'This release targets EFT 1.3.0.1.48558. Other builds are not supported by this package.'}
foreach($relative in @('BepInEx/core/BepInEx.Unity.IL2CPP.dll','BepInEx/interop/Assembly-CSharp.dll','BepInEx/plugins/sptushonka/SPTushonka.Common.dll')){
 if(-not(Test-Path -LiteralPath (Target $relative) -PathType Leaf)){throw ('Start the matching SPT installation once to generate dependencies: '+$relative)}
}
$files=@{
 'payload/Local.Spt5.ESP.dll'='BepInEx/plugins/LocalSpt5ESP/Local.Spt5.ESP.dll'
 'payload/Local.Spt5.ESP.Core.module'='BepInEx/plugins/LocalSpt5ESP/runtime/Local.Spt5.ESP.Core.module'
}
if(-not $ClientOnly){
 if(-not(Test-Path -LiteralPath (Target 'SPT_Runtime/SPTarkov.Server.Core.dll'))){throw 'SPT 5.0 server runtime was not found'}
 $files['payload/LocalGearMail.dll']='SPT_Runtime/user/mods/LocalGearMail/LocalGearMail.dll'
 $files['payload/LocalGearAdmin.module']='SPT_Runtime/user/mods/LocalGearMail/LocalGearAdmin.module'
 $files['payload/LocalRaidControls.dll']='SPT_Runtime/user/mods/LocalRaidControls/LocalRaidControls.dll'
}
$resourceManifest=Get-Content -LiteralPath (Join-Path $package 'docs/blackwall-assets.json') -Raw -Encoding UTF8|ConvertFrom-Json
foreach($entry in $resourceManifest.files){
 if($entry.name -notmatch '^[a-zA-Z0-9_-]+\.(glb|png|wav)$'){throw 'Invalid Blackwall resource name'}
 $relative='payload/Blackwall/Assets/'+$entry.name
 $files[$relative]='BepInEx/plugins/LocalSpt5ESP/Blackwall/Assets/'+$entry.name
 $asset=Join-Path $package $relative
 if(!(Test-Path -LiteralPath $asset -PathType Leaf) -or (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash -ne $entry.sha256){throw ('Invalid Blackwall resource: '+$entry.name)}
}
$checks=Get-Content -LiteralPath (Join-Path $package 'checksums.json') -Raw -Encoding utf8 | ConvertFrom-Json
foreach($entry in $checks.PSObject.Properties){
 $inputFile=[IO.Path]::GetFullPath((Join-Path $package $entry.Name))
 $packagePrefix=$package.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
 if(-not $inputFile.StartsWith($packagePrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid package checksum path'}
 if(-not(Test-Path -LiteralPath $inputFile -PathType Leaf) -or (Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash -ne $entry.Value){throw ('Package checksum mismatch: '+$entry.Name)}
}
foreach($entry in $files.GetEnumerator()){
 if(-not($checks.PSObject.Properties.Name -contains $entry.Key)){throw ('Required payload missing from checksum manifest: '+$entry.Key)}
}
# Generate caches from the user's own database; never redistribute game data.
$localePath=Target ('SPT_Runtime/SPT_Data/database/locales/global/'+$Language+'.json')
$itemsPath=Target 'SPT_Runtime/SPT_Data/database/templates/items.json'
$pricesPath=Target 'SPT_Runtime/SPT_Data/database/templates/prices.json'
# Windows PowerShell ConvertFrom-Json rejects native case-distinct properties
# such as shotgunDispersion/ShotgunDispersion. Keep native dictionaries intact.
Add-Type -AssemblyName System.Web.Extensions
$json=[Web.Script.Serialization.JavaScriptSerializer]::new()
$json.MaxJsonLength=[int]::MaxValue
$json.RecursionLimit=256
$localized=$json.DeserializeObject([IO.File]::ReadAllText($localePath))
$items=$json.DeserializeObject([IO.File]::ReadAllText($itemsPath))
$prices=$json.DeserializeObject([IO.File]::ReadAllText($pricesPath))
$names=@{}
foreach($item in $items.GetEnumerator()){
 foreach($suffix in @(' Name',' ShortName')){
  $key=$item.Key+$suffix
  if($localized.ContainsKey($key)){$names[$key]=[string]$localized[$key]}
 }
 if($localized.ContainsKey($item.Key+' Name')){$names[$item.Key]=[string]$localized[$item.Key+' Name']}else{$names[$item.Key]=[string]$item.Value._name}
}
if($names.Count -lt 100 -or $prices.Count -lt 100){throw 'SPT item database is incomplete'}
$generated=@{
 'BepInEx/plugins/LocalSpt5ESP/esp5-names.json'=($names|ConvertTo-Json -Depth 3 -Compress)
 'BepInEx/plugins/LocalSpt5ESP/esp5-prices.json'=($prices|ConvertTo-Json -Depth 3 -Compress)
}
if($VerifyOnly){Write-Output 'PASS package hashes, target build/dependencies and local item database. No game files changed.';return}
$processNames=@('EscapeFromTarkov');if(-not $ClientOnly){$processNames+='SPT.Server'}
foreach($process in Get-Process -Name $processNames -ErrorAction SilentlyContinue){
 $processPath=$null
 try{$processPath=$process.Path}catch{}
 if(-not $processPath -or $processPath.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Close the target game and SPT server before installation'}
}
$backup=Target ('mod-backups/assistant-2.2-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmssfff'))
$completed=[Collections.Generic.List[object]]::new()
$legacyBackup=$null
function Prepare([string]$relative){
 $destination=Target $relative
 $old=$null
 if(Test-Path -LiteralPath $destination){
  $old=Join-Path $backup $relative
  [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($old))|Out-Null
  Copy-Item -LiteralPath $destination -Destination $old
 }
 [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))|Out-Null
 $completed.Add([pscustomobject]@{Destination=$destination;Old=$old})
 return $destination
}
try{
 foreach($entry in $files.GetEnumerator()){
  $destination=Prepare $entry.Value
  [IO.File]::WriteAllBytes($destination,[IO.File]::ReadAllBytes((Join-Path $package $entry.Key)))
  if((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $checks.($entry.Key)){throw ('Installed payload hash mismatch: '+$entry.Key)}
 }
 foreach($entry in $generated.GetEnumerator()){$destination=Prepare $entry.Key;[IO.File]::WriteAllText($destination,$entry.Value,$utf8)}
 $keyRelative='SPT_Runtime/user/mods/LocalGearMail/api-key.txt'
 if(-not $ClientOnly -and -not(Test-Path -LiteralPath (Target $keyRelative))){
  $destination=Prepare $keyRelative
  $random=New-Object byte[] 32
  $rng=[Security.Cryptography.RandomNumberGenerator]::Create()
  try{$rng.GetBytes($random)}finally{$rng.Dispose()}
  [IO.File]::WriteAllText($destination,[BitConverter]::ToString($random).Replace('-',''),$utf8)
 }
 # Retire the standalone DLL outside the entire BepInEx plugin search tree.
 $legacy=Target 'BepInEx/plugins/BlackwallPulse'
 if(Test-Path -LiteralPath $legacy -PathType Container){
  $legacyBackup=Join-Path $backup 'disabled-standalone-BlackwallPulse'
  [IO.Directory]::CreateDirectory($backup)|Out-Null
  Move-Item -LiteralPath $legacy -Destination $legacyBackup
 }
 $destination=Prepare 'BepInEx/plugins/LocalSpt5ESP/install-manifest.json'
 $manifest=@{version='2.2';hostVersion='1.5.0';targetSpt='5.0 BE';targetEft='1.3.0.1.48558';clientOnly=[bool]$ClientOnly;blackwallIntegrated=$true;blackwallAssets=$resourceManifest.files.Count;experimental=$true;inRaidVerified=$false;installedUtc=[DateTime]::UtcNow.ToString('O');backup=$backup;coreSha256=$checks.'payload/Local.Spt5.ESP.Core.module'}
 [IO.File]::WriteAllText($destination,($manifest|ConvertTo-Json -Depth 4),$utf8)
 Write-Output ('Installed SPT Assistant 2.2 (EXPERIMENTAL). Backup: '+$backup)
 Write-Output 'Start the SPT server, then its client. Press Right Shift. Fika members: connect to the host; do not start a local server.'
}catch{
 $failure=$_
 if($legacyBackup -and (Test-Path -LiteralPath $legacyBackup)){Move-Item -LiteralPath $legacyBackup -Destination (Target 'BepInEx/plugins/BlackwallPulse')}
 for($index=$completed.Count-1;$index -ge 0;$index--){
  $entry=$completed[$index]
  if($entry.Old){Copy-Item -LiteralPath $entry.Old -Destination $entry.Destination -Force}else{Remove-Item -LiteralPath $entry.Destination -Force -ErrorAction SilentlyContinue}
 }
 throw $failure
}
