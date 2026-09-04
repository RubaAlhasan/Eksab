import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/demo/demo_data.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_tabs.dart';
import '../../shared/widgets/business_tiles.dart';
import '../profile/error_screen.dart';

/// Prototype: `customer/store-details.html` — brand cover, follow toggle,
/// join/points CTA, and About / Offers / Rewards / Branches tabs.
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
    if (business == null) return const ErrorScreen(kind: ErrorKind.notFound);

    final membership = ref.watch(
      membershipForBusinessProvider(widget.businessId),
    );
    final campaigns = ref.watch(campaignsForBusinessProvider(business.id));
    final rewards = ref.watch(rewardsForBusinessProvider(business.id));

    return Scaffold(
      backgroundColor: palette.surface,
      body: ListView(
        padding: EdgeInsets.zero,
        children: [
          _Cover(business: business),
          Transform.translate(
            offset: const Offset(0, -32),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  BusinessLogo(
                    initials: business.initials,
                    gradient: business.gradient,
                    size: 80,
                    radius: 24,
                    fontSize: 24,
                    border: Border.all(color: palette.surface, width: 4),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          business.name,
                          style: AppText.h1.copyWith(
                            color: palette.textPrimary,
                          ),
                        ),
                      ),
                      if (business.member)
                        const AppBadge('Member', tone: AppTone.primary),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(
                    business.category,
                    style: AppText.body.copyWith(color: palette.textMuted),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      const Icon(
                        Icons.star_rounded,
                        size: 16,
                        color: AppColors.warning500,
                      ),
                      const SizedBox(width: 4),
                      Text(
                        '${business.rating}',
                        style: AppText.body.copyWith(
                          color: palette.textSecondary,
                        ),
                      ),
                      _dot(palette),
                      Text(
                        '${business.distanceKm} km away',
                        style: AppText.body.copyWith(
                          color: palette.textSecondary,
                        ),
                      ),
                      _dot(palette),
                      Text(
                        '${business.branches} branches',
                        style: AppText.body.copyWith(
                          color: palette.textSecondary,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 20),

                  // CTA — join, or show the balance for an existing member.
                  if (business.member && membership != null)
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
                                context.push(Routes.points(business.id)),
                          ),
                        ],
                      ),
                    )
                  else
                    AppButton(
                      label: 'Join ${business.name}',
                      size: AppButtonSize.lg,
                      expand: true,
                      onPressed: () => context.push(Routes.join(business.id)),
                    ),
                  const SizedBox(height: 24),

                  UnderlineTabs(
                    labels: const ['About', 'Offers', 'Rewards', 'Branches'],
                    selectedIndex: _tab,
                    onChanged: (i) => setState(() => _tab = i),
                  ),
                  const SizedBox(height: 16),

                  switch (_tab) {
                    0 => _About(business: business, palette: palette),
                    1 => _Offers(campaigns: campaigns, palette: palette),
                    2 => _Rewards(rewards: rewards, palette: palette),
                    _ => _Branches(business: business, palette: palette),
                  },
                  const SizedBox(height: 32),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  static Widget _dot(AppPalette palette) => Padding(
    padding: const EdgeInsets.symmetric(horizontal: 8),
    child: Text(
      '·',
      style: TextStyle(
        color: palette.isDark ? AppColors.slate700 : AppColors.slate300,
      ),
    ),
  );
}

class _Cover extends ConsumerWidget {
  const _Cover({required this.business});

  final Business business;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return SizedBox(
      height: 200,
      child: Stack(
        fit: StackFit.expand,
        children: [
          DecoratedBox(
            decoration: BoxDecoration(gradient: business.gradient.gradient),
          ),
          SafeArea(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  AppIconButton(
                    icon: Icons.arrow_back_rounded,
                    tooltip: 'Back',
                    foreground: Colors.white,
                    background: Colors.white.withValues(alpha: 0.2),
                    onPressed: () => context.canPop()
                        ? context.pop()
                        : context.go(Routes.home),
                  ),
                  AppIconButton(
                    icon: business.following
                        ? Icons.favorite_rounded
                        : Icons.favorite_border_rounded,
                    tooltip: business.following ? 'Unfollow' : 'Follow',
                    foreground: business.following
                        ? AppColors.danger300
                        : Colors.white,
                    background: Colors.white.withValues(alpha: 0.2),
                    onPressed: () {
                      ref
                          .read(businessesProvider.notifier)
                          .toggleFollow(business.id);
                      showAppToast(
                        context,
                        title: business.following
                            ? 'Removed from favorites'
                            : 'Added to favorites',
                      );
                    },
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _About extends StatelessWidget {
  const _About({required this.business, required this.palette});

  final Business business;
  final AppPalette palette;

  @override
  Widget build(BuildContext context) {
    return Text(
      '${business.name} is a ${business.category.toLowerCase()} with '
      '${business.branches} branches near you. Join the loyalty program to '
      'start earning points on every visit, unlock tier perks, and get first '
      'access to campaigns and offers.',
      style: AppText.body.copyWith(color: palette.textSecondary, height: 1.6),
    );
  }
}

class _Offers extends StatelessWidget {
  const _Offers({required this.campaigns, required this.palette});

  final List<Campaign> campaigns;
  final AppPalette palette;

  @override
  Widget build(BuildContext context) {
    if (campaigns.isEmpty) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 24),
        child: Center(
          child: Text(
            'No active offers right now — check back soon.',
            style: AppText.body.copyWith(color: palette.textMuted),
          ),
        ),
      );
    }

    return Column(
      children: [
        for (final campaign in campaigns) ...[
          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
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
                    const AppBadge('Active', tone: AppTone.success),
                  ],
                ),
                const SizedBox(height: 4),
                Text(
                  campaign.description,
                  style: AppText.small.copyWith(color: palette.textSecondary),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
        ],
      ],
    );
  }
}

class _Rewards extends StatelessWidget {
  const _Rewards({required this.rewards, required this.palette});

  final List<Reward> rewards;
  final AppPalette palette;

  @override
  Widget build(BuildContext context) {
    if (rewards.isEmpty) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 24),
        child: Center(
          child: Text(
            'No rewards published yet.',
            style: AppText.body.copyWith(color: palette.textMuted),
          ),
        ),
      );
    }

    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      crossAxisSpacing: 12,
      mainAxisSpacing: 12,
      childAspectRatio: 1.05,
      children: [
        for (final reward in rewards)
          AppCard(
            padding: const EdgeInsets.all(12),
            onTap: () => context.push(Routes.reward(reward.id)),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Text(reward.emoji, style: const TextStyle(fontSize: 30)),
                const SizedBox(height: 8),
                Text(
                  reward.name,
                  maxLines: 2,
                  textAlign: TextAlign.center,
                  overflow: TextOverflow.ellipsis,
                  style: AppText.smallBold.copyWith(color: palette.textPrimary),
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
    );
  }
}

class _Branches extends StatelessWidget {
  const _Branches({required this.business, required this.palette});

  final Business business;
  final AppPalette palette;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        for (var i = 0; i < business.branches; i++) ...[
          AppCard(
            child: Row(
              children: [
                IconTile(
                  icon: Icons.location_on_outlined,
                  tone: AppColors.slate500,
                  iconSize: 16,
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        DemoData.branchNames[i % DemoData.branchNames.length],
                        style: AppText.bodySemi.copyWith(
                          color: palette.textPrimary,
                        ),
                      ),
                      Text(
                        'Open until 10:00 PM',
                        style: AppText.small.copyWith(color: palette.textMuted),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
        ],
      ],
    );
  }
}
