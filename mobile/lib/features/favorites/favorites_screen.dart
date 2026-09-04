import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/favorites.html` — businesses the customer follows but
/// hasn't joined, with a one-tap unfollow.
class FavoritesScreen extends ConsumerWidget {
  const FavoritesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final favorites = ref
        .watch(businessesProvider)
        .where((b) => b.following && !b.member)
        .toList();

    return AppScaffold(
      title: 'Favorites',
      onBack: () =>
          context.canPop() ? context.pop() : context.go(Routes.profile),
      body: favorites.isEmpty
          ? EmptyState(
              icon: Icons.favorite_border_rounded,
              title: 'No favorites yet',
              message:
                  "Tap the heart on any store's profile to follow it and see "
                  'their offers here.',
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
                  "Businesses you're following but haven't joined yet.",
                  style: AppText.small.copyWith(color: palette.textMuted),
                ),
                const SizedBox(height: 16),
                for (final business in favorites) ...[
                  BusinessRow(
                    business: business,
                    meta:
                        '${business.category} · ${business.distanceKm} km',
                    onTap: () => context.push(Routes.store(business.id)),
                    trailing: AppIconButton(
                      icon: Icons.favorite_rounded,
                      tooltip: 'Unfollow',
                      foreground: AppColors.danger500,
                      onPressed: () {
                        ref
                            .read(businessesProvider.notifier)
                            .toggleFollow(business.id);
                        showAppToast(
                          context,
                          title: 'Removed from favorites',
                          icon: Icons.info_outline_rounded,
                          accent: AppColors.info600,
                        );
                      },
                    ),
                  ),
                  const SizedBox(height: 12),
                ],
              ],
            ),
    );
  }
}
