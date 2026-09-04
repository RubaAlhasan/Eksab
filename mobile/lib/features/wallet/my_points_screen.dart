import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';
import '../profile/error_screen.dart';

/// Prototype: `customer/my-points.html` — per-business balance, tier progress,
/// quick links to rewards/history, and the five most recent transactions.
class MyPointsScreen extends ConsumerWidget {
  const MyPointsScreen({super.key, required this.businessId});

  final String businessId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final business = ref.watch(businessByIdProvider(businessId));
    final membership = ref.watch(membershipForBusinessProvider(businessId));
    if (business == null) return const ErrorScreen(kind: ErrorKind.notFound);

    final transactions = ref
        .watch(transactionsForBusinessProvider(businessId))
        .take(5)
        .toList();

    return AppScaffold(
      title: business.name,
      onBack: () =>
          context.canPop() ? context.pop() : context.go(Routes.wallet),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: [
          AppCard(
            padding: const EdgeInsets.all(24),
            child: Column(
              children: [
                BusinessLogo(
                  initials: business.initials,
                  gradient: business.gradient,
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
                if (membership != null) ...[
                  const SizedBox(height: 12),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      AppBadge('${membership.tier} Tier', tone: AppTone.primary),
                      const SizedBox(width: 8),
                      Flexible(
                        child: Text(
                          '${membership.pointsToNextTier} pts to '
                          '${membership.nextTier}',
                          overflow: TextOverflow.ellipsis,
                          style: AppText.small.copyWith(
                            color: palette.textMuted,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  ProgressTrack(percent: membership.tierProgressPct),
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
                  onTap: () => context.push(Routes.rewards(businessId)),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _QuickLink(
                  icon: Icons.schedule_rounded,
                  tone: AppColors.info600,
                  label: 'History',
                  onTap: () => context.push(Routes.history(businessId)),
                ),
              ),
            ],
          ),
          const SizedBox(height: 20),

          SectionHeader(
            title: 'Recent Activity',
            actionLabel: 'See all',
            onAction: () => context.push(Routes.history(businessId)),
          ),
          if (transactions.isEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 24),
              child: Center(
                child: Text(
                  'No activity yet at ${business.name}.',
                  style: AppText.body.copyWith(color: palette.textMuted),
                ),
              ),
            )
          else
            for (final transaction in transactions) ...[
              TransactionRow(transaction: transaction),
              const SizedBox(height: 8),
            ],
        ],
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
