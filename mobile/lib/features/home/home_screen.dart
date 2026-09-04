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
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/home.html`.
///
/// Renders from cache-shaped local state immediately; the skeleton stands in
/// for the first network fetch. Sections mirror the prototype exactly: my
/// businesses, active campaigns, quick actions, discover nearby.
class HomeScreen extends ConsumerStatefulWidget {
  const HomeScreen({super.key});

  @override
  ConsumerState<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends ConsumerState<HomeScreen> {
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    Future<void>.delayed(const Duration(milliseconds: 500), () {
      if (mounted) setState(() => _loading = false);
    });
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final customer = ref.watch(currentCustomerProvider);
    final unread = ref.watch(unreadCountProvider);

    return Scaffold(
      backgroundColor: palette.scaffold,
      body: SafeArea(
        bottom: false,
        child: Column(
          children: [
            _Header(customer: customer, hasUnread: unread > 0),
            Expanded(
              child: _loading
                  ? const _HomeSkeleton()
                  : RefreshIndicator(
                      onRefresh: () async {
                        setState(() => _loading = true);
                        await Future<void>.delayed(
                          const Duration(milliseconds: 500),
                        );
                        if (mounted) setState(() => _loading = false);
                      },
                      child: const _HomeContent(),
                    ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.customer, required this.hasUnread});

  final Customer customer;
  final bool hasUnread;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final hour = DateTime.now().hour;
    final greeting = hour < 12
        ? 'Good morning'
        : (hour < 18 ? 'Good afternoon' : 'Good evening');

    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 16),
      child: Row(
        children: [
          AppAvatar(
            initials: customer.initials,
            onTap: () => context.go(Routes.profile),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  greeting,
                  style: AppText.small.copyWith(color: palette.textMuted),
                ),
                Text(
                  customer.fullName,
                  overflow: TextOverflow.ellipsis,
                  style: AppText.bodyBold.copyWith(color: palette.textPrimary),
                ),
              ],
            ),
          ),
          Stack(
            clipBehavior: Clip.none,
            children: [
              AppIconButton(
                icon: Icons.notifications_none_rounded,
                variant: AppButtonVariant.secondary,
                tooltip: 'Notifications',
                onPressed: () => context.go(Routes.notifications),
              ),
              if (hasUnread)
                Positioned(
                  top: 6,
                  right: 6,
                  child: Container(
                    width: 8,
                    height: 8,
                    decoration: const BoxDecoration(
                      color: AppColors.danger500,
                      shape: BoxShape.circle,
                    ),
                  ),
                ),
            ],
          ),
        ],
      ),
    );
  }
}

class _HomeContent extends ConsumerWidget {
  const _HomeContent();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final businesses = ref.watch(businessesProvider);
    final memberships = ref.watch(membershipsProvider);
    final campaigns = ref.watch(activeCampaignsProvider);
    final discover = businesses.where((b) => !b.member).take(4).toList();

    Business? businessById(String id) {
      for (final b in businesses) {
        if (b.id == id) return b;
      }
      return null;
    }

    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
      children: [
        // ── My businesses ────────────────────────────────────────────────
        SectionHeader(
          title: 'My Businesses',
          actionLabel: 'See all',
          onAction: () => context.go(Routes.wallet),
        ),
        if (memberships.isEmpty)
          EmptyState(
            icon: Icons.account_balance_wallet_outlined,
            title: 'No businesses joined yet',
            message: 'Join your first business to start earning points.',
            action: AppButton(
              label: 'Discover businesses',
              size: AppButtonSize.sm,
              onPressed: () => context.push(Routes.nearby),
            ),
          )
        else
          SizedBox(
            // Logo (40) + name + tier + balance, plus the card's 16px padding.
            height: 168,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              itemCount: memberships.length,
              separatorBuilder: (_, __) => const SizedBox(width: 12),
              itemBuilder: (context, i) {
                final membership = memberships[i];
                final business = businessById(membership.businessId);
                if (business == null) return const SizedBox.shrink();
                final hasCampaign = campaigns.any(
                  (c) => c.businessId == business.id,
                );
                return _MyBusinessCard(
                  business: business,
                  membership: membership,
                  hasCampaign: hasCampaign,
                );
              },
            ),
          ),
        const SizedBox(height: 28),

        // ── Active campaigns ─────────────────────────────────────────────
        SectionHeader(
          title: 'Active Campaigns',
          actionLabel: 'See all',
          onAction: () => context.push(Routes.campaigns),
        ),
        if (campaigns.isEmpty)
          Text(
            'No active campaigns right now.',
            style: AppText.small.copyWith(color: palette.textMuted),
          )
        else
          SizedBox(
            height: 116,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              itemCount: campaigns.length > 4 ? 4 : campaigns.length,
              separatorBuilder: (_, __) => const SizedBox(width: 12),
              itemBuilder: (context, i) {
                final campaign = campaigns[i];
                final business = businessById(campaign.businessId);
                if (business == null) return const SizedBox.shrink();
                return _CampaignBanner(
                  campaign: campaign,
                  business: business,
                );
              },
            ),
          ),
        const SizedBox(height: 28),

        // ── Quick actions ────────────────────────────────────────────────
        Row(
          children: [
            Expanded(
              child: _QuickAction(
                icon: Icons.qr_code_scanner_rounded,
                tone: AppColors.primary600,
                label: 'Scan & Check-in',
                onTap: () => context.push(Routes.qrScanner),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: _QuickAction(
                icon: Icons.qr_code_rounded,
                tone: AppColors.success600,
                label: 'My Wallet QR',
                onTap: () => context.push(Routes.qrCode),
              ),
            ),
          ],
        ),
        const SizedBox(height: 28),

        // ── Discover nearby ──────────────────────────────────────────────
        SectionHeader(
          title: 'Discover Nearby',
          actionLabel: 'See all',
          onAction: () => context.push(Routes.nearby),
        ),
        GridView.count(
          crossAxisCount: 2,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          crossAxisSpacing: 12,
          mainAxisSpacing: 12,
          childAspectRatio: 0.95,
          children: [
            for (final business in discover)
              _DiscoverCard(business: business),
          ],
        ),
      ],
    );
  }
}

class _MyBusinessCard extends StatelessWidget {
  const _MyBusinessCard({
    required this.business,
    required this.membership,
    required this.hasCampaign,
  });

  final Business business;
  final Membership membership;
  final bool hasCampaign;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return SizedBox(
      width: 176,
      child: AppCard(
        onTap: () => context.push(Routes.points(business.id)),
        child: Stack(
          children: [
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                BusinessLogo(
                  initials: business.initials,
                  gradient: business.gradient,
                  size: 40,
                  radius: 12,
                ),
                const SizedBox(height: 12),
                Text(
                  business.name,
                  overflow: TextOverflow.ellipsis,
                  style: AppText.bodyBold.copyWith(color: palette.textPrimary),
                ),
                Text(
                  '${membership.tier} tier',
                  style: AppText.small.copyWith(color: palette.textMuted),
                ),
                const SizedBox(height: 10),
                Text.rich(
                  TextSpan(
                    children: [
                      TextSpan(
                        text: formatPoints(membership.balance),
                        style: AppText.h2.copyWith(
                          color: palette.primaryOnDarkAware,
                        ),
                      ),
                      TextSpan(
                        text: ' pts',
                        style: AppText.smallSemi.copyWith(
                          color: palette.textMuted,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            if (hasCampaign)
              const Positioned(
                top: 0,
                right: 0,
                child: AppBadge('Campaign', tone: AppTone.warning),
              ),
          ],
        ),
      ),
    );
  }
}

class _CampaignBanner extends StatelessWidget {
  const _CampaignBanner({required this.campaign, required this.business});

  final Campaign campaign;
  final Business business;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 256,
      child: AppCard(
        gradient: business.gradient.gradient,
        onTap: () => context.push(Routes.campaigns),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              business.name.toUpperCase(),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: AppText.overline.copyWith(
                color: Colors.white.withValues(alpha: 0.7),
                fontSize: 10,
              ),
            ),
            const SizedBox(height: 4),
            Text(
              campaign.name,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: AppText.bodyBold.copyWith(color: Colors.white),
            ),
            const SizedBox(height: 4),
            Text(
              campaign.description,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: AppText.small.copyWith(
                color: Colors.white.withValues(alpha: 0.8),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _QuickAction extends StatelessWidget {
  const _QuickAction({
    required this.icon,
    required this.tone,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final Color tone;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return AppCard(
      onTap: onTap,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          IconTile(icon: icon, tone: tone, size: 40, iconSize: 20),
          const SizedBox(height: 8),
          Text(
            label,
            textAlign: TextAlign.center,
            style: AppText.smallSemi.copyWith(color: palette.textPrimary),
          ),
        ],
      ),
    );
  }
}

class _DiscoverCard extends StatelessWidget {
  const _DiscoverCard({required this.business});

  final Business business;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return AppCard(
      onTap: () => context.push(Routes.store(business.id)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          BusinessLogo(
            initials: business.initials,
            gradient: business.gradient,
            size: 40,
            radius: 12,
          ),
          const SizedBox(height: 12),
          Text(
            business.name,
            overflow: TextOverflow.ellipsis,
            style: AppText.bodyBold.copyWith(color: palette.textPrimary),
          ),
          Text(
            business.category,
            overflow: TextOverflow.ellipsis,
            style: AppText.small.copyWith(color: palette.textMuted),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              const Icon(Icons.star_rounded, size: 14, color: AppColors.warning500),
              const SizedBox(width: 4),
              Flexible(
                child: Text(
                  '${business.rating} · ${business.distanceKm} km',
                  overflow: TextOverflow.ellipsis,
                  style: AppText.small.copyWith(color: palette.textSecondary),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _HomeSkeleton extends StatelessWidget {
  const _HomeSkeleton();

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
      children: const [
        Row(
          children: [
            Skeleton(height: 168, width: 176, radius: 16),
            SizedBox(width: 12),
            Skeleton(height: 168, width: 176, radius: 16),
          ],
        ),
        SizedBox(height: 28),
        Skeleton(height: 96, radius: 16),
        SizedBox(height: 28),
        Row(
          children: [
            Expanded(child: Skeleton(height: 144, radius: 16)),
            SizedBox(width: 12),
            Expanded(child: Skeleton(height: 144, radius: 16)),
          ],
        ),
      ],
    );
  }
}
