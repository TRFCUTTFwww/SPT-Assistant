param(
 [Parameter(Mandatory=$true)][string]$GameRoot,
 [ValidateSet('ch','en')][string]$Language='en',
 [switch]$ClientOnly,
 [switch]$VerifyOnly,
 [string]$ProgressPath,
 [string]$ResultPath
)
$ErrorActionPreference='Stop'
$rollbackSucceeded=$true
function Report-Progress([int]$percent,[string]$phase){
 if(!$ProgressPath){return}
 try{$value=@{percent=$percent;phase=$phase};[IO.File]::WriteAllText($ProgressPath+'.tmp',($value|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false));if(Test-Path -LiteralPath $ProgressPath){[IO.File]::Delete($ProgressPath)};[IO.File]::Move($ProgressPath+'.tmp',$ProgressPath)}catch{}
}
function Stop-Install([string]$code,[string]$message){$errorObject=[InvalidOperationException]::new($message);$errorObject.Data['InstallerCode']=$code;throw $errorObject}
function Write-Result([bool]$ok,[string]$code,[string]$message){
 if(!$ResultPath){return}
 $value=@{ok=$ok;code=$code;message=$message;version='2.3.4';backup=$backup;verified=[bool]$VerifyOnly;rollbackSucceeded=[bool]$rollbackSucceeded}
 [IO.File]::WriteAllText($ResultPath,($value|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
}
try{
Report-Progress 3 'CHECK_GAME'
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
if(-not(Test-Path -LiteralPath $gameExe -PathType Leaf)){Stop-Install 'GAME_NOT_FOUND' 'Choose the folder containing EscapeFromTarkov.exe.'}
$gameVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($gameExe)
if(($gameVersion.ProductVersion+' '+$gameVersion.FileVersion) -notmatch '48558'){Stop-Install 'WRONG_GAME_VERSION' 'This installer requires EFT 1.3.0.1.48558 / SPT 5.0 BE.'}
foreach($relative in @('BepInEx/core/BepInEx.Unity.IL2CPP.dll','BepInEx/interop/Assembly-CSharp.dll','BepInEx/plugins/sptushonka/SPTushonka.Common.dll')){
 if(-not(Test-Path -LiteralPath (Target $relative) -PathType Leaf)){Stop-Install 'DEPENDENCIES_MISSING' ('Start the matching SPT game once to generate required files: '+$relative)}
}
$files=@{
 'payload/Assistant.CrashMonitor.exe'='BepInEx/plugins/LocalSpt5ESP/Assistant.CrashMonitor.exe'
 'payload/Local.Spt5.ESP.dll'='BepInEx/plugins/LocalSpt5ESP/Local.Spt5.ESP.dll'
 'payload/Local.Spt5.ESP.Core.module'='BepInEx/plugins/LocalSpt5ESP/runtime/Local.Spt5.ESP.Core.module'
}
if(-not $ClientOnly){
 if(-not(Test-Path -LiteralPath (Target 'SPT_Runtime/SPTarkov.Server.Core.dll'))){Stop-Install 'SERVER_NOT_FOUND' 'SPT 5.0 server runtime was not found in the selected game directory.'}
 $files['payload/LocalGearMail.dll']='SPT_Runtime/user/mods/LocalGearMail/LocalGearMail.dll'
 $files['payload/LocalGearAdmin.module']='SPT_Runtime/user/mods/LocalGearMail/LocalGearAdmin.module'
 $files['payload/LocalRaidControls.dll']='SPT_Runtime/user/mods/LocalRaidControls/LocalRaidControls.dll'
}
# Existing Quickhacks installs receive this compatible core update; installing
# its separate resource package remains optional.
if((Test-Path -LiteralPath (Target 'BepInEx/plugins/CP2077Quickhacks/Local.Cp2077.Quickhacks.dll')) -and (Test-Path -LiteralPath (Join-Path $package 'payload/Quickhacks.Core.module'))){
 $files['payload/Quickhacks.Core.module']='BepInEx/plugins/CP2077Quickhacks/runtime/Local.Cp2077.Quickhacks.Core.module'
}
Report-Progress 10 'CHECK_PACKAGE'
$resourceManifest=Get-Content -LiteralPath (Join-Path $package 'docs/blackwall-assets.json') -Raw -Encoding UTF8|ConvertFrom-Json
foreach($entry in $resourceManifest.files){
 if($entry.name -notmatch '^[a-zA-Z0-9_-]+\.(glb|png|wav)$'){throw 'Invalid Blackwall resource name'}
 $relative='payload/Blackwall/Assets/'+$entry.name
 $files[$relative]='BepInEx/plugins/LocalSpt5ESP/Blackwall/Assets/'+$entry.name
 $asset=Join-Path $package $relative
 if(!(Test-Path -LiteralPath $asset -PathType Leaf) -or (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash -ne $entry.sha256){throw ('Invalid Blackwall resource: '+$entry.name)}
}
$minecraftManifest=Get-Content -LiteralPath (Join-Path $package 'docs/minecraft-assets.json') -Raw -Encoding UTF8|ConvertFrom-Json
foreach($entry in $minecraftManifest.files){
 if($entry.server -and $ClientOnly){continue}
 $relative='payload/'+$entry.payload
 $asset=Join-Path $package $relative
 if(!(Test-Path -LiteralPath $asset -PathType Leaf) -or (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash -ne $entry.sha256){throw ('Invalid MC resource: '+$entry.payload)}
 $files[$relative]=$entry.target
}
$checks=Get-Content -LiteralPath (Join-Path $package 'checksums.json') -Raw -Encoding utf8 | ConvertFrom-Json
foreach($entry in $checks.PSObject.Properties){
 $inputFile=[IO.Path]::GetFullPath((Join-Path $package $entry.Name))
 $packagePrefix=$package.TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
 if(-not $inputFile.StartsWith($packagePrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid package checksum path'}
 if(-not(Test-Path -LiteralPath $inputFile -PathType Leaf) -or (Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash -ne $entry.Value){Stop-Install 'PACKAGE_DAMAGED' ('Package checksum mismatch: '+$entry.Name)}
}
foreach($entry in $files.GetEnumerator()){
 if(-not($checks.PSObject.Properties.Name -contains $entry.Key)){Stop-Install 'PACKAGE_DAMAGED' ('Required payload missing from checksum manifest: '+$entry.Key)}
}
Report-Progress 20 'CHECK_DATA'
# Generate caches from the user's own database; never redistribute game data.
$localePath=Target ('SPT_Runtime/SPT_Data/database/locales/global/'+$Language+'.json')
$itemsPath=Target 'SPT_Runtime/SPT_Data/database/templates/items.json'
$pricesPath=Target 'SPT_Runtime/SPT_Data/database/templates/prices.json'
foreach($path in @($localePath,$itemsPath,$pricesPath)){if(!(Test-Path -LiteralPath $path -PathType Leaf)){Stop-Install 'DATABASE_MISSING' 'The selected SPT installation is missing its local database.'}}
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
if($VerifyOnly){Report-Progress 100 'VERIFIED';Write-Result $true 'OK' 'Package and selected game directory verified. No game files changed.';Write-Output 'PASS package hashes, target build/dependencies and local item database. No game files changed.';return}
$processNames=@('EscapeFromTarkov');if(-not $ClientOnly){$processNames+='SPT.Server'}
foreach($process in Get-Process -Name $processNames -ErrorAction SilentlyContinue){
 $processPath=$null
 try{$processPath=$process.Path}catch{}
 if(-not $processPath -or $processPath.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){Stop-Install 'GAME_RUNNING' 'Close the target game and SPT server, then retry.'}
}
$backup=Target ('mod-backups/assistant-2.3.1-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmssfff'))
$completed=[Collections.Generic.List[object]]::new()
$legacyBackup=$null
$retired=[Collections.Generic.List[object]]::new()
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
 Report-Progress 32 'BACKUP'
 # Preserve profiles and custom storage before retiring 3-X templates/old MC code.
 if(-not $ClientOnly){
  foreach($relative in @('SPT_Runtime/user/profiles','SPT_Runtime/user/mods/ProjectE/players','SPT_Runtime/user/mods/ProjectE/storage')){
   $origin=Target $relative
   if(Test-Path -LiteralPath $origin -PathType Container){$saved=Join-Path $backup $relative;[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved))|Out-Null;Copy-Item -LiteralPath $origin -Destination $saved -Recurse}
  }
 }
 # Recover pending MC conversion journals at the new integrated data path.
 foreach($id in @('atm-swords','projecte')){
  $oldState=Target ('BepInEx/plugins/UniversalModHost/state/'+$id+'/data')
  if(Test-Path -LiteralPath $oldState -PathType Container){
   foreach($item in Get-ChildItem -LiteralPath $oldState -File -Recurse){$suffix=$item.FullName.Substring($oldState.Length+1);$relative='BepInEx/plugins/LocalSpt5ESP/Minecraft/state/'+$id+'/'+$suffix;$destination=Target $relative;if(!(Test-Path -LiteralPath $destination)){$destination=Prepare $relative;Copy-Item -LiteralPath $item.FullName -Destination $destination}}
  }
 }
 Report-Progress 42 'RETIRE'
 $oldCode=@('BepInEx/plugins/UniversalModHost/modules/atm-swords','BepInEx/plugins/UniversalModHost/modules/projecte','BepInEx/plugins/UniversalModHost/modules/scp3x')
 if(-not $ClientOnly){$oldCode+=@('SPT_Runtime/user/mods/AllTheModiumSwords','SPT_Runtime/user/mods/ProjectE')}
 foreach($relative in $oldCode){
  $origin=Target $relative
  if(Test-Path -LiteralPath $origin){$saved=Join-Path $backup ('disabled-code/'+$relative);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved))|Out-Null;Move-Item -LiteralPath $origin -Destination $saved;$retired.Add([pscustomobject]@{Origin=$origin;Saved=$saved})}
 }
 $copied=0
 foreach($entry in $files.GetEnumerator()){
  Report-Progress (45+[int](40*$copied/$files.Count)) 'COPY'
  $destination=Prepare $entry.Value
  [IO.File]::WriteAllBytes($destination,[IO.File]::ReadAllBytes((Join-Path $package $entry.Key)))
  if((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $checks.($entry.Key)){throw ('Installed payload hash mismatch: '+$entry.Key)}
  $copied++
 }
 Report-Progress 88 'SETUP'
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
 $manifest=@{version='2.3.4';hostVersion='1.6.2';targetSpt='5.0 BE';targetEft='1.3.0.1.48558';clientOnly=[bool]$ClientOnly;minecraftIntegrated=$true;independentMinecraftHotReload=$false;scp3xRemoved=$true;blackwallIntegrated=$true;blackwallAssets=$resourceManifest.files.Count;experimental=$true;inRaidVerified=$false;installedUtc=[DateTime]::UtcNow.ToString('O');backup=$backup;coreSha256=$checks.'payload/Local.Spt5.ESP.Core.module'}
 [IO.File]::WriteAllText($destination,($manifest|ConvertTo-Json -Depth 4),$utf8)
 Report-Progress 100 'COMPLETE'
 Write-Result $true 'OK' 'Installation completed.'
 Write-Output ('Installed SPT Assistant 2.3.4 (EXPERIMENTAL). Backup: '+$backup)
 Write-Output 'Start the SPT server, then its client. Press Right Shift. Fika members: connect to the host; do not start a local server.'
 }catch{
 $failure=$_
 Report-Progress 90 'ROLLBACK'
 foreach($entry in $retired){try{if(Test-Path -LiteralPath $entry.Saved){Move-Item -LiteralPath $entry.Saved -Destination $entry.Origin}}catch{$rollbackSucceeded=$false}}
 try{if($legacyBackup -and (Test-Path -LiteralPath $legacyBackup)){Move-Item -LiteralPath $legacyBackup -Destination (Target 'BepInEx/plugins/BlackwallPulse')}}catch{$rollbackSucceeded=$false}
 for($index=$completed.Count-1;$index -ge 0;$index--){
  $entry=$completed[$index]
  try{if($entry.Old){Copy-Item -LiteralPath $entry.Old -Destination $entry.Destination -Force}elseif(Test-Path -LiteralPath $entry.Destination){Remove-Item -LiteralPath $entry.Destination -Force -ErrorAction Stop}}catch{$rollbackSucceeded=$false}
 }
 throw $failure
}
}catch{
 $failure=$_;$code='INSTALL_FAILED';$exception=$failure.Exception
 while($exception){if($exception.Data['InstallerCode']){$code=[string]$exception.Data['InstallerCode'];break};if($exception -is [UnauthorizedAccessException]){$code='ACCESS_DENIED'}elseif($exception -is [IO.IOException]){$code='FILES_LOCKED'};$exception=$exception.InnerException}
 if(!$rollbackSucceeded){$code='ROLLBACK_FAILED'}
 if($ResultPath){Write-Result $false $code $failure.Exception.Message;Write-Output ('ERROR '+$code+': '+$failure.Exception.Message);exit 1}
 throw
}
