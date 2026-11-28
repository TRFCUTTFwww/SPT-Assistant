param(
    [string]$GameRoot = 'D:\SPT-5.0.0-48558',
    [string]$DotnetPath = 'D:\SPT_Mod_Staging_20261027\dotnet-sdk\dotnet.exe',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sw = [System.Diagnostics.Stopwatch]::StartNew()

if (-not $SkipBuild) {
    & $DotnetPath build (Join-Path $repoRoot 'esp-cover\Esp.csproj') -c Release -p:GameRoot=$GameRoot --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Client core build failed.' }
}

$coreDll = Join-Path $repoRoot 'esp-cover\bin\Release\net10.0\Local.Spt5.ESP.Core.dll'
$hostDll = Join-Path $repoRoot 'esp-host\bin\Release\net10.0\Local.Spt5.ESP.dll'

$pluginDir = Join-Path $GameRoot 'BepInEx\plugins\LocalSpt5ESP'
$runtimeDir = Join-Path $pluginDir 'runtime'
New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null

# Atomic publish of Core.module so FileSystemWatcher / RuntimeReloader never reads a partial file
if (-not ('Win32AtomicMove' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class Win32AtomicMove {
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool MoveFileExW(string existingFileName, string newFileName, int flags);
    public static void Replace(string src, string dst) {
        if (!MoveFileExW(src, dst, 0x1 | 0x8)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}
'@
}
$tmpModule = Join-Path $runtimeDir ('Local.Spt5.ESP.Core.' + [Guid]::NewGuid().ToString('N') + '.tmp')
$dstModule = Join-Path $runtimeDir 'Local.Spt5.ESP.Core.module'
Copy-Item -LiteralPath $coreDll -Destination $tmpModule -Force
[Win32AtomicMove]::Replace($tmpModule, $dstModule)
[System.IO.File]::WriteAllText((Join-Path $runtimeDir 'reload.request'), [DateTime]::UtcNow.ToString('O'))

# Also update top-level module copy if present
$legacyTop = Join-Path $GameRoot 'BepInEx\plugins\Local.Spt5.ESP.Core.module'
if (Test-Path -LiteralPath $legacyTop) {
    Copy-Item -LiteralPath $coreDll -Destination $legacyTop -Force
}

# Update Host DLL directly if EFT is not currently locking it
$installedHost = Join-Path $pluginDir 'Local.Spt5.ESP.dll'
if (Test-Path -LiteralPath $hostDll) {
    try {
        Copy-Item -LiteralPath $hostDll -Destination $installedHost -Force
        Write-Host "Updated HostPlugin -> $installedHost (v1.7.0)"
    } catch {
        Copy-Item -LiteralPath $hostDll -Destination ($installedHost + '.pending') -Force
        Write-Host "Note: EFT is currently running; staged v1.7.0 HostPlugin at $($installedHost).pending (Core.module hot-reload is already active!)."
    }
}

Write-Host ("Published Core.module -> $dstModule in {0:N0} ms" -f $sw.Elapsed.TotalMilliseconds)
