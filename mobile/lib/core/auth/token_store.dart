import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'auth_tokens.dart';

/// Persists the token set in the platform keystore (Keychain on iOS, the
/// EncryptedSharedPreferences-backed keystore on Android) so a refresh token
/// never sits in plain shared preferences.
class TokenStore {
  TokenStore({FlutterSecureStorage? storage})
    : _storage =
          storage ??
          const FlutterSecureStorage(
            aOptions: AndroidOptions(encryptedSharedPreferences: true),
            iOptions: IOSOptions(
              accessibility: KeychainAccessibility.first_unlock,
            ),
          );

  final FlutterSecureStorage _storage;

  static const _accessKey = 'eksabli.access_token';
  static const _refreshKey = 'eksabli.refresh_token';
  static const _expiryKey = 'eksabli.expires_at';

  Future<AuthTokens?> read() async {
    final access = await _storage.read(key: _accessKey);
    final expiryRaw = await _storage.read(key: _expiryKey);
    if (access == null || expiryRaw == null) return null;

    final expiresAt = DateTime.tryParse(expiryRaw);
    if (expiresAt == null) return null;

    return AuthTokens(
      accessToken: access,
      refreshToken: await _storage.read(key: _refreshKey),
      expiresAt: expiresAt,
    );
  }

  Future<void> write(AuthTokens tokens) async {
    await _storage.write(key: _accessKey, value: tokens.accessToken);
    await _storage.write(
      key: _expiryKey,
      value: tokens.expiresAt.toIso8601String(),
    );

    // A refresh response may omit the refresh token when the server is
    // configured to reuse it — keep the existing one in that case.
    if (tokens.refreshToken != null) {
      await _storage.write(key: _refreshKey, value: tokens.refreshToken);
    }
  }

  Future<void> clear() async {
    await _storage.delete(key: _accessKey);
    await _storage.delete(key: _refreshKey);
    await _storage.delete(key: _expiryKey);
  }
}
