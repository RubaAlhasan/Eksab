# Eksabli Mobile (Flutter)

The customer-facing app for the Eksabli loyalty platform — a Flutter port of every screen in
[`prototype/customer/`](../prototype/customer/), following the architecture agreed in
[`docs/eksabli-loyalty-platform/05-flutter-architecture.md`](../docs/eksabli-loyalty-platform/05-flutter-architecture.md).

## Status

All **31 customer prototype screens** are implemented. **Login is real** — it authenticates
against the same ABP/OpenIddict server the Angular app uses. Everything else still renders from
in-memory fixture data (`lib/core/demo/demo_data.dart`, a direct port of
`prototype/assets/js/demo-data.js`) — see [Next steps](#next-steps).

## Running

```bash
cd mobile
flutter pub get
flutter run
flutter analyze
flutter test
```

The app talks to `https://localhost:44330` by default, so **the API host must be running**
(`dotnet run --project src/Eksabli.HttpApi.Host`) before you can log in. On the Android emulator
the host is reached at `10.0.2.2`, which `AppConfig` selects automatically. Point it elsewhere
with:

```bash
flutter run --dart-define=EKSABLI_API_URL=https://your-host
```

Requires Flutter **3.47+** (Dart 3.13+), which is what this targets
(`environment: sdk: ^3.9.0`).

## Architecture

Feature-first, mirroring the backend's module boundaries. Each feature owns its screens; shared
UI and data live one level up.

```
lib/
├── main.dart                     # ProviderScope + EksabliApp
├── app/
│   ├── app.dart                  # MaterialApp.router, theme mode, en/ar locales
│   ├── theme/                    # design tokens ported from the prototype
│   │   ├── app_colors.dart       # Tailwind palette + AppPalette theme extension
│   │   ├── app_tokens.dart       # radii, shadows, type scale, brand gradients
│   │   └── app_theme.dart        # light/dark ThemeData
│   └── router/
│       ├── app_router.dart       # routes, Routes constants, auth guard
│       └── app_shell.dart        # 5-tab bottom nav (StatefulShellRoute)
├── core/
│   ├── config/app_config.dart    # endpoints + OpenIddict client (mirrors Angular env)
│   ├── auth/                     # token store, password/refresh grants, error mapping
│   ├── network/api_client.dart   # Dio + bearer/refresh interceptors
│   └── demo/demo_data.dart       # fixture data — the remaining API boundary
├── shared/
│   ├── models/models.dart        # Business, Membership, Reward, Coupon, …
│   ├── providers/app_providers.dart  # Riverpod providers (state *and* DI)
│   └── widgets/                  # the design-system kit (see below)
└── features/<feature>/*_screen.dart
```

### State management & DI

Riverpod, per the architecture doc — its provider graph doubles as the DI container, so no
second framework (`get_it`/`injectable`) is layered on top. Screens never touch `DemoData`
directly; they read providers, which is what makes the swap to real repositories a change in
`app_providers.dart` rather than in 31 screens.

### Design system

`shared/widgets/` is a 1:1 port of the component primitives in
`prototype/assets/css/design-system.css`, so the two stay visually in sync:

| Prototype CSS | Flutter widget |
|---|---|
| `.card`, `.card-hover` | `AppCard` |
| card + hairline rows | `AppCardList` / `AppListRow` |
| `.btn-*`, `.btn-icon` | `AppButton` / `AppIconButton` |
| `.badge-*` | `AppBadge` + `AppTone` |
| `.alert-*` | `AppAlert` |
| `.input`, `.field-label`, `.field-error` | `AppField` |
| `.otp-box` | `OtpInput` |
| `.tabs-pill` | `PillTabs` |
| `.tabs-list` / `.tab-trigger` | `UnderlineTabs` |
| scrolling chip row | `FilterChipsRow` |
| `.avatar-*` | `AppAvatar` |
| business gradient tile | `BusinessLogo` |
| `.empty-state` | `EmptyState` |
| `.skeleton` | `Skeleton` |
| `.progress-track` / `.progress-fill` | `ProgressTrack` |
| `.bottom-nav` | `AppShell` |
| top bar (`CustomerShell.topBarHtml`) | `AppTopBar` / `AppScaffold` |
| `Eksabli.showToast` | `showAppToast` |

Colours come from `AppPalette`, a `ThemeExtension` resolved per brightness, so widgets read
`AppPalette.of(context).textMuted` instead of branching on `Theme.of(context).brightness`
at every call site.

**Fonts:** the prototype uses Inter. No font is bundled yet, so the app falls back to the
platform UI font. Dropping `Inter*.ttf` into `assets/fonts/` and declaring it in `pubspec.yaml`
is the only change needed to match exactly.

## Authentication

Login performs an **OpenIddict password grant** against the same auth server and the same
registered client the Angular app uses:

| | Angular | Flutter |
|---|---|---|
| Issuer | `https://localhost:44330/` | same |
| Client | `Eksabli_App` | same |
| Scope | `offline_access Eksabli` | same |
| Grant | `authorization_code` + PKCE (browser redirect) | `password` (native form) |

The client is seeded as a **public** client allowing `password` and `refresh_token` — see
`src/Eksabli.Domain/OpenIddict/OpenIddictDataSeedContributor.cs`. Angular uses the redirect flow
because it runs in a browser; a native app with its own login screen uses the password grant
against the same client instead.

What that gives you:

- Real credential validation — a wrong password is rejected **by the server**
- Tokens in the platform keystore (`flutter_secure_storage`), never in shared preferences
- Automatic refresh: proactively when the access token is stale, and once on a 401 before
  giving up
- Session restore on launch — a returning user goes straight to Home
- A **route guard** (`routerProvider`): unauthenticated users cannot reach any screen except
  splash, onboarding, login, register, OTP and forgot-password
- Server error messages shown verbatim. `invalid_grant` is mapped to a credential error rather
  than a generic failure, which matters because OpenIddict reports a bad password as HTTP 400,
  not 401 (`lib/core/auth/auth_exception.dart`)

Profile data comes from ABP's `/api/account/my-profile`. Eksabli's own `CustomerProfileAppService`
is `[RemoteService(IsEnabled = false)]`, so **date of birth and gender are not reachable over
HTTP** — `Customer.dateOfBirth`, `.gender` and `.memberSince` are nullable and the UI hides those
rows rather than inventing values.

### Still simulated

**OTP** (`auth/otp_verify_screen.dart`) — the code is checked locally against `123456` and mints
no tokens, so API calls after an OTP sign-in will 401. The client is already registered for an
`otp` grant and the backend has an `OtpAppService`, so wiring it is a change in
`SessionNotifier`, not in the screen.

**Register** (`auth/register_screen.dart`) — validates fully but creates no account; it flows into
the simulated OTP step.

### Dev certificate

The solution's `openiddict.pfx` is self-signed, so `api_client.dart` accepts it — but only in
**debug builds** and only for `localhost` / `127.0.0.1` / `10.0.2.2`. Release builds always
enforce normal certificate validation.

## Screen map

Every prototype page has a route and a screen. `:id` params replace the prototype's `?id=` query
strings.

| Prototype page | Route | Screen |
|---|---|---|
| `splash.html` | `/` | `onboarding/splash_screen.dart` |
| `onboarding.html` | `/onboarding` | `onboarding/onboarding_screen.dart` |
| `login.html` | `/login` | `auth/login_screen.dart` |
| `register.html` | `/register` | `auth/register_screen.dart` |
| `otp-verify.html` | `/otp-verify` | `auth/otp_verify_screen.dart` |
| `forgot-password.html` | `/forgot-password` | `auth/forgot_password_screen.dart` |
| `home.html` | `/home` | `home/home_screen.dart` |
| `search.html` | `/search` | `discovery/search_screen.dart` |
| `nearby-stores.html` | `/nearby` | `discovery/nearby_stores_screen.dart` |
| `store-details.html` | `/store/:id` | `store/store_details_screen.dart` |
| `join-store.html` | `/join/:id` | `membership/join_store_screen.dart` |
| `my-memberships.html` | `/memberships` | `membership/my_memberships_screen.dart` |
| `wallet.html` | `/wallet` | `wallet/wallet_screen.dart` |
| `my-points.html` | `/points/:id` | `wallet/my_points_screen.dart` |
| `transaction-history.html` | `/points/:id/history` | `wallet/transaction_history_screen.dart` |
| `qr-code.html` | `/qr-code` | `wallet/qr_code_screen.dart` |
| `qr-scanner.html` | `/qr-scanner` | `wallet/qr_scanner_screen.dart` |
| `rewards.html` | `/points/:id/rewards` | `rewards/rewards_screen.dart` |
| `reward-details.html` | `/reward/:id` | `rewards/reward_details_screen.dart` |
| `redeem-reward.html` | `/reward/:id/redeem` | `rewards/redeem_reward_screen.dart` |
| `coupons.html` | `/coupons` | `rewards/coupons_screen.dart` |
| `campaigns.html` | `/campaigns` | `campaigns/campaigns_screen.dart` |
| `birthday-rewards.html` | `/birthday-rewards` | `campaigns/birthday_rewards_screen.dart` |
| `notifications.html` | `/notifications` | `notifications/notifications_screen.dart` |
| `favorites.html` | `/favorites` | `favorites/favorites_screen.dart` |
| `referral.html` | `/referral` | `referral/referral_screen.dart` |
| `profile.html` | `/profile` | `profile/profile_screen.dart` |
| `edit-profile.html` | `/edit-profile` | `profile/edit_profile_screen.dart` |
| `settings.html` | `/settings` | `profile/settings_screen.dart` |
| `help.html` | `/help` | `profile/help_screen.dart` |
| `error.html` | `/error` | `profile/error_screen.dart` |

`ErrorScreen` also serves as go_router's `errorBuilder`, so an unknown deep link lands on the
real 404 state rather than Flutter's default error screen.

## Deliberate stubs

These are placeholders on purpose, each isolated to one widget so the real implementation is a
drop-in:

| Stub | Where | Replace with |
|---|---|---|
| QR rendering | `shared/widgets/qr_placeholder.dart` | `qr_flutter` (API is already `seed` + `size`) |
| Camera scanning | `wallet/qr_scanner_screen.dart` → `_ScannerFrame` | `mobile_scanner` |
| Map view | `discovery/nearby_stores_screen.dart` → `_MapPlaceholder` | real map + PostGIS nearby-search endpoint |
| OTP verification | `auth/otp_verify_screen.dart` | the `otp` grant + `OtpAppService` |
| Registration | `auth/register_screen.dart` | the customer sign-up endpoint |
| Staff redemption confirm | `rewards/redeem_reward_screen.dart` | server-side redemption endpoint |

## Next steps

1. **API layer** — `core/network/api_client.dart` (Dio + auth/refresh interceptors) already
   exists and is used by auth. Add per-feature `data/` repositories on top of it, then point the
   remaining providers in `app_providers.dart` at them instead of `DemoData`. Nothing in
   `features/` should need to change.
2. **Localization** — `ar` and `en` are already declared on `MaterialApp` and the Settings
   screen switches locale, but strings are still inline. Extract to ARB files
   (`flutter_localizations` + `intl` are already dependencies) and budget explicit RTL QA for
   Wallet and Rewards, where numeric/currency layouts hide the most RTL bugs.
3. **Offline cache** — read-through cache (Drift/Isar) for balances, store profiles, history and
   reward catalogues. Points mutations stay server-authoritative and must never be queued
   offline.
4. **Push** — see [`docs/notification-hub/flutter-fcm-integration.md`](../docs/notification-hub/flutter-fcm-integration.md);
   deep links should route straight to the relevant screen, which the `Routes` constants already
   support.
