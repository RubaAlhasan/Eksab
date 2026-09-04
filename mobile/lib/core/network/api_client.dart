import 'package:dio/dio.dart';

import '../auth/auth_tokens.dart';
import '../auth/token_store.dart';
import '../config/app_config.dart';
import 'dev_certificate.dart';

/// Builds the Dio instance every API request goes through.
///
/// [refreshTokens] performs a refresh-token grant and returns the new tokens
/// (or null if the session cannot be recovered). [onSessionLost] fires when
/// that fails, so the session provider can drop the user back to the login
/// screen rather than leaving the app in a state where every call 401s.
Dio buildApiClient({
  required TokenStore tokenStore,
  required Future<AuthTokens?> Function() refreshTokens,
  required void Function() onSessionLost,
}) {
  final dio = Dio(
    BaseOptions(
      baseUrl: AppConfig.baseUrl,
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 20),
      headers: {'Accept': 'application/json'},
    ),
  );

  allowDevCertificate(dio);

  dio.interceptors.add(
    _AuthInterceptor(
      dio: dio,
      tokenStore: tokenStore,
      refreshTokens: refreshTokens,
      onSessionLost: onSessionLost,
    ),
  );

  return dio;
}

/// A bare client for `/connect/token` — no interceptors, so a failed refresh
/// can never recurse back into the refresh path.
Dio buildTokenClient() {
  final dio = Dio(
    BaseOptions(
      connectTimeout: const Duration(seconds: 15),
      receiveTimeout: const Duration(seconds: 20),
      headers: {'Accept': 'application/json'},
    ),
  );
  allowDevCertificate(dio);
  return dio;
}

/// Attaches the bearer token, and refreshes once on a 401 before giving up.
class _AuthInterceptor extends Interceptor {
  _AuthInterceptor({
    required this.dio,
    required this.tokenStore,
    required this.refreshTokens,
    required this.onSessionLost,
  });

  final Dio dio;
  final TokenStore tokenStore;
  final Future<AuthTokens?> Function() refreshTokens;
  final void Function() onSessionLost;

  static const _retriedFlag = 'eksabli.retriedAfterRefresh';

  /// The token endpoint authenticates by grant, never by bearer token.
  static bool _isAuthEndpoint(RequestOptions options) =>
      options.path.contains('/connect/token');

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    if (_isAuthEndpoint(options)) return handler.next(options);

    var tokens = await tokenStore.read();

    // Refresh proactively rather than spending a request to discover it died.
    if (tokens != null && tokens.isExpired && tokens.canRefresh) {
      tokens = await refreshTokens();
    }

    if (tokens != null) {
      options.headers['Authorization'] = 'Bearer ${tokens.accessToken}';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    final options = err.requestOptions;

    final shouldRetry =
        err.response?.statusCode == 401 &&
        !_isAuthEndpoint(options) &&
        options.extra[_retriedFlag] != true;

    if (!shouldRetry) return handler.next(err);

    final refreshed = await refreshTokens();
    if (refreshed == null) {
      onSessionLost();
      return handler.next(err);
    }

    options
      ..headers['Authorization'] = 'Bearer ${refreshed.accessToken}'
      ..extra[_retriedFlag] = true;

    try {
      return handler.resolve(await dio.fetch<dynamic>(options));
    } on DioException catch (retryError) {
      return handler.next(retryError);
    }
  }
}
