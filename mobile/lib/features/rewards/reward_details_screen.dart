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
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/reward-details.html` — cost vs balance, terms, and a
/// redeem CTA disabled (with the shortfall spelled out) when unaffordable.
class RewardDetailsScreen extends ConsumerWidget {
  const RewardDetailsScreen({
    super.key,
    required this.businessId,
    required this.rewardId,
  });

  final String businessId;
  final String rewardId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final reward = ref.watch(
      rewardByIdProvider(
        RewardKey(businessId: businessId, rewardId: rewardId),
      ),
    );
    final business = ref.watch(businessByIdProvider(businessId));
    final membership = ref.watch(membershipForBusinessProvider(businessId));
    final balance = membership?.balance ?? 0;

    return AppScaffold(
      backgroundColor: palette.surface,
      title: 'Reward Details',
      body: AsyncSection<Reward?>(
        value: reward,
        onRetry: () => ref.invalidate(rewardsForBusinessProvider(businessId)),
        data: (r) {
          if (r == null) {
            return const EmptyState(
              icon: Icons.card_giftcard_rounded,
              title: 'Reward not found',
              message: 'It may have been withdrawn by the business.',
            );
          }

          final affordable = balance >= r.pointsCost;
          final shortfall = r.pointsCost - balance;

          return Column(
            children: [
              Expanded(
                child: ListView(
                  padding: const EdgeInsets.fromLTRB(24, 24, 24, 24),
                  children: [
                    Center(
                      child: IconTile(
                        icon: r.icon,
                        tone: r.tone,
                        size: 88,
                        iconSize: 42,
                      ),
                    ),
                    const SizedBox(height: 24),
                    Text(
                      r.name,
                      textAlign: TextAlign.center,
                      style: AppText.h1.copyWith(color: palette.textPrimary),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      business.valueOrNull?.name ?? '',
                      textAlign: TextAlign.center,
                      style: AppText.body.copyWith(color: palette.textMuted),
                    ),
                    const SizedBox(height: 24),

                    AppCard(
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(
                                'Cost',
                                style: AppText.small.copyWith(
                                  color: palette.textMuted,
                                ),
                              ),
                              Text(
                                '${r.pointsCost} pts',
                                style: AppText.h2.copyWith(
                                  color: palette.primaryOnDarkAware,
                                ),
                              ),
                            ],
                          ),
                          Column(
                            crossAxisAlignment: CrossAxisAlignment.end,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(
                                'Your balance',
                                style: AppText.small.copyWith(
                                  color: palette.textMuted,
                                ),
                              ),
                              Text(
                                '${formatPoints(balance)} pts',
                                style: AppText.h2.copyWith(
                                  color: palette.textPrimary,
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 16),

                    if (!affordable) ...[
                      AppAlert(
                        tone: AppTone.warning,
                        message:
                            'You need ${formatPoints(shortfall)} more points '
                            'to redeem this reward.',
                      ),
                      const SizedBox(height: 16),
                    ],

                    const SizedBox(height: 8),
                    Text(
                      'Terms',
                      style: AppText.bodyBold.copyWith(
                        color: palette.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 8),
                    for (final term in [
                      if (r.validTo != null)
                        'Valid until ${formatDateLong(r.validTo!)}',
                      if (r.stock != null) '${r.stock} remaining',
                      'One redemption per coupon code',
                      'Cannot be combined with other offers',
                      'Non-transferable and non-refundable once redeemed',
                    ])
                      Padding(
                        padding: const EdgeInsets.only(bottom: 6),
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Padding(
                              padding: const EdgeInsets.only(top: 7, right: 10),
                              child: Container(
                                width: 4,
                                height: 4,
                                decoration: BoxDecoration(
                                  color: palette.textMuted,
                                  shape: BoxShape.circle,
                                ),
                              ),
                            ),
                            Expanded(
                              child: Text(
                                term,
                                style: AppText.body.copyWith(
                                  color: palette.textSecondary,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                  ],
                ),
              ),
              SafeArea(
                top: false,
                child: Container(
                  padding: const EdgeInsets.fromLTRB(24, 12, 24, 20),
                  decoration: BoxDecoration(
                    color: palette.surface,
                    border: Border(
                      top: BorderSide(color: palette.borderSubtle),
                    ),
                  ),
                  child: AppButton(
                    label: affordable ? 'Redeem Now' : 'Not Enough Points',
                    size: AppButtonSize.lg,
                    expand: true,
                    onPressed: affordable
                        ? () => context.push(
                            Routes.redeem(businessId, r.id),
                          )
                        : null,
                  ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}
