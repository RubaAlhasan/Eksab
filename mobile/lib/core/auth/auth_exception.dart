import 'package:dio/dio.dart';

/// A login failure translated into something the UI can show verbatim.
///
/// OpenIddict reports credential problems as HTTP 400 with an
/// `{"error": "...", "error_description": "..."}` body, so a failed password
/// is *not* a 401 — mapping it correctly is the difference between "wrong
/// password" and a generic "something went wrong".
class AuthException implements Exception {
  const AuthException(this.message, {this.kind = AuthFailure.unknown});

  final String message;
  final AuthFailure kind;

  /// True when the credentials themselves were rejected, as opposed to the
  /// request never reaching the server.
  bool get isCredentialError => kind == AuthFailure.invalidCredentials;

  @override
  String toString() => 'AuthException($kind): $message';

  factory AuthException.fromDio(DioException error) {
    switch (error.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.transformTimeout:
        return const AuthException(
          'The server took too long to respond. Check your connection and try '
          'again.',
          kind: AuthFailure.network,
        );

      case DioExceptionType.connectionError:
      case DioExceptionType.unknown:
        return AuthException(
          _isCertificateError(error)
              ? 'Could not establish a secure connection to the server. If you '
                    'are running against the local dev host, trust its '
                    'development certificate.'
              : 'Could not reach the server. Check that the API is running and '
                    'that you are on the same network.',
          kind: AuthFailure.network,
        );

      case DioExceptionType.badCertificate:
        return const AuthException(
          'The server presented an untrusted certificate.',
          kind: AuthFailure.network,
        );

      case DioExceptionType.cancel:
        return const AuthException('The request was cancelled.');

      case DioExceptionType.badResponse:
        return _fromResponse(error.response);
    }
  }

  static AuthException _fromResponse(Response<dynamic>? response) {
    final status = response?.statusCode;
    final data = response?.data;
    final body = data is Map<String, dynamic> ? data : const {};

    final code = body['error'] as String?;
    final description = (body['error_description'] as String?)?.trim();

    // ABP's own error envelope, used by the API endpoints (not /connect/token).
    final abpMessage =
        (body['error'] is Map<String, dynamic>
                ? (body['error'] as Map<String, dynamic>)['message']
                : null)
            as String?;

    if (code == 'invalid_grant') {
      // OpenIddict returns invalid_grant for a bad password *and* for a locked
      // or unconfirmed account; the description distinguishes them, so prefer
      // it when the server sends one.
      return AuthException(
        description == null || description.isEmpty
            ? 'Incorrect phone number or password.'
            : description,
        kind: AuthFailure.invalidCredentials,
      );
    }

    if (code == 'invalid_client' || code == 'unauthorized_client') {
      return const AuthException(
        'This app is not authorised by the server. Check the OpenIddict client '
        'configuration.',
        kind: AuthFailure.configuration,
      );
    }

    if (status == 401) {
      return const AuthException(
        'Your session has expired. Please log in again.',
        kind: AuthFailure.sessionExpired,
      );
    }

    if (status != null && status >= 500) {
      return const AuthException(
        'The server had a problem handling that. Try again in a moment.',
        kind: AuthFailure.server,
      );
    }

    return AuthException(
      description ?? abpMessage ?? 'Login failed (HTTP ${status ?? '?'}).',
    );
  }

  static bool _isCertificateError(DioException error) {
    final message = '${error.error}'.toLowerCase();
    return message.contains('certificate') || message.contains('handshake');
  }
}

enum AuthFailure {
  invalidCredentials,
  sessionExpired,
  network,
  server,
  configuration,
  unknown,
}
