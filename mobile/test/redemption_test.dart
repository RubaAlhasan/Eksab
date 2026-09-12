import 'package:eksabli_mobile/app/router/app_router.dart';
import 'package:eksabli_mobile/app/theme/app_theme.dart';
import 'package:eksabli_mobile/core/api/eksabli_api.dart';
import 'package:eksabli_mobile/shared/models/models.dart';
import 'package:eksabli_mobile/shared/providers/app_providers.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:qr_flutter/qr_flutter.dart';

import 'fakes.dart';

/// The customer half of the two-phase redemption.
///
/// These exist because the previous version of this screen passed every test it had while being
/// broken in the two ways that mattered: it auto-confirmed on a six-second timer, and it drew a
/// locally-generated PIN that corresponded to nothing on the server. Both are asserted against here
/// directly, so neither can come back quietly.
void main() {
  const tenantId = 'tenant-1';
  const rewardId = 'rew-1';

  late FakeApi api;

  Future<void> pumpRedeem(WidgetTester tester) async {
    api = FakeApi();
    final router = GoRouter(
      initialLocation: Routes.redeem(tenantId, rewardId),
      routes: appRoutes,
    );
    addTearDown(router.dispose);

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          sessionProvider.overrideWith(SignedInSession.new),
          apiProvider.overrideWith((ref) => api as EksabliApi),
        ],
        child: MaterialApp.router(routerConfig: router, theme: AppTheme.light()),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('opens on confirmation and spends nothing on its own', (tester) async {
    await pumpRedeem(tester);

    expect(find.text('Get Redemption Code'), findsOneWidget);
    expect(find.textContaining('held now, not spent'), findsOneWidget);

    // The heart of it: let real time pass without touching anything. The old screen confirmed after
    // six seconds, so a customer who opened this and walked away had already spent their points.
    await tester.pump(const Duration(seconds: 30));
    await tester.pumpAndSettle();

    expect(api.pendingCoupons, isEmpty, reason: 'no redemption may be opened without a tap');
    expect(find.text('Get Redemption Code'), findsOneWidget);
  });

  testWidgets('shows the server-issued code, as a QR and as text', (tester) async {
    await pumpRedeem(tester);

    await tester.tap(find.text('Get Redemption Code'));
    await tester.pump();                       // start the request
    await tester.pump(const Duration(milliseconds: 100));

    final issued = api.pendingCoupons.values.single;

    // The QR payload must be the coupon code itself. Staff scan this straight into
    // POST /api/app/pos/lookup-redemption, so anything else here is an unscannable code.
    // qr_flutter keeps `data` private, so the screen keys the widget by the value it encodes.
    expect(issued.code, '3B02543F');
    expect(find.byKey(const ValueKey('redemption-qr-3B02543F')), findsOneWidget);
    expect(find.byType(QrImageView), findsOneWidget);

    // ...and the same code is readable underneath, for a counter with no working camera.
    expect(find.text('3B02 543F'), findsOneWidget);

    expect(find.textContaining('Waiting for staff'), findsOneWidget);
    expect(find.textContaining('are held until staff decide'), findsOneWidget);
  });

  testWidgets('copies the raw code to the clipboard', (tester) async {
    // Intercept the platform clipboard channel — there is no real one under test.
    String? copied;
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
      SystemChannels.platform,
      (call) async {
        if (call.method == 'Clipboard.setData') {
          copied = (call.arguments as Map)['text'] as String?;
        }
        return null;
      },
    );
    addTearDown(() => tester.binding.defaultBinaryMessenger
        .setMockMethodCallHandler(SystemChannels.platform, null));

    await pumpRedeem(tester);
    await tester.tap(find.text('Get Redemption Code'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));

    await tester.tap(find.byTooltip('Copy code'));
    await tester.pump();

    // The RAW eight characters, not the spaced form on screen — this is pasted into the Business
    // Portal's code field, where `3B02 543F` would look like a typo even though the server accepts it.
    expect(copied, '3B02543F');
    expect(find.text('Code copied'), findsOneWidget);
  });

  testWidgets('reports a server rejection instead of claiming success', (tester) async {
    await pumpRedeem(tester);
    api.failRedeemWith = 'This reward is out of stock.';

    await tester.tap(find.text('Get Redemption Code'));
    await tester.pumpAndSettle();

    // The old screen showed "Redeemed!" unconditionally, so every one of these rejections looked
    // like a success with a missing coupon.
    expect(find.text('This reward is out of stock.'), findsOneWidget);
    expect(find.text('Redeemed!'), findsNothing);
    expect(find.text('Get Redemption Code'), findsOneWidget);
  });

  testWidgets('follows the staff decision through to approval', (tester) async {
    await pumpRedeem(tester);

    await tester.tap(find.text('Get Redemption Code'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));

    // Stand in for a staff member approving it in the Business Portal.
    api.settlePending(CouponStatus.redeemed);
    await tester.pump(const Duration(seconds: 4));   // let the poll tick
    await tester.pump(const Duration(milliseconds: 100));

    expect(find.text('Redeemed!'), findsOneWidget);
    expect(find.text('View My Coupons'), findsOneWidget);
  });

  testWidgets('explains a decline and says the points are back', (tester) async {
    await pumpRedeem(tester);

    await tester.tap(find.text('Get Redemption Code'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));

    api.settlePending(CouponStatus.cancelled, reason: 'Out of oat milk');
    await tester.pump(const Duration(seconds: 4));
    await tester.pump(const Duration(milliseconds: 100));

    expect(find.text('Not approved'), findsOneWidget);
    expect(find.textContaining('Out of oat milk'), findsOneWidget);
    expect(find.textContaining('back in your balance'), findsOneWidget);
  });
}
