param([string]$SoundFileLibrary='D:/SPT_Mod_Staging_20261027/projecte-python/_soundfile_data/libsndfile_x64.dll')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$repo=Split-Path -Parent $PSScriptRoot
$assets=Join-Path $repo 'projecte-client/Assets'
$renderer=Join-Path $repo 'atm-swords-client/Assets'
$utf8=[Text.UTF8Encoding]::new($false)
$culture=[Globalization.CultureInfo]::InvariantCulture
foreach($key in @('tablet','wireless_terminal','totem')){
 $bitmap=[Drawing.Bitmap]::new((Join-Path $assets ($key+'.png')))
 try{
  $vertices=[Collections.Generic.List[string]]::new(); $uvs=[Collections.Generic.List[string]]::new(); $faces=[Collections.Generic.List[string]]::new(); $index=1
  for($y=0;$y -lt $bitmap.Height;$y++){for($x=0;$x -lt $bitmap.Width;$x++){
   if($bitmap.GetPixel($x,$y).A -lt 128){continue}
   $low=$bitmap.Height-$y-1; $high=$low+1; $right=$x+1
   foreach($vertex in @("$x $low -0.5","$right $low -0.5","$right $high -0.5","$x $high -0.5","$x $low 0.5","$right $low 0.5","$right $high 0.5","$x $high 0.5")){$vertices.Add('v '+$vertex)}
   $u=(($x+0.5)/$bitmap.Width).ToString($culture); $v=(1-($y+0.5)/$bitmap.Height).ToString($culture)
   for($i=0;$i -lt 8;$i++){$uvs.Add("vt $u $v")}
   foreach($face in @(@(0,3,2,1),@(4,5,6,7),@(0,1,5,4),@(3,7,6,2),@(0,4,7,3),@(1,2,6,5))){$parts=foreach($n in $face){$j=$index+$n; "$j/$j"}; $faces.Add('f '+($parts -join ' '))}
   $index+=8
  }}
  $lines=@('# Original sprite extrusion; see portable-provenance.json and upstream licenses.')+$vertices.ToArray()+$uvs.ToArray()+$faces.ToArray()
  [IO.File]::WriteAllText((Join-Path $renderer ($key+'_sword.obj')),($lines -join "`n")+"`n",$utf8)
  Copy-Item -LiteralPath (Join-Path $assets ($key+'.png')) -Destination (Join-Path $renderer ($key+'_sword.png'))
 }finally{$bitmap.Dispose()}
}
if(!(Test-Path -LiteralPath $SoundFileLibrary)){throw 'Pass the path of an installed libsndfile DLL to decode the original totem OGG.'}
$native=@'
using System;using System.Runtime.InteropServices;
public static class PortableSoundFile {
 [StructLayout(LayoutKind.Sequential)] public struct Info {public long Frames;public int Rate,Channels,Format,Sections,Seekable;}
 [DllImport(@"LIBRARY",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Ansi)] public static extern IntPtr sf_open(string path,int mode,ref Info info);
 [DllImport(@"LIBRARY",CallingConvention=CallingConvention.Cdecl)] public static extern long sf_readf_short(IntPtr file,[Out] short[] data,long frames);
 [DllImport(@"LIBRARY",CallingConvention=CallingConvention.Cdecl)] public static extern int sf_close(IntPtr file);
}
'@
Add-Type -TypeDefinition $native.Replace('LIBRARY',[IO.Path]::GetFullPath($SoundFileLibrary))
$info=[PortableSoundFile+Info]::new(); $handle=[PortableSoundFile]::sf_open((Join-Path $assets 'totem.ogg'),0x10,[ref]$info)
if($handle -eq [IntPtr]::Zero){throw 'Cannot decode totem OGG'}
try{
 if($info.Frames -le 0 -or $info.Frames -gt 4000000 -or $info.Channels -notin @(1,2)){throw 'Invalid totem audio format'}
 $samples=[int16[]]::new($info.Frames*$info.Channels)
 if([PortableSoundFile]::sf_readf_short($handle,$samples,$info.Frames) -ne $info.Frames){throw 'Incomplete totem audio decode'}
 $stream=[IO.File]::Create((Join-Path $assets 'totem.wav')); $writer=[IO.BinaryWriter]::new($stream)
 try{$bytes=[byte[]]::new($samples.Length*2); [Buffer]::BlockCopy($samples,0,$bytes,0,$bytes.Length); $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'));$writer.Write([int](36+$bytes.Length));$writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '));$writer.Write([int]16);$writer.Write([int16]1);$writer.Write([int16]$info.Channels);$writer.Write([int]$info.Rate);$writer.Write([int]($info.Rate*$info.Channels*2));$writer.Write([int16]($info.Channels*2));$writer.Write([int16]16);$writer.Write([Text.Encoding]::ASCII.GetBytes('data'));$writer.Write([int]$bytes.Length);$writer.Write($bytes)}finally{$writer.Dispose()}
}finally{[void][PortableSoundFile]::sf_close($handle)}
$sources=[ordered]@{
 'tablet.png'=@{repository='https://github.com/sinkillerj/ProjectE';commit='f432b0c66837759fb0731c9144dc53176b949c5d';path='src/main/resources/assets/projecte/textures/item/transmutation_tablet.png';license='MIT'}
 'wireless_terminal.png'=@{repository='https://github.com/AppliedEnergistics/Applied-Energistics-2';commit='5a544ce4aa4d51a1e92a21d316bb89c4a5f4a75a';path='src/main/resources/assets/ae2/textures/item/wireless_terminal.png';license='LGPL-3.0; see AE2-LICENSE.txt'}
 'totem.png'=@{url='https://piston-data.mojang.com/v1/objects/30c73b1c5da787909b2f73340419fdf13b9def88/client.jar';version='1.21.1';clientSha1='30c73b1c5da787909b2f73340419fdf13b9def88';path='assets/minecraft/textures/item/totem_of_undying.png';license='Mojang/Microsoft copyrighted Minecraft asset'}
 'totem.ogg'=@{url='https://resources.download.minecraft.net/e7/e7f0337931cdb05c4234d2a9bc1f38ead675db26';sha1='e7f0337931cdb05c4234d2a9bc1f38ead675db26';path='minecraft/sounds/item/totem/use_totem.ogg';license='Mojang/Microsoft copyrighted Minecraft asset'}
 'totem.wav'=@{derivedFrom='totem.ogg';conversion='libsndfile, 16-bit signed PCM, original channels and sample rate'}
}
foreach($name in @($sources.Keys)){$sources[$name].sha256=(Get-FileHash -LiteralPath (Join-Path $assets $name) -Algorithm SHA256).Hash.ToLowerInvariant()}
[IO.File]::WriteAllText((Join-Path $assets 'portable-provenance.json'),(($sources | ConvertTo-Json -Depth 8).Replace("`r`n","`n"))+"`n",$utf8)
[IO.File]::WriteAllText((Join-Path $assets 'Portable-NOTICE.txt'),"Portable tablet: ProjectE MIT. Wireless terminal: Applied Energistics 2, original unmodified sprite, LGPL-3.0; source revision and file path in portable-provenance.json. Totem sprite and original sound: Minecraft 1.21.1, copyright Mojang/Microsoft; not covered by the ProjectE MIT license. Generated meshes retain the source sprite colors and attribution. Animation and gameplay adaptation are local code, not extracted Minecraft implementation.`n",$utf8)
foreach($name in @('portable-provenance.json','AE2-LICENSE.txt','Portable-NOTICE.txt')){Copy-Item -LiteralPath (Join-Path $assets $name) -Destination $renderer}
Write-Output 'Built three original-sprite handheld meshes, PCM totem audio and provenance.'
