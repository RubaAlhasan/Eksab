import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/referral.html` — referral code, share CTA, and the
/// invite history with per-invite status.
class ReferralScreen extends ConsumerWidget {
  const ReferralScreen({super.key});

  static AppTone _tone(String status) => switch (status) {
    'Rewarded' => AppTone.success,
    'Completed' => AppTone.info,
    _ => AppTone.warning,
  };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final referral = ref.watch(referralProvider);

    return AppScaffold(
      title: 'Invite Friends',
      onBack: () =>
          context.canPop() ? context.pop() : context.go(Routes.profile),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: [
          AppCard(
            padding: const EdgeInsets.all(24),
            gradient: const LinearGradient(
              colors: [AppColors.primary600, AppColors.primary800],
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
            child: Column(
              children: [
                Container(
                  width: 48,
                  height: 48,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha: 0.15),
                    borderRadius: AppRadius.rLg,
                  ),
                  child: const Icon(
                    Icons.card_giftcard_rounded,
                    size: 22,
                    color: Colors.white,
                  ),
                ),
                const SizedBox(height: 12),
                Text(
                  'Give points, get points',
                  style: AppText.h2.copyWith(color: Colors.white),
                ),
                const SizedBox(height: 4),
                Text(
                  'Invite a friend — you both earn bonus points when they join '
                  'their first business.',
                  textAlign: TextAlign.center,
                  style: AppText.small.copyWith(
                    color: Colors.white.withValues(alpha: 0.7),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 20),

          Row(
            children: [
              Expanded(
                child: _Stat(label: 'Invited', value: '${referral.invited}'),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _Stat(label: 'Joined', value: '${referral.joined}'),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _Stat(
                  label: 'Points earned',
                  value: '${referral.pointsEarned}',
                  accent: true,
                ),
              ),
            ],
          ),
          const SizedBox(height: 20),

          AppCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Your referral code',
                  style: AppText.smallSemi.copyWith(color: palette.textMuted),
                ),
                const SizedBox(height: 8),
                Container(
                  padding: const EdgeInsets.fromLTRB(16, 10, 8, 10),
                  decoration: BoxDecoration(
                    color: palette.isDark
                        ? AppColors.slate950
                        : AppColors.slate50,
                    borderRadius: AppRadius.rMd,
                  ),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          referral.code,
                          style: AppText.h2.copyWith(
                            color: palette.textPrimary,
                            letterSpacing: 1.2,
                            fontFeatures: const [FontFeature.tabularFigures()],
                          ),
                        ),
                      ),
                      AppIconButton(
                        icon: Icons.copy_rounded,
                        tooltip: 'Copy code',
                        onPressed: () async {
                          await Clipboard.setData(
                            ClipboardData(text: referral.code),
                          );
                          if (!context.mounted) return;
                          showAppToast(
                            context,
                            title: 'Code copied',
                            message: referral.code,
                          );
                        },
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                AppButton(
                  label: 'Share Invite Link',
                  icon: Icons.ios_share_rounded,
                  expand: true,
                  onPressed: () async {
                    await Clipboard.setData(
                      ClipboardData(text: referral.link),
                    );
                    if (!context.mounted) return;
                    showAppToast(
                      context,
                      title: 'Invite link copied',
                      message: 'Wire up the native share sheet to send it '
                          'directly.',
                      icon: Icons.info_outline_rounded,
                      accent: AppColors.info600,
                    );
                  },
                ),
              ],
            ),
          ),
          const SizedBox(height: 20),

          const SectionHeader(title: 'Invite History'),
          for (final invite in referral.history) ...[
            AppCard(
              padding: const EdgeInsets.all(14),
              child: Row(
                children: [
                  AppAvatar(initials: invite.initials, size: AvatarSize.sm),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          invite.name,
                          style: AppText.bodySemi.copyWith(
                            color: palette.textPrimary,
                          ),
                        ),
                        Text(
                          formatDate(invite.date),
                          style: AppText.small.copyWith(
                            color: palette.textMuted,
                          ),
                        ),
                      ],
                    ),
                  ),
                  AppBadge(invite.status, tone: _tone(invite.status)),
                ],
              ),
            ),
            const SizedBox(height: 8),
          ],
        ],
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.label, required this.value, this.accent = false});

  final String label;
  final String value;
  final bool accent;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return AppCard(
      padding: const EdgeInsets.all(12),
      child: Column(
        children: [
          Text(
            value,
            style: AppText.h1.copyWith(
              color: accent ? palette.primaryOnDarkAware : palette.textPrimary,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            label,
            textAlign: TextAlign.center,
            style: AppText.small.copyWith(color: palette.textMuted),
          ),
        ],
      ),
    );
  }
}
