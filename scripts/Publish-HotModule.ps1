param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9][a-z0-9._-]{0,63}$')][string]$Id,
    [Parameter(Mandatory=$true)][string]$Assembly,
    [Parameter(Mandatory=$true)][string]$EntryType,
    [Parameter(Mandatory=$true)][string]$Version,
    [string]$Name='',
    [string]$HostRoot='D:/SPT-5.0.0-48558/BepInEx/plugins/UniversalModHost',
    [string]$AssetsDirectory='',
    [string[]]$PrivateLibraries=@(),
    [switch]$Disabled
)
$ErrorActionPreference='Stop'
if(!$Name){$Name=$Id}
if([string]::IsNullOrWhiteSpace($EntryType) -or [string]::IsNullOrWhiteSpace($Version)){throw 'EntryType and Version are required.'}
$source=(Resolve-Path -LiteralPath $Assembly).Path
if([IO.Path]::GetExtension($source) -notin @('.dll','.module')){throw 'Assembly must be a managed .dll or .module.'}
$hostPath=[IO.Path]::GetFullPath($HostRoot)
$module=Join-Path $hostPath ('modules/'+$Id)
$revision='r'+(Get-Date -Format 'yyyyMMddHHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$release=Join-Path $module ('releases/'+$revision)
foreach($path in @($hostPath,(Join-Path $hostPath 'modules'),$module,(Join-Path $module 'releases'))){
    if((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Publish destination cannot be a link.'}
}
New-Item -ItemType Directory -Path $release -Force | Out-Null
$entry=[IO.Path]::GetFileNameWithoutExtension($source)+'.module'
Copy-Item -LiteralPath $source -Destination (Join-Path $release $entry)
foreach($library in $PrivateLibraries){
    $resolved=(Resolve-Path -LiteralPath $library).Path
    $leaf=[IO.Path]::GetFileNameWithoutExtension($resolved)+'.module'
    if(Test-Path -LiteralPath (Join-Path $release $leaf)){throw ('Duplicate library: '+$leaf)}
    Copy-Item -LiteralPath $resolved -Destination (Join-Path $release $leaf)
}
if($AssetsDirectory){
    $assetRoot=(Resolve-Path -LiteralPath $AssetsDirectory).Path
    if(!(Get-Item -LiteralPath $assetRoot).PSIsContainer){throw 'AssetsDirectory must be a directory.'}
    if((Get-Item -LiteralPath $assetRoot).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Asset source cannot be a link.'}
    foreach($item in Get-ChildItem -LiteralPath $assetRoot -Recurse -Force){
        if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Asset sources cannot contain links.'}
        if($item.PSIsContainer){continue}
        $relative=$item.FullName.Substring($assetRoot.TrimEnd('\','/').Length+1)
        if($item.Extension -in @('.dll','.module')){throw 'Use PrivateLibraries for code; AssetsDirectory must contain assets only.'}
        $target=Join-Path $release ('Assets/'+$relative)
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $item.FullName -Destination $target
    }
}
$files=[ordered]@{};[long]$total=0
foreach($file in Get-ChildItem -LiteralPath $release -Recurse -File | Sort-Object FullName){
    $relative=$file.FullName.Substring($release.Length+1).Replace('\','/')
    if($file.Extension -eq '.module'){
        $leaf=[IO.Path]::GetFileNameWithoutExtension($file.Name)
        if($leaf -match '^(System\.|UnityEngine|BepInEx|Il2Cpp)' -or $leaf -in @('Local.ModHost.Contracts','Local.UniversalModHost','0Harmony','Assembly-CSharp')){throw ('Do not publish shared runtime libraries: '+$leaf)}
    }
    $total+=$file.Length
    if($file.Length -gt 256MB -or $total -gt 512MB){throw 'Package exceeds host size limits.'}
    $files[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
}
if($files.Count -gt 2048){throw 'Package exceeds 2048 file limit.'}
$manifest=[ordered]@{id=$Id;name=$Name;apiVersion=1;version=$Version;revision=$revision;entryAssembly=$entry;entryType=$EntryType;enabled=(!$Disabled);files=$files}
$destination=Join-Path $module 'module.json'
$json=$manifest | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText(($destination+'.new'),$json,[Text.UTF8Encoding]::new($false))
# Publish the pointer last. Existing releases remain available for rollback.
if(Test-Path -LiteralPath $destination){[IO.File]::Replace(($destination+'.new'),$destination,(Join-Path $module 'previous-manifest.json'))}
else{[IO.File]::Move(($destination+'.new'),$destination)}
Write-Output ('Published '+$Id+' '+$Version+' / '+$revision+' ('+$files.Count+' files)')
Write-Output $destination
