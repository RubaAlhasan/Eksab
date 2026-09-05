import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/referral.html`.
///
/// `ReferralDto` exposes no referee name, and there is no "points earned from
/// referrals" figure — so the history shows status and date, and the stats are
/// counts derived from the list rather than an invented points total.
class ReferralScreen extends ConsumerWidget {
  const ReferralScreen({super.key});

  static AppTone _tone(ReferralStatus status) => switch (status) {
    ReferralStatus.rewarded => AppTone.success,
    ReferralStatus.completed => AppTone.info,
    ReferralStatus.pending => AppTone.warning,
  };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final referral = ref.watch(referralProvider);

    return AppScaffold(
      title: 'Invite Friends',
      onBack: () =>
          context.canPop() ? context.pop() : context.go(Routes.profile),
      body: AsyncSection<ReferralProgram>(
        value: referral,
        onRetry: () => ref.invalidate(referralProvider),
        data: (program) => ListView(
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
                    'Invite a friend — you both earn bonus points when they '
                    'join their first business.',
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
                  child: _Stat(label: 'Invited', value: '${program.invited}'),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _Stat(label: 'Joined', value: '${program.joined}'),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: _Stat(
                    label: 'Rewarded',
                    value: '${program.rewarded}',
                    accent: true,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 20),

            // One code per joined business — `referral/my-code` is scoped by
            // tenant and returns that membership's id, so there is no single
            // account-wide code to show.
            if (program.codes.isEmpty)
              const EmptyState(
                icon: Icons.qr_code_2_rounded,
                title: 'No referral codes yet',
                message: 'Join a business to get a code you can share.',
              )
            else
              for (final entry in program.codes) ...[
                AppCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        entry.business.name,
                        overflow: TextOverflow.ellipsis,
                        style: AppText.bodyBold.copyWith(
                          color: palette.textPrimary,
                        ),
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
                                entry.code,
                                overflow: TextOverflow.ellipsis,
                                style: AppText.smallSemi.copyWith(
                                  color: palette.textPrimary,
                                  fontFeatures: const [
                                    FontFeature.tabularFigures(),
                                  ],
                                ),
                              ),
                            ),
                            AppIconButton(
                              icon: Icons.copy_rounded,
                              tooltip: 'Copy code',
                              onPressed: () async {
                                await Clipboard.setData(
                                  ClipboardData(text: entry.code),
                                );
                                if (!context.mounted) return;
                                showAppToast(context, title: 'Code copied');
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
                            ClipboardData(text: entry.link),
                          );
                          if (!context.mounted) return;
                          showAppToast(
                            context,
                            title: 'Invite link copied',
                            icon: Icons.info_outline_rounded,
                            accent: AppColors.info600,
                          );
                        },
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
              ],
            const SizedBox(height: 20),

            const SectionHeader(title: 'Invite History'),
            if (program.history.isEmpty)
              const EmptyState(
                icon: Icons.group_add_outlined,
                title: 'No invites yet',
                message: 'Share your code to get started.',
              )
            else
              for (final invite in program.history) ...[
                AppCard(
                  padding: const EdgeInsets.all(14),
                  child: Row(
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(
                              'Invite',
                              style: AppText.bodySemi.copyWith(
                                color: palette.textPrimary,
                              ),
                            ),
                            if (invite.date != null)
                              Text(
                                formatDate(invite.date!),
                                style: AppText.small.copyWith(
                                  color: palette.textMuted,
                                ),
                              ),
                          ],
                        ),
                      ),
                      AppBadge(
                        invite.status.label,
                        tone: _tone(invite.status),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 8),
              ],
          ],
        ),
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
