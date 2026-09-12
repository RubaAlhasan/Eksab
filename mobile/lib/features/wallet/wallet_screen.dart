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
            // Was "Total points across all businesses", summed across memberships.
            //
            // Points are not fungible: 2,250 at one business buys nothing at another, so the sum was
            // a number the customer could never spend — and the larger it grew, the more it implied
            // otherwise. The per-business list below already carries the balances that are real.
            //
            // The slot now holds what is actually reached for at a counter. Showing the wallet QR is
            // how points get earned in the first place, and it was buried behind an unlabelled icon
            // in the top-right corner.
            AppCard(
              padding: const EdgeInsets.all(20),
              onTap: () => context.push(Routes.qrCode),
              gradient: const LinearGradient(
                colors: [AppColors.primary600, AppColors.primary800],
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
              ),
              child: Row(
                children: [
                  Container(
                    width: 48,
                    height: 48,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.16),
                      borderRadius: AppRadius.rMd,
                    ),
                    child: const Icon(
                      Icons.qr_code_rounded,
                      color: Colors.white,
                      size: 26,
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          'Your wallet QR',
                          style: AppText.bodyBold.copyWith(color: Colors.white),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          'Show this at checkout to earn points',
                          style: AppText.small.copyWith(
                            color: Colors.white.withValues(alpha: 0.75),
                          ),
                        ),
                      ],
                    ),
                  ),
                  Icon(
                    Icons.chevron_right_rounded,
                    color: Colors.white.withValues(alpha: 0.8),
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
