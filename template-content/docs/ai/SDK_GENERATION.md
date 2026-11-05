# Typed TypeScript and Dart SDKs (Phase 8)

The API template ships two **versioned, local-path SDK libraries** in clients/typescript and clients/dart. Both are deterministically generated from openapi/v1.json, a canonical snapshot of the actual running Swagger endpoint. The current contract has 32 operations, 33 schemas and four distinct authentication schemes. Generated model and operation files are never edited by hand.

## Regenerate after any API contract change

Run from the generated solution root (Python 3.10+, .NET 10 required):

    dotnet build Mkx.Templates.slnx -c Release
    python tools/export_openapi.py --configuration Release
    python tools/generate_sdks.py

The exporter starts the Development Swagger endpoint on 127.0.0.1 with a disposable JWT signing key and with admin/database/bootstrap/external integration features OFF. It never queries the production database or registers a real account. The deterministic code generator fails if it encounters unsupported schema constructs or unrecognized security schemes. Commit the OpenAPI snapshot and regenerated SDK source alongside backend changes.

Check for drift, without modifying files:

    python tools/export_openapi.py --check --configuration Release
    python tools/generate_sdks.py --check

CI verifies the live Swagger snapshot, both SDKs and a freshly instantiated dotnet-new template. A client model typed according to the contract is not a runtime validator; the backend remains the authority for required fields and business rules. Nullability/requiredness reflect what Swashbuckle emits, not an inferred alternative contract.

## TypeScript client (React, React Native / Expo, browser, Node 24+)

    cd clients/typescript
    npm ci
    npm test

The compiled ES module is dist/index.js and its declarations dist/index.d.ts. In a frontend app, build the SDK and install it as a local file package, adjusting the relative path:

    npm install /path/to/generated-api/clients/typescript

Example: examples/react-native/use-mkx-api.ts. For Expo, install expo-secure-store in the consuming application; use iOS Keychain/Android Keystore to protect the opaque refresh token. The SDK never accesses localStorage or persists tokens on its own. The host implements SessionStore.read/write asynchronously.

Example import:

    import { MkxApiClient } from '@mkx/templates-api-client';
    const api = new MkxApiClient({ baseUrl: 'https://api.example.com', sessionStore });
    const result = await api.authLogin({ username: 'person', password: 'secret' });
    const me = await api.authMe();

## Dart / Flutter client (Android, iOS, web, desktop)

    cd clients/dart
    dart pub get
    dart analyze
    dart test

In the consuming Flutter app's pubspec.yaml, include a local path dependency:

    dependencies:
      mkx_templates_api_client:
        path: ../path-to-generated-api/clients/dart

Then use package:mkx_templates_api_client/mkx_api.dart, and implement the SessionStore interface. Example: examples/flutter/use_mkx_api.dart, using the optional FlutterSecureStorage package. Network is implemented with package:http for portability across Flutter web/mobile/desktop. Keep tokens out of debug logs, URLs and insecure preferences. Never ship a server-side integration API key to an installed application.

## Security and HTTP behavior

- LocalJwt uses ONLY SessionStore local bearer tokens. On HTTP 401 for GET/HEAD only, the SDK performs a single-flight POST /api/auth/refresh, stores the rotated session through SessionStore, and retries that read-only request ONCE. Write operations (POST/PUT/PATCH/DELETE) never refresh or retry automatically, since side effects might already have occurred. The refresh request carries no Authorization header. Do not instantiate competing clients using the same refresh session store without cross-instance rotation coordination.
- ExternalOidc uses ONLY externalAccessToken, OpaqueToken uses ONLY opaqueAccessToken. No silent mapping to the local JWT scheme; no local refresh on external or opaque 401.
- ApiKey uses ONLY the X-API-Key provider and is appropriate for **server-to-server** use. Do not configure that provider in public browser, React Native, or Flutter apps.
- Anonymous routes have NO bearer token or API key, even if the host has a local session. A missing scheme-specific credential fails before network access.
- HTTPS is required for non-loopback origins. 429/403 are returned as typed HTTP errors; SDKs do not automatically retry mutating operations or use a refresh token after a non-local 401. Protect app sessions with HTTPS and secure storage. A valid access JWT may remain valid until its configured expiration after the refresh session is revoked.
- WebAuthn passkeys in clients still require a native/browserside navigator.credentials / platform authenticator ceremony with the exact configured RP ID and Origin. The SDK transports ceremony options and credential JSON; it does not replace OS biometric APIs or override an MFA requirement.

## Publication and validation scope

The NuGet **template** containing both client libraries is shipped as a GitHub Release artifact. Neither client is published to npm or pub.dev, and NuGet.org publishing requires explicit owner authorization and publisher credentials. Do not claim a registry publish on the basis of a local pack or GitHub Release.

TypeScript and Dart are compiled/analyzed and transport-tested with mock HTTP clients for session rotation, concurrency, security scheme isolation and errors. The source and newly generated API build/test separately. Live React Native, Expo, Flutter device/web deployment and real passkey hardware are not verified by these SDK tests.

## Real HTTP + disposable SQL Server verification

From repository root on a development machine with local SQL Server:

    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-sql.ps1

Creates a unique disposable database and runs the local API on loopback. Both TypeScript and Dart clients independently verify actual username/password login, JWT, rotated refresh on unauthorized GET, admin authorization and logout. Existing API-key and WebAuthn challenges are also tested. The temporary database is dropped on cleanup.

## NuGet.org publication via Trusted Publishing (OIDC)

No permanent NuGet API key is required. NuGet.org Trusted Publishers authorizes this repo via GitHub OIDC. The NuGet.org policy for owner mskhodadadi must specify:

- Repository owner: urmiaking
- Repository: Mkx.Templates.Api
- Workflow file: deploy.yaml (exact filename inside .github/workflows)
- Environment: production

The manual GitHub Actions workflow is .github/workflows/deploy.yaml. It requires the id-token: write permission and uses NuGet/login@v1 with NuGet user mskhodadadi to acquire a short-lived NuGet.org API key. No key is committed or saved as a GitHub Secret. The workflow downloads the exact existing GitHub Release package, checks package identity/version and contents, then publishes it without rebuilding.

To publish: Actions > Publish verified NuGet package (Trusted Publishing) > Run workflow on main and enter an existing GitHub release version. Then verify at https://www.nuget.org/packages/Mkx.Templates.Api . NuGet.org package IDs are unique and mismatched policy fields prevent authorization.

The workflow is manually dispatched, not auto-triggered on every push. It respects GitHub Environment approvals if enabled.
