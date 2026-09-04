import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';
import '../profile/error_screen.dart';

/// Prototype: `customer/rewards.html` — a business's reward catalogue with the
/// customer's balance pinned above it and unaffordable rewards dimmed.
class RewardsScreen extends ConsumerStatefulWidget {
  const RewardsScreen({super.key, required this.businessId});

  final String businessId;

  @override
  ConsumerState<RewardsScreen> createState() => _RewardsScreenState();
}

class _RewardsScreenState extends ConsumerState<RewardsScreen> {
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    Future<void>.delayed(const Duration(milliseconds: 450), () {
      if (mounted) setState(() => _loading = false);
    });
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final business = ref.watch(businessByIdProvider(widget.businessId));
    if (business == null) return const ErrorScreen(kind: ErrorKind.notFound);

    final membership = ref.watch(
      membershipForBusinessProvider(widget.businessId),
    );
    final balance = membership?.balance ?? 0;
    final rewards = ref.watch(rewardsForBusinessProvider(widget.businessId));

    return AppScaffold(
      title: '${business.name} Rewards',
      onBack: () => context.canPop()
          ? context.pop()
          : context.go(Routes.points(widget.businessId)),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 12, 20, 4),
            child: AppCard(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
              color: palette.isDark
                  ? const Color(0x1F6248E3)
                  : AppColors.primary50,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    'Your balance',
                    style: AppText.smallSemi.copyWith(
                      color: palette.isDark
                          ? AppColors.primary300
                          : AppColors.primary700,
                    ),
                  ),
                  Text(
                    '${formatPoints(balance)} pts available',
                    style: AppText.bodyBold.copyWith(
                      color: palette.isDark
                          ? AppColors.primary300
                          : AppColors.primary700,
                    ),
                  ),
                ],
              ),
            ),
          ),
          Expanded(
            child: _loading
                ? GridView.count(
                    padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                    crossAxisCount: 2,
                    crossAxisSpacing: 12,
                    mainAxisSpacing: 12,
                    childAspectRatio: 0.82,
                    children: const [
                      Skeleton(radius: 16),
                      Skeleton(radius: 16),
                      Skeleton(radius: 16),
                      Skeleton(radius: 16),
                    ],
                  )
                : rewards.isEmpty
                ? const EmptyState(
                    icon: Icons.card_giftcard_rounded,
                    title: 'No rewards yet',
                    message:
                        "This business hasn't published a rewards catalog yet.",
                  )
                : GridView.count(
                    padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                    crossAxisCount: 2,
                    crossAxisSpacing: 12,
                    mainAxisSpacing: 12,
                    childAspectRatio: 0.82,
                    children: [
                      for (final reward in rewards)
                        Opacity(
                          opacity: balance >= reward.pointsCost ? 1 : 0.55,
                          child: AppCard(
                            onTap: () => context.push(Routes.reward(reward.id)),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Center(
                                  child: Text(
                                    reward.emoji,
                                    style: const TextStyle(fontSize: 34),
                                  ),
                                ),
                                const SizedBox(height: 12),
                                Text(
                                  reward.name,
                                  maxLines: 2,
                                  overflow: TextOverflow.ellipsis,
                                  style: AppText.bodyBold.copyWith(
                                    color: palette.textPrimary,
                                  ),
                                ),
                                const SizedBox(height: 2),
                                Text(
                                  reward.stock == null
                                      ? 'Unlimited'
                                      : '${reward.stock} left',
                                  style: AppText.small.copyWith(
                                    color: palette.textMuted,
                                  ),
                                ),
                                const Spacer(),
                                Row(
                                  mainAxisAlignment:
                                      MainAxisAlignment.spaceBetween,
                                  children: [
                                    Text(
                                      '${reward.pointsCost} pts',
                                      style: AppText.bodyBold.copyWith(
                                        color: balance >= reward.pointsCost
                                            ? palette.primaryOnDarkAware
                                            : palette.textMuted,
                                      ),
                                    ),
                                    if (balance < reward.pointsCost)
                                      const AppBadge('Locked'),
                                  ],
                                ),
                              ],
                            ),
                          ),
                        ),
                    ],
                  ),
          ),
        ],
      ),
    );
  }
}
