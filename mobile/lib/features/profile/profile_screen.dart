import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/profile.html` — identity card, activity and preference
/// groups, and a confirmed log-out.
class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final customer = ref.watch(currentCustomerProvider);

    return Scaffold(
      backgroundColor: palette.scaffold,
      body: SafeArea(
        bottom: false,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 24),
          children: [
            Text(
              'Profile',
              style: AppText.h1.copyWith(color: palette.textPrimary),
            ),
            const SizedBox(height: 16),

            AppCard(
              padding: const EdgeInsets.all(20),
              child: Row(
                children: [
                  AppAvatar(initials: customer.initials, size: AvatarSize.lg),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          customer.fullName,
                          overflow: TextOverflow.ellipsis,
                          style: AppText.title.copyWith(
                            color: palette.textPrimary,
                          ),
                        ),
                        Text(
                          customer.email,
                          overflow: TextOverflow.ellipsis,
                          style: AppText.small.copyWith(
                            color: palette.textMuted,
                          ),
                        ),
                        if (customer.memberSince != null)
                          Text(
                            'Member since '
                            '${formatMonthYear(customer.memberSince!)}',
                            style: AppText.small.copyWith(
                              color: palette.textMuted,
                            ),
                          ),
                      ],
                    ),
                  ),
                  AppIconButton(
                    icon: Icons.edit_outlined,
                    variant: AppButtonVariant.secondary,
                    size: 16,
                    tooltip: 'Edit profile',
                    onPressed: () => context.push(Routes.editProfile),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),

            const SectionLabel('Your Activity'),
            AppCardList(
              children: [
                AppListRow(
                  icon: Icons.storefront_outlined,
                  label: 'My Memberships',
                  onTap: () => context.push(Routes.memberships),
                ),
                AppListRow(
                  icon: Icons.confirmation_number_outlined,
                  label: 'My Coupons',
                  onTap: () => context.push(Routes.coupons),
                ),
                AppListRow(
                  icon: Icons.favorite_border_rounded,
                  label: 'Favorites',
                  onTap: () => context.push(Routes.favorites),
                ),
                AppListRow(
                  icon: Icons.card_giftcard_rounded,
                  label: 'Refer Friends',
                  onTap: () => context.push(Routes.referral),
                ),
                AppListRow(
                  icon: Icons.cake_outlined,
                  label: 'Birthday Rewards',
                  onTap: () => context.push(Routes.birthdayRewards),
                ),
              ],
            ),
            const SizedBox(height: 20),

            const SectionLabel('Preferences'),
            AppCardList(
              children: [
                AppListRow(
                  icon: Icons.settings_outlined,
                  label: 'Settings',
                  onTap: () => context.push(Routes.settings),
                ),
                AppListRow(
                  icon: Icons.help_outline_rounded,
                  label: 'Help & Support',
                  onTap: () => context.push(Routes.help),
                ),
              ],
            ),
            const SizedBox(height: 24),

            AppButton(
              label: 'Log Out',
              icon: Icons.logout_rounded,
              variant: AppButtonVariant.secondary,
              expand: true,
              foregroundOverride: palette.isDark
                  ? AppColors.danger300
                  : AppColors.danger600,
              onPressed: () => _confirmLogOut(context, ref),
            ),
            const SizedBox(height: 16),
            Center(
              child: Text(
                'Eksabli v0.1.0',
                style: AppText.small.copyWith(
                  color: palette.isDark
                      ? AppColors.slate700
                      : AppColors.slate300,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _confirmLogOut(BuildContext context, WidgetRef ref) async {
    final palette = AppPalette.of(context);

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(
          'Log out of Eksabli?',
          style: AppText.title.copyWith(color: palette.textPrimary),
        ),
        content: Text(
          "You'll need your phone number and password (or OTP) to log back in.",
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
            label: 'Log Out',
            variant: AppButtonVariant.danger,
            onPressed: () => Navigator.of(dialogContext).pop(true),
          ),
        ],
      ),
    );

    if (confirmed != true) return;
    // The router's guard returns us to /login once the session clears.
    await ref.read(sessionProvider.notifier).signOut();
  }
}
