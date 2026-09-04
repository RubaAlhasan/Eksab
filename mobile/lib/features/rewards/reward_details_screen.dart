import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/business_tiles.dart';
import '../profile/error_screen.dart';

/// Prototype: `customer/reward-details.html` — cost vs balance, terms, and a
/// redeem CTA that is disabled (with the shortfall spelled out) when the
/// customer can't afford it.
class RewardDetailsScreen extends ConsumerWidget {
  const RewardDetailsScreen({super.key, required this.rewardId});

  final String rewardId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final reward = ref.watch(rewardByIdProvider(rewardId));
    if (reward == null) return const ErrorScreen(kind: ErrorKind.notFound);

    final business = ref.watch(businessByIdProvider(reward.businessId));
    final membership = ref.watch(
      membershipForBusinessProvider(reward.businessId),
    );
    final balance = membership?.balance ?? 0;
    final affordable = balance >= reward.pointsCost;
    final shortfall = reward.pointsCost - balance;

    return AppScaffold(
      backgroundColor: palette.surface,
      title: 'Reward Details',
      bottomBar: AppButton(
        label: affordable ? 'Redeem Now' : 'Not Enough Points',
        size: AppButtonSize.lg,
        expand: true,
        onPressed: affordable
            ? () => context.push(Routes.redeem(reward.id))
            : null,
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(24, 24, 24, 24),
        children: [
          Center(child: Text(reward.emoji, style: const TextStyle(fontSize: 64))),
          const SizedBox(height: 24),
          Text(
            reward.name,
            textAlign: TextAlign.center,
            style: AppText.h1.copyWith(color: palette.textPrimary),
          ),
          const SizedBox(height: 4),
          Text(
            business?.name ?? '',
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
                      style: AppText.small.copyWith(color: palette.textMuted),
                    ),
                    Text(
                      '${reward.pointsCost} pts',
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
                      style: AppText.small.copyWith(color: palette.textMuted),
                    ),
                    Text(
                      '${formatPoints(balance)} pts',
                      style: AppText.h2.copyWith(color: palette.textPrimary),
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
                  'You need ${formatPoints(shortfall)} more points to redeem '
                  'this reward.',
            ),
            const SizedBox(height: 16),
          ],

          const SizedBox(height: 8),
          Text(
            'Description',
            style: AppText.bodyBold.copyWith(color: palette.textPrimary),
          ),
          const SizedBox(height: 8),
          Text(
            'Redeem your points for ${reward.name.toLowerCase()} at any '
            '${business?.name ?? 'participating'} branch. Show your redemption '
            'QR or PIN to staff at checkout to claim.',
            style: AppText.body.copyWith(
              color: palette.textSecondary,
              height: 1.6,
            ),
          ),
          const SizedBox(height: 24),

          Text(
            'Terms',
            style: AppText.bodyBold.copyWith(color: palette.textPrimary),
          ),
          const SizedBox(height: 8),
          for (final term in [
            'Valid until ${formatDateLong(reward.validTo)}',
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
                      style: AppText.body.copyWith(color: palette.textSecondary),
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}
