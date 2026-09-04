import 'package:eksabli_mobile/app/router/app_router.dart';
import 'package:eksabli_mobile/app/theme/app_theme.dart';
import 'package:eksabli_mobile/core/demo/demo_data.dart';
import 'package:eksabli_mobile/features/home/home_screen.dart';
import 'package:eksabli_mobile/features/wallet/wallet_screen.dart';
import 'package:eksabli_mobile/shared/models/models.dart';
import 'package:eksabli_mobile/shared/providers/app_providers.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

/// A session that is already signed in, so screens can be pumped without
/// standing up the auth server. Subclassing the real notifier keeps the rest of
/// the provider graph untouched.
class _SignedInSession extends SessionNotifier {
  @override
  Future<Customer?> build() async => DemoData.currentCustomer;
}

/// Smoke coverage for the shell: that the routes mount, the tab bar switches
/// branches, and the wallet total is derived from memberships rather than
/// hard-coded.
///
/// These mount [appRoutes] in a plain router, deliberately without the auth
/// guard from `routerProvider` — the guard is exercised separately in
/// `auth_test.dart`.
void main() {
  Future<void> pumpApp(WidgetTester tester, {String at = Routes.home}) async {
    final router = GoRouter(initialLocation: at, routes: appRoutes);
    addTearDown(router.dispose);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [sessionProvider.overrideWith(_SignedInSession.new)],
        child: MaterialApp.router(
          routerConfig: router,
          theme: AppTheme.light(),
        ),
      ),
    );
    await tester.pump(const Duration(milliseconds: 600)); // skeleton delay
  }

  testWidgets('home renders the customer greeting and quick actions', (
    tester,
  ) async {
    await pumpApp(tester);

    expect(find.byType(HomeScreen), findsOneWidget);
    expect(find.text('Layla Haddad'), findsOneWidget);
    expect(find.text('My Businesses'), findsOneWidget);
    expect(find.text('Scan & Check-in'), findsOneWidget);
  });

  testWidgets('bottom nav switches to the wallet branch', (tester) async {
    await pumpApp(tester);

    await tester.tap(find.text('Wallet'));
    await tester.pumpAndSettle();

    expect(find.byType(WalletScreen), findsOneWidget);
    expect(find.text('My Wallet'), findsOneWidget);
  });

  testWidgets('wallet total is the sum of membership balances', (tester) async {
    await pumpApp(tester, at: Routes.wallet);

    final container = ProviderScope.containerOf(
      tester.element(find.byType(WalletScreen)),
    );
    final memberships = container.read(membershipsProvider);
    final expected = memberships.fold<int>(0, (sum, m) => sum + m.balance);

    expect(container.read(totalPointsProvider), expected);
    expect(expected, greaterThan(0));
  });

  testWidgets('marking all notifications read clears the unread badge', (
    tester,
  ) async {
    await pumpApp(tester, at: Routes.notifications);

    final container = ProviderScope.containerOf(
      tester.element(find.text('Notifications').first),
    );
    expect(container.read(unreadCountProvider), greaterThan(0));

    await tester.tap(find.text('Mark all read'));
    await tester.pump();

    expect(container.read(unreadCountProvider), 0);
  });

  testWidgets('an unknown route lands on the 404 state', (tester) async {
    final router = GoRouter(
      initialLocation: '/does-not-exist',
      routes: appRoutes,
      errorBuilder: (context, state) =>
          const ErrorScreenForTest(),
    );
    addTearDown(router.dispose);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [sessionProvider.overrideWith(_SignedInSession.new)],
        child: MaterialApp.router(
          routerConfig: router,
          theme: AppTheme.light(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('not found'), findsOneWidget);
  });
}

/// Minimal stand-in so the unknown-route test asserts on routing, not on the
/// error screen's copy.
class ErrorScreenForTest extends StatelessWidget {
  const ErrorScreenForTest({super.key});

  @override
  Widget build(BuildContext context) =>
      const Scaffold(body: Center(child: Text('not found')));
}
