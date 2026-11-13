param(
    [Parameter(Mandatory=$true)][string]$GameRoot,
    [switch]$WaitForClientExit
)
$ErrorActionPreference='Stop'
$gamePath=[IO.Path]::GetFullPath($GameRoot)
$payload=Join-Path $PSScriptRoot 'CP2077Quickhacks'
$destination=Join-Path $gamePath 'BepInEx/plugins/CP2077Quickhacks'
$hostName='Local.Cp2077.Quickhacks.dll'
$moduleName='Local.Cp2077.Quickhacks.Core.module'
$updateRoot=Join-Path $gamePath 'mod-updates'
New-Item -ItemType Directory -Path $updateRoot -Force | Out-Null
$statusPath=Join-Path $updateRoot 'quickhacks-install-status.json'
function Write-Status([string]$State,[string]$Detail) {
    @{state=$State;detail=$Detail;version=$release.version;package=$PSScriptRoot;updatedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding UTF8
}
function Get-Clients {
    @(Get-Process EscapeFromTarkov -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -and [IO.Path]::GetDirectoryName($_.Path).TrimEnd('\','/') -eq $gamePath.TrimEnd('\','/') } catch { $false }
    })
}
function Publish-File([string]$Source,[string]$Target) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $Target) -Force | Out-Null
    $temporary=$Target+'.new'
    Copy-Item -LiteralPath $Source -Destination $temporary -Force
    [IO.File]::SetLastWriteTimeUtc($temporary,[DateTime]::UtcNow)
    if(Test-Path -LiteralPath $Target) { [IO.File]::Replace($temporary,$Target,[NullString]::Value) }
    else { [IO.File]::Move($temporary,$Target) }
}
try {
    $release=Get-Content (Join-Path $payload 'release-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $assets=Get-Content (Join-Path $payload 'asset-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $sourceHost=Join-Path $payload $hostName
    $sourceModule=Join-Path $payload ('runtime/'+$moduleName)
    if((Get-FileHash -LiteralPath $sourceHost -Algorithm SHA256).Hash -ne $release.hostSha256 -or (Get-FileHash -LiteralPath $sourceModule -Algorithm SHA256).Hash -ne $release.coreSha256) { throw 'Package code hash mismatch.' }
    foreach($asset in $assets.files) {
        if([IO.Path]::GetFileName($asset.name) -ne $asset.name) { throw 'Invalid asset name.' }
        if((Get-FileHash -LiteralPath (Join-Path $payload ('Assets/'+$asset.name)) -Algorithm SHA256).Hash -ne $asset.sha256) { throw ('Package asset hash mismatch: '+$asset.name) }
    }
    $targetHost=Join-Path $destination $hostName
    $replaceHost=!(Test-Path -LiteralPath $targetHost)
    if(!$replaceHost) { $replaceHost=(Get-FileHash -LiteralPath $targetHost -Algorithm SHA256).Hash -ne $release.hostSha256 }
    $clients=Get-Clients
    if($WaitForClientExit) {
        while($clients.Count -gt 0) {
            Write-Status 'waiting-for-client-exit' 'The old plugin is still loaded. Installation will finish when this client exits.'
            $clients | Wait-Process -ErrorAction SilentlyContinue
            $clients=Get-Clients
        }
    } elseif($replaceHost -and $clients.Count -gt 0) {
        $pending=Join-Path $updateRoot ('quickhacks-pending-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
        New-Item -ItemType Directory -Path $pending -Force | Out-Null
        Copy-Item -LiteralPath $payload -Destination $pending -Recurse
        Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $pending 'Install.ps1')
        Write-Status 'queued' ('First host installation requires one client restart. Pending package: '+$pending)
        $worker=Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList @(
            '-NoProfile','-ExecutionPolicy','Bypass','-File',('"'+(Join-Path $pending 'Install.ps1')+'"'),
            '-GameRoot',('"'+$gamePath+'"'),'-WaitForClientExit'
        ) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $pending 'install.log') -RedirectStandardError (Join-Path $pending 'install-error.log')
        Write-Output ('Quickhacks '+$release.version+' queued; worker '+$worker.Id+' installs after current client exits. First host migration needs one restart.')
        return
    }
    # Probe the bootstrap DLL before copying resources, so an unexpected lock
    # cannot leave a half-installed package.
    if($replaceHost -and (Test-Path -LiteralPath $targetHost)) {
        $probe=[IO.File]::Open($targetHost,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
        $probe.Dispose()
    }
    if(Test-Path -LiteralPath $destination) {
        $backup=Join-Path $gamePath ('mod-backups/quickhacks-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        Copy-Item -LiteralPath $destination -Destination $backup -Recurse
    }
    New-Item -ItemType Directory -Path (Join-Path $destination 'Assets') -Force | Out-Null
    foreach($asset in $assets.files) { Copy-Item -LiteralPath (Join-Path $payload ('Assets/'+$asset.name)) -Destination (Join-Path $destination ('Assets/'+$asset.name)) -Force }
    Copy-Item -LiteralPath (Join-Path $payload 'asset-manifest.json') -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $payload 'release-manifest.json') -Destination $destination -Force
    if($replaceHost) { Publish-File $sourceHost $targetHost }
    # Publish the module last, with a fresh timestamp, after every asset is ready.
    Publish-File $sourceModule (Join-Path $destination ('runtime/'+$moduleName))
    Write-Status $(if($replaceHost) {'installed-awaiting-startup'} else {'published'}) 'Code and asset hashes verified. Runtime module published atomically.'
    Write-Output ('Installed standalone Quickhacks '+$release.version+': '+$destination+'; '+$(if($replaceHost) {'loads at next client start'} else {'automatic hot reload / Ctrl+F8'}))
} catch {
    Write-Status 'failed' $_.Exception.Message
    throw
}
