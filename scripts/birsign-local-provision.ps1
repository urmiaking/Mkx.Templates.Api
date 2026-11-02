# Local BirSignDb only. Creates isolated machine client and per-user DPAPI-protected credential.
$ErrorActionPreference='Stop'
$id='mkx.api.local.smoke';$scope='mkx.api.read';$resource='mkx.api'
$dir=Join-Path $env:LOCALAPPDATA 'Mkx.Templates.Api';[IO.Directory]::CreateDirectory($dir)|Out-Null
$file=Join-Path $dir 'BirSignLocalSmoke.dpapi';$new=!(Test-Path $file)
if($new){$secret=(& { $bytes = New-Object byte[] 48; $rng = [Security.Cryptography.RandomNumberGenerator]::Create(); try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }; [Convert]::ToBase64String($bytes) });ConvertTo-SecureString $secret -AsPlainText -Force|ConvertFrom-SecureString|Set-Content $file -NoNewline}
else{$secure=(Get-Content $file -Raw).Trim()|ConvertTo-SecureString;$ptr=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure);try{$secret=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)}finally{[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)}}
$hash=(& { $sha=[Security.Cryptography.SHA256]::Create(); try { [Convert]::ToBase64String($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($secret))) } finally { $sha.Dispose() } })
$conn=[System.Data.SqlClient.SqlConnection]::new('Server=localhost;Database=BirSignDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=8')
try{
$conn.Open();if($conn.Database -ne 'BirSignDb'){throw 'Wrong database'}
$tx=$conn.BeginTransaction([System.Data.IsolationLevel]::Serializable)
try{
function sql($q,$a=@{}){$c=$conn.CreateCommand();$c.Transaction=$tx;$c.CommandText=$q;foreach($v in $a.GetEnumerator()){$null=$c.Parameters.AddWithValue('@'+$v.Key,$v.Value)};try{$c.ExecuteScalar()}finally{$c.Dispose()}}
$exists=[int](sql 'SELECT COUNT(*) FROM dbo.Clients WHERE ClientId=@x' @{x=$id})
if($exists -ne 0){$match=[int](sql "SELECT COUNT(*) FROM dbo.Clients c JOIN dbo.ClientGrantTypes g ON g.ClientId=c.Id AND g.GrantType='client_credentials' JOIN dbo.ClientScopes s ON s.ClientId=c.Id AND s.Scope=@s JOIN dbo.ClientSecrets x ON x.ClientId=c.Id AND x.Value=@h WHERE c.ClientId=@x" @{s=$scope;h=$hash;x=$id});if($match -ne 1){throw 'Client already exists with different settings'};$tx.Commit();'PROVISION=ALREADY_PRESENT';return}
if([int](sql 'SELECT COUNT(*) FROM dbo.ApiScopes WHERE Name=@x' @{x=$scope}) -ne 0 -or [int](sql 'SELECT COUNT(*) FROM dbo.ApiResources WHERE Name=@x' @{x=$resource}) -ne 0){throw 'Scope/resource name already in use'}
$sid=sql "INSERT dbo.ApiScopes(Enabled,Name,DisplayName,Required,Emphasize,ShowInDiscoveryDocument,Created,NonEditable) VALUES(1,@x,'Mkx API Local Read',0,0,1,SYSUTCDATETIME(),0);SELECT CONVERT(int,SCOPE_IDENTITY())" @{x=$scope}
$rid=sql "INSERT dbo.ApiResources(Enabled,Name,DisplayName,ShowInDiscoveryDocument,RequireResourceIndicator,Created,NonEditable) VALUES(1,@x,'Mkx Local API',1,0,SYSUTCDATETIME(),0);SELECT CONVERT(int,SCOPE_IDENTITY())" @{x=$resource}
$null=sql 'INSERT dbo.ApiResourceScopes(ApiResourceId,Scope) VALUES(@i,@s); SELECT 1' @{i=$rid;s=$scope}
$client=sql @'
INSERT dbo.Clients(Enabled,ClientId,ProtocolType,RequireClientSecret,ClientName,RequireConsent,AllowRememberConsent,AlwaysIncludeUserClaimsInIdToken,RequirePkce,AllowPlainTextPkce,RequireRequestObject,AllowAccessTokensViaBrowser,RequireDPoP,DPoPValidationMode,DPoPClockSkew,FrontChannelLogoutSessionRequired,BackChannelLogoutSessionRequired,AllowOfflineAccess,IdentityTokenLifetime,AccessTokenLifetime,AuthorizationCodeLifetime,AbsoluteRefreshTokenLifetime,SlidingRefreshTokenLifetime,RefreshTokenUsage,UpdateAccessTokenClaimsOnRefresh,RefreshTokenExpiration,AccessTokenType,EnableLocalLogin,IncludeJwtId,AlwaysSendClientClaims,DeviceCodeLifetime,Created,NonEditable,RequirePushedAuthorization)
VALUES(1,@x,'oidc',1,'Mkx API Local Smoke',0,0,0,0,0,0,0,0,0,'00:00:00',0,0,0,300,600,300,2592000,1296000,1,0,1,0,0,0,1,300,SYSUTCDATETIME(),0,0);
SELECT CONVERT(int,SCOPE_IDENTITY())
'@ @{x=$id}
$null=sql "INSERT dbo.ClientGrantTypes(ClientId,GrantType) VALUES(@i,'client_credentials'); SELECT 1" @{i=$client}
$null=sql 'INSERT dbo.ClientScopes(ClientId,Scope) VALUES(@i,@s);SELECT 1' @{i=$client;s=$scope}
$null=sql "INSERT dbo.ClientSecrets(ClientId,Value,Type,Description,Expiration,Created) VALUES(@i,@h,'SharedSecret','Local DPAPI test credential',DATEADD(day,30,SYSUTCDATETIME()),SYSUTCDATETIME());SELECT 1" @{i=$client;h=$hash}
$tx.Commit();'PROVISION=CREATED';'CLIENT=mkx.api.local.smoke; SCOPE=mkx.api.read; RESOURCE=mkx.api';'SECRET=DPAPI_LOCAL_ONLY'
}catch{try{$tx.Rollback()}catch{};if($new){Remove-Item $file -Force -ErrorAction SilentlyContinue};throw}
}finally{$conn.Dispose();$secret=$null;$hash=$null}
