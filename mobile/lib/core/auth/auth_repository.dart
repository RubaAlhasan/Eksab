import 'package:dio/dio.dart';

import '../../shared/models/models.dart';
import '../config/app_config.dart';
import 'auth_exception.dart';
import 'auth_tokens.dart';
import 'token_store.dart';

/// Talks to the ABP/OpenIddict auth server.
///
/// Uses a dedicated [Dio] for `/connect/token` so token calls never pass
/// through the bearer/refresh interceptor that wraps ordinary API calls.
class AuthRepository {
  AuthRepository({required this.tokenStore, required Dio tokenClient})
    : _tokenClient = tokenClient;

  final TokenStore tokenStore;
  final Dio _tokenClient;

  /// Resource-owner password grant. `Eksabli_App` is a public client, so no
  /// client secret is sent — the seed registers it with `secret: null`.
  Future<AuthTokens> signIn({
    required String username,
    required String password,
  }) async {
    try {
      final response = await _tokenClient.post<Map<String, dynamic>>(
        AppConfig.tokenEndpoint,
        data: {
          'grant_type': 'password',
          'username': username,
          'password': password,
          'client_id': AppConfig.clientId,
          'scope': AppConfig.scope,
        },
        options: Options(contentType: Headers.formUrlEncodedContentType),
      );

      final tokens = AuthTokens.fromResponse(response.data!);
      await tokenStore.write(tokens);
      return tokens;
    } on DioException catch (error) {
      throw AuthException.fromDio(error);
    }
  }

  /// Exchanges the stored refresh token for a fresh set. Returns null when the
  /// session cannot be recovered, which the caller should treat as "log out",
  /// not as a transient error.
  Future<AuthTokens?> refresh() async {
    final current = await tokenStore.read();
    final refreshToken = current?.refreshToken;
    if (refreshToken == null) return null;

    try {
      final response = await _tokenClient.post<Map<String, dynamic>>(
        AppConfig.tokenEndpoint,
        data: {
          'grant_type': 'refresh_token',
          'refresh_token': refreshToken,
          'client_id': AppConfig.clientId,
          'scope': AppConfig.scope,
        },
        options: Options(contentType: Headers.formUrlEncodedContentType),
      );

      final tokens = AuthTokens.fromResponse(response.data!);
      await tokenStore.write(tokens);
      return tokens;
    } on DioException {
      // A rejected refresh token is terminal — clear it so the app stops
      // retrying with a credential the server has already refused.
      await tokenStore.clear();
      return null;
    }
  }

  Future<void> signOut() => tokenStore.clear();

  /// Loads the signed-in user from ABP's Account module.
  ///
  /// The Eksabli-specific `CustomerProfileAppService` is
  /// `[RemoteService(IsEnabled = false)]`, so date of birth and gender are not
  /// reachable over HTTP yet — [Customer.dateOfBirth] and [Customer.gender]
  /// stay null until that service is exposed or a customer-facing endpoint is
  /// added.
  Future<Customer> fetchProfile(Dio apiClient) async {
    try {
      final response = await apiClient.get<Map<String, dynamic>>(
        AppConfig.myProfilePath,
      );
      return _customerFromProfile(response.data!);
    } on DioException catch (error) {
      throw AuthException.fromDio(error);
    }
  }

  static Customer _customerFromProfile(Map<String, dynamic> json) {
    final firstName = (json['name'] as String?)?.trim() ?? '';
    final surname = (json['surname'] as String?)?.trim() ?? '';
    final userName = (json['userName'] as String?)?.trim() ?? '';

    // ABP allows name/surname to be empty; fall back to the username so the
    // app never greets the user with a blank line.
    final effectiveFirst = firstName.isNotEmpty
        ? firstName
        : (userName.isNotEmpty ? userName : 'There');

    return Customer(
      id: (json['id'] as String?) ?? userName,
      firstName: effectiveFirst,
      lastName: surname,
      initials: _initialsFrom(effectiveFirst, surname),
      email: (json['email'] as String?) ?? '',
      phone: (json['phoneNumber'] as String?) ?? '',
      dateOfBirth: null,
      gender: null,
      memberSince: null,
    );
  }

  static String _initialsFrom(String first, String last) {
    final a = first.isNotEmpty ? first[0].toUpperCase() : '';
    final b = last.isNotEmpty ? last[0].toUpperCase() : '';
    final initials = '$a$b';
    return initials.isEmpty ? '?' : initials;
  }
}
