import 'package:eksabli_mobile/app/router/app_router.dart';
import 'package:eksabli_mobile/app/theme/app_theme.dart';
import 'package:eksabli_mobile/core/api/eksabli_api.dart';
import 'package:eksabli_mobile/shared/models/models.dart';
import 'package:eksabli_mobile/shared/providers/app_providers.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'fakes.dart';

/// Shell coverage: the routes mount, the tab bar switches branches, and the
/// wallet total is derived from the API's balances rather than hard-coded.
///
/// These mount [appRoutes] in a plain router, deliberately without the auth
/// guard from `routerProvider` — the guard is exercised in `auth_test.dart`.
void main() {
  Future<void> pumpApp(WidgetTester tester, {String at = Routes.home}) async {
    final router = GoRouter(initialLocation: at, routes: appRoutes);
    addTearDown(router.dispose);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionProvider.overrideWith(SignedInSession.new),
          apiProvider.overrideWith((ref) => FakeApi() as EksabliApi),
        ],
        child: MaterialApp.router(
          routerConfig: router,
          theme: AppTheme.light(),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('home renders the customer greeting', (tester) async {
    await pumpApp(tester);

    expect(find.text('Layla Haddad'), findsOneWidget);
    expect(find.text('My Businesses'), findsOneWidget);
    expect(find.text('Scan & Check-in'), findsOneWidget);
  });

  testWidgets('bottom nav switches to the wallet branch', (tester) async {
    await pumpApp(tester);

    await tester.tap(find.text('Wallet'));
    await tester.pumpAndSettle();

    expect(find.text('My Wallet'), findsOneWidget);
  });

  testWidgets('wallet total is the sum of the API balances', (tester) async {
    await pumpApp(tester, at: Routes.wallet);

    final container = ProviderScope.containerOf(
      tester.element(find.text('My Wallet')),
    );
    final memberships = container.read(membershipsProvider).valueOrNull ?? [];
    final expected = memberships.fold<int>(0, (sum, m) => sum + m.balance);

    expect(container.read(totalPointsProvider), expected);
    expect(expected, greaterThan(0));
  });

  testWidgets('marking all notifications read clears the badge', (
    tester,
  ) async {
    await pumpApp(tester, at: Routes.notifications);

    final container = ProviderScope.containerOf(
      tester.element(find.text('Notifications').first),
    );
    final before = container.read(notificationsProvider).valueOrNull ?? [];
    expect(before.where((n) => !n.read), isNotEmpty);

    await tester.tap(find.text('Mark all read'));
    await tester.pump();

    final after = container.read(notificationsProvider).valueOrNull ?? [];
    expect(after.where((n) => !n.read), isEmpty);
  });

  test('business initials are derived from the name', () {
    expect(Business.initialsFor('Cedar & Bean Coffee'), 'CB');
    expect(Business.initialsFor('Pulse'), 'PU');
    expect(Business.initialsFor(''), '?');
  });

  test('a business always gets the same gradient for the same id', () {
    final a = Business.gradientFor('tenant-1');
    final b = Business.gradientFor('tenant-1');
    expect(a, b);
  });
}
