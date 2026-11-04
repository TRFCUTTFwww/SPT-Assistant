param([Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid version'}
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$game=Split-Path $repo -Parent
$release=Join-Path $repo ('release-'+$Version)
$artifacts=Join-Path $repo 'version-control-artifacts'
$record=Join-Path $repo ('history/v'+$Version)
New-Item -ItemType Directory -Path $artifacts,$record -Force|Out-Null
$zip=Join-Path $artifacts ('SPT-Assistant-'+$Version+'.zip')
if(-not(Test-Path -LiteralPath $zip)){
 $stage=Join-Path $artifacts ('work-'+$Version+'-'+[Guid]::NewGuid().ToString('N'))
 $target=Join-Path $stage ('_spt5_esp_fix/release-'+$Version)
 New-Item -ItemType Directory -Path $target -Force|Out-Null
 foreach($name in @('SptControlInstaller.dll','SptControlInstaller.pdb','SptControlInstaller.deps.json','SptControlInstaller.runtimeconfig.json')){Copy-Item -LiteralPath (Join-Path $release $name) -Destination $target}
 Copy-Item -LiteralPath (Join-Path $release 'payload') -Destination $target -Recurse
 Copy-Item -LiteralPath (Join-Path $game ('安装辅助模组'+$Version+'.cmd')) -Destination $stage
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 [IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
}
$zipHash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
$sum=$zip+'.sha256'
[IO.File]::WriteAllText($sum,$zipHash+'  '+[IO.Path]::GetFileName($zip)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
$module=Join-Path $release 'payload/Local.Spt5.ESP.Core.module'
$moduleHash=(Get-FileHash -LiteralPath $module -Algorithm SHA256).Hash
$binary=[ordered]@{version=$Version;path=('release-'+$Version+'/payload/Local.Spt5.ESP.Core.module');sha256=$moduleHash;length=(Get-Item -LiteralPath $module).Length;modified=(Get-Item -LiteralPath $module).LastWriteTimeUtc.ToString('o')}
$assets=@($zip,$sum)|ForEach-Object {[ordered]@{name=[IO.Path]::GetFileName($_);sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash;bytes=(Get-Item -LiteralPath $_).Length}}
$manifest=[ordered]@{version=$Version;tag=('v'+$Version);importKind='native-git-release';sourceStatus='native-git-source-at-tag-root';artifactKind='installer-package';canonicalModule=$binary;assets=@($assets);inRaidVerified=$false}
[IO.File]::WriteAllText((Join-Path $record 'manifest.json'),($manifest|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
$note='# v'+$Version+[Environment]::NewLine+[Environment]::NewLine+'This version was developed and committed in Git. Source code is at the repository root of this tag. It is not a reconstructed historical source snapshot. See manifest.json for exact package and module checksums.'+[Environment]::NewLine
[IO.File]::WriteAllText((Join-Path $record 'README.md'),$note,[Text.UTF8Encoding]::new($false))
$catalogPath=Join-Path $repo 'history/catalog.json'
$catalog=@(Get-Content -LiteralPath $catalogPath -Raw -Encoding utf8|ConvertFrom-Json)|Where-Object {$_.version -ne $Version}
$catalog+=([ordered]@{version=$Version;sourceStatus='native-git-source-at-tag-root';artifactKind='installer-package';manifest=('v'+$Version+'/manifest.json');moduleSha256=$moduleHash;assets=@($assets)})
[IO.File]::WriteAllText($catalogPath,($catalog|Sort-Object {[version]$_.version}|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
$inventoryPath=Join-Path $repo 'binary-history-inventory.json'
$inventory=@(Get-Content -LiteralPath $inventoryPath -Raw -Encoding utf8|ConvertFrom-Json)|Where-Object {$_.version -ne $Version}
$inventory+=$binary
[IO.File]::WriteAllText($inventoryPath,($inventory|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
$indexPath=Join-Path $repo 'history/README.md'
$index=[IO.File]::ReadAllText($indexPath)
if(-not$index.Contains('](v'+$Version+'/README.md)')){$index+='| ['+$Version+'](v'+$Version+'/README.md) | native-git-source-at-tag-root | installer-package |'+[Environment]::NewLine;[IO.File]::WriteAllText($indexPath,$index,[Text.UTF8Encoding]::new($false))}
[pscustomobject]@{version=$Version;moduleSha256=$moduleHash;zipSha256=$zipHash;zip=$zip}|ConvertTo-Json -Compress
