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
import '../../shared/widgets/app_tabs.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/store-details.html` — brand cover, follow toggle,
/// join/points CTA, and About / Rewards / Branches tabs.
///
/// The Offers tab is backed by `/api/app/customer-campaign/business/{id}`,
/// which only returns campaigns this customer is actually targeted by.
class StoreDetailsScreen extends ConsumerStatefulWidget {
  const StoreDetailsScreen({super.key, required this.businessId});

  final String businessId;

  @override
  ConsumerState<StoreDetailsScreen> createState() => _StoreDetailsScreenState();
}

class _StoreDetailsScreenState extends ConsumerState<StoreDetailsScreen> {
  int _tab = 0;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final business = ref.watch(businessByIdProvider(widget.businessId));

    return Scaffold(
      backgroundColor: palette.surface,
      body: AsyncSection<Business>(
        value: business,
        errorTitle: 'Could not load this business',
        onRetry: () => ref.invalidate(businessByIdProvider(widget.businessId)),
        data: (biz) => _content(context, palette, biz),
      ),
    );
  }

  Widget _content(BuildContext context, AppPalette palette, Business biz) {
    final membership = ref.watch(
      membershipForBusinessProvider(widget.businessId),
    );
    final rewards = ref.watch(rewardsForBusinessProvider(widget.businessId));
    final offers = ref.watch(
      campaignsForBusinessProvider(widget.businessId),
    );

    return ListView(
      padding: EdgeInsets.zero,
      children: [
        // Was a 200pt band of the business's generated gradient with nothing inside it — a saturated
        // colour field carrying no information, sized for a cover photo that does not exist anywhere
        // in the data model, so the space was never going to fill. It also forced the back and
        // favourite buttons into translucent-white-on-unknown-colour, which is only ever a guess.
        //
        // The business's own colour now survives where it means something — the logo tile, which is
        // its identity — and the surface behind it stays neutral and out of the way.
        Container(
          decoration: BoxDecoration(
            color: palette.surface,
            border: Border(bottom: BorderSide(color: palette.borderSubtle)),
          ),
          child: SafeArea(
            bottom: false,
            child: Column(
              children: [
                Padding(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 4,
                  ),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      AppIconButton(
                        icon: Icons.arrow_back_rounded,
                        tooltip: 'Back',
                        foreground: palette.textSecondary,
                        onPressed: () => context.canPop()
                            ? context.pop()
                            : context.go(Routes.home),
                      ),
                      AppIconButton(
                        icon: biz.following
                            ? Icons.favorite_rounded
                            : Icons.favorite_border_rounded,
                        tooltip: biz.following ? 'Unfollow' : 'Follow',
                        foreground: biz.following
                            ? AppColors.danger500
                            : palette.textSecondary,
                        onPressed: () async {
                          await ref
                              .read(followActionsProvider)
                              .toggle(biz.id, follow: !biz.following);
                          ref.invalidate(businessByIdProvider(biz.id));
                          if (!context.mounted) return;
                          showAppToast(
                            context,
                            title: biz.following
                                ? 'Removed from favorites'
                                : 'Added to favorites',
                          );
                        },
                      ),
                    ],
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(20, 4, 20, 20),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      BusinessLogo(
                        initials: biz.initials,
                        gradient: biz.gradient,
                        logoUrl: biz.logoUrl,
                        size: 64,
                        radius: 20,
                        fontSize: 22,
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    biz.name,
                                    style: AppText.h2.copyWith(
                                      color: palette.textPrimary,
                                    ),
                                  ),
                                ),
                                if (biz.member)
                                  const AppBadge(
                                    'Member',
                                    tone: AppTone.primary,
                                  ),
                              ],
                            ),
                            if (biz.category.isNotEmpty) ...[
                              const SizedBox(height: 2),
                              Text(
                                biz.category,
                                style: AppText.small.copyWith(
                                  color: palette.textMuted,
                                ),
                              ),
                            ],
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 0),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
                Row(
                  children: [
                    Text(
                      '${biz.branches} '
                      '${biz.branches == 1 ? 'branch' : 'branches'}',
                      style: AppText.body.copyWith(
                        color: palette.textSecondary,
                      ),
                    ),
                    if (biz.distanceKm != null) ...[
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 8),
                        child: Text(
                          '·',
                          style: TextStyle(
                            color: palette.isDark
                                ? AppColors.slate700
                                : AppColors.slate300,
                          ),
                        ),
                      ),
                      Text(
                        '${biz.distanceKm!.toStringAsFixed(1)} km away',
                        style: AppText.body.copyWith(
                          color: palette.textSecondary,
                        ),
                      ),
                    ],
                  ],
                ),
                const SizedBox(height: 20),

                if (biz.member && membership != null)
                  AppCard(
                    child: Row(
                      children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(
                                'Your balance',
                                style: AppText.small.copyWith(
                                  color: palette.textMuted,
                                ),
                              ),
                              Text(
                                '${formatPoints(membership.balance)} pts',
                                style: AppText.h2.copyWith(
                                  color: palette.primaryOnDarkAware,
                                ),
                              ),
                            ],
                          ),
                        ),
                        AppButton(
                          label: 'View My Points',
                          onPressed: () =>
                              context.push(Routes.points(biz.id)),
                        ),
                      ],
                    ),
                  )
                else
                  AppButton(
                    label: 'Join ${biz.name}',
                    size: AppButtonSize.lg,
                    expand: true,
                    onPressed: () => context.push(Routes.join(biz.id)),
                  ),
                const SizedBox(height: 24),

                UnderlineTabs(
                  labels: const ['About', 'Offers', 'Rewards', 'Branches'],
                  selectedIndex: _tab,
                  onChanged: (i) => setState(() => _tab = i),
                ),
                const SizedBox(height: 16),

                if (_tab == 0)
                  Text(
                    biz.description?.isNotEmpty == true
                        ? biz.description!
                        : '${biz.name} has ${biz.branches} '
                              '${biz.branches == 1 ? 'branch' : 'branches'}. '
                              'Join the loyalty program to start earning points '
                              'on every visit.',
                    style: AppText.body.copyWith(
                      color: palette.textSecondary,
                      height: 1.6,
                    ),
                  )
                else if (_tab == 1)
                  AsyncSection<List<Campaign>>(
                    value: offers,
                    onRetry: () => ref.invalidate(
                      campaignsForBusinessProvider(widget.businessId),
                    ),
                    data: (list) => list.isEmpty
                        ? Padding(
                            padding: const EdgeInsets.symmetric(vertical: 24),
                            child: Center(
                              child: Text(
                                biz.member
                                    ? 'No active offers right now.'
                                    : 'Join to see offers from this business.',
                                style: AppText.body.copyWith(
                                  color: palette.textMuted,
                                ),
                              ),
                            ),
                          )
                        : Column(
                            children: [
                              for (final campaign in list) ...[
                                AppCard(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      Row(
                                        children: [
                                          Expanded(
                                            child: Text(
                                              campaign.name,
                                              style: AppText.bodyBold.copyWith(
                                                color: palette.textPrimary,
                                              ),
                                            ),
                                          ),
                                          AppBadge(
                                            campaign.type.label,
                                            tone: AppTone.success,
                                          ),
                                        ],
                                      ),
                                      const SizedBox(height: 4),
                                      Text(
                                        'Ends ${formatDate(campaign.endDate)}',
                                        style: AppText.small.copyWith(
                                          color: palette.textMuted,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                                const SizedBox(height: 12),
                              ],
                            ],
                          ),
                  )
                else if (_tab == 2)
                  AsyncSection<List<Reward>>(
                    value: rewards,
                    onRetry: () => ref.invalidate(
                      rewardsForBusinessProvider(widget.businessId),
                    ),
                    data: (list) => list.isEmpty
                        ? Padding(
                            padding: const EdgeInsets.symmetric(vertical: 24),
                            child: Center(
                              child: Text(
                                'No rewards published yet.',
                                style: AppText.body.copyWith(
                                  color: palette.textMuted,
                                ),
                              ),
                            ),
                          )
                        : GridView.count(
                            crossAxisCount: 2,
                            shrinkWrap: true,
                            physics: const NeverScrollableScrollPhysics(),
                            crossAxisSpacing: 12,
                            mainAxisSpacing: 12,
                            childAspectRatio: 1.05,
                            children: [
                              for (final reward in list)
                                AppCard(
                                  padding: const EdgeInsets.all(12),
                                  onTap: () => context.push(
                                    Routes.reward(biz.id, reward.id),
                                  ),
                                  child: Column(
                                    mainAxisAlignment:
                                        MainAxisAlignment.center,
                                    children: [
                                      IconTile(
                                        icon: reward.icon,
                                        tone: reward.tone,
                                        size: 40,
                                        iconSize: 20,
                                      ),
                                      const SizedBox(height: 8),
                                      Text(
                                        reward.name,
                                        maxLines: 2,
                                        textAlign: TextAlign.center,
                                        overflow: TextOverflow.ellipsis,
                                        style: AppText.smallBold.copyWith(
                                          color: palette.textPrimary,
                                        ),
                                      ),
                                      const SizedBox(height: 2),
                                      Text(
                                        '${reward.pointsCost} pts',
                                        style: AppText.smallSemi.copyWith(
                                          color: palette.primaryOnDarkAware,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                            ],
                          ),
                  )
                else
                  Column(
                    children: [
                      // Only a branch count is exposed to customers, not the
                      // names or addresses — so the list is numbered.
                      for (var i = 0; i < biz.branches; i++) ...[
                        AppCard(
                          child: Row(
                            children: [
                              IconTile(
                                icon: Icons.location_on_outlined,
                                tone: AppColors.slate500,
                                iconSize: 16,
                              ),
                              const SizedBox(width: 12),
                              Text(
                                'Branch ${i + 1}',
                                style: AppText.bodySemi.copyWith(
                                  color: palette.textPrimary,
                                ),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 12),
                      ],
                      if (biz.branches == 0)
                        Padding(
                          padding: const EdgeInsets.symmetric(vertical: 24),
                          child: Text(
                            'No branches listed.',
                            style: AppText.body.copyWith(
                              color: palette.textMuted,
                            ),
                          ),
                        ),
                    ],
                  ),
                const SizedBox(height: 32),
              ],
            ),
          ),
      ],
    );
  }
}
