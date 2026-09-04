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

/// Prototype: `customer/campaigns.html` — every active offer across the
/// businesses the customer follows or has joined.
class CampaignsScreen extends ConsumerStatefulWidget {
  const CampaignsScreen({super.key});

  @override
  ConsumerState<CampaignsScreen> createState() => _CampaignsScreenState();
}

class _CampaignsScreenState extends ConsumerState<CampaignsScreen> {
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    Future<void>.delayed(const Duration(milliseconds: 450), () {
      if (mounted) setState(() => _loading = false);
    });
  }

  static AppTone _tone(CampaignType type) => switch (type) {
    CampaignType.doublePoints => AppTone.primary,
    CampaignType.birthday => AppTone.warning,
    CampaignType.winBack => AppTone.info,
    CampaignType.newCustomer => AppTone.success,
    CampaignType.discount => AppTone.danger,
    CampaignType.referral => AppTone.primary,
  };

  static String _typeLabel(CampaignType type) => switch (type) {
    CampaignType.doublePoints => 'Double Points',
    CampaignType.birthday => 'Birthday',
    CampaignType.winBack => 'Win Back',
    CampaignType.newCustomer => 'New Customer',
    CampaignType.discount => 'Discount',
    CampaignType.referral => 'Referral',
  };

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final campaigns = ref.watch(activeCampaignsProvider);

    return AppScaffold(
      title: 'Campaigns & Offers',
      onBack: () => context.canPop() ? context.pop() : context.go(Routes.home),
      body: _loading
          ? ListView.separated(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
              itemCount: 3,
              separatorBuilder: (_, __) => const SizedBox(height: 12),
              itemBuilder: (_, __) => const Skeleton(height: 112, radius: 16),
            )
          : campaigns.isEmpty
          ? const EmptyState(
              icon: Icons.campaign_outlined,
              title: 'No active campaigns',
              message: 'Follow more businesses to see their offers here.',
            )
          : ListView.separated(
              padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
              itemCount: campaigns.length,
              separatorBuilder: (_, __) => const SizedBox(height: 12),
              itemBuilder: (context, i) {
                final campaign = campaigns[i];
                final business = ref.watch(
                  businessByIdProvider(campaign.businessId),
                );
                if (business == null) return const SizedBox.shrink();

                return AppCard(
                  padding: EdgeInsets.zero,
                  clipContent: true,
                  onTap: () => context.push(Routes.store(business.id)),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Container(
                        padding: const EdgeInsets.all(16),
                        decoration: BoxDecoration(
                          gradient: business.gradient.gradient,
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    business.name.toUpperCase(),
                                    overflow: TextOverflow.ellipsis,
                                    style: AppText.overline.copyWith(
                                      color: Colors.white.withValues(alpha: 0.7),
                                    ),
                                  ),
                                ),
                                AppBadge(
                                  _typeLabel(campaign.type),
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
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              campaign.description,
                              style: AppText.body.copyWith(
                                color: palette.textSecondary,
                              ),
                            ),
                            const SizedBox(height: 8),
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
    );
  }
}
