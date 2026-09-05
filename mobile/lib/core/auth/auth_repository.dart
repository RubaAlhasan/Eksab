import 'package:dio/dio.dart';

import '../../shared/models/models.dart';
import '../config/app_config.dart';
import 'auth_exception.dart';
import 'auth_tokens.dart';
import 'registration.dart';
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

  /// Creates the account via `POST /api/app/otp/register`.
  ///
  /// On success the server has created an **unconfirmed** account and already
  /// sent the verification code — the caller goes straight to the OTP screen,
  /// no separate `requestOtp` needed.
  Future<void> register(RegisterRequest request, Dio client) async {
    try {
      await client.post<dynamic>(
        AppConfig.registerPath,
        data: request.toJson(),
      );
    } on DioException catch (error) {
      throw AuthException.fromDio(error);
    }
  }

  /// Sends a code to an existing account (`POST /api/app/otp/request`).
  Future<void> requestOtp(String phoneNumber, Dio client) async {
    try {
      await client.post<dynamic>(
        AppConfig.requestOtpPath,
        data: {'phoneNumber': phoneNumber},
      );
    } on DioException catch (error) {
      throw AuthException.fromDio(error);
    }
  }

  /// The real sign-in: OpenIddict custom grant `otp`, handled by
  /// `OtpLoginGrantHandler` which expects `phone_number` and `otp_code`.
  ///
  /// This also flips `PhoneNumberConfirmed` to true on first successful use,
  /// which is what activates a freshly registered account.
  Future<AuthTokens> signInWithOtp({
    required String phoneNumber,
    required String code,
  }) async {
    try {
      final response = await _tokenClient.post<Map<String, dynamic>>(
        AppConfig.tokenEndpoint,
        data: {
          'grant_type': 'otp',
          'phone_number': phoneNumber,
          'otp_code': code,
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

  /// Loads the signed-in user.
  ///
  /// Two calls because the data is split: Eksabli's own
  /// `CustomerProfileController` owns first/last name, date of birth and
  /// gender, while ABP's Account module owns the email and phone number.
  /// The ABP call is best-effort — a missing email should not block sign-in.
  Future<Customer> fetchProfile(Dio apiClient) async {
    try {
      final profile = await apiClient.get<Map<String, dynamic>>(
        AppConfig.customerProfilePath,
      );

      Map<String, dynamic> account = const {};
      try {
        final res = await apiClient.get<Map<String, dynamic>>(
          AppConfig.myProfilePath,
        );
        account = res.data ?? const {};
      } on DioException {
        // Non-fatal: we simply show no email/phone.
      }

      return _customerFrom(profile.data ?? const {}, account);
    } on DioException catch (error) {
      throw AuthException.fromDio(error);
    }
  }

  /// Applies a profile edit via `PUT /api/app/customer-profile/my`.
  Future<Customer> updateProfile({
    required Dio apiClient,
    required String firstName,
    required String lastName,
    DateTime? dateOfBirth,
    CustomerGender? gender,
  }) async {
    try {
      await apiClient.put<Map<String, dynamic>>(
        AppConfig.customerProfilePath,
        data: {
          'firstName': firstName,
          'lastName': lastName,
          if (dateOfBirth != null)
            'dateOfBirth': dateOfBirth.toIso8601String().split('T').first,
          'gender': (gender ?? CustomerGender.unspecified).value,
        },
      );
      return await fetchProfile(apiClient);
    } on DioException catch (error) {
      throw AuthException.fromDio(error);
    }
  }

  static Customer _customerFrom(
    Map<String, dynamic> profile,
    Map<String, dynamic> account,
  ) {
    final first = (profile['firstName'] as String?)?.trim() ?? '';
    final last = (profile['lastName'] as String?)?.trim() ?? '';
    final userName = (account['userName'] as String?)?.trim() ?? '';

    // The server allows a profile with no name yet; fall back to the username
    // (which is the phone number) so the app never greets a blank.
    final effectiveFirst = first.isNotEmpty
        ? first
        : (userName.isNotEmpty ? userName : 'There');

    final gender = CustomerGender.fromValue(profile['gender']);

    return Customer(
      id: (profile['userId'] as String?) ?? (account['id'] as String?) ?? '',
      firstName: effectiveFirst,
      lastName: last,
      initials: _initialsFrom(effectiveFirst, last),
      email: (account['email'] as String?) ?? '',
      phone: (account['phoneNumber'] as String?) ?? userName,
      dateOfBirth: DateTime.tryParse('${profile['dateOfBirth']}'),
      gender: gender == CustomerGender.unspecified ? null : gender.label,
      // CustomerProfileDto is an AuditedEntityDto, so creation time doubles as
      // "member since".
      memberSince: DateTime.tryParse('${profile['creationTime']}'),
    );
  }

  static String _initialsFrom(String first, String last) {
    final a = first.isNotEmpty ? first[0].toUpperCase() : '';
    final b = last.isNotEmpty ? last[0].toUpperCase() : '';
    final initials = '$a$b';
    return initials.isEmpty ? '?' : initials;
  }
}
