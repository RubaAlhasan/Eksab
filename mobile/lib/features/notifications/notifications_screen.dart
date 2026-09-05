import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/notifications.html` — unread rows are tinted, tapping
/// one marks it read, and "Mark all read" clears the tab badge.
/// Prototype: `customer/notifications.html`.
///
/// `UserNotificationDto` carries no tenant reference, so a notification cannot
/// be attributed to a business — the row shows a tone-coloured icon instead of
/// a business logo.
class NotificationsScreen extends ConsumerWidget {
  const NotificationsScreen({super.key});

  static ({IconData icon, Color tone}) _visual(NotificationTone tone) =>
      switch (tone) {
        NotificationTone.success => (
          icon: Icons.check_circle_outline_rounded,
          tone: AppColors.success600,
        ),
        NotificationTone.warning => (
          icon: Icons.warning_amber_rounded,
          tone: AppColors.warning600,
        ),
        NotificationTone.error => (
          icon: Icons.error_outline_rounded,
          tone: AppColors.danger600,
        ),
        NotificationTone.info => (
          icon: Icons.notifications_none_rounded,
          tone: AppColors.primary600,
        ),
      };

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final notifications = ref.watch(notificationsProvider);

    return Scaffold(
      backgroundColor: palette.scaffold,
      body: SafeArea(
        bottom: false,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 8, 20, 12),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    'Notifications',
                    style: AppText.h1.copyWith(color: palette.textPrimary),
                  ),
                  GestureDetector(
                    onTap: () async {
                      await ref
                          .read(notificationsProvider.notifier)
                          .markAllRead();
                      if (!context.mounted) return;
                      showAppToast(
                        context,
                        title: 'All notifications marked as read',
                      );
                    },
                    behavior: HitTestBehavior.opaque,
                    child: Text(
                      'Mark all read',
                      style: AppText.smallSemi.copyWith(
                        color: palette.primaryOnDarkAware,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            Expanded(
              child: AsyncSection<List<AppNotification>>(
                value: notifications,
                onRetry: () => ref.invalidate(notificationsProvider),
                data: (list) => list.isEmpty
                    ? const EmptyState(
                        icon: Icons.notifications_none_rounded,
                        title: "You're all caught up",
                        message:
                            'New notifications from your businesses will '
                            'appear here.',
                      )
                    : RefreshIndicator(
                        onRefresh: () async {
                          ref.invalidate(notificationsProvider);
                          await ref.read(notificationsProvider.future);
                        },
                        child: ListView.separated(
                          padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
                          itemCount: list.length,
                          separatorBuilder: (_, _) => const SizedBox(height: 8),
                          itemBuilder: (context, i) {
                            final n = list[i];
                            final visual = _visual(n.tone);

                            return AppCard(
                              color: n.read
                                  ? null
                                  : (palette.isDark
                                        ? const Color(0x0D6248E3)
                                        : const Color(0x80F4F3FF)),
                              border: n.read
                                  ? null
                                  : Border.all(
                                      color: palette.isDark
                                          ? const Color(0x336248E3)
                                          : AppColors.primary100,
                                    ),
                              onTap: n.read
                                  ? null
                                  : () => ref
                                        .read(notificationsProvider.notifier)
                                        .markRead(n.id),
                              child: Row(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  IconTile(
                                    icon: visual.icon,
                                    tone: visual.tone,
                                  ),
                                  const SizedBox(width: 12),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      mainAxisSize: MainAxisSize.min,
                                      children: [
                                        Row(
                                          children: [
                                            Expanded(
                                              child: Text(
                                                n.title,
                                                overflow:
                                                    TextOverflow.ellipsis,
                                                style: AppText.bodyBold
                                                    .copyWith(
                                                      color:
                                                          palette.textPrimary,
                                                    ),
                                              ),
                                            ),
                                            if (!n.read)
                                              Container(
                                                width: 8,
                                                height: 8,
                                                margin: const EdgeInsets.only(
                                                  left: 8,
                                                  top: 4,
                                                ),
                                                decoration:
                                                    const BoxDecoration(
                                                      color: AppColors
                                                          .primary600,
                                                      shape: BoxShape.circle,
                                                    ),
                                              ),
                                          ],
                                        ),
                                        const SizedBox(height: 2),
                                        Text(
                                          n.body,
                                          style: AppText.small.copyWith(
                                            color: palette.textSecondary,
                                          ),
                                        ),
                                        const SizedBox(height: 6),
                                        Text(
                                          n.category == null
                                              ? formatDate(n.sentAt)
                                              : '${n.category} · '
                                                    '${formatDate(n.sentAt)}',
                                          style: AppText.tiny.copyWith(
                                            color: palette.textMuted,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                ],
                              ),
                            );
                          },
                        ),
                      ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
