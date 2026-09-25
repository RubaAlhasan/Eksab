import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/auth/auth_exception.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/tier_progress.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/my-points.html` — per-business balance, quick links to
/// rewards/history, and the five most recent transactions.
class MyPointsScreen extends ConsumerStatefulWidget {
  const MyPointsScreen({super.key, required this.businessId});

  final String businessId;

  @override
  ConsumerState<MyPointsScreen> createState() => _MyPointsScreenState();
}

class _MyPointsScreenState extends ConsumerState<MyPointsScreen> {
  bool _leaving = false;

  /// Same native AlertDialog confirm shape as SettingsScreen's own
  /// "Delete my account" danger action — Cancel (secondary) vs. the
  /// destructive action (danger), rather than Angular's inline two-step UI
  /// (a web-page pattern, not how a confirm reads on a phone).
  Future<void> _confirmLeave(String businessName) async {
    final palette = AppPalette.of(context);

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 40,
              height: 40,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: AppColors.danger500.withValues(
                  alpha: palette.isDark ? 0.15 : 0.12,
                ),
                borderRadius: AppRadius.rMd,
              ),
              child: const Icon(
                Icons.logout_rounded,
                size: 20,
                color: AppColors.danger500,
              ),
            ),
            const SizedBox(height: 16),
            Text(
              'Leave $businessName?',
              style: AppText.title.copyWith(color: palette.textPrimary),
            ),
          ],
        ),
        content: Text(
          "You'll stop earning points here. Your balance and history are "
          'kept — rejoin any time to pick up where you left off.',
          style: AppText.body.copyWith(color: palette.textSecondary),
        ),
        actionsPadding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
        actions: [
          AppButton(
            label: 'Cancel',
            variant: AppButtonVariant.secondary,
            onPressed: () => Navigator.of(dialogContext).pop(false),
          ),
          AppButton(
            label: 'Leave',
            variant: AppButtonVariant.danger,
            onPressed: () => Navigator.of(dialogContext).pop(true),
          ),
        ],
      ),
    );

    if (confirmed != true || !mounted) return;

    setState(() => _leaving = true);
    try {
      await ref.read(membershipsProvider.notifier).leave(widget.businessId);
      if (!mounted) return;
      // The wallet this page shows no longer exists in myMemberships() once
      // left (GetMyWalletsAsync filters to Active-only server-side) —
      // nothing left here worth staying on, same reasoning as the Angular
      // version's own redirect after a successful leave.
      context.go(Routes.wallet);
    } on AuthException catch (error) {
      if (!mounted) return;
      setState(() => _leaving = false);
      showAppToast(
        context,
        title: 'Could not leave',
        message: error.message,
        icon: Icons.error_outline_rounded,
        accent: AppColors.danger600,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final business = ref.watch(businessByIdProvider(widget.businessId));
    final membership = ref.watch(
      membershipForBusinessProvider(widget.businessId),
    );
    final transactions = ref.watch(
      transactionsForBusinessProvider(widget.businessId),
    );

    return AppScaffold(
      title: business.valueOrNull?.name ?? 'My Points',
      onBack: () =>
          context.canPop() ? context.pop() : context.go(Routes.wallet),
      body: AsyncSection<Business>(
        value: business,
        onRetry: () => ref.invalidate(businessByIdProvider(widget.businessId)),
        data: (biz) => ListView(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
          children: [
            AppCard(
              padding: const EdgeInsets.all(24),
              child: Column(
                children: [
                  BusinessLogo(
                    initials: biz.initials,
                    gradient: biz.gradient,
                    logoUrl: biz.logoUrl,
                    size: 48,
                  ),
                  const SizedBox(height: 12),
                  Text(
                    'Current balance',
                    style: AppText.small.copyWith(color: palette.textMuted),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${formatPoints(membership?.balance ?? 0)} pts',
                    style: AppText.displayLg.copyWith(
                      fontSize: 36,
                      color: palette.primaryOnDarkAware,
                    ),
                  ),
                  if (membership?.tier != null) ...[
                    const SizedBox(height: 12),
                    AppBadge('${membership!.tier} Tier', tone: AppTone.primary),
                  ],
                  if (membership != null) ...[
                    const SizedBox(height: 12),
                    Text(
                      '${formatPoints(membership.lifetimeEarned)} earned · '
                      '${formatPoints(membership.lifetimeRedeemed)} redeemed',
                      style: AppText.small.copyWith(color: palette.textMuted),
                    ),
                  ],
                  if (membership != null && membership.hasTierProgress) ...[
                    const SizedBox(height: 20),
                    TierProgress(membership: membership),
                  ],
                ],
              ),
            ),
            const SizedBox(height: 20),

            Row(
              children: [
                Expanded(
                  child: _QuickLink(
                    icon: Icons.card_giftcard_rounded,
                    tone: AppColors.warning600,
                    label: 'Rewards',
                    onTap: () => context.push(Routes.rewards(widget.businessId)),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _QuickLink(
                    icon: Icons.schedule_rounded,
                    tone: AppColors.info600,
                    label: 'History',
                    onTap: () => context.push(Routes.history(widget.businessId)),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 20),

            SectionHeader(
              title: 'Recent Activity',
              actionLabel: 'See all',
              onAction: () => context.push(Routes.history(widget.businessId)),
            ),
            AsyncSection<List<PointTransaction>>(
              value: transactions,
              onRetry: () => ref.invalidate(
                transactionsForBusinessProvider(widget.businessId),
              ),
              data: (list) => list.isEmpty
                  ? Padding(
                      padding: const EdgeInsets.symmetric(vertical: 24),
                      child: Center(
                        child: Text(
                          'No activity yet at ${biz.name}.',
                          style: AppText.body.copyWith(
                            color: palette.textMuted,
                          ),
                        ),
                      ),
                    )
                  : Column(
                      children: [
                        for (final t in list.take(5)) ...[
                          TransactionRow(transaction: t),
                          const SizedBox(height: 8),
                        ],
                      ],
                    ),
            ),
            const SizedBox(height: 24),

            AppButton(
              label: 'Leave This Business',
              loadingLabel: 'Leaving…',
              loading: _leaving,
              icon: Icons.logout_rounded,
              variant: AppButtonVariant.secondary,
              foregroundOverride: palette.isDark
                  ? AppColors.danger300
                  : AppColors.danger600,
              expand: true,
              onPressed: () => _confirmLeave(biz.name),
            ),
          ],
        ),
      ),
    );
  }
}

class _QuickLink extends StatelessWidget {
  const _QuickLink({
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
      child: Row(
        children: [
          IconTile(icon: icon, tone: tone),
          const SizedBox(width: 12),
          Text(
            label,
            style: AppText.bodySemi.copyWith(color: palette.textPrimary),
          ),
        ],
      ),
    );
  }
}
