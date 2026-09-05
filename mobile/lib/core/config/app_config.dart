import 'package:flutter/foundation.dart';

/// Backend endpoints and OpenIddict client settings.
///
/// These mirror `angular/src/environments/environment.ts` so both clients talk
/// to the same auth server with the same registered client:
///
/// ```ts
/// issuer:   'https://localhost:44330/'
/// clientId: 'Eksabli_App'
/// scope:    'offline_access Eksabli'
/// ```
///
/// `Eksabli_App` is seeded as a **public** client (no secret) that allows the
/// `password` and `refresh_token` grants — see
/// `src/Eksabli.Domain/OpenIddict/OpenIddictDataSeedContributor.cs`. Angular
/// uses the authorization-code redirect flow because it runs in a browser; a
/// native app with its own login form uses the password grant against the same
/// client instead.
abstract final class AppConfig {
  /// Override for real devices / staging:
  /// `flutter run --dart-define=EKSABLI_API_URL=https://api.example.com`
  static const _urlOverride = String.fromEnvironment('EKSABLI_API_URL');

  static const clientId = 'Eksabli_App';

  /// Matches the Angular client's scope string exactly.
  static const scope = 'offline_access Eksabli';

  /// The API and the auth server are the same host in this solution
  /// (`AuthServer:Authority` == `App:SelfUrl` == `https://localhost:44330`).
  static String get baseUrl {
    if (_urlOverride.isNotEmpty) return _urlOverride;

    // The Android emulator reaches the host machine on 10.0.2.2, never on
    // localhost — that resolves to the emulator itself. `defaultTargetPlatform`
    // is used rather than `Platform.isAndroid` so this file stays web-safe.
    if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) {
      return 'https://10.0.2.2:44330';
    }

    return 'https://localhost:44330';
  }

  static String get tokenEndpoint => '$baseUrl/connect/token';

  // ---------------------------------------------------------------------
  // Endpoints
  //
  // App services are `[RemoteService(IsEnabled = false)]` and exposed through
  // explicit controllers in `src/Eksabli.HttpApi/Controllers/`, so these are
  // hand-written routes rather than ABP's conventional API paths.
  // ---------------------------------------------------------------------

  /// `OtpController.RegisterAsync` — creates the IdentityUser + CustomerProfile
  /// (unconfirmed) **and** sends the verification code in the same call.
  static const registerPath = '/api/app/otp/register';

  /// `OtpController.RequestOtpAsync` — sends a code to an existing account.
  static const requestOtpPath = '/api/app/otp/request';

  /// `CustomerProfileController` — first name, last name, date of birth, gender.
  static const customerProfilePath = '/api/app/customer-profile/my';

  /// ABP Account module — carries the email and phone number that the Eksabli
  /// profile does not.
  static const myProfilePath = '/api/account/my-profile';

  /// True when pointed at a local dev host whose HTTPS certificate is the
  /// self-signed `openiddict.pfx` dev cert.
  static bool get isLocalDevHost =>
      baseUrl.contains('localhost') || baseUrl.contains('10.0.2.2');
}
