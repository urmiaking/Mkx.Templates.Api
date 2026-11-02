# Local Duende Authorization Code + S256 PKCE request verification, no user credentials.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$bytes=New-Object byte[] 48;$rng=[Security.Cryptography.RandomNumberGenerator]::Create()
try{$rng.GetBytes($bytes)}finally{$rng.Dispose()}
$sha=[Security.Cryptography.SHA256]::Create()
try{$digest=$sha.ComputeHash($bytes)}finally{$sha.Dispose()}
$challenge=[Convert]::ToBase64String($digest).TrimEnd('=').Replace('+','-').Replace('/','_')
$p=@{client_id='mkx.api.local.pkce';response_type='code';scope='openid profile mkx.api.read';redirect_uri='http://127.0.0.1:52173/callback';state=[Guid]::NewGuid().ToString('N');nonce=[Guid]::NewGuid().ToString('N');code_challenge=$challenge;code_challenge_method='S256'}
$q=($p.GetEnumerator()|ForEach-Object {[uri]::EscapeDataString($_.Key)+'='+[uri]::EscapeDataString([string]$_.Value)}) -join '&'
$h=[Net.Http.HttpClientHandler]::new();$h.AllowAutoRedirect=$false
$http=[Net.Http.HttpClient]::new($h);$http.Timeout=[TimeSpan]::FromSeconds(15)
try{
 $d=Invoke-RestMethod https://localhost:44310/.well-known/openid-configuration -TimeoutSec 10
 if($d.issuer -ne 'https://localhost:44310'){throw 'Unexpected issuer'}
 $response=$http.GetAsync($d.authorization_endpoint+'?'+$q).GetAwaiter().GetResult()
 if([int]$response.StatusCode -ne 302 -or !$response.Headers.Location -or $response.Headers.Location.AbsolutePath -notmatch '/Account/Login'){throw 'PKCE authorization was not accepted'}
 Write-Output 'BIRSIGN_PKCE_AUTHORIZATION_REQUEST=PASS'
 Write-Output 'PKCE_AUTHORIZE_HTTP=302'
}finally{$http.Dispose();$h.Dispose()}
