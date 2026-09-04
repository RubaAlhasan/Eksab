import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/demo/demo_data.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/app_tabs.dart';

/// Prototype: `customer/settings.html` — notification channels, appearance,
/// linked devices, and the account-deletion danger zone.
class SettingsScreen extends ConsumerStatefulWidget {
  const SettingsScreen({super.key});

  @override
  ConsumerState<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends ConsumerState<SettingsScreen> {
  final List<LinkedDevice> _devices = List.of(DemoData.devices);

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final prefs = ref.watch(preferencesProvider);
    final notifier = ref.read(preferencesProvider.notifier);

    // ThemeMode.system follows the OS, so reflect what is actually on screen.
    final isDark = prefs.themeMode == ThemeMode.system
        ? MediaQuery.platformBrightnessOf(context) == Brightness.dark
        : prefs.themeMode == ThemeMode.dark;

    return AppScaffold(
      title: 'Settings',
      onBack: () =>
          context.canPop() ? context.pop() : context.go(Routes.profile),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: [
          const SectionLabel('Notifications'),
          AppCardList(
            children: [
              _SwitchRow(
                label: 'Push notifications',
                value: prefs.pushEnabled,
                onChanged: notifier.setPush,
              ),
              _SwitchRow(
                label: 'Email',
                value: prefs.emailEnabled,
                onChanged: notifier.setEmail,
              ),
              _SwitchRow(
                label: 'SMS',
                value: prefs.smsEnabled,
                onChanged: notifier.setSms,
              ),
            ],
          ),
          const SizedBox(height: 20),

          const SectionLabel('Appearance'),
          AppCardList(
            children: [
              _SwitchRow(
                label: 'Dark mode',
                value: isDark,
                onChanged: notifier.setDarkMode,
              ),
              Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Language',
                      style: AppText.bodySemi.copyWith(
                        color: palette.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 10),
                    PillTabs(
                      labels: const ['English', 'العربية'],
                      expand: true,
                      selectedIndex:
                          prefs.locale.languageCode == 'ar' ? 1 : 0,
                      onChanged: (i) {
                        final locale = Locale(i == 1 ? 'ar' : 'en');
                        notifier.setLocale(locale);
                        showAppToast(
                          context,
                          title: 'Language updated',
                          message: i == 1
                              ? 'واجهة التطبيق ستظهر بالعربية.'
                              : 'English applied',
                          icon: Icons.info_outline_rounded,
                          accent: AppColors.info600,
                        );
                      },
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 20),

          const SectionLabel('Linked Devices'),
          AppCardList(
            children: [
              for (final device in _devices)
                Padding(
                  padding: const EdgeInsets.all(16),
                  child: Row(
                    children: [
                      IconTile(
                        icon: Icons.smartphone_rounded,
                        tone: AppColors.slate500,
                        iconSize: 16,
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Row(
                              children: [
                                Flexible(
                                  child: Text(
                                    device.name,
                                    overflow: TextOverflow.ellipsis,
                                    style: AppText.bodySemi.copyWith(
                                      color: palette.textPrimary,
                                    ),
                                  ),
                                ),
                                if (device.isCurrent) ...[
                                  const SizedBox(width: 6),
                                  const AppBadge(
                                    'This device',
                                    tone: AppTone.success,
                                  ),
                                ],
                              ],
                            ),
                            Text(
                              device.location,
                              style: AppText.small.copyWith(
                                color: palette.textMuted,
                              ),
                            ),
                          ],
                        ),
                      ),
                      if (!device.isCurrent)
                        GestureDetector(
                          onTap: () {
                            setState(() => _devices.remove(device));
                            showAppToast(context, title: 'Device logged out');
                          },
                          behavior: HitTestBehavior.opaque,
                          child: Text(
                            'Log out',
                            style: AppText.smallSemi.copyWith(
                              color: palette.isDark
                                  ? AppColors.danger300
                                  : AppColors.danger600,
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
            ],
          ),
          const SizedBox(height: 20),

          SectionLabel(
            'Danger Zone',
            tone: palette.isDark ? AppColors.danger300 : AppColors.danger500,
          ),
          AppCard(
            onTap: () => _confirmDelete(context),
            child: Row(
              children: [
                const Icon(
                  Icons.delete_outline_rounded,
                  size: 18,
                  color: AppColors.danger500,
                ),
                const SizedBox(width: 12),
                Text(
                  'Delete my account',
                  style: AppText.bodySemi.copyWith(
                    color: palette.isDark
                        ? AppColors.danger300
                        : AppColors.danger600,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _confirmDelete(BuildContext context) async {
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
                color: AppColors.danger500.withValues(alpha: palette.isDark ? 0.15 : 0.12),
                borderRadius: AppRadius.rMd,
              ),
              child: const Icon(
                Icons.warning_amber_rounded,
                size: 20,
                color: AppColors.danger500,
              ),
            ),
            const SizedBox(height: 16),
            Text(
              'Delete your account?',
              style: AppText.title.copyWith(color: palette.textPrimary),
            ),
          ],
        ),
        content: Text(
          'This freezes your memberships at every business — your data is '
          'retained for 90 days before permanent deletion, per our data '
          'retention policy.',
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
            label: 'Delete Account',
            variant: AppButtonVariant.danger,
            onPressed: () => Navigator.of(dialogContext).pop(true),
          ),
        ],
      ),
    );

    if (confirmed != true || !context.mounted) return;

    showAppToast(
      context,
      title: 'Account deletion requested',
      message: 'You will be logged out shortly.',
      icon: Icons.error_outline_rounded,
      accent: AppColors.danger600,
    );
    await Future<void>.delayed(const Duration(milliseconds: 1500));
    // The router's guard returns us to /login once the session clears.
    await ref.read(sessionProvider.notifier).signOut();
  }
}

class _SwitchRow extends StatelessWidget {
  const _SwitchRow({
    required this.label,
    required this.value,
    required this.onChanged,
  });

  final String label;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 6, 16, 6),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Expanded(
            child: Text(
              label,
              style: AppText.bodySemi.copyWith(color: palette.textPrimary),
            ),
          ),
          Switch(value: value, onChanged: onChanged),
        ],
      ),
    );
  }
}
