import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/eksabli_api.dart';
import '../../core/auth/auth_exception.dart';
import '../../core/auth/auth_repository.dart';
import '../../core/auth/registration.dart';
import '../../core/auth/token_store.dart';
import '../../core/network/api_client.dart';
import '../models/models.dart';

/// Riverpod doubles as the DI container for this app — see
/// `docs/eksabli-loyalty-platform/05-flutter-architecture.md#dependency-injection`.
/// Every screen reads its data through a provider here, and every provider here
/// reads from the API — there is no fixture data left in the running app.

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
    onSessionLost: () => ref.read(sessionProvider.notifier).handleSessionLost(),
  );
});

final apiProvider = Provider<EksabliApi>(
  (ref) => EksabliApi(ref.watch(apiClientProvider)),
);

// ---------------------------------------------------------------------------
// Session
// ---------------------------------------------------------------------------

/// The signed-in user, or null when logged out.
///
/// Sign-in is the OTP flow: `POST /api/app/otp/request` (or `otp/register` for
/// a new account) sends a code, then OpenIddict's custom `otp` grant exchanges
/// it for tokens.
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

  /// Creates the account. The server also sends the verification code, so the
  /// caller goes straight to the OTP screen afterwards.
  Future<void> register(RegisterRequest request) => ref
      .read(authRepositoryProvider)
      .register(request, ref.read(apiClientProvider));

  /// Sends a code to an existing account (the login path).
  Future<void> requestOtp(String phoneNumber) => ref
      .read(authRepositoryProvider)
      .requestOtp(phoneNumber, ref.read(apiClientProvider));

  /// Completes sign-in with the `otp` grant. This is what actually confirms a
  /// freshly registered phone number server-side.
  Future<void> verifyOtp({
    required String phoneNumber,
    required String code,
  }) async {
    final repository = ref.read(authRepositoryProvider);
    await repository.signInWithOtp(phoneNumber: phoneNumber, code: code);

    try {
      state = AsyncData(
        await repository.fetchProfile(ref.read(apiClientProvider)),
      );
    } on AuthException {
      // Tokens are valid but the profile call failed — don't strand the user
      // in a half-signed-in state.
      await repository.signOut();
      rethrow;
    }
  }

  Future<void> signOut() async {
    await ref.read(authRepositoryProvider).signOut();
    state = const AsyncData(null);
  }

  /// Called by the API client when a refresh fails mid-session.
  void handleSessionLost() => state = const AsyncData(null);

  /// Persists a profile edit, then adopts the server's version of the record.
  Future<void> updateProfile({
    required String firstName,
    required String lastName,
    DateTime? dateOfBirth,
    CustomerGender? gender,
  }) async {
    final updated = await ref
        .read(authRepositoryProvider)
        .updateProfile(
          apiClient: ref.read(apiClientProvider),
          firstName: firstName,
          lastName: lastName,
          dateOfBirth: dateOfBirth,
          gender: gender,
        );
    state = AsyncData(updated);
  }
}

final sessionProvider = AsyncNotifierProvider<SessionNotifier, Customer?>(
  SessionNotifier.new,
);

/// The signed-in customer, or [Customer.empty] while the session resolves.
///
/// The router's guard means screens only build once this is a real user; the
/// blank fallback exists so widgets never null-check, and deliberately shows
/// nothing rather than a placeholder identity.
final currentCustomerProvider = Provider<Customer>(
  (ref) => ref.watch(sessionProvider).valueOrNull ?? Customer.empty,
);

final isAuthenticatedProvider = Provider<bool>(
  (ref) => ref.watch(sessionProvider).valueOrNull != null,
);

// ---------------------------------------------------------------------------
// Businesses
// ---------------------------------------------------------------------------

/// Query key for [businessSearchProvider]. A value type so Riverpod caches per
/// distinct query instead of refetching on every rebuild.
@immutable
class BusinessQuery {
  const BusinessQuery({this.text, this.categoryId});

  final String? text;
  final String? categoryId;

  @override
  bool operator ==(Object other) =>
      other is BusinessQuery &&
      other.text == text &&
      other.categoryId == categoryId;

  @override
  int get hashCode => Object.hash(text, categoryId);
}

/// Directory search. An empty query returns the whole approved directory, which
/// is what Home's "Discover" and the Nearby screen use.
final businessSearchProvider =
    FutureProvider.family<List<Business>, BusinessQuery>((ref, q) async {
      final businesses = await ref
          .watch(apiProvider)
          .searchBusinesses(query: q.text, categoryId: q.categoryId);

      // Membership and follow state come from other endpoints; fold them in
      // here so every screen sees a consistent Business.
      final memberIds = (await ref.watch(membershipsProvider.future))
          .map((m) => m.businessId)
          .toSet();
      final followedIds = (await ref.watch(followedIdsProvider.future)).toSet();

      return businesses
          .map(
            (b) => b.copyWith(
              member: memberIds.contains(b.id),
              following: followedIds.contains(b.id),
            ),
          )
          .toList();
    });

final businessByIdProvider = FutureProvider.family<Business, String>((
  ref,
  tenantId,
) async {
  final business = await ref.watch(apiProvider).getBusiness(tenantId);
  final memberIds = (await ref.watch(membershipsProvider.future))
      .map((m) => m.businessId)
      .toSet();
  final followedIds = (await ref.watch(followedIdsProvider.future)).toSet();

  return business.copyWith(
    member: memberIds.contains(business.id),
    following: followedIds.contains(business.id),
  );
});

/// Distinct categories present in the directory, for the filter chips.
final categoriesProvider = FutureProvider<List<String>>((ref) async {
  final businesses = await ref.watch(
    businessSearchProvider(const BusinessQuery()).future,
  );
  final seen = <String>[];
  for (final b in businesses) {
    if (b.category.isNotEmpty && !seen.contains(b.category)) {
      seen.add(b.category);
    }
  }
  return seen;
});

// ---------------------------------------------------------------------------
// Memberships & wallet
// ---------------------------------------------------------------------------

class MembershipsNotifier extends AsyncNotifier<List<Membership>> {
  @override
  Future<List<Membership>> build() => ref.watch(apiProvider).myMemberships();

  /// Joining is server-authoritative; refetch rather than guessing the result.
  Future<void> join(String tenantId) async {
    await ref.read(apiProvider).joinBusiness(tenantId);
    ref.invalidateSelf();
    await future;
  }
}

final membershipsProvider =
    AsyncNotifierProvider<MembershipsNotifier, List<Membership>>(
      MembershipsNotifier.new,
    );

final membershipForBusinessProvider = Provider.family<Membership?, String>((
  ref,
  businessId,
) {
  final memberships = ref.watch(membershipsProvider).valueOrNull ?? const [];
  for (final m in memberships) {
    if (m.businessId == businessId) return m;
  }
  return null;
});

final totalPointsProvider = Provider<int>((ref) {
  final memberships = ref.watch(membershipsProvider).valueOrNull ?? const [];
  return memberships.fold(0, (sum, m) => sum + m.balance);
});

/// A membership paired with its resolved business — what the wallet renders.
class WalletEntry {
  const WalletEntry({required this.business, required this.membership});

  final Business business;
  final Membership membership;
}

final walletEntriesProvider = FutureProvider<List<WalletEntry>>((ref) async {
  final memberships = await ref.watch(membershipsProvider.future);
  if (memberships.isEmpty) return const [];

  final businesses = await ref
      .watch(apiProvider)
      .lookupBusinesses(memberships.map((m) => m.businessId).toList());

  return [
    for (final m in memberships)
      if (businesses[m.businessId] != null)
        WalletEntry(business: businesses[m.businessId]!, membership: m),
  ]..sort((a, b) => b.membership.balance.compareTo(a.membership.balance));
});

final transactionsForBusinessProvider =
    FutureProvider.family<List<PointTransaction>, String>((
      ref,
      businessId,
    ) async {
      final list = await ref.watch(apiProvider).transactions(businessId);
      list.sort((a, b) => b.date.compareTo(a.date));
      return list;
    });

// ---------------------------------------------------------------------------
// Rewards & coupons
// ---------------------------------------------------------------------------

final rewardsForBusinessProvider = FutureProvider.family<List<Reward>, String>(
  (ref, businessId) => ref.watch(apiProvider).rewardCatalog(businessId),
);

/// Rewards are only listed per business, so a reward is addressed by both ids.
@immutable
class RewardKey {
  const RewardKey({required this.businessId, required this.rewardId});

  final String businessId;
  final String rewardId;

  @override
  bool operator ==(Object other) =>
      other is RewardKey &&
      other.businessId == businessId &&
      other.rewardId == rewardId;

  @override
  int get hashCode => Object.hash(businessId, rewardId);
}

final rewardByIdProvider = FutureProvider.family<Reward?, RewardKey>((
  ref,
  key,
) async {
  final rewards = await ref.watch(
    rewardsForBusinessProvider(key.businessId).future,
  );
  for (final r in rewards) {
    if (r.id == key.rewardId) return r;
  }
  return null;
});

class CouponsNotifier extends AsyncNotifier<List<Coupon>> {
  @override
  Future<List<Coupon>> build() => ref.watch(apiProvider).myCoupons();

  /// Redemption is server-side — it issues the coupon and debits the wallet, so
  /// both this list and the balances are refetched.
  Future<Coupon> redeem({
    required String tenantId,
    required String rewardId,
  }) async {
    final coupon = await ref
        .read(apiProvider)
        .redeemReward(tenantId: tenantId, rewardId: rewardId);
    ref.invalidateSelf();
    ref.invalidate(membershipsProvider);
    return coupon;
  }
}

final couponsProvider = AsyncNotifierProvider<CouponsNotifier, List<Coupon>>(
  CouponsNotifier.new,
);

// ---------------------------------------------------------------------------
// Campaigns
// ---------------------------------------------------------------------------

/// Live campaigns for the businesses the customer has joined. Segment targeting
/// is applied server-side, so this never advertises something unclaimable.
final myCampaignsProvider = FutureProvider<List<Campaign>>(
  (ref) => ref.watch(apiProvider).myCampaigns(),
);

final campaignsForBusinessProvider =
    FutureProvider.family<List<Campaign>, String>(
      (ref, tenantId) => ref.watch(apiProvider).campaignsForBusiness(tenantId),
    );

// ---------------------------------------------------------------------------
// Notifications
// ---------------------------------------------------------------------------

class NotificationsNotifier extends AsyncNotifier<List<AppNotification>> {
  @override
  Future<List<AppNotification>> build() async {
    final list = await ref.watch(apiProvider).notifications();
    list.sort((a, b) => b.sentAt.compareTo(a.sentAt));
    return list;
  }

  /// Optimistic: the row greys out immediately, and the unread badge refetches.
  Future<void> markRead(String id) async {
    state = AsyncData([
      for (final n in state.valueOrNull ?? const <AppNotification>[])
        if (n.id == id) n.copyWith(read: true) else n,
    ]);
    await ref.read(apiProvider).markNotificationRead(id);
    ref.invalidate(unreadCountProvider);
  }

  Future<void> markAllRead() async {
    state = AsyncData([
      for (final n in state.valueOrNull ?? const <AppNotification>[])
        n.copyWith(read: true),
    ]);
    await ref.read(apiProvider).markAllNotificationsRead();
    ref.invalidate(unreadCountProvider);
  }
}

final notificationsProvider =
    AsyncNotifierProvider<NotificationsNotifier, List<AppNotification>>(
      NotificationsNotifier.new,
    );

final unreadCountProvider = FutureProvider<int>(
  (ref) => ref.watch(apiProvider).unreadCount(),
);

// ---------------------------------------------------------------------------
// Referrals & follows
// ---------------------------------------------------------------------------

final referralProvider = FutureProvider<ReferralProgram>((ref) async {
  // A code exists per joined business, so the wallet entries drive this.
  final entries = await ref.watch(walletEntriesProvider.future);
  return ref
      .watch(apiProvider)
      .referral(entries.map((e) => e.business).toList());
});

final followedIdsProvider = FutureProvider<List<String>>(
  (ref) => ref.watch(apiProvider).myFollowedTenantIds(),
);

/// Favourites: followed businesses, resolved to displayable records.
final favoriteBusinessesProvider = FutureProvider<List<Business>>((ref) async {
  final ids = await ref.watch(followedIdsProvider.future);
  if (ids.isEmpty) return const [];

  final businesses = await ref.watch(apiProvider).lookupBusinesses(ids);
  final memberIds = (await ref.watch(membershipsProvider.future))
      .map((m) => m.businessId)
      .toSet();

  return [
    for (final id in ids)
      if (businesses[id] != null)
        businesses[id]!.copyWith(following: true, member: memberIds.contains(id)),
  ];
});

/// Follow/unfollow, invalidating everything that renders follow state.
final followActionsProvider = Provider<FollowActions>(
  (ref) => FollowActions(ref),
);

class FollowActions {
  const FollowActions(this._ref);

  final Ref _ref;

  Future<void> toggle(String tenantId, {required bool follow}) async {
    final api = _ref.read(apiProvider);
    if (follow) {
      await api.follow(tenantId);
    } else {
      await api.unfollow(tenantId);
    }
    _ref.invalidate(followedIdsProvider);
    _ref.invalidate(favoriteBusinessesProvider);
  }
}

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

  void setDarkMode(bool enabled) => state = state.copyWith(
    themeMode: enabled ? ThemeMode.dark : ThemeMode.light,
  );

  void setLocale(Locale locale) => state = state.copyWith(locale: locale);

  void setPush(bool value) => state = state.copyWith(pushEnabled: value);
  void setEmail(bool value) => state = state.copyWith(emailEnabled: value);
  void setSms(bool value) => state = state.copyWith(smsEnabled: value);
}

final preferencesProvider =
    NotifierProvider<PreferencesNotifier, AppPreferences>(
      PreferencesNotifier.new,
    );
