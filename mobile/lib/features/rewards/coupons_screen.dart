import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/app_tabs.dart';

/// Prototype: `customer/coupons.html` — issued coupons grouped by status.
class CouponsScreen extends ConsumerStatefulWidget {
  const CouponsScreen({super.key});

  @override
  ConsumerState<CouponsScreen> createState() => _CouponsScreenState();
}

class _CouponsScreenState extends ConsumerState<CouponsScreen> {
  int _tab = 0;

  static const _filters = <CouponStatus?>[
    null,
    CouponStatus.issued,
    CouponStatus.redeemed,
    CouponStatus.expired,
  ];

  static ({String label, AppTone tone}) _meta(CouponStatus status) =>
      switch (status) {
        CouponStatus.issued => (label: 'Active', tone: AppTone.success),
        CouponStatus.redeemed => (label: 'Used', tone: AppTone.neutral),
        CouponStatus.expired => (label: 'Expired', tone: AppTone.danger),
        CouponStatus.cancelled => (label: 'Cancelled', tone: AppTone.danger),
      };

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final all = ref.watch(couponsProvider);
    final filter = _filters[_tab];
    final coupons = filter == null
        ? all
        : all.where((c) => c.status == filter).toList();

    return AppScaffold(
      title: 'My Coupons',
      onBack: () =>
          context.canPop() ? context.pop() : context.go(Routes.profile),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 20),
            child: UnderlineTabs(
              labels: const ['All', 'Active', 'Used', 'Expired'],
              selectedIndex: _tab,
              onChanged: (i) => setState(() => _tab = i),
            ),
          ),
          Expanded(
            child: coupons.isEmpty
                ? const EmptyState(
                    icon: Icons.confirmation_number_outlined,
                    title: 'Nothing here yet',
                    message:
                        'Redeem a reward to see your coupons appear here.',
                  )
                : ListView.separated(
                    padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                    itemCount: coupons.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 12),
                    itemBuilder: (context, i) {
                      final coupon = coupons[i];
                      final reward = ref.watch(
                        rewardByIdProvider(coupon.rewardId),
                      );
                      final business = ref.watch(
                        businessByIdProvider(coupon.businessId),
                      );
                      final meta = _meta(coupon.status);
                      final spent = coupon.status != CouponStatus.issued;

                      return Opacity(
                        opacity: spent ? 0.55 : 1,
                        child: AppCard(
                          child: Row(
                            children: [
                              Text(
                                reward?.emoji ?? '🎟️',
                                style: const TextStyle(fontSize: 28),
                              ),
                              const SizedBox(width: 16),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Text(
                                      reward?.name ?? 'Reward',
                                      overflow: TextOverflow.ellipsis,
                                      style: AppText.bodyBold.copyWith(
                                        color: palette.textPrimary,
                                      ),
                                    ),
                                    const SizedBox(height: 2),
                                    Text(
                                      '${business?.name ?? ''} · ${coupon.code}',
                                      overflow: TextOverflow.ellipsis,
                                      style: AppText.small.copyWith(
                                        color: palette.textMuted,
                                        fontFeatures: const [
                                          FontFeature.tabularFigures(),
                                        ],
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              const SizedBox(width: 8),
                              AppBadge(meta.label, tone: meta.tone),
                            ],
                          ),
                        ),
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }
}
