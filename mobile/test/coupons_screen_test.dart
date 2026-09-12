import 'package:eksabli_mobile/app/router/app_router.dart';
import 'package:eksabli_mobile/app/theme/app_theme.dart';
import 'package:eksabli_mobile/core/api/eksabli_api.dart';
import 'package:eksabli_mobile/shared/models/models.dart';
import 'package:eksabli_mobile/shared/providers/app_providers.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:eksabli_mobile/shared/widgets/app_tabs.dart';

import 'fakes.dart';

/// The filter tabs were a hardcoded label list beside a separate list of statuses, and adding a
/// status to one without the other shifted every tab's meaning by a position — "Expired" quietly
/// started showing redeemed coupons. These assert what each tab actually contains.
void main() {
  late _CouponFake api;

  // "Active", "Used" and "Expired" are each BOTH a tab label and a status badge, so a bare
  // find.text is ambiguous — scope to the tab row.
  Finder tab(String label) => find.descendant(
    of: find.byType(UnderlineTabs),
    matching: find.text(label),
  );

  Future<void> pumpCoupons(WidgetTester tester) async {
    api = _CouponFake();
    final router = GoRouter(initialLocation: Routes.coupons, routes: appRoutes);
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

  testWidgets('Active covers both pending and legacy issued coupons', (tester) async {
    await pumpCoupons(tester);
    await tester.tap(tab('Active'));
    await tester.pumpAndSettle();

    // Both are "still yours to use" from the customer's side, whatever the server calls them.
    expect(find.textContaining('AAAA1111'), findsOneWidget); // pending
    expect(find.textContaining('BBBB2222'), findsOneWidget); // issued
    expect(find.textContaining('CCCC3333'), findsNothing);   // redeemed
  });

  testWidgets('Used shows redeemed coupons, not expired ones', (tester) async {
    await pumpCoupons(tester);
    await tester.tap(tab('Used'));
    await tester.pumpAndSettle();

    expect(find.textContaining('CCCC3333'), findsOneWidget);
    expect(find.textContaining('EEEE5555'), findsNothing);
  });

  testWidgets('Expired covers declined coupons too, so none are unreachable', (tester) async {
    await pumpCoupons(tester);
    await tester.tap(tab('Expired'));
    await tester.pumpAndSettle();

    expect(find.textContaining('EEEE5555'), findsOneWidget); // expired
    expect(find.textContaining('DDDD4444'), findsOneWidget); // cancelled
    expect(find.textContaining('CCCC3333'), findsNothing);   // redeemed
  });

  testWidgets('shows why staff declined a redemption', (tester) async {
    await pumpCoupons(tester);

    // "Why did that fail?" is the customer's first question, and an unexplained Cancelled is worse
    // than no status at all. Staff type this at the counter.
    expect(find.text('Out of oat milk'), findsOneWidget);
  });

  testWidgets('dates every coupon so identical rewards are tellable apart', (tester) async {
    await pumpCoupons(tester);
    expect(find.textContaining('Issued Sep 5, 2026'), findsWidgets);
  });
}

/// Coupons in every status, which [FakeApi] does not provide.
class _CouponFake extends FakeApi {
  @override
  Future<List<Coupon>> myCoupons() async => [
    _c('AAAA1111', CouponStatus.pending),
    _c('BBBB2222', CouponStatus.issued),
    _c('CCCC3333', CouponStatus.redeemed),
    _c('DDDD4444', CouponStatus.cancelled, reason: 'Out of oat milk'),
    _c('EEEE5555', CouponStatus.expired),
  ];

  static Coupon _c(String code, CouponStatus status, {String? reason}) =>
      Coupon.fromJson({
        'id': 'c-$code',
        'rewardId': 'rew-1',
        'tenantId': 'tenant-1',
        'rewardNameEn': 'Free Large Latte',
        'code': code,
        'status': status.index,
        'pointsCost': 500,
        'issuedAt': '2026-09-05T10:00:00',
        'rejectionReason': reason,
      });
}
