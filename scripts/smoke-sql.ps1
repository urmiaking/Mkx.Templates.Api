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
$env:Logging__LogLevel__Microsoft__EntityFrameworkCore = 'Warning'
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$listener.Start(); $port=$listener.LocalEndpoint.Port; $listener.Stop()
$base="http://127.0.0.1:$port"; $env:ASPNETCORE_URLS=$base
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
  $me=Invoke-RestMethod "$base/api/auth/me" -Headers @{Authorization="Bearer $($login.accessToken)"}
  if($me.username -ne $env:BootstrapAdmin__Username){throw 'Identity mismatch'}; Write-Output 'ME=PASS'
  $users=Invoke-RestMethod "$base/api/users" -Headers @{Authorization="Bearer $($login.accessToken)"}
  if($users.total -lt 1){throw 'Admin policy failed'}; Write-Output 'ADMIN_POLICY=PASS'
  $refresh=Invoke-RestMethod "$base/api/auth/refresh" -Method Post -ContentType 'application/json' -Body (@{refreshToken=$login.refreshToken}|ConvertTo-Json)
  if(-not $refresh.accessToken -or $refresh.refreshToken -eq $login.refreshToken){throw 'Rotation failed'}; Write-Output 'REFRESH=PASS'
  $logout=Invoke-WebRequest "$base/api/auth/logout" -UseBasicParsing -Method Post -ContentType 'application/json' -Headers @{Authorization="Bearer $($refresh.accessToken)"} -Body (@{refreshToken=$refresh.refreshToken}|ConvertTo-Json)
  if($logout.StatusCode -ne 204){throw 'Logout failed'}; Write-Output 'LOGOUT=PASS'
  $rejected=$false
  try{Invoke-WebRequest "$base/api/auth/refresh" -UseBasicParsing -Method Post -ContentType 'application/json' -Body (@{refreshToken=$refresh.refreshToken}|ConvertTo-Json)|Out-Null}catch{$rejected=($_.Exception.Response.StatusCode.value__ -eq 401)}
  if(-not $rejected){throw 'Revoked token reused'}; Write-Output 'REVOKE=PASS'
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
  Remove-Item Env:BootstrapAdmin__Password,Env:BootstrapAdmin__Username,Env:Jwt__SigningKey,Env:ConnectionStrings__DefaultConnection,Env:ASPNETCORE_URLS,Env:Logging__LogLevel__Microsoft__EntityFrameworkCore -ErrorAction SilentlyContinue
  Remove-Item ($log+'.out'),($log+'.err') -Force -ErrorAction SilentlyContinue
}
