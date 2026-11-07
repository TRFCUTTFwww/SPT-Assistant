param([Parameter(Mandatory=$true)][string]$GameRoot,[Parameter(Mandatory=$true)][string]$PackageDirectory,[string]$ProcessName='EscapeFromTarkov')
$ErrorActionPreference='Stop'
$GameRoot=[IO.Path]::GetFullPath($GameRoot)
$PackageDirectory=[IO.Path]::GetFullPath($PackageDirectory)
$plugin=Join-Path $GameRoot 'BepInEx/plugins/LocalSpt5ESP'
$status=Join-Path $PackageDirectory 'install-status.json'
$utf8=New-Object Text.UTF8Encoding($false)
function Record([string]$state,[string]$reason=''){
 $result=@{state=$state;reason=$reason;updatedUtc=[DateTime]::UtcNow.ToString('o')}
 [IO.File]::WriteAllText($status,($result|ConvertTo-Json -Compress),$utf8)
}
$lock=$null;$changed=@();$backup=$null
try{
 $lock=[IO.File]::Open((Join-Path $PackageDirectory 'install.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
 $manifest=Get-Content -LiteralPath (Join-Path $PackageDirectory 'package.json') -Raw -Encoding UTF8|ConvertFrom-Json
 $files=@(
  @{source=Join-Path $PackageDirectory 'Local.Spt5.ESP.dll';target=Join-Path $plugin 'Local.Spt5.ESP.dll';hash=$manifest.hostSha256},
  @{source=Join-Path $PackageDirectory 'Local.Spt5.ESP.Core.module';target=Join-Path $plugin 'runtime/Local.Spt5.ESP.Core.module';hash=$manifest.coreSha256}
 )
 foreach($file in $files){if((Get-FileHash -LiteralPath $file.source).Hash -ne $file.hash){throw 'Staged client file hash mismatch'}}
 Record 'waiting-for-game-exit'
 # Never stop or restart the user's game. The loaded host cannot replace itself.
 while(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue){Start-Sleep -Seconds 1}
 $backup=Join-Path $GameRoot ('mod-backups/assistant-client-before-1.38.23-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmssfff'))
 [IO.Directory]::CreateDirectory($backup)|Out-Null
 Record 'installing'
 foreach($file in $files){
  if(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue){throw 'Game restarted during installation; close the game and rerun this installer'}
  $leaf=[IO.Path]::GetFileName($file.target);Copy-Item -LiteralPath $file.target -Destination (Join-Path $backup $leaf)
  Copy-Item -LiteralPath $file.source -Destination ($file.target+'.upgrade.tmp')
  Move-Item -LiteralPath ($file.target+'.upgrade.tmp') -Destination $file.target -Force
  $changed+=$file
  if((Get-FileHash -LiteralPath $file.target).Hash -ne $file.hash){throw 'Installed client file hash mismatch'}
 }
 Record 'installed'
}catch{
 $problem=$_.Exception.Message
 foreach($file in $changed){Copy-Item -LiteralPath (Join-Path $backup ([IO.Path]::GetFileName($file.target))) -Destination $file.target -Force}
 Record 'failed' $problem
 throw
}finally{if($lock){$lock.Dispose()}}
