param(
 [string]$GameRoot='D:/SPT-5.0.0-48558',
 [Parameter(Mandatory=$true)][string]$UpdateDirectory,
 [switch]$WaitForExit,[switch]$StartServer,[switch]$VerifyOnly
)
$ErrorActionPreference='Stop'
$GameRoot=(Resolve-Path -LiteralPath $GameRoot).Path
$UpdateDirectory=(Resolve-Path -LiteralPath $UpdateDirectory).Path
$serverRoot=Join-Path $GameRoot 'SPT_Runtime'
$live=Join-Path $serverRoot 'user/mods/LocalGearMail'
$entries=@('LocalGearMail.dll','LocalGearAdmin.module')
$hashes=Get-Content -LiteralPath (Join-Path $UpdateDirectory 'checksums.json') -Raw|ConvertFrom-Json
$status=Join-Path $UpdateDirectory 'install-status.json'
function Status([string]$state,[string]$detail){[IO.File]::WriteAllText($status,(@{state=$state;detail=$detail;utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))}
foreach($name in $entries){
 $source=Join-Path $UpdateDirectory $name
 if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $hashes.$name){throw ('Payload checksum mismatch: '+$name)}
 if(!(Test-Path -LiteralPath (Join-Path $live $name) -PathType Leaf)){throw ('Existing installation missing: '+$name)}
}
if($VerifyOnly){Write-Output 'Verified both server payloads and existing targets. No files changed.';exit 0}
if(Get-Process -Name SPT.Server -ErrorAction SilentlyContinue){
 if(!$WaitForExit){throw 'Close SPT.Server first, or specify -WaitForExit.'}
 Status 'waiting' 'Waiting for the current SPT server to close; no process will be stopped.'
 while(Get-Process -Name SPT.Server -ErrorAction SilentlyContinue){Start-Sleep -Seconds 2}
}
# Refuse to replace server logic in a live raid even if the old server exited.
$raidPath=Join-Path $GameRoot 'BepInEx/plugins/LocalSpt5ESP/runtime/raid-state.json'
if(Get-Process -Name EscapeFromTarkov -ErrorAction SilentlyContinue){
 $raid=Get-Content -LiteralPath $raidPath -Raw|ConvertFrom-Json
 if($raid.inRaid -or ([DateTime]::UtcNow-[DateTime]$raid.utc).TotalSeconds -gt 30){Status 'blocked' 'Raid active or state stale. Run this installer again from the main menu.';throw 'Raid active or state stale.'}
}
$backup=Join-Path $UpdateDirectory ('server-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,6))
[IO.Directory]::CreateDirectory($backup)|Out-Null
foreach($name in $entries){Copy-Item -LiteralPath (Join-Path $live $name) -Destination (Join-Path $backup $name)}
$changed=[Collections.Generic.List[string]]::new()
try{
 foreach($name in $entries){
  $target=Join-Path $live $name;$temporary=$target+'.editor-update.tmp'
  Copy-Item -LiteralPath (Join-Path $UpdateDirectory $name) -Destination $temporary -Force
  [IO.File]::Replace($temporary,$target,(Join-Path $backup ($name+'.replaced')))
  $changed.Add($name)
  if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hashes.$name){throw ('Installed checksum mismatch: '+$name)}
 }
 Status 'installed' ('Installed both components. Previous binaries: '+$backup)
}catch{
 foreach($name in $changed){Copy-Item -LiteralPath (Join-Path $backup $name) -Destination (Join-Path $live $name) -Force}
 Status 'failed' $_.Exception.Message;throw
}
if($StartServer){
 $process=Start-Process -FilePath (Join-Path $serverRoot 'SPT.Server.exe') -WorkingDirectory $serverRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $UpdateDirectory 'server-stdout.log') -RedirectStandardError (Join-Path $UpdateDirectory 'server-stderr.log') -PassThru
 Status 'started' ('Started SPT.Server PID '+$process.Id+'. Check server logs and admin-hotreload-status.json for readiness.')
}
Write-Output ('Installed server update; backup: '+$backup)
