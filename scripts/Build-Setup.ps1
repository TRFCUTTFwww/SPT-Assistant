param(
 [Parameter(Mandatory=$true)][string]$PackageDirectory,
 [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$package=(Resolve-Path -LiteralPath $PackageDirectory).Path
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(!(Test-Path -LiteralPath (Join-Path $package 'checksums.json')) -or !(Test-Path -LiteralPath (Join-Path $package 'Install.ps1'))){throw 'A complete verified Assistant package is required'}
if($output.StartsWith($package.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or $package.StartsWith($output.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or $output.Equals($package,[StringComparison]::OrdinalIgnoreCase)){throw 'Setup output must be outside the package directory'}
[IO.Directory]::CreateDirectory($output)|Out-Null
$build=Join-Path $output ('build-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($build)|Out-Null
$version=([IO.File]::ReadAllText((Join-Path $repo 'VERSION'))).Trim()
$parsedVersion=[Version]$version
$assemblyVersion='{0}.{1}.{2}.0' -f $parsedVersion.Major,$parsedVersion.Minor,[Math]::Max(0,$parsedVersion.Build)
$checks=Get-Content -LiteralPath (Join-Path $package 'checksums.json') -Raw -Encoding UTF8|ConvertFrom-Json
foreach($entry in $checks.PSObject.Properties){
 $file=[IO.Path]::GetFullPath((Join-Path $package $entry.Name))
 if(!$file.StartsWith($package.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $file -PathType Leaf) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.Value){throw ('Package verification failed: '+$entry.Name)}
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$bundle=Join-Path $build 'package.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($package,$bundle,[IO.Compression.CompressionLevel]::Optimal,$false)
$hash=(Get-FileHash -LiteralPath $bundle -Algorithm SHA256).Hash.ToLowerInvariant()
$info=Join-Path $build 'BundleInfo.cs'
$infoText='[assembly:System.Reflection.AssemblyDescription("Offline bilingual installer for SPT Assistant '+$version+'")]'+[Environment]::NewLine+'[assembly:System.Reflection.AssemblyVersion("'+$assemblyVersion+'")]'+[Environment]::NewLine+'[assembly:System.Reflection.AssemblyFileVersion("'+$assemblyVersion+'")]'+[Environment]::NewLine+'namespace SptAssistantSetup {internal static class BundleInfo {internal const string Sha256="'+$hash+'";internal const string Version="'+$version+'";}}'
[IO.File]::WriteAllText($info,$infoText,[Text.UTF8Encoding]::new($false))
$manifest=Join-Path $build 'app.manifest'
[xml]$manifestXml=[IO.File]::ReadAllText((Join-Path $repo 'installer-ui/app.manifest'))
$manifestXml.assembly.assemblyIdentity.SetAttribute('version',$assemblyVersion)
$manifestXml.Save($manifest)
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(!(Test-Path -LiteralPath $compiler)){throw 'Windows .NET Framework compiler was not found'}
$exe=Join-Path $output ('SPT-Assistant-'+$version+'-Setup.exe')
$arguments=@('/nologo','/target:winexe','/platform:anycpu','/optimize+','/codepage:65001','/langversion:5',('/out:'+$exe),('/win32manifest:'+$manifest),('/resource:'+$bundle+',SPT.Assistant.Package.zip'),'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll')
$sources=@(Get-ChildItem -LiteralPath (Join-Path $repo 'installer-ui') -Filter '*.cs' -File | Where-Object {$_.Name -ne 'InstallerTests.cs'} | ForEach-Object {$_.FullName})
& $compiler @arguments @sources $info
if($LASTEXITCODE -ne 0){throw 'Graphical installer build failed'}
$sum=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS-Setup.txt'),($sum+'  '+[IO.Path]::GetFileName($exe)+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $output 'setup-build.json'),(@{exe=$exe;bundle=$bundle;bundleInfo=$info;sha256=$sum;version=$version}|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Write-Output ('SETUP_BUILT '+$exe)
