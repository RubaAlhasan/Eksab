import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/wallet.html` — the cross-business total plus one row
/// per membership, sorted by balance.
class WalletScreen extends ConsumerStatefulWidget {
  const WalletScreen({super.key});

  @override
  ConsumerState<WalletScreen> createState() => _WalletScreenState();
}

class _WalletScreenState extends ConsumerState<WalletScreen> {
  @override
  Widget build(BuildContext context) {
    final entries = ref.watch(walletEntriesProvider);
    final total = ref.watch(totalPointsProvider);

    return AppScaffold(
      appBar: AppTopBar(
        title: 'My Wallet',
        showBack: false,
        actionIcon: Icons.qr_code_rounded,
        actionTooltip: 'My wallet QR',
        onAction: () => context.push(Routes.qrCode),
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          ref.invalidate(membershipsProvider);
          await ref.read(walletEntriesProvider.future);
        },
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
          children: [
            AppCard(
              padding: const EdgeInsets.all(20),
              gradient: const LinearGradient(
                colors: [AppColors.primary600, AppColors.primary800],
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    'Total points across all businesses',
                    style: AppText.small.copyWith(
                      color: Colors.white.withValues(alpha: 0.7),
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${formatPoints(total)} pts',
                    style: AppText.displayLg.copyWith(color: Colors.white),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    entries.maybeWhen(
                      data: (list) =>
                          'Across ${list.length} '
                          '${list.length == 1 ? 'business' : 'businesses'}',
                      orElse: () => ' ',
                    ),
                    style: AppText.small.copyWith(
                      color: Colors.white.withValues(alpha: 0.6),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            AsyncSection<List<WalletEntry>>(
              value: entries,
              onRetry: () => ref.invalidate(membershipsProvider),
              loading: const Column(
                children: [
                  Skeleton(height: 80, radius: 16),
                  SizedBox(height: 12),
                  Skeleton(height: 80, radius: 16),
                  SizedBox(height: 12),
                  Skeleton(height: 80, radius: 16),
                ],
              ),
              data: (list) => list.isEmpty
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
                  : Column(
                      children: [
                        for (final entry in list) ...[
                          WalletRow(
                            business: entry.business,
                            membership: entry.membership,
                            onTap: () =>
                                context.push(Routes.points(entry.business.id)),
                          ),
                          const SizedBox(height: 12),
                        ],
                      ],
                    ),
            ),
          ],
        ),
      ),
    );
  }
}
