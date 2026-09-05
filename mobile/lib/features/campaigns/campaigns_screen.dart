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
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/campaigns.html`.
///
/// Backed by `/api/app/customer-campaign/my`, which returns only campaigns that
/// are live, from an approved business the customer has joined, and whose
/// target segment includes them.
class CampaignsScreen extends ConsumerWidget {
  const CampaignsScreen({super.key});

  static AppTone _tone(CampaignType type) => switch (type) {
    CampaignType.doublePoints => AppTone.primary,
    CampaignType.birthday => AppTone.warning,
    CampaignType.winBack => AppTone.info,
    CampaignType.newCustomer => AppTone.success,
    CampaignType.spendXGetY => AppTone.danger,
    CampaignType.vip => AppTone.primary,
    CampaignType.referral => AppTone.primary,
  };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final campaigns = ref.watch(myCampaignsProvider);

    return AppScaffold(
      title: 'Campaigns & Offers',
      onBack: () => context.canPop() ? context.pop() : context.go(Routes.home),
      body: AsyncSection<List<Campaign>>(
        value: campaigns,
        onRetry: () => ref.invalidate(myCampaignsProvider),
        loading: ListView.separated(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
          itemCount: 3,
          separatorBuilder: (_, _) => const SizedBox(height: 12),
          itemBuilder: (_, _) => const Skeleton(height: 112, radius: 16),
        ),
        data: (list) => list.isEmpty
            ? const EmptyState(
                icon: Icons.campaign_outlined,
                title: 'No active campaigns',
                message:
                    'Join more businesses to see the offers they are running.',
              )
            : RefreshIndicator(
                onRefresh: () async {
                  ref.invalidate(myCampaignsProvider);
                  await ref.read(myCampaignsProvider.future);
                },
                child: ListView.separated(
                  padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                  itemCount: list.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, i) {
                    final campaign = list[i];
                    final gradient = Business.gradientFor(campaign.businessId);

                    return AppCard(
                      padding: EdgeInsets.zero,
                      clipContent: true,
                      onTap: () =>
                          context.push(Routes.store(campaign.businessId)),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Container(
                            padding: const EdgeInsets.all(16),
                            decoration: BoxDecoration(
                              gradient: gradient.gradient,
                            ),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  children: [
                                    Expanded(
                                      child: Text(
                                        campaign.businessName.toUpperCase(),
                                        overflow: TextOverflow.ellipsis,
                                        style: AppText.overline.copyWith(
                                          color: Colors.white.withValues(
                                            alpha: 0.7,
                                          ),
                                        ),
                                      ),
                                    ),
                                    AppBadge(
                                      campaign.type.label,
                                      tone: _tone(campaign.type),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 8),
                                Text(
                                  campaign.name,
                                  style: AppText.title.copyWith(
                                    color: Colors.white,
                                    fontWeight: FontWeight.w800,
                                  ),
                                ),
                              ],
                            ),
                          ),
                          Padding(
                            padding: const EdgeInsets.all(16),
                            child: Row(
                              children: [
                                Icon(
                                  Icons.schedule_rounded,
                                  size: 14,
                                  color: palette.textMuted,
                                ),
                                const SizedBox(width: 6),
                                Text(
                                  'Ends ${formatDate(campaign.endDate)}',
                                  style: AppText.small.copyWith(
                                    color: palette.textMuted,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    );
                  },
                ),
              ),
      ),
    );
  }
}
