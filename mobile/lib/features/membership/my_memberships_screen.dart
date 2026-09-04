import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
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
    final memberships = ref.watch(membershipsProvider);

    return AppScaffold(
      title: 'My Memberships',
      onBack: () =>
          context.canPop() ? context.pop() : context.go(Routes.profile),
      body: memberships.isEmpty
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
                for (final membership in memberships) ...[
                  Builder(
                    builder: (context) {
                      final business = ref.watch(
                        businessByIdProvider(membership.businessId),
                      );
                      if (business == null) return const SizedBox.shrink();
                      return BusinessRow(
                        business: business,
                        logoSize: 44,
                        meta:
                            'Joined ${formatDate(membership.joinedAt, withYear: true)}'
                            ' · ${business.category}',
                        trailing: AppBadge(
                          membership.status,
                          tone: membership.status == 'Active'
                              ? AppTone.success
                              : AppTone.neutral,
                        ),
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
