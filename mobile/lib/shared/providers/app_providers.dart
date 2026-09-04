import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/auth/auth_exception.dart';
import '../../core/auth/auth_repository.dart';
import '../../core/auth/token_store.dart';
import '../../core/demo/demo_data.dart';
import '../../core/network/api_client.dart';
import '../models/models.dart';

/// Riverpod doubles as the DI container for this app — see
/// `docs/eksabli-loyalty-platform/05-flutter-architecture.md#dependency-injection`.
/// Every screen reads its data through a provider here, so replacing [DemoData]
/// with real repositories later means changing these providers only.

// ---------------------------------------------------------------------------
// Auth infrastructure
// ---------------------------------------------------------------------------

final tokenStoreProvider = Provider<TokenStore>((ref) => TokenStore());

final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => AuthRepository(
    tokenStore: ref.watch(tokenStoreProvider),
    tokenClient: buildTokenClient(),
  ),
);

/// The authenticated client for ordinary API calls. Kept separate from the
/// repository's token client so a failed refresh cannot recurse into itself.
final apiClientProvider = Provider<Dio>((ref) {
  return buildApiClient(
    tokenStore: ref.watch(tokenStoreProvider),
    refreshTokens: () => ref.read(authRepositoryProvider).refresh(),
    onSessionLost: () =>
        ref.read(sessionProvider.notifier).handleSessionLost(),
  );
});

// ---------------------------------------------------------------------------
// Session
// ---------------------------------------------------------------------------

/// The signed-in user, or null when logged out.
///
/// Authenticates against the same OpenIddict server and the same `Eksabli_App`
/// client the Angular app uses, via the password grant (Angular uses the
/// authorization-code redirect because it runs in a browser).
///
/// `build()` is async, so the app starts in [AsyncValue.loading] while a stored
/// session is restored — the router holds on the splash screen until it
/// resolves rather than flashing the login page at a returning user.
class SessionNotifier extends AsyncNotifier<Customer?> {
  @override
  Future<Customer?> build() => _restore();

  Future<Customer?> _restore() async {
    final repository = ref.read(authRepositoryProvider);
    final tokens = await repository.tokenStore.read();
    if (tokens == null) return null;

    if (tokens.isExpired && !tokens.canRefresh) {
      await repository.signOut();
      return null;
    }

    try {
      // The interceptor refreshes on the way out if the access token is stale,
      // so this doubles as the "is the stored session still valid" check.
      return await repository.fetchProfile(ref.read(apiClientProvider));
    } on AuthException {
      await repository.signOut();
      return null;
    }
  }

  /// Throws [AuthException] so the login screen can show the server's own
  /// message. State only becomes authenticated once the profile also loads.
  Future<void> signIn({
    required String username,
    required String password,
  }) async {
    final repository = ref.read(authRepositoryProvider);
    await repository.signIn(username: username, password: password);

    try {
      final customer = await repository.fetchProfile(
        ref.read(apiClientProvider),
      );
      state = AsyncData(customer);
    } on AuthException {
      // Tokens are valid but the profile call failed — don't strand the user
      // in a half-signed-in state.
      await repository.signOut();
      rethrow;
    }
  }

  /// OTP sign-in is still simulated: it mints no tokens, so API calls made
  /// afterwards will 401. Everything except the login form currently renders
  /// from [DemoData], so this is enough to walk the flow — replace it with the
  /// `otp` grant (already registered on the client) when wiring OtpAppService.
  void signInSimulated() => state = AsyncData(DemoData.currentCustomer);

  Future<void> signOut() async {
    await ref.read(authRepositoryProvider).signOut();
    state = const AsyncData(null);
  }

  /// Called by the API client when a refresh fails mid-session.
  void handleSessionLost() => state = const AsyncData(null);

  void updateProfile(Customer customer) => state = AsyncData(customer);
}

final sessionProvider = AsyncNotifierProvider<SessionNotifier, Customer?>(
  SessionNotifier.new,
);

/// The signed-in customer, or [Customer.empty] while the session resolves.
///
/// The router's guard means screens only build once this is a real user; the
/// blank fallback exists so widgets never null-check, and deliberately shows
/// nothing rather than fixture data.
final currentCustomerProvider = Provider<Customer>(
  (ref) => ref.watch(sessionProvider).valueOrNull ?? Customer.empty,
);

final isAuthenticatedProvider = Provider<bool>(
  (ref) => ref.watch(sessionProvider).valueOrNull != null,
);

// ---------------------------------------------------------------------------
// Businesses / memberships
// ---------------------------------------------------------------------------

class BusinessesNotifier extends Notifier<List<Business>> {
  @override
  List<Business> build() => List.of(DemoData.businesses);

  void toggleFollow(String businessId) {
    state = [
      for (final b in state)
        if (b.id == businessId) b.copyWith(following: !b.following) else b,
    ];
  }

  void markJoined(String businessId) {
    state = [
      for (final b in state)
        if (b.id == businessId) b.copyWith(member: true) else b,
    ];
  }
}

final businessesProvider =
    NotifierProvider<BusinessesNotifier, List<Business>>(
      BusinessesNotifier.new,
    );

final businessByIdProvider = Provider.family<Business?, String>((ref, id) {
  final all = ref.watch(businessesProvider);
  for (final b in all) {
    if (b.id == id) return b;
  }
  return null;
});

/// Categories present in the catalogue, used by Search and Nearby filters.
final categoriesProvider = Provider<List<String>>((ref) {
  final seen = <String>[];
  for (final b in ref.watch(businessesProvider)) {
    if (!seen.contains(b.category)) seen.add(b.category);
  }
  return seen;
});

class MembershipsNotifier extends Notifier<List<Membership>> {
  @override
  List<Membership> build() => List.of(DemoData.memberships);

  /// Joining is a server-authoritative mutation; this only reflects the
  /// optimistic local result of a successful join call.
  void join(Business business) {
    if (state.any((m) => m.businessId == business.id)) return;
    state = [
      ...state,
      Membership(
        businessId: business.id,
        membershipId: 'mem_${state.length + 1}',
        joinedAt: DateTime.now(),
        status: 'Active',
        balance: 0,
        lifetimeEarned: 0,
        lifetimeRedeemed: 0,
        tier: 'Bronze',
        tierProgressPct: 0,
        nextTier: 'Silver',
        pointsToNextTier: 500,
      ),
    ];
  }
}

final membershipsProvider =
    NotifierProvider<MembershipsNotifier, List<Membership>>(
      MembershipsNotifier.new,
    );

final membershipForBusinessProvider = Provider.family<Membership?, String>((
  ref,
  businessId,
) {
  for (final m in ref.watch(membershipsProvider)) {
    if (m.businessId == businessId) return m;
  }
  return null;
});

final totalPointsProvider = Provider<int>((ref) {
  var total = 0;
  for (final m in ref.watch(membershipsProvider)) {
    total += m.balance;
  }
  return total;
});

// ---------------------------------------------------------------------------
// Points / rewards / coupons / campaigns
// ---------------------------------------------------------------------------

final transactionsProvider = Provider<List<PointTransaction>>(
  (ref) => DemoData.transactions,
);

final transactionsForBusinessProvider =
    Provider.family<List<PointTransaction>, String>((ref, businessId) {
      final list = ref
          .watch(transactionsProvider)
          .where((t) => t.businessId == businessId)
          .toList();
      list.sort((a, b) => b.date.compareTo(a.date));
      return list;
    });

final rewardsProvider = Provider<List<Reward>>((ref) => DemoData.rewards);

final rewardsForBusinessProvider = Provider.family<List<Reward>, String>(
  (ref, businessId) => ref
      .watch(rewardsProvider)
      .where((r) => r.businessId == businessId)
      .toList(),
);

final rewardByIdProvider = Provider.family<Reward?, String>((ref, id) {
  for (final r in ref.watch(rewardsProvider)) {
    if (r.id == id) return r;
  }
  return null;
});

class CouponsNotifier extends Notifier<List<Coupon>> {
  @override
  List<Coupon> build() => List.of(DemoData.coupons);

  void issue(Reward reward) {
    state = [
      Coupon(
        id: 'cpn_${state.length + 1}',
        rewardId: reward.id,
        businessId: reward.businessId,
        code: _code(reward),
        status: CouponStatus.redeemed,
        issuedAt: DateTime.now(),
        redeemedAt: DateTime.now(),
      ),
      ...state,
    ];
  }

  /// Mirrors the prototype's `CB-8F3K2Q` shape: a business prefix and six
  /// unambiguous characters (no O/0/I/1). The real codes are minted
  /// server-side when the redemption endpoint issues the coupon.
  String _code(Reward reward) {
    const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    final prefix = reward.businessId
        .replaceAll(RegExp('[^0-9a-zA-Z]'), '')
        .toUpperCase()
        .padRight(2, 'X')
        .substring(0, 2);

    final buffer = StringBuffer();
    var value = DateTime.now().microsecondsSinceEpoch ^ reward.id.hashCode;
    for (var i = 0; i < 6; i++) {
      buffer.write(alphabet[value.abs() % alphabet.length]);
      value = value ~/ alphabet.length + 7;
    }
    return '$prefix-$buffer';
  }
}

final couponsProvider = NotifierProvider<CouponsNotifier, List<Coupon>>(
  CouponsNotifier.new,
);

final campaignsProvider = Provider<List<Campaign>>((ref) => DemoData.campaigns);

final activeCampaignsProvider = Provider<List<Campaign>>(
  (ref) => ref
      .watch(campaignsProvider)
      .where((c) => c.status == CampaignStatus.active)
      .toList(),
);

final campaignsForBusinessProvider = Provider.family<List<Campaign>, String>(
  (ref, businessId) => ref
      .watch(activeCampaignsProvider)
      .where((c) => c.businessId == businessId)
      .toList(),
);

// ---------------------------------------------------------------------------
// Notifications
// ---------------------------------------------------------------------------

class NotificationsNotifier extends Notifier<List<AppNotification>> {
  @override
  List<AppNotification> build() {
    final list = List.of(DemoData.notifications);
    list.sort((a, b) => b.sentAt.compareTo(a.sentAt));
    return list;
  }

  void markRead(String id) {
    state = [
      for (final n in state)
        if (n.id == id) n.copyWith(read: true) else n,
    ];
  }

  void markAllRead() => state = [for (final n in state) n.copyWith(read: true)];
}

final notificationsProvider =
    NotifierProvider<NotificationsNotifier, List<AppNotification>>(
      NotificationsNotifier.new,
    );

final unreadCountProvider = Provider<int>(
  (ref) => ref.watch(notificationsProvider).where((n) => !n.read).length,
);

final referralProvider = Provider<ReferralProgram>((ref) => DemoData.referral);

// ---------------------------------------------------------------------------
// Preferences (Settings screen)
// ---------------------------------------------------------------------------

class AppPreferences {
  const AppPreferences({
    this.themeMode = ThemeMode.system,
    this.locale = const Locale('en'),
    this.pushEnabled = true,
    this.emailEnabled = true,
    this.smsEnabled = false,
  });

  final ThemeMode themeMode;
  final Locale locale;
  final bool pushEnabled;
  final bool emailEnabled;
  final bool smsEnabled;

  AppPreferences copyWith({
    ThemeMode? themeMode,
    Locale? locale,
    bool? pushEnabled,
    bool? emailEnabled,
    bool? smsEnabled,
  }) => AppPreferences(
    themeMode: themeMode ?? this.themeMode,
    locale: locale ?? this.locale,
    pushEnabled: pushEnabled ?? this.pushEnabled,
    emailEnabled: emailEnabled ?? this.emailEnabled,
    smsEnabled: smsEnabled ?? this.smsEnabled,
  );
}

class PreferencesNotifier extends Notifier<AppPreferences> {
  @override
  AppPreferences build() => const AppPreferences();

  void setDarkMode(bool enabled) =>
      state = state.copyWith(themeMode: enabled ? ThemeMode.dark : ThemeMode.light);

  void setLocale(Locale locale) => state = state.copyWith(locale: locale);

  void setPush(bool value) => state = state.copyWith(pushEnabled: value);
  void setEmail(bool value) => state = state.copyWith(emailEnabled: value);
  void setSms(bool value) => state = state.copyWith(smsEnabled: value);
}

final preferencesProvider =
    NotifierProvider<PreferencesNotifier, AppPreferences>(
      PreferencesNotifier.new,
    );
