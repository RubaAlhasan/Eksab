/// The token set returned by OpenIddict's `/connect/token` endpoint.
class AuthTokens {
  const AuthTokens({
    required this.accessToken,
    required this.refreshToken,
    required this.expiresAt,
  });

  factory AuthTokens.fromResponse(Map<String, dynamic> json) {
    final expiresIn = json['expires_in'];
    final seconds = expiresIn is int
        ? expiresIn
        : int.tryParse('$expiresIn') ?? 3600;

    return AuthTokens(
      accessToken: json['access_token'] as String,
      refreshToken: json['refresh_token'] as String?,
      expiresAt: DateTime.now().add(Duration(seconds: seconds)),
    );
  }

  final String accessToken;

  /// Null when the server did not issue one — only possible if `offline_access`
  /// was refused, which would mean silent re-auth is unavailable.
  final String? refreshToken;
  final DateTime expiresAt;

  /// Treated as expired 30s early so a request never leaves with a token that
  /// dies in flight.
  bool get isExpired =>
      DateTime.now().isAfter(expiresAt.subtract(const Duration(seconds: 30)));

  bool get canRefresh => refreshToken != null;
}
