param([string]$GameRoot='D:/SPT-5.0.0-48558',[Parameter(Mandatory=$true)][string]$SessionId,[string]$ServerUrl='https://127.0.0.1:6969')
$ErrorActionPreference='Stop'
if($SessionId -notmatch '^[a-f0-9]{24}$'){throw 'Invalid profile ID'}
function Entry([int]$index){return [uint32](([uint64]1624453*([uint64]417645+[uint64]$index)+[uint64]1023920427)%[uint64]2396496269)}
function Frame([byte[]]$payload){$bytes=[byte[]]::new($payload.Length+4);[BitConverter]::GetBytes([int]$payload.Length).CopyTo($bytes,0);$payload.CopyTo($bytes,4);$seed=$bytes.Length%2731;for($i=$bytes.Length-1;$i -gt 0;$i--){$j=[int]((Entry ($i+$seed))%$i);$temp=$bytes[$i];$bytes[$i]=$bytes[$j];$bytes[$j]=$temp};return ,$bytes}
function Unframe([byte[]]$bytes){$seed=$bytes.Length%2731;for($i=1;$i -lt $bytes.Length;$i++){$j=[int]((Entry ($i+$seed))%$i);$temp=$bytes[$i];$bytes[$i]=$bytes[$j];$bytes[$j]=$temp};$length=[BitConverter]::ToInt32($bytes,0);if($length -lt 2 -or $length -gt $bytes.Length-4){throw 'Invalid response frame'};$stream=[IO.MemoryStream]::new($bytes,6,$length-2);$inflate=[IO.Compression.DeflateStream]::new($stream,[IO.Compression.CompressionMode]::Decompress);$reader=[IO.StreamReader]::new($inflate,[Text.Encoding]::UTF8);try{return $reader.ReadToEnd()}finally{$reader.Dispose();$stream.Dispose()}}
$requestPath=[IO.Path]::GetTempFileName();$responsePath=[IO.Path]::GetTempFileName()
$runtime=[IO.Path]::GetFullPath((Join-Path $GameRoot 'SPT_Runtime'))
$fixtureId='user/mods/LocalGearMail/json-editor-test-'+[Guid]::NewGuid().ToString('N')+'.json'
$fixture=[IO.Path]::GetFullPath((Join-Path $runtime $fixtureId))
$backupRoots=[Collections.Generic.List[string]]::new()
$checks=0
function Assert($condition,[string]$message){if(!$condition){throw $message};$script:checks++}
function Call([hashtable]$payload,[bool]$reject=$false){
 $payload.apiKey=$script:apiKey
 [IO.File]::WriteAllBytes($requestPath,(Frame ([Text.Encoding]::UTF8.GetBytes(($payload|ConvertTo-Json -Depth 20 -Compress)))))
 & curl.exe --silent --show-error --insecure --fail --max-time 60 -H 'Content-Type: application/json' -H 'requestcompressed: 0' -H ('Cookie: PHPSESSID='+$SessionId) --data-binary ('@'+$requestPath) --output $responsePath ($ServerUrl.TrimEnd('/')+'/local/gear/data')
 if($LASTEXITCODE -ne 0){throw 'Server JSON test request failed'}
 $result=(Unframe ([IO.File]::ReadAllBytes($responsePath)))|ConvertFrom-Json
 if($reject){Assert ($result.err -ne 0) 'Expected rejected request';return}
 if($result.err -and $result.err -ne 0){throw $result.errmsg}
 return $result.data
}
try{
 $apiKey=[IO.File]::ReadAllText((Join-Path $runtime 'user/mods/LocalGearMail/api-key.txt')).Trim()
 [IO.File]::WriteAllText($fixture,'{"value":1,"external":0,"empty":null,"array":[true,false]}')
 $inventory=Call @{action='serverJsonList'}
 Assert (@($inventory.files).Count -ge 443) 'Missing installed JSON files'
 $opened=Call @{action='serverJsonOpen';id=$fixtureId}
 $token=$opened.token
 Assert ($token.Length -gt 0) 'Missing snapshot token'
 [IO.File]::WriteAllText($fixture,'{"value":8,"external":77,"empty":null,"array":[true,false]}')
 $changed=Call @{action='serverJsonCheck';id=$fixtureId;jsonToken=$token}
 Assert $changed.changed 'External update was not reported'
 $old=Call @{action='serverJsonNode';id=$fixtureId;jsonToken=$token;jsonPath='$.value'}
 Assert ($old.raw -eq '1') 'Snapshot was automatically refreshed'
 $saved=Call @{action='serverJsonSave';id=$fixtureId;jsonToken=$token;jsonPath='$.value';preview='3'}
 $backupRoots.Add([IO.Path]::GetFullPath((Join-Path $runtime $saved.backup)))
 Assert $saved.saved 'Explicit save failed'
 Assert $saved.changed 'Update notice disappeared during save'
 $persisted=[IO.File]::ReadAllText($fixture)|ConvertFrom-Json
 Assert ($persisted.value -eq 3 -and $persisted.external -eq 77) 'Save lost an unrelated external change'
 $unrefreshed=Call @{action='serverJsonNode';id=$fixtureId;jsonToken=$token;jsonPath='$.external'}
 Assert ($unrefreshed.raw -eq '0') 'Save refreshed unrelated snapshot values'
 $nullField=Call @{action='serverJsonNode';id=$fixtureId;jsonToken=$token;jsonPath='$.empty'}
 Assert ($nullField.raw -eq 'null') 'Null field inaccessible'
 Call @{action='serverJsonOpen';id='SPT_Data/../../outside.json'} $true
 $bot=Call @{action='serverJsonOpen';id='SPT_Data/database/bots/types/assault.json'}
 $field=Call @{action='serverJsonNode';id='SPT_Data/database/bots/types/assault.json';jsonToken=$bot.token;jsonPath='$.difficulty.normal.Grenade.NO_RUN_FROM_AI_GRENADES'}
 Assert ($field.purpose.detailEn -match 'players') 'Grenade field documentation missing'
 [pscustomobject]@{passed=$checks;files=@($inventory.files).Count;externalUpdateNotice=$true;noAutomaticRefresh=$true;latestFileMerge=$true;nativeFieldDocs=$true}
}finally{
 if($fixture.StartsWith((Join-Path $runtime 'user/mods/LocalGearMail')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $fixture)){Remove-Item -LiteralPath $fixture}
 $allowedBackup=[IO.Path]::GetFullPath((Join-Path $runtime 'user/mods/LocalGearMail/backups/server-json'))+[IO.Path]::DirectorySeparatorChar
 foreach($backup in $backupRoots){if($backup.StartsWith($allowedBackup,[StringComparison]::OrdinalIgnoreCase) -and (Split-Path -Leaf $backup) -match '^\d{8}-\d{6}-\d{3}-[a-f0-9]{8}$' -and (Test-Path -LiteralPath $backup)){Remove-Item -LiteralPath $backup -Recurse -Force}}
 Remove-Item -LiteralPath $requestPath,$responsePath -ErrorAction SilentlyContinue
 $apiKey=$null
}
