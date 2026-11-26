param(
 [Parameter(Mandatory=$true)][string]$GameRoot,
 [Parameter(Mandatory=$true)][string]$Source,
 [Parameter(Mandatory=$true)][string]$Sha256,
 [Parameter(Mandatory=$true)][string]$PreviousSha256,
 [Parameter(Mandatory=$true)][string]$StatusPath
)
$ErrorActionPreference='Stop'
$utf8=[Text.UTF8Encoding]::new($false)
function Record([string]$state,[string]$reason='') {
 [IO.File]::WriteAllText($StatusPath,(@{state=$state;reason=$reason;utc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Compress),$utf8)
}
try {
 $GameRoot=[IO.Path]::GetFullPath($GameRoot)
 $Source=[IO.Path]::GetFullPath($Source)
 $target=Join-Path $GameRoot 'BepInEx/plugins/LocalSpt5ESP/Local.Spt5.ESP.dll'
 if((Get-FileHash -LiteralPath $Source).Hash -ne $Sha256){throw 'Staged host hash mismatch'}
 Record 'waiting-for-game-exit'
 while(Get-Process -Name EscapeFromTarkov -ErrorAction SilentlyContinue){Start-Sleep -Seconds 1}
 $current=(Get-FileHash -LiteralPath $target).Hash
 if($current -eq $Sha256){Record 'installed';exit 0}
 if($current -ne $PreviousSha256){throw 'Host was changed by another update; skipped to preserve the newer file'}
 $backup=Join-Path $GameRoot ('mod-backups/assistant-host-name-'+[DateTime]::Now.ToString('yyyyMMdd-HHmmssfff'))
 [IO.Directory]::CreateDirectory($backup)|Out-Null
 Copy-Item -LiteralPath $target -Destination $backup
 if(Get-Process -Name EscapeFromTarkov -ErrorAction SilentlyContinue){throw 'Game restarted; close it and retry the host name update'}
 Copy-Item -LiteralPath $Source -Destination ($target+'.name-update.tmp')
 Move-Item -LiteralPath ($target+'.name-update.tmp') -Destination $target -Force
 if((Get-FileHash -LiteralPath $target).Hash -ne $Sha256){throw 'Installed host hash mismatch'}
 Record 'installed'
}catch {Record 'failed' $_.Exception.Message;exit 1}
