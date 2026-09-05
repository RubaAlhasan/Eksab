# Eksabli Mobile (Flutter)

The customer-facing app for the Eksabli loyalty platform — a Flutter port of every screen in
[`prototype/customer/`](../prototype/customer/), following the architecture agreed in
[`docs/eksabli-loyalty-platform/05-flutter-architecture.md`](../docs/eksabli-loyalty-platform/05-flutter-architecture.md).

## Status

All **31 customer prototype screens** are implemented and read from the live API — there is no
fixture data left in the running app. Auth is the real OTP flow against the same ABP/OpenIddict
server the Angular portal uses.

Verified end to end against a running host: register → OTP sign-in → profile → directory → join →
wallet → campaigns → rewards → redemption → token refresh.

What is still a placeholder is listed under [Deliberate stubs](#deliberate-stubs).

## Running

### 1. Start the API

Run it **from its own directory**. `dotnet run --project …` leaves the working directory as the
content root, so `appsettings.json` is not found and the connection string comes back empty:

```bash
cd src/Eksabli.HttpApi.Host
dotnet run
```

First time only, generate the dev certificate (from the repo root):

```bash
dotnet dev-certs https -v -ep openiddict.pfx -p a8a5c4df-3387-44f7-b368-e21f3f0b2f4e
```

### 2. Run the app

```bash
cd mobile
flutter pub get
flutter run -d edge --web-port 4200 --no-web-resources-cdn
```

Both flags matter on web:

| Flag | Why |
|---|---|
| `--web-port 4200` | The API's `CorsOrigins` allows exactly `http://localhost:4200`. On any other port every request is blocked by the browser. |
| `--no-web-resources-cdn` | Otherwise CanvasKit (~7 MB) is fetched from `gstatic.com` at runtime. If that request stalls or is blocked the app shows a **blank page with nothing in the console** — the engine simply never initialises. The flag bundles CanvasKit locally instead. |

`-d chrome` works the same. Neither flag applies to Android or iOS, which compile the renderer
into the binary.

For a production-shaped bundle:

```bash
flutter build web --no-web-resources-cdn
cd build/web && python -m http.server 4200 --bind 127.0.0.1
```

### 3. Log in

There is no SMS provider yet, so `NullSmsSender` writes the verification code to the `AppSmsLogs`
table (also browsable in Admin Portal → Verification Codes):

```sql
SELECT "Message" FROM "AppSmsLogs" ORDER BY "CreationTime" DESC LIMIT 1;
```

### Pointing at another host

`AppConfig` selects `10.0.2.2` automatically on the Android emulator, since `localhost` there is
the emulator itself. Override for a device or staging:

```bash
flutter run --dart-define=EKSABLI_API_URL=https://your-host
```

### Checks

```bash
flutter analyze
flutter test
```

`integration_test/live_api_test.dart` runs the real widget tree against a live host and skips
itself without a token:

```bash
flutter test integration_test/live_api_test.dart \
  --dart-define=ACCESS_TOKEN=<token> --dart-define=TENANT_ID=<guid>
```

> **Do not run `flutter run` and `flutter test` at the same time.** Both take
> `bin/cache/lockfile`; the second sits at 0% CPU printing *nothing*, which looks like a hang
> rather than a wait.

Requires Flutter **3.47+** (Dart 3.13+), which is what this targets (`environment: sdk: ^3.9.0`).

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
│   ├── api/eksabli_api.dart      # every endpoint the app calls, one class
│   └── static_content.dart       # copy with no server source (FAQs, etc.)
├── shared/
│   ├── models/models.dart        # Business, Membership, Reward, Coupon, …
│   ├── providers/app_providers.dart  # Riverpod providers (state *and* DI)
│   └── widgets/                  # the design-system kit (see below)
└── features/<feature>/*_screen.dart
```

### State management & DI

Riverpod, per the architecture doc — its provider graph doubles as the DI container, so no
second framework (`get_it`/`injectable`) is layered on top. Screens never touch `EksabliApi`
directly; they read providers, so the HTTP surface stays in one file and tests swap it out by
overriding a single provider (see `test/fakes.dart`).

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

### What is real

Registration, OTP delivery, sign-in, token refresh and sign-out all hit the server.
`POST /api/app/otp/register` (or `otp/request`) sends a code, then OpenIddict's custom `otp`
grant exchanges it for tokens. Tokens are held in the platform keystore, refreshed proactively
and once more on a 401.

The password grant is deliberately unused: `RegisterCustomerDto` stores a password that
authenticates nothing today, so OTP is the only login path.

**No SMS provider is configured yet.** `NullSmsSender` writes the code to `AppSmsLogs` instead of
sending it — fine for development, a hard blocker for release. Choosing a provider is the one
thing standing between this app and a live deployment.

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
| Device location | `discovery/nearby_stores_screen.dart` | `geolocator`; the server already sorts by distance when given coordinates, so this is a `BusinessQuery` argument, not a new endpoint |
| Map view | `discovery/nearby_stores_screen.dart` | a real map once a nearby-search endpoint exists |

Referral codes are raw GUIDs. They work, but nobody is going to read one out loud — the server
should mint a short human-shareable code.

## Next steps

1. **SMS provider** — the only hard blocker for release. Swap `NullSmsSender` for a real
   sender; nothing in the app changes, since the client never sees the code either way.
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
