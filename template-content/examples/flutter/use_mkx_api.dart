// Example Flutter mobile/web integration.
// Add mkx_templates_api_client via local path dependency and:
// flutter pub add flutter_secure_storage
//
// The API base URL must be HTTPS outside localhost. Do not put an API key
// intended for server-to-server access in an installed mobile app.
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:mkx_templates_api_client/mkx_api.dart';

class ProtectedSession implements SessionStore {
  static const _access = 'mkx_access_token';
  static const _refresh = 'mkx_refresh_token';
  final FlutterSecureStorage secure;
  ProtectedSession(this.secure);

  @override
  Future<LocalTokens?> read() async {
    final access = await secure.read(key: _access);
    final refresh = await secure.read(key: _refresh);
    return access != null && refresh != null ? LocalTokens(access, refresh) : null;
  }

  @override
  Future<void> write(LocalTokens? tokens) async {
    if (tokens == null) {
      await secure.delete(key: _access);
      await secure.delete(key: _refresh);
    } else {
      await secure.write(key: _access, value: tokens.accessToken);
      await secure.write(key: _refresh, value: tokens.refreshToken);
    }
  }
}

class AppApi {
  final ProtectedSession session = ProtectedSession(const FlutterSecureStorage());
  late final MkxApiClient api = MkxApiClient(
    baseUrl: 'https://api.example.com',
    sessionStore: session,
  );

  Future<TokenResponse> login(String username, String password, {String? totp}) =>
      api.authLogin(LoginRequest(username: username, password: password,
          authenticatorCode: totp));

  Future<AccountResponse> me() => api.authMe();

  Future<void> logout() async {
    final old = await session.read();
    try {
      if (old != null) await api.authLogout(RevokeRequest(refreshToken: old.refreshToken));
    } finally {
      await session.write(null);
    }
  }
}
