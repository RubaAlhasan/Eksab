import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/birthday-rewards.html` — birthday-type campaigns from
/// the businesses the customer belongs to.
/// Prototype: `customer/birthday-rewards.html`.
///
/// Both halves are real now: the birthday comes from `CustomerProfileDto`, and
/// the perks are birthday-type campaigns from the customer campaign feed.
class BirthdayRewardsScreen extends ConsumerWidget {
  const BirthdayRewardsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final customer = ref.watch(currentCustomerProvider);
    final campaigns = ref.watch(myCampaignsProvider);

    return AppScaffold(
      title: 'Birthday Rewards',
      onBack: () =>
          context.canPop() ? context.pop() : context.go(Routes.profile),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: [
          AppCard(
            padding: const EdgeInsets.all(20),
            gradient: const LinearGradient(
              colors: [AppColors.warning400, AppColors.warning600],
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
            child: Column(
              children: [
                const Text('🎂', style: TextStyle(fontSize: 28)),
                const SizedBox(height: 8),
                Text(
                  customer.dateOfBirth == null
                      ? 'Add your birthday to unlock birthday perks'
                      : 'Your birthday is '
                            '${formatDate(customer.dateOfBirth!)}',
                  textAlign: TextAlign.center,
                  style: AppText.bodyBold.copyWith(color: Colors.white),
                ),
                const SizedBox(height: 4),
                Text(
                  customer.dateOfBirth == null
                      ? 'You can set it in Edit Profile.'
                      : 'Businesses you have joined may offer a perk that month.',
                  textAlign: TextAlign.center,
                  style: AppText.small.copyWith(
                    color: Colors.white.withValues(alpha: 0.85),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          AsyncSection<List<Campaign>>(
            value: campaigns,
            onRetry: () => ref.invalidate(myCampaignsProvider),
            data: (all) {
              final birthday = all
                  .where((c) => c.type == CampaignType.birthday)
                  .toList();

              if (birthday.isEmpty) {
                return const EmptyState(
                  icon: Icons.card_giftcard_rounded,
                  title: 'No birthday offers yet',
                  message:
                      'Join more businesses to unlock birthday perks through '
                      'the year.',
                );
              }

              return Column(
                children: [
                  for (final campaign in birthday) ...[
                    AppCard(
                      onTap: () =>
                          context.push(Routes.store(campaign.businessId)),
                      child: Row(
                        children: [
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Text(
                                  campaign.businessName,
                                  overflow: TextOverflow.ellipsis,
                                  style: AppText.bodyBold.copyWith(
                                    color: AppPalette.of(context).textPrimary,
                                  ),
                                ),
                                Text(
                                  campaign.name,
                                  overflow: TextOverflow.ellipsis,
                                  style: AppText.small.copyWith(
                                    color: AppPalette.of(context).textSecondary,
                                  ),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(width: 8),
                          const Text('🎁', style: TextStyle(fontSize: 22)),
                        ],
                      ),
                    ),
                    const SizedBox(height: 12),
                  ],
                ],
              );
            },
          ),
        ],
      ),
    );
  }
}
