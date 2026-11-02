import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mkx_templates_api_client/mkx_api.dart';
import 'package:test/test.dart';

class MemorySession implements SessionStore {
  LocalTokens? value;
  MemorySession(this.value);
  @override
  Future<LocalTokens?> read() async => value;
  @override
  Future<void> write(LocalTokens? tokens) async => value = tokens;
}

http.Response reply(Object? data, [int status = 200]) =>
    http.Response(status == 204 ? '' : jsonEncode(data), status,
        headers: const {'content-type': 'application/json'});

void main() {
  test('username login never leaks stale bearer and stores in host', () async {
    final headers = <Map<String, String>>[];
    final session = MemorySession(LocalTokens('stale', 'old'));
    final api = MkxApiClient(
      baseUrl: 'https://api.example.test',
      sessionStore: session,
      client: MockClient((request) async {
        headers.add(request.headers);
        expect(jsonDecode(request.body)['username'], 'msd');
        return reply({
          'accessToken': 'new-access',
          'refreshToken': 'new-refresh',
          'accessTokenExpiresAt': DateTime.now().toUtc().toIso8601String(),
          'refreshTokenExpiresAt': DateTime.now().toUtc().toIso8601String(),
        });
      }),
    );
    final result = await api.authLogin(const LoginRequest(username: 'msd', password: 'pass'));
    expect(result.accessToken, 'new-access');
    expect(headers.single['Authorization'], isNull);
    expect(session.value!.refreshToken, 'new-refresh');
    api.close();
  });

  test('parallel local 401 share one refresh and resend with new access token', () async {
    var refreshCalls = 0;
    final session = MemorySession(LocalTokens('expired', 'refresh-one'));
    final api = MkxApiClient(
      baseUrl: 'https://api.example.test',
      sessionStore: session,
      client: MockClient((request) async {
        if (request.url.path == '/api/auth/refresh') {
          refreshCalls++;
          expect(request.headers['authorization'], isNull);
          expect(jsonDecode(request.body)['refreshToken'], 'refresh-one');
          await Future<void>.delayed(const Duration(milliseconds: 14));
          return reply({'accessToken': 'fresh', 'refreshToken': 'refresh-two'});
        }
        expect(request.url.path, '/api/auth/me');
        if (request.headers['authorization'] == 'Bearer expired') return reply({}, 401);
        expect(request.headers['authorization'], 'Bearer fresh');
        return reply({'id': 'u', 'username': 'msd', 'displayName': 'User', 'roles': []});
      }),
    );
    final result = await Future.wait([api.authMe(), api.authMe()]);
    expect(result[0].username, 'msd');
    expect(result[1].id, 'u');
    expect(refreshCalls, 1);
    expect(session.value!.accessToken, 'fresh');
    api.close();
  });

  test('four authorization schemes remain disjoint', () async {
    final observed = <Map<String, String>>[];
    final api = MkxApiClient(
      baseUrl: 'https://api.example.test',
      sessionStore: MemorySession(LocalTokens('LOCAL', 'R')),
      externalAccessToken: () async => 'EXTERNAL',
      opaqueAccessToken: () async => 'OPAQUE',
      apiKey: () async => 'SERVICE',
      client: MockClient((req) async {
        observed.add(req.headers);
        return reply({'status': 'ok'});
      }),
    );
    await api.externalIdentityMe();
    await api.opaqueIdentityMe();
    await api.integrationsPing();
    expect(observed[0]['authorization'], 'Bearer EXTERNAL');
    expect(observed[1]['authorization'], 'Bearer OPAQUE');
    expect(observed[2]['x-api-key'], 'SERVICE');
    expect(observed[2]['authorization'], isNull);
    api.close();
  });

  test('missing API-key provider does not fall back to local bearer', () async {
    var calls = 0;
    final api = MkxApiClient(
      baseUrl: 'https://api.example.test',
      sessionStore: MemorySession(LocalTokens('LOCAL', 'R')),
      client: MockClient((req) async { calls++; return reply({}); }),
    );
    await expectLater(api.integrationsPing(), throwsA(isA<CredentialsRequired>()));
    expect(calls, 0);
    api.close();
  });

  test('rate limiting raises HTTP 429 without refreshing', () async {
    var calls = 0;
    final api = MkxApiClient(
      baseUrl: 'https://api.example.test',
      sessionStore: MemorySession(LocalTokens('LOCAL', 'R')),
      client: MockClient((req) async { calls++; return reply({}, 429); }),
    );
    await expectLater(api.authMe(), throwsA(isA<ApiHttpException>()
      .having((error) => error.status, 'status', 429)));
    expect(calls, 1);
    api.close();
  });

  test('invalid API URL scheme or user info is rejected', () {
    for (final address in ['file:///tmp/test', 'https://user:pass@host.test', 'https://host.test/path']) {
      expect(() => MkxApiClient(baseUrl: address), throwsArgumentError);
    }
  });

  test('typed model serialization omits optional null TOTP code', () {
    final body = const LoginRequest(username: 'user', password: 'secret').toJson();
    expect(body['username'], 'user');
    expect(body.containsKey('authenticatorCode'), false);
    final response = PasskeyCeremonyResponse.fromJson({
      'ceremonyId': 'my-uuid', 'optionsJson': '{"challenge":"x"}',
    });
    expect(response.optionsJson, contains('challenge'));
  });
}
