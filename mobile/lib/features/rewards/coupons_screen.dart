import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_avatar.dart';
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

  static AppTone _tone(CouponStatus status) => switch (status) {
    CouponStatus.issued => AppTone.success,
    CouponStatus.redeemed => AppTone.neutral,
    CouponStatus.expired => AppTone.danger,
    CouponStatus.cancelled => AppTone.danger,
  };

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final coupons = ref.watch(couponsProvider);

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
            child: AsyncSection<List<Coupon>>(
              value: coupons,
              onRetry: () => ref.invalidate(couponsProvider),
              data: (all) {
                final filter = _filters[_tab];
                final list = filter == null
                    ? all
                    : all.where((c) => c.status == filter).toList();

                if (list.isEmpty) {
                  return const EmptyState(
                    icon: Icons.confirmation_number_outlined,
                    title: 'Nothing here yet',
                    message: 'Redeem a reward to see your coupons appear here.',
                  );
                }

                return ListView.separated(
                  padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                  itemCount: list.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, i) {
                    final coupon = list[i];
                    final business = ref
                        .watch(businessByIdProvider(coupon.businessId))
                        .valueOrNull;
                    final spent = coupon.status != CouponStatus.issued;

                    return Opacity(
                      opacity: spent ? 0.55 : 1,
                      child: AppCard(
                        child: Row(
                          children: [
                            // Icon rather than 🎟️ — CanvasKit renders that
                            // emoji as tofu on web.
                            IconTile(
                              icon: Icons.confirmation_number_outlined,
                              tone: _tone(coupon.status) == AppTone.success
                                  ? AppColors.success600
                                  : AppColors.slate500,
                            ),
                            const SizedBox(width: 16),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  Text(
                                    coupon.rewardName ?? 'Reward',
                                    overflow: TextOverflow.ellipsis,
                                    style: AppText.bodyBold.copyWith(
                                      color: palette.textPrimary,
                                    ),
                                  ),
                                  const SizedBox(height: 2),
                                  Text(
                                    business == null
                                        ? coupon.code
                                        : '${business.name} · ${coupon.code}',
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
                            AppBadge(
                              coupon.status.label,
                              tone: _tone(coupon.status),
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
