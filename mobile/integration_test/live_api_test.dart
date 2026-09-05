import 'package:eksabli_mobile/app/router/app_router.dart';
import 'package:eksabli_mobile/app/theme/app_theme.dart';
import 'package:eksabli_mobile/core/auth/auth_tokens.dart';
import 'package:eksabli_mobile/core/auth/token_store.dart';
import 'package:eksabli_mobile/shared/providers/app_providers.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';

/// End-to-end verification against a **live** API host.
///
/// Unlike `test/`, nothing here is faked except the token store: the widget
/// tree is real, and every provider makes a real HTTP call to
/// `https://localhost:44330`. That is the point — it exercises the actual
/// response shapes rather than a hand-written fake that can drift from them.
///
/// Run with a token minted by the OTP flow (the grant needs an SMS code, which
/// is not reachable from inside a test):
///
/// ```
/// flutter test integration_test/live_api_test.dart \
///   --dart-define=ACCESS_TOKEN=<token> --dart-define=TENANT_ID=<guid>
/// ```
///
/// Skips itself when no token is supplied, so `flutter test` over the whole
/// repo stays green without a running server.
const _accessToken = String.fromEnvironment('ACCESS_TOKEN');
const _tenantId = String.fromEnvironment('TENANT_ID');

/// Keeps the injected token in memory. The real store uses
/// `flutter_secure_storage`, whose platform channel does not exist on the test
/// host — and a test should not be writing to the developer's keychain anyway.
class _MemoryTokenStore extends TokenStore {
  _MemoryTokenStore(this._tokens);

  AuthTokens? _tokens;

  @override
  Future<AuthTokens?> read() async => _tokens;

  @override
  Future<void> write(AuthTokens tokens) async => _tokens = tokens;

  @override
  Future<void> clear() async => _tokens = null;
}

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  // testWidgets takes a bool; the reason is in the file docs above.
  final skip = _accessToken.isEmpty;

  Future<ProviderContainer> pumpApp(WidgetTester tester) async {
    final container = ProviderContainer(
      overrides: [
        tokenStoreProvider.overrideWithValue(
          _MemoryTokenStore(
            AuthTokens(
              accessToken: _accessToken,
              refreshToken: null,
              expiresAt: DateTime.now().add(const Duration(hours: 1)),
            ),
          ),
        ),
      ],
    );
    addTearDown(container.dispose);

    await tester.pumpWidget(
      UncontrolledProviderScope(
        container: container,
        child: MaterialApp.router(
          routerConfig: container.read(routerProvider),
          theme: AppTheme.light(),
        ),
      ),
    );

    // Real network round-trips; pumpAndSettle alone can return before they land.
    for (var i = 0; i < 40; i++) {
      await tester.pump(const Duration(milliseconds: 250));
      if (container.read(sessionProvider).valueOrNull != null) break;
    }
    await tester.pumpAndSettle(const Duration(seconds: 2));
    return container;
  }

  Future<void> settle(WidgetTester tester, {int rounds = 20}) async {
    for (var i = 0; i < rounds; i++) {
      await tester.pump(const Duration(milliseconds: 250));
    }
    await tester.pumpAndSettle(const Duration(seconds: 2));
  }

  testWidgets('session restores from the live profile endpoint', (
    tester,
  ) async {
    final container = await pumpApp(tester);

    final customer = container.read(sessionProvider).valueOrNull;
    expect(customer, isNotNull, reason: 'profile fetch failed');
    expect(customer!.firstName, isNotEmpty);
    // Guard against silently rendering the blank placeholder identity.
    expect(customer.id, isNotEmpty);
  }, skip: skip);

  testWidgets('wallet shows the live balance and business name', (
    tester,
  ) async {
    final container = await pumpApp(tester);

    container.read(routerProvider).go(Routes.wallet);
    await settle(tester);

    final entries = await container.read(walletEntriesProvider.future);
    expect(entries, isNotEmpty, reason: 'no memberships returned');

    final entry = entries.first;
    // The whole point of the directory endpoint: a name, not a bare Guid.
    expect(entry.business.name, isNotEmpty);
    expect(entry.business.name, isNot(contains('-')));
    expect(entry.membership.balance, greaterThan(0));

    expect(find.text('My Wallet'), findsOneWidget);
    expect(find.text(entry.business.name), findsWidgets);
  }, skip: skip);

  testWidgets('campaigns are live-only and carry a business name', (
    tester,
  ) async {
    final container = await pumpApp(tester);

    final campaigns = await container.read(myCampaignsProvider.future);
    expect(campaigns, isNotEmpty, reason: 'no live campaigns returned');

    for (final c in campaigns) {
      expect(c.businessName, isNotEmpty);
      // Server filters to Active and inside the window — assert the window.
      expect(c.endDate.isAfter(DateTime.now()), isTrue);
      expect(c.startDate.isBefore(DateTime.now()), isTrue);
    }

    container.read(routerProvider).go(Routes.home);
    await settle(tester);
    container.read(routerProvider).push(Routes.campaigns);
    await settle(tester);

    expect(find.text(campaigns.first.name), findsWidgets);
  }, skip: skip);

  testWidgets('reward catalogue and coupons load for the joined business', (
    tester,
  ) async {
    final container = await pumpApp(tester);
    expect(_tenantId, isNotEmpty, reason: 'TENANT_ID not supplied');

    final rewards = await container.read(
      rewardsForBusinessProvider(_tenantId).future,
    );
    expect(rewards, isNotEmpty, reason: 'empty reward catalogue');
    expect(rewards.first.name, isNotEmpty);
    expect(rewards.first.pointsCost, greaterThan(0));

    final coupons = await container.read(couponsProvider.future);
    for (final c in coupons) {
      expect(c.code, isNotEmpty);
    }

    container.read(routerProvider).go(Routes.home);
    await settle(tester);
    container.read(routerProvider).push(Routes.rewards(_tenantId));
    await settle(tester);

    expect(find.text(rewards.first.name), findsWidgets);
  }, skip: skip);

  testWidgets('transactions and referral codes resolve', (tester) async {
    final container = await pumpApp(tester);

    // Both were contract bugs found by hand-testing; assert them so a
    // regression fails here instead of in someone's hands.
    final referral = await container.read(referralProvider.future);
    expect(
      referral.codes,
      isNotEmpty,
      reason: 'referral/my-code needs a tenantId per membership',
    );
    expect(referral.codes.first.code, isNotEmpty);
    expect(referral.codes.first.business.name, isNotEmpty);

    await container.read(transactionsForBusinessProvider(_tenantId).future);
  }, skip: skip);

  testWidgets('every tab renders without throwing', (tester) async {
    final container = await pumpApp(tester);
    final router = container.read(routerProvider);

    for (final route in [
      Routes.home,
      Routes.search,
      Routes.wallet,
      Routes.notifications,
      Routes.profile,
    ]) {
      router.go(route);
      await settle(tester, rounds: 12);
      expect(tester.takeException(), isNull, reason: 'threw on $route');
    }

    for (final route in [
      Routes.nearby,
      Routes.campaigns,
      Routes.coupons,
      Routes.memberships,
      Routes.favorites,
      Routes.referral,
      Routes.birthdayRewards,
      Routes.settings,
      Routes.help,
      Routes.qrCode,
      Routes.editProfile,
      Routes.store(_tenantId),
      Routes.points(_tenantId),
      Routes.history(_tenantId),
      Routes.rewards(_tenantId),
    ]) {
      router.go(route);
      await settle(tester, rounds: 12);
      expect(tester.takeException(), isNull, reason: 'threw on $route');
    }
  }, skip: skip);
}
