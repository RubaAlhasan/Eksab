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
    final memberships = [...ref.watch(membershipsProvider)]
      ..sort((a, b) => b.balance.compareTo(a.balance));
    final total = ref.watch(totalPointsProvider);

    return AppScaffold(
      appBar: AppTopBar(
        title: 'My Wallet',
        showBack: false,
        actionIcon: Icons.qr_code_rounded,
        actionTooltip: 'My wallet QR',
        onAction: () => context.push(Routes.qrCode),
      ),
      body: ListView(
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
                  'Across ${memberships.length} '
                  '${memberships.length == 1 ? 'business' : 'businesses'}',
                  style: AppText.small.copyWith(
                    color: Colors.white.withValues(alpha: 0.6),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 20),

          if (_loading)
            for (var i = 0; i < 3; i++) ...[
              const Skeleton(height: 80, radius: 16),
              const SizedBox(height: 12),
            ]
          else if (memberships.isEmpty)
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
            for (final membership in memberships) ...[
              Builder(
                builder: (context) {
                  final business = ref.watch(
                    businessByIdProvider(membership.businessId),
                  );
                  if (business == null) return const SizedBox.shrink();
                  return WalletRow(
                    business: business,
                    membership: membership,
                    onTap: () => context.push(Routes.points(business.id)),
                  );
                },
              ),
              const SizedBox(height: 12),
            ],
        ],
      ),
    );
  }
}
