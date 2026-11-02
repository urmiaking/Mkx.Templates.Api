param([switch]$TokenOnly,[string]$ApiBaseUrl='http://localhost:5287')
$ErrorActionPreference='Stop'
$file=Join-Path $env:LOCALAPPDATA 'Mkx.Templates.Api\BirSignLocalSmoke.dpapi'
if(!(Test-Path $file)){throw 'Missing local DPAPI credential. Run birsign-local-provision.ps1.'}
$secure=(Get-Content $file -Raw).Trim() | ConvertTo-SecureString
$p=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try{$secret=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($p)}
finally{[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($p)}
try{
 $body=@{grant_type='client_credentials';client_id='mkx.api.local.smoke';client_secret=$secret;scope='mkx.api.read'}
 $reply=Invoke-RestMethod 'https://localhost:44310/connect/token' -Method Post -ContentType 'application/x-www-form-urlencoded' -Body $body -TimeoutSec 15
 if(!$reply.access_token){throw 'Missing access token'}
 $parts=$reply.access_token.Split('.');if($parts.Length -ne 3){throw 'Expected JWT'}
 $encoded=$parts[1].Replace('-','+').Replace('_','/').PadRight([int][Math]::Ceiling($parts[1].Length/4)*4,'=')
 $claims=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded))|ConvertFrom-Json
 if($claims.iss -ne 'https://localhost:44310' -or @($claims.aud) -notcontains 'mkx.api'){throw 'Issuer/audience mismatch'}
 if((@($claims.scope)-join ' ') -notmatch '(^| )mkx\.api\.read( |$)'){throw 'Scope missing'}
 Write-Output 'LIVE_BIRSIGN_TOKEN=PASS'
 if(!$TokenOnly){
   $headers=@{Authorization='Bearer '+$reply.access_token}
   $me=Invoke-RestMethod ($ApiBaseUrl.TrimEnd('/')+'/api/auth/external/me') -Headers $headers -TimeoutSec 12
   if($me.authentication -ne 'external-oidc'){throw 'External token rejected'}
   Write-Output 'LIVE_BIRSIGN_TO_MKX_API=PASS'
   $denied=$false
   try{$null=Invoke-WebRequest -UseBasicParsing ($ApiBaseUrl.TrimEnd('/')+'/api/users') -Headers $headers -TimeoutSec 12}
   catch{$denied=$_.Exception.Response.StatusCode.value__ -eq 401}
   if(!$denied){throw 'External token accessed local administrator endpoint'}
   Write-Output 'EXTERNAL_CANNOT_ACCESS_LOCAL_ADMIN=PASS'
 }
}catch{
 Write-Output ('SMOKE_ERROR_TYPE='+$_.Exception.GetType().Name)
 if($_.ErrorDetails.Message){try{Write-Output ('OAUTH_ERROR_CODE='+(($_.ErrorDetails.Message|ConvertFrom-Json).error))}catch{}}
 throw
}finally{$secret=$null;$body=$null;$reply=$null}
