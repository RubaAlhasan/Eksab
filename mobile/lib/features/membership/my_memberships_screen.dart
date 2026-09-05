import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/my-memberships.html`.
class MyMembershipsScreen extends ConsumerWidget {
  const MyMembershipsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final entries = ref.watch(walletEntriesProvider);

    return AppScaffold(
      title: 'My Memberships',
      onBack: () =>
          context.canPop() ? context.pop() : context.go(Routes.profile),
      body: AsyncSection<List<WalletEntry>>(
        value: entries,
        onRetry: () => ref.invalidate(membershipsProvider),
        data: (list) => list.isEmpty
            ? EmptyState(
                icon: Icons.storefront_outlined,
                title: 'No memberships yet',
                message: 'Join a business to start collecting points.',
                action: AppButton(
                  label: 'Discover businesses',
                  size: AppButtonSize.sm,
                  onPressed: () => context.push(Routes.nearby),
                ),
              )
            : ListView(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
                children: [
                  Text(
                    "Every business you've joined, and its membership status.",
                    style: AppText.small.copyWith(color: palette.textMuted),
                  ),
                  const SizedBox(height: 16),
                  for (final entry in list) ...[
                    BusinessRow(
                      business: entry.business,
                      logoSize: 44,
                      meta: entry.membership.joinedAt == null
                          ? entry.business.category
                          : 'Joined '
                                '${formatDate(entry.membership.joinedAt!, withYear: true)}'
                                ' · ${entry.business.category}',
                      trailing: AppBadge(
                        entry.membership.status.label,
                        tone: entry.membership.status == MembershipStatus.active
                            ? AppTone.success
                            : AppTone.neutral,
                      ),
                      onTap: () =>
                          context.push(Routes.points(entry.business.id)),
                    ),
                    const SizedBox(height: 12),
                  ],
                ],
              ),
      ),
    );
  }
}
