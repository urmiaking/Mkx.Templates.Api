SET XACT_ABORT ON;
IF DB_NAME() <> N'BirSignDb' THROW 51002, 'This script may only run in BirSignDb', 1;
BEGIN TRY
 BEGIN TRAN;
 DECLARE @id INT=(SELECT Id FROM dbo.Clients WITH(UPDLOCK,HOLDLOCK) WHERE ClientId=N'mkx.api.local.pkce');
 IF @id IS NULL
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.ApiScopes WHERE Name=N'mkx.api.read') THROW 51000,'First run birsign-local-provision.ps1',1;
  INSERT dbo.Clients(Enabled,ClientId,ProtocolType,RequireClientSecret,ClientName,RequireConsent,AllowRememberConsent,
  AlwaysIncludeUserClaimsInIdToken,RequirePkce,AllowPlainTextPkce,RequireRequestObject,AllowAccessTokensViaBrowser,
  RequireDPoP,DPoPValidationMode,DPoPClockSkew,FrontChannelLogoutSessionRequired,BackChannelLogoutSessionRequired,
  AllowOfflineAccess,IdentityTokenLifetime,AccessTokenLifetime,AuthorizationCodeLifetime,AbsoluteRefreshTokenLifetime,
  SlidingRefreshTokenLifetime,RefreshTokenUsage,UpdateAccessTokenClaimsOnRefresh,RefreshTokenExpiration,
  AccessTokenType,EnableLocalLogin,IncludeJwtId,AlwaysSendClientClaims,DeviceCodeLifetime,Created,
  NonEditable,RequirePushedAuthorization)
  VALUES(1,N'mkx.api.local.pkce',N'oidc',0,N'Mkx API local PKCE',0,0,0,1,0,0,0,0,0,
  '00:00:00',0,0,0,300,600,300,2592000,1296000,1,0,1,0,1,0,1,300,SYSUTCDATETIME(),0,0);
  SET @id=SCOPE_IDENTITY();
  INSERT dbo.ClientGrantTypes(ClientId,GrantType) VALUES(@id,N'authorization_code');
  INSERT dbo.ClientRedirectUris(ClientId,RedirectUri) VALUES(@id,N'http://127.0.0.1:52173/callback');
  INSERT dbo.ClientScopes(ClientId,Scope) VALUES(@id,N'openid'),(@id,N'profile'),(@id,N'mkx.api.read');
  PRINT 'PKCE_PROVISION=CREATED';
 END
 ELSE
 BEGIN
  IF NOT EXISTS(SELECT 1 FROM dbo.Clients WHERE Id=@id AND Enabled=1 AND RequireClientSecret=0 AND RequirePkce=1 AND AllowPlainTextPkce=0 AND AllowAccessTokensViaBrowser=0)
   OR (SELECT COUNT(*) FROM dbo.ClientGrantTypes WHERE ClientId=@id)<>1
   OR NOT EXISTS(SELECT 1 FROM dbo.ClientGrantTypes WHERE ClientId=@id AND GrantType=N'authorization_code')
   OR (SELECT COUNT(*) FROM dbo.ClientRedirectUris WHERE ClientId=@id)<>1
   OR NOT EXISTS(SELECT 1 FROM dbo.ClientRedirectUris WHERE ClientId=@id AND RedirectUri=N'http://127.0.0.1:52173/callback')
   OR (SELECT COUNT(*) FROM dbo.ClientScopes WHERE ClientId=@id)<>3
   OR EXISTS(SELECT 1 FROM dbo.ClientScopes WHERE ClientId=@id AND Scope NOT IN (N'openid',N'profile',N'mkx.api.read'))
   OR EXISTS(SELECT 1 FROM dbo.ClientSecrets WHERE ClientId=@id)
   THROW 51001,'Existing PKCE client differs; refusing to overwrite',1;
  PRINT 'PKCE_PROVISION=ALREADY_PRESENT';
 END
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
