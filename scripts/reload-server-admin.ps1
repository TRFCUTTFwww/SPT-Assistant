param([string]$GameRoot='D:/SPT-5.0.0-48558',[string]$SessionId='',[string]$ServerUrl='https://127.0.0.1:6969')
$ErrorActionPreference='Stop'
if($SessionId -notmatch '^[a-f0-9]{24}$'){throw 'Specify the current profile ID with -SessionId.'}
function Entry([int]$index){return [uint32](([uint64]1624453*([uint64]417645+[uint64]$index)+[uint64]1023920427)%[uint64]2396496269)}
function Frame([byte[]]$payload){$bytes=[byte[]]::new($payload.Length+4);[BitConverter]::GetBytes([int]$payload.Length).CopyTo($bytes,0);$payload.CopyTo($bytes,4);$seed=$bytes.Length%2731;for($i=$bytes.Length-1;$i -gt 0;$i--){$j=[int]((Entry ($i+$seed))%$i);$temp=$bytes[$i];$bytes[$i]=$bytes[$j];$bytes[$j]=$temp};return ,$bytes}
function Unframe([byte[]]$bytes){$seed=$bytes.Length%2731;for($i=1;$i -lt $bytes.Length;$i++){$j=[int]((Entry ($i+$seed))%$i);$temp=$bytes[$i];$bytes[$i]=$bytes[$j];$bytes[$j]=$temp};$length=[BitConverter]::ToInt32($bytes,0);if($length -lt 2 -or $length -gt $bytes.Length-4){throw 'Invalid server response frame'};$stream=[IO.MemoryStream]::new($bytes,6,$length-2);$inflate=[IO.Compression.DeflateStream]::new($stream,[IO.Compression.CompressionMode]::Decompress);$reader=[IO.StreamReader]::new($inflate,[Text.Encoding]::UTF8);try{return $reader.ReadToEnd()}finally{$reader.Dispose();$stream.Dispose()}}
$requestPath=[IO.Path]::GetTempFileName();$responsePath=[IO.Path]::GetTempFileName()
try{
 $apiKey=[IO.File]::ReadAllText((Join-Path $GameRoot 'SPT_Runtime/user/mods/LocalGearMail/api-key.txt')).Trim()
 [IO.File]::WriteAllBytes($requestPath,(Frame ([Text.Encoding]::UTF8.GetBytes((@{apiKey=$apiKey}|ConvertTo-Json -Compress)))))
 & curl.exe --silent --show-error --insecure --fail -H 'Content-Type: application/json' -H 'requestcompressed: 0' -H ('Cookie: PHPSESSID='+$SessionId) --data-binary ('@'+$requestPath) --output $responsePath ($ServerUrl.TrimEnd('/')+'/local/gear/reload')
 if($LASTEXITCODE -ne 0){throw 'Reload request failed'}
 $result=(Unframe ([IO.File]::ReadAllBytes($responsePath)))|ConvertFrom-Json
 if($result.err -and $result.err -ne 0){throw $result.errmsg}
 $result.data|Select-Object ready,generation,sha256,changed
}finally{Remove-Item -LiteralPath $requestPath,$responsePath -ErrorAction SilentlyContinue;$apiKey=$null}