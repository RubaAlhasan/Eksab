import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/birthday-rewards.html` — birthday-type campaigns from
/// the businesses the customer belongs to.
class BirthdayRewardsScreen extends ConsumerWidget {
  const BirthdayRewardsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final customer = ref.watch(currentCustomerProvider);
    final campaigns = ref
        .watch(activeCampaignsProvider)
        .where((c) => c.type == CampaignType.birthday)
        .toList();

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
                      // ABP's profile endpoint carries no date of birth, and
                      // CustomerProfileAppService is not exposed over HTTP yet.
                      ? 'Add your birthday to unlock birthday perks'
                      : 'Your birthday is '
                            '${formatDate(customer.dateOfBirth!)}',
                  textAlign: TextAlign.center,
                  style: AppText.bodyBold.copyWith(color: Colors.white),
                ),
                const SizedBox(height: 4),
                Text(
                  'Businesses below are offering a birthday perk this month.',
                  textAlign: TextAlign.center,
                  style: AppText.small.copyWith(
                    color: Colors.white.withValues(alpha: 0.8),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),

          if (campaigns.isEmpty)
            const EmptyState(
              icon: Icons.card_giftcard_rounded,
              title: 'No birthday offers yet',
              message:
                  'Join more businesses to unlock birthday perks throughout '
                  'the year.',
            )
          else
            for (final campaign in campaigns) ...[
              Builder(
                builder: (context) {
                  final business = ref.watch(
                    businessByIdProvider(campaign.businessId),
                  );
                  if (business == null) return const SizedBox.shrink();
                  return AppCard(
                    onTap: () => context.push(Routes.store(business.id)),
                    child: Row(
                      children: [
                        BusinessLogo(
                          initials: business.initials,
                          gradient: business.gradient,
                        ),
                        const SizedBox(width: 16),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(
                                business.name,
                                overflow: TextOverflow.ellipsis,
                                style: AppText.bodyBold.copyWith(
                                  color: palette.textPrimary,
                                ),
                              ),
                              Text(
                                campaign.description,
                                overflow: TextOverflow.ellipsis,
                                style: AppText.small.copyWith(
                                  color: palette.textSecondary,
                                ),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(width: 8),
                        const Text('🎁', style: TextStyle(fontSize: 22)),
                      ],
                    ),
                  );
                },
              ),
              const SizedBox(height: 12),
            ],
        ],
      ),
    );
  }
}
