param(
    [string]$GameRoot='D:/SPT-5.0.0-48558',
    [string]$Dotnet='dotnet'
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$game=[IO.Path]::GetFullPath($GameRoot)
if(!(Test-Path -LiteralPath (Join-Path $game 'BepInEx/interop/Assembly-CSharp.dll'))){throw 'Start this SPT/EFT 48558 IL2CPP installation once before installing.'}
$client=Join-Path $game 'EscapeFromTarkov.exe'
if(!(Test-Path -LiteralPath $client) -or (Get-Item -LiteralPath $client).VersionInfo.ProductVersion -notmatch '48558'){throw 'This build targets EFT 48558.'}
if(Get-Process EscapeFromTarkov -ErrorAction SilentlyContinue){throw 'Close the EFT client before installing or updating the permanent host.'}
foreach($project in @('universal-host/Host.csproj','universal-example/Example.csproj','universal-host-tests/Fixture/Fixture.csproj')){
    & $Dotnet build (Join-Path $repo $project) -c Release --nologo ('-p:GameRoot='+$game)
    if($LASTEXITCODE -ne 0){throw ('Build failed: '+$project)}
}
& $Dotnet run --project (Join-Path $repo 'universal-host-tests/Tests.csproj') -c Release -- (Join-Path $repo 'universal-host-tests/Fixture/bin/Release/net10.0/Host.TestModule.dll') (Join-Path $repo 'universal-host-tests/Fixture/bin/Release/net10.0/Fixture.Private.dll')
if($LASTEXITCODE -ne 0){throw 'Universal host lifecycle checks failed.'}
if(Get-Process EscapeFromTarkov -ErrorAction SilentlyContinue){throw 'EFT started during the build; host installation requires a closed client.'}
$destination=Join-Path $game 'BepInEx/plugins/UniversalModHost'
$backup=Join-Path $game ('mod-backups/universal-host-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach($name in @('Local.UniversalModHost.dll','Local.ModHost.Contracts.dll')){
    $target=Join-Path $destination $name
    if(Test-Path -LiteralPath $target){New-Item -ItemType Directory -Path $backup -Force | Out-Null;Copy-Item -LiteralPath $target -Destination $backup}
    $source=Join-Path $repo ('universal-host/bin/Release/net10.0/'+$name)
    Copy-Item -LiteralPath $source -Destination $target
    if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw 'Installed host hash mismatch.'}
}
Copy-Item -LiteralPath (Join-Path $repo 'docs/universal-mod-host.md') -Destination (Join-Path $destination 'README.md')
# The example demonstrates the contract without changing gameplay.
if(!(Test-Path -LiteralPath (Join-Path $destination 'modules/example/module.json'))){
    & (Join-Path $PSScriptRoot 'Publish-HotModule.ps1') -Id example -Name 'Example module' -Assembly (Join-Path $repo 'universal-example/bin/Release/net10.0/Example.HotModule.dll') -EntryType Example.ExampleModule -Version '1.0.0' -HostRoot $destination
}
Write-Output ('Installed Universal Mod Host: '+$destination)
Write-Output 'Next client launch loads the host. Future module releases can be published while it is running.'
