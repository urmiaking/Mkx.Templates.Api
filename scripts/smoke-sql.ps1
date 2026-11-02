param([string]$SqlServer = "localhost")
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..\template-content")
function Secret([int]$count) {
  $bytes = New-Object byte[] $count
  $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
  try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
  [Convert]::ToBase64String($bytes)
}
$db = 'MkxTemplateSmoke_' + [guid]::NewGuid().ToString('N').Substring(0,12)
$env:ConnectionStrings__DefaultConnection = "Server=$SqlServer;Database=$db;Trusted_Connection=True;TrustServerCertificate=True"
$env:Jwt__SigningKey = Secret 48
$env:BootstrapAdmin__Enabled = 'true'
$env:BootstrapAdmin__Username = 'template-smoke-admin'
$env:BootstrapAdmin__Password = (Secret 40)+'aA1!'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:RateLimit__AuthRequestsPerMinute = '60'
$env:Logging__LogLevel__Microsoft__EntityFrameworkCore = 'Warning'
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$listener.Start(); $port=$listener.LocalEndpoint.Port; $listener.Stop()
$base="http://127.0.0.1:$port"; $env:ASPNETCORE_URLS=$base
$env:Passkeys__Enabled='true'
$env:Passkeys__RelyingPartyId='127.0.0.1'
$env:Passkeys__Origin=$base
$log = Join-Path ([IO.Path]::GetTempPath()) ("mkx-smoke-"+[guid]::NewGuid().ToString('N'))
$proc=$null; $created=$false
try {
  dotnet ef database update --project src/Core/Mkx.Templates.Infrastructure --startup-project src/Server/Mkx.Templates.Server --no-build --no-color
  if($LASTEXITCODE -ne 0) { throw 'Migration apply failed' }
  $created=$true
  $proc=Start-Process dotnet -ArgumentList @('run','--project','src/Server/Mkx.Templates.Server','--no-build','--no-launch-profile') -RedirectStandardOutput ($log+'.out') -RedirectStandardError ($log+'.err') -PassThru -WindowStyle Hidden
  $ready=$false
  for($i=0;$i -lt 45;$i++){
    Start-Sleep -Milliseconds 350
    try{if((Invoke-WebRequest "$base/health/live" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200){$ready=$true;break}}catch{}
  }
  if(-not $ready){throw 'API startup timeout'}
  $login=Invoke-RestMethod "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{username=$env:BootstrapAdmin__Username;password=$env:BootstrapAdmin__Password}|ConvertTo-Json)
  if(-not $login.accessToken -or -not $login.refreshToken){throw 'Missing tokens'}; Write-Output 'LOGIN=PASS'

  # Real SQL Server + HTTP passkey options, with no biometric device needed.
  $passkeyOptions=Invoke-RestMethod "$base/api/passkeys/login/options" -Method Post -ContentType 'application/json' -Body '{}'
  if(-not $passkeyOptions.ceremonyId -or -not $passkeyOptions.optionsJson.Contains('challenge')){
    throw 'Discoverable passkey options were not issued'
  }
  Write-Output 'PASSKEY_LOGIN_OPTIONS=PASS'
  $enroll=Invoke-RestMethod "$base/api/passkeys/register/options" -Method Post -Headers @{Authorization="Bearer $($login.accessToken)"} -ContentType 'application/json' -Body (@{password=$env:BootstrapAdmin__Password}|ConvertTo-Json)
  if(-not $enroll.ceremonyId -or -not $enroll.optionsJson.Contains('challenge')){
    throw 'Passkey enrollment options were not issued'
  }
  Write-Output 'PASSKEY_REGISTER_OPTIONS=PASS'
  $invalidAttestation=$false
  try{
    Invoke-WebRequest "$base/api/passkeys/register/complete" -UseBasicParsing -Method Post -Headers @{Authorization="Bearer $($login.accessToken)"} -ContentType 'application/json' -Body (@{ceremonyId=$enroll.ceremonyId;credentialJson='{}'}|ConvertTo-Json)|Out-Null
  }catch{$invalidAttestation=($_.Exception.Response.StatusCode.value__ -eq 400)}
  if(-not $invalidAttestation){throw 'Invalid WebAuthn attestation was not rejected'}
  $invalidAgain=$false
  try{
    Invoke-WebRequest "$base/api/passkeys/register/complete" -UseBasicParsing -Method Post -Headers @{Authorization="Bearer $($login.accessToken)"} -ContentType 'application/json' -Body (@{ceremonyId=$enroll.ceremonyId;credentialJson='{}'}|ConvertTo-Json)|Out-Null
  }catch{$invalidAgain=($_.Exception.Response.StatusCode.value__ -eq 400)}
  if(-not $invalidAgain){throw 'WebAuthn ceremony was replayable'}
  Write-Output 'PASSKEY_INVALID_ATTESTATION_REPLAY_DENIED=PASS'
  $me=Invoke-RestMethod "$base/api/auth/me" -Headers @{Authorization="Bearer $($login.accessToken)"}
  if($me.username -ne $env:BootstrapAdmin__Username){throw 'Identity mismatch'}; Write-Output 'ME=PASS'
  $users=Invoke-RestMethod "$base/api/users" -Headers @{Authorization="Bearer $($login.accessToken)"}
  if($users.total -lt 1){throw 'Admin policy failed'}; Write-Output 'ADMIN_POLICY=PASS'

  # API key administration uses LOCAL JWT only. Raw keys must only appear in create response.
  $adminHeaders=@{Authorization="Bearer $($login.accessToken)"}
  $created=Invoke-RestMethod "$base/api/api-keys" -Headers $adminHeaders -Method Post -ContentType 'application/json' -Body (@{name='ci-integration-reader';scopes=@('integration:read');validDays=30}|ConvertTo-Json)
  if(-not $created.apiKey -or $created.apiKey.Length -ne 68){throw 'API key creation failed'}; Write-Output 'API_KEY_CREATE=PASS'
  $ping=Invoke-RestMethod "$base/api/integrations/ping" -Headers @{'X-API-Key'=$created.apiKey}
  if($ping.status -ne 'ok'){throw 'API key authentication failed'}; Write-Output 'API_KEY_USE=PASS'
  $keyList=Invoke-RestMethod "$base/api/api-keys" -Headers $adminHeaders
  if(($keyList|ConvertTo-Json -Depth 5).Contains($created.apiKey)){throw 'API key leaked from metadata list'}
  Write-Output 'API_KEY_NOT_DISCLOSED=PASS'

  # Wrong scopes authenticate but fail authorization (403).
  $writer=Invoke-RestMethod "$base/api/api-keys" -Headers $adminHeaders -Method Post -ContentType 'application/json' -Body (@{name='ci-integration-writer';scopes=@('integration:write');validDays=30}|ConvertTo-Json)
  $forbidden=$false
  try{Invoke-WebRequest "$base/api/integrations/ping" -UseBasicParsing -Headers @{'X-API-Key'=$writer.apiKey}|Out-Null}catch{$forbidden=($_.Exception.Response.StatusCode.value__ -eq 403)}
  if(-not $forbidden){throw 'Insufficient API key scope was accepted'}; Write-Output 'API_KEY_SCOPE_DENIED=PASS'

  $revoked=Invoke-WebRequest "$base/api/api-keys/$($created.id)" -UseBasicParsing -Headers $adminHeaders -Method Delete
  if($revoked.StatusCode -ne 204){throw 'API key revocation failed'}
  $keyRejected=$false
  try{Invoke-WebRequest "$base/api/integrations/ping" -UseBasicParsing -Headers @{'X-API-Key'=$created.apiKey}|Out-Null}catch{$keyRejected=($_.Exception.Response.StatusCode.value__ -eq 401)}
  if(-not $keyRejected){throw 'Revoked API key was accepted'}; Write-Output 'API_KEY_REVOKE=PASS'
  $refresh=Invoke-RestMethod "$base/api/auth/refresh" -Method Post -ContentType 'application/json' -Body (@{refreshToken=$login.refreshToken}|ConvertTo-Json)
  if(-not $refresh.accessToken -or $refresh.refreshToken -eq $login.refreshToken){throw 'Rotation failed'}; Write-Output 'REFRESH=PASS'

  # Reusing the rotated token must revoke the whole family, including its active successor.
  $replayRejected=$false
  try{Invoke-WebRequest "$base/api/auth/refresh" -UseBasicParsing -Method Post -ContentType 'application/json' -Body (@{refreshToken=$login.refreshToken}|ConvertTo-Json)|Out-Null}catch{$replayRejected=($_.Exception.Response.StatusCode.value__ -eq 401)}
  if(-not $replayRejected){throw 'Previous refresh token was accepted'}; Write-Output 'REPLAY=PASS'
  $familyRejected=$false
  try{Invoke-WebRequest "$base/api/auth/refresh" -UseBasicParsing -Method Post -ContentType 'application/json' -Body (@{refreshToken=$refresh.refreshToken}|ConvertTo-Json)|Out-Null}catch{$familyRejected=($_.Exception.Response.StatusCode.value__ -eq 401)}
  if(-not $familyRejected){throw 'Rotated token family was not revoked'}; Write-Output 'REPLAY_FAMILY_REVOCATION=PASS'

  # A new session has a separate family and remains usable after the first family is revoked.
  $login=Invoke-RestMethod "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{username=$env:BootstrapAdmin__Username;password=$env:BootstrapAdmin__Password}|ConvertTo-Json)
  $refresh=Invoke-RestMethod "$base/api/auth/refresh" -Method Post -ContentType 'application/json' -Body (@{refreshToken=$login.refreshToken}|ConvertTo-Json)
  if(-not $refresh.refreshToken){throw 'Re-login after replay failed'}; Write-Output 'RELOGIN=PASS'
  $logout=Invoke-WebRequest "$base/api/auth/logout" -UseBasicParsing -Method Post -ContentType 'application/json' -Headers @{Authorization="Bearer $($refresh.accessToken)"} -Body (@{refreshToken=$refresh.refreshToken}|ConvertTo-Json)
  if($logout.StatusCode -ne 204){throw 'Logout failed'}; Write-Output 'LOGOUT=PASS'
  $rejected=$false
  try{Invoke-WebRequest "$base/api/auth/refresh" -UseBasicParsing -Method Post -ContentType 'application/json' -Body (@{refreshToken=$refresh.refreshToken}|ConvertTo-Json)|Out-Null}catch{$rejected=($_.Exception.Response.StatusCode.value__ -eq 401)}
  if(-not $rejected){throw 'Revoked token reused'}; Write-Output 'REVOKE=PASS'

  # Logout-all revokes the current login's refresh token.
  $lastLogin=Invoke-RestMethod "$base/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{username=$env:BootstrapAdmin__Username;password=$env:BootstrapAdmin__Password}|ConvertTo-Json)
  $logoutAll=Invoke-WebRequest "$base/api/auth/logout-all" -UseBasicParsing -Method Post -Headers @{Authorization="Bearer $($lastLogin.accessToken)"}
  if($logoutAll.StatusCode -ne 204){throw 'Logout-all failed'}
  $allRejected=$false
  try{Invoke-WebRequest "$base/api/auth/refresh" -UseBasicParsing -Method Post -ContentType 'application/json' -Body (@{refreshToken=$lastLogin.refreshToken}|ConvertTo-Json)|Out-Null}catch{$allRejected=($_.Exception.Response.StatusCode.value__ -eq 401)}
  if(-not $allRejected){throw 'Logout-all left refresh token usable'}; Write-Output 'LOGOUT_ALL=PASS'
  if("$(Invoke-RestMethod "$base/health/ready")" -ne 'Healthy'){throw 'Ready check failed'}; Write-Output 'READY=PASS'
  Write-Output 'ALL_SQL_SMOKE_CHECKS_PASSED'
}catch{
  Write-Output "SMOKE_FAILURE=$($_.Exception.Message)"
  if(Test-Path ($log+'.out')){Get-Content ($log+'.out') -Tail 35}
  if(Test-Path ($log+'.err')){Get-Content ($log+'.err') -Tail 20}
  throw
}finally{
  if($proc -and -not $proc.HasExited){Stop-Process $proc.Id -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1}
  $env:BootstrapAdmin__Enabled='false'
  if($created){dotnet ef database drop --force --project src/Core/Mkx.Templates.Infrastructure --startup-project src/Server/Mkx.Templates.Server --no-build --no-color; Write-Output "DATABASE_CLEANUP_EXIT=$LASTEXITCODE"}
  Remove-Item Env:BootstrapAdmin__Password,Env:BootstrapAdmin__Username,Env:Jwt__SigningKey,Env:ConnectionStrings__DefaultConnection,Env:ASPNETCORE_URLS,Env:RateLimit__AuthRequestsPerMinute,Env:Logging__LogLevel__Microsoft__EntityFrameworkCore,Env:Passkeys__Enabled,Env:Passkeys__RelyingPartyId,Env:Passkeys__Origin -ErrorAction SilentlyContinue
  Remove-Item ($log+'.out'),($log+'.err') -Force -ErrorAction SilentlyContinue
}
