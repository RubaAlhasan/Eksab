import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';
import '../../shared/widgets/tier_progress.dart';

/// Prototype: `customer/home.html`.
///
/// The "Active Offers" strip is backed by the customer campaign feed, which is
/// already filtered server-side to campaigns this customer is targeted by.
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final customer = ref.watch(currentCustomerProvider);
    final unread = ref.watch(unreadCountProvider).valueOrNull ?? 0;
    final wallet = ref.watch(walletEntriesProvider);
    final directory = ref.watch(businessSearchProvider(const BusinessQuery()));
    final campaigns = ref.watch(myCampaignsProvider);

    return Scaffold(
      backgroundColor: palette.scaffold,
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          onRefresh: () async {
            ref
              ..invalidate(membershipsProvider)
              ..invalidate(businessSearchProvider)
              ..invalidate(unreadCountProvider);
            await ref.read(walletEntriesProvider.future);
          },
          child: ListView(
            padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
            children: [
              _Header(customer: customer, hasUnread: unread > 0),

              SectionHeader(
                title: 'My Businesses',
                actionLabel: 'See all',
                onAction: () => context.go(Routes.wallet),
              ),
              AsyncSection<List<WalletEntry>>(
                value: wallet,
                onRetry: () => ref.invalidate(membershipsProvider),
                loading: const Column(
                  children: [
                    Skeleton(height: 92, radius: 16),
                    SizedBox(height: 12),
                    Skeleton(height: 92, radius: 16),
                  ],
                ),
                data: (entries) => entries.isEmpty
                    ? EmptyState(
                        icon: Icons.account_balance_wallet_outlined,
                        title: 'No businesses joined yet',
                        message:
                            'Join your first business to start earning points.',
                        action: AppButton(
                          label: 'Discover businesses',
                          size: AppButtonSize.sm,
                          onPressed: () => context.push(Routes.nearby),
                        ),
                      )
                    // Stacked, not a horizontal strip — same reasoning as the offers below. A row
                    // of fixed 176pt cards left most of the width empty for a customer with one
                    // membership, and clipped the last card for one with several.
                    : Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          for (var i = 0; i < entries.length; i++) ...[
                            if (i > 0) const SizedBox(height: 12),
                            _MyBusinessCard(entry: entries[i]),
                          ],
                        ],
                      ),
              ),
              const SizedBox(height: 28),

              // Only rendered when there is something live to show — an empty
              // offers strip is noise, not information.
              ...switch (campaigns.valueOrNull) {
                final list? when list.isNotEmpty => [
                  SectionHeader(
                    title: 'Active Offers',
                    actionLabel: 'See all',
                    onAction: () => context.push(Routes.campaigns),
                  ),
                  _OffersStack(campaigns: list),
                  const SizedBox(height: 28),
                ],
                _ => const <Widget>[],
              },

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

              SectionHeader(
                title: 'Discover',
                actionLabel: 'See all',
                onAction: () => context.push(Routes.nearby),
              ),
              AsyncSection<List<Business>>(
                value: directory,
                onRetry: () => ref.invalidate(businessSearchProvider),
                data: (all) {
                  final discover = all.where((b) => !b.member).take(4).toList();
                  if (discover.isEmpty) {
                    return const EmptyState(
                      icon: Icons.storefront_outlined,
                      title: 'Nothing new to discover',
                      message: "You've joined every business listed so far.",
                    );
                  }
                  return GridView.count(
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
                  );
                },
              ),
            ],
          ),
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
      padding: const EdgeInsets.only(top: 8, bottom: 16),
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

/// One joined business: who, what tier, how many points, and how close to the next tier.
///
/// Laid out as a full-width row rather than the tall fixed-width tile it used to be — see the
/// stacking comment at the call site. Reading left to right also puts the balance where the eye
/// already is for every other list in the app.
class _MyBusinessCard extends StatelessWidget {
  const _MyBusinessCard({required this.entry});

  final WalletEntry entry;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);

    return AppCard(
      onTap: () => context.push(Routes.points(entry.business.id)),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              BusinessLogo(
                initials: entry.business.initials,
                gradient: entry.business.gradient,
                logoUrl: entry.business.logoUrl,
                size: 44,
                radius: 14,
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      entry.business.name,
                      overflow: TextOverflow.ellipsis,
                      style: AppText.bodyBold.copyWith(
                        color: palette.textPrimary,
                      ),
                    ),
                    Text(
                      entry.membership.tier ?? entry.business.category,
                      overflow: TextOverflow.ellipsis,
                      style: AppText.small.copyWith(color: palette.textMuted),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              Text.rich(
                TextSpan(
                  children: [
                    TextSpan(
                      text: formatPoints(entry.membership.balance),
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
          // The card's whole job is to pull someone back in; the balance alone says where they are,
          // this says how close they are to the next thing worth having.
          if (entry.membership.hasTierProgress) ...[
            const SizedBox(height: 12),
            TierProgress(membership: entry.membership, compact: true),
          ],
        ],
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
            logoUrl: business.logoUrl,
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
          Text(
            business.distanceKm == null
                ? '${business.branches} '
                      '${business.branches == 1 ? 'branch' : 'branches'}'
                : '${business.distanceKm!.toStringAsFixed(1)} km away',
            overflow: TextOverflow.ellipsis,
            style: AppText.small.copyWith(color: palette.textSecondary),
          ),
        ],
      ),
    );
  }
}

/// Offers, stacked full width.
///
/// This was a horizontal carousel of fixed-width cards, then a PageView with a peek. Both cut the
/// next card at the viewport edge, and at desktop widths the slice was large enough to chop a
/// headline mid-word — it read as a rendering fault rather than as "scroll for more". A peek only
/// works when it is a thin, obviously-deliberate sliver, and there is no card width that stays thin
/// across a 360pt phone and a maximised browser window.
///
/// Full-width rows have no edge to cut against, match every other list on this screen, and need no
/// width arithmetic at all. Offers beyond the first few live behind the section's own "See all",
/// which is where a long list belongs anyway.
class _OffersStack extends StatelessWidget {
  const _OffersStack({required this.campaigns});

  /// Home is a summary, not the offers screen. Past a handful the section would push everything else
  /// below the fold, and "See all" already exists.
  static const _maxVisible = 3;

  final List<Campaign> campaigns;

  @override
  Widget build(BuildContext context) {
    final visible = campaigns.take(_maxVisible).toList();

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        for (var i = 0; i < visible.length; i++) ...[
          if (i > 0) const SizedBox(height: 12),
          _CampaignCard(campaign: visible[i]),
        ],
      ],
    );
  }
}

class _CampaignCard extends StatelessWidget {
  const _CampaignCard({required this.campaign});

  final Campaign campaign;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      child: AppCard(
        // Brand gradient, not the business's generated colour: this card is Eksabli chrome
        // announcing an offer, and the business is already named inside it. Hashing the whole
        // surface off the tenant id made the app's accent depend on whose campaign it was.
        gradient: BrandGradient.violet.gradient,
        onTap: () => context.push(Routes.store(campaign.businessId)),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              campaign.businessName.toUpperCase(),
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
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: AppText.bodyBold.copyWith(color: Colors.white),
            ),
            const SizedBox(height: 10),
            Text(
              'Ends ${formatDate(campaign.endDate)}',
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
