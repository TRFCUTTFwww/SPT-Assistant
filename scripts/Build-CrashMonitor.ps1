param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(-not $OutputDirectory){$OutputDirectory=Join-Path $repo 'crash-monitor/bin'}
[void](New-Item -ItemType Directory -Path $OutputDirectory -Force)
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(-not(Test-Path -LiteralPath $compiler)){throw '.NET Framework 4.x C# compiler is required.'}
$output=Join-Path $OutputDirectory 'Assistant.CrashMonitor.exe'
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /utf8output /codepage:65001 ('/out:'+$output) /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll (Join-Path $repo 'crash-monitor/CrashMonitor.cs') (Join-Path $repo 'diagnostics-shared/LogBundle.cs')
if($LASTEXITCODE -ne 0){throw 'Crash monitor build failed.'}
Write-Output $output
