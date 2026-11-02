import 'dart:async';
import 'dart:convert';
import 'package:http/http.dart' as http;

/// Never store credentials in an SDK global or in insecure localStorage.
class LocalTokens {
  final String accessToken;
  final String refreshToken;
  const LocalTokens(this.accessToken, this.refreshToken);
}
abstract interface class SessionStore {
  Future<LocalTokens?> read();
  Future<void> write(LocalTokens? tokens);
}
typedef CredentialProvider = Future<String?> Function();

class ApiHttpException implements Exception {
  final int status;
  final String responseText;
  ApiHttpException(this.status, this.responseText);
  @override
  String toString() => 'API HTTP $status';
}
class CredentialsRequired implements Exception {
  final String scheme;
  CredentialsRequired(this.scheme);
  @override
  String toString() => 'Credential provider unavailable for $scheme';
}

/// Works with Flutter mobile, desktop, and Flutter web (package:http).
class ApiTransport {
  final Uri base;
  final http.Client httpClient;
  final SessionStore? sessionStore;
  final CredentialProvider? externalAccessToken;
  final CredentialProvider? opaqueAccessToken;
  final CredentialProvider? apiKey;
  Future<bool>? _refreshInFlight;

  ApiTransport({
    required String baseUrl,
    http.Client? client,
    this.sessionStore,
    this.externalAccessToken,
    this.opaqueAccessToken,
    this.apiKey,
  }) : base = Uri.parse(baseUrl),
       httpClient = client ?? http.Client() {
    if ((base.scheme != 'http' && base.scheme != 'https') ||
        (base.scheme == 'http' && base.host != 'localhost' && base.host != '127.0.0.1') ||
        base.host.isEmpty || base.userInfo.isNotEmpty ||
        base.path.isNotEmpty && base.path != '/' ||
        base.hasQuery || base.hasFragment) {
      throw ArgumentError('baseUrl must be an HTTP(S) origin with no credentials/path/query');
    }
  }

  Future<void> persistTokens(String accessToken, String refreshToken) async {
    await sessionStore?.write(LocalTokens(accessToken, refreshToken));
  }

  Future<dynamic> request(
    String method, String path, String? security, {
    Object? body, Map<String, String?>? query, bool retry = true,
  }) async {
    final uri = base.resolve(path).replace(queryParameters: {
      for (final item in (query ?? {}).entries)
        if (item.value != null) item.key: item.value!,
    });
    final headers = <String, String>{'Accept': 'application/json'};
    String? usedLocal;
    if (security == 'LocalJwt') {
      usedLocal = (await sessionStore?.read())?.accessToken;
      if (usedLocal == null || usedLocal.isEmpty) throw CredentialsRequired(security!);
      headers['Authorization'] = 'Bearer $usedLocal';
    } else if (security != null) {
      final source = switch (security) {
        'ExternalOidc' => externalAccessToken,
        'OpaqueToken' => opaqueAccessToken,
        'ApiKey' => apiKey,
        _ => throw ArgumentError('Unknown security scheme: $security'),
      };
      final token = await source?.call();
      if (token == null || token.isEmpty) throw CredentialsRequired(security);
      if (security == 'ApiKey') { headers['X-API-Key'] = token; }
      else { headers['Authorization'] = 'Bearer $token'; }
    }
    if (body != null) headers['Content-Type'] = 'application/json';
    final req = http.Request(method, uri)..headers.addAll(headers);
    if (body != null) req.body = jsonEncode(body);
    // package:http does not automatically follow redirects on this request.
    req.followRedirects = false;
    final res = await http.Response.fromStream(await httpClient.send(req));
    if (res.statusCode == 401 && security == 'LocalJwt' && retry && sessionStore != null) {
      final newest = (await sessionStore!.read())?.accessToken;
      if (newest != null && newest != usedLocal) {
        return request(method, path, security, body: body, query: query, retry: false);
      }
      if (await _refreshLocalOnce()) {
        return request(method, path, security, body: body, query: query, retry: false);
      }
    }
    if (res.statusCode < 200 || res.statusCode >= 300) {
      throw ApiHttpException(res.statusCode,
          res.body.length > 2048 ? res.body.substring(0, 2048) : res.body);
    }
    if (res.statusCode == 204 || res.body.isEmpty) return null;
    try { return jsonDecode(res.body); }
    on FormatException { return res.body; }
  }

  Future<bool> _refreshLocalOnce() {
    final pending = _refreshInFlight;
    if (pending != null) return pending;
    final next = _doRefresh();
    _refreshInFlight = next;
    return next.whenComplete(() { _refreshInFlight = null; });
  }

  Future<bool> _doRefresh() async {
    final store = sessionStore;
    final prior = await store?.read();
    if (prior == null || store == null) return false;
    try {
      final refreshRequest = http.Request('POST', base.resolve('/api/auth/refresh'))
        ..headers.addAll(const {'Content-Type':'application/json','Accept':'application/json'})
        ..body = jsonEncode({'refreshToken':prior.refreshToken})
        ..followRedirects = false;
      final response = await http.Response.fromStream(await httpClient.send(refreshRequest));
      if (response.statusCode < 200 || response.statusCode >= 300) {
        await store.write(null);
        return false;
      }
      final data = jsonDecode(response.body);
      if (data is! Map<String, dynamic> ||
          data['accessToken'] is! String || data['refreshToken'] is! String) {
        throw const FormatException('Invalid refresh JSON');
      }
      await store.write(LocalTokens(data['accessToken'] as String, data['refreshToken'] as String));
      return true;
    } catch (_) {
      await store.write(null);
      return false;
    }
  }

  /// Close when the host app disposes this client, if it owns the HTTP transport.
  void close() => httpClient.close();
}
