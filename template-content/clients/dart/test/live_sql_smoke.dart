// Optional real HTTP + disposable SQL Server smoke; not part of dart test.
import 'dart:io';

import 'package:mkx_templates_api_client/mkx_api.dart';

class LocalSession implements SessionStore {
  LocalTokens? value;
  @override
  Future<LocalTokens?> read() async => value;
  @override
  Future<void> write(LocalTokens? tokens) async => value = tokens;
}

Future<void> main() async {
  final env = Platform.environment;
  final baseUrl = env['MKX_SMOKE_BASE_URL'];
  final username = env['MKX_SMOKE_USERNAME'];
  final password = env['MKX_SMOKE_PASSWORD'];
  if (baseUrl == null || username == null || password == null) {
    throw StateError('Disposable MKX_SMOKE_* environment variables are required.');
  }
  final session = LocalSession();
  final api = MkxApiClient(baseUrl: baseUrl, sessionStore: session);
  try {
    final token = await api.authLogin(LoginRequest(username: username, password: password));
    if (token.accessToken == null || token.refreshToken == null) {
      throw StateError('Missing login tokens');
    }
    final initial = await api.authMe();
    if (initial.username != username) throw StateError('Login identity mismatch');

    await session.write(LocalTokens('invalid-access-token', token.refreshToken!));
    final restored = await api.authMe();
    if (restored.username != username) throw StateError('Refresh identity mismatch');
    final rotated = session.value;
    if (rotated == null || rotated.accessToken == 'invalid-access-token' ||
        rotated.refreshToken == token.refreshToken) {
      throw StateError('Real SQL-backed refresh did not rotate credentials');
    }

    final users = await api.usersList(page: 1, pageSize: 1);
    if (users.total < 1) throw StateError('Administrator policy not enforced');

    await api.authLogout(RevokeRequest(refreshToken: rotated.refreshToken));
    await session.write(null);
    stdout.writeln('DART_SDK_REAL_HTTP_SQL=PASS');
  } finally {
    api.close();
  }
}
