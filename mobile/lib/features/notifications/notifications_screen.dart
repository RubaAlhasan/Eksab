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
class NotificationsScreen extends ConsumerWidget {
  const NotificationsScreen({super.key});

  static IconData _channelIcon(NotificationChannel channel) => switch (channel) {
    NotificationChannel.push => Icons.notifications_none_rounded,
    NotificationChannel.email => Icons.mail_outline_rounded,
    NotificationChannel.sms => Icons.sms_outlined,
    NotificationChannel.inApp => Icons.auto_awesome_rounded,
  };

  static String _channelLabel(NotificationChannel channel) => switch (channel) {
    NotificationChannel.push => 'Push',
    NotificationChannel.email => 'Email',
    NotificationChannel.sms => 'SMS',
    NotificationChannel.inApp => 'In-app',
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
                    onTap: () {
                      ref.read(notificationsProvider.notifier).markAllRead();
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
              child: notifications.isEmpty
                  ? const EmptyState(
                      icon: Icons.notifications_none_rounded,
                      title: "You're all caught up",
                      message:
                          'New notifications from your businesses will appear '
                          'here.',
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
                      itemCount: notifications.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 8),
                      itemBuilder: (context, i) {
                        final notification = notifications[i];
                        final business = ref.watch(
                          businessByIdProvider(notification.businessId),
                        );

                        return AppCard(
                          color: notification.read
                              ? null
                              : (palette.isDark
                                    ? const Color(0x0D6248E3)
                                    : const Color(0x80F4F3FF)),
                          border: notification.read
                              ? null
                              : Border.all(
                                  color: palette.isDark
                                      ? const Color(0x336248E3)
                                      : AppColors.primary100,
                                ),
                          onTap: () => ref
                              .read(notificationsProvider.notifier)
                              .markRead(notification.id),
                          child: Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              if (business != null)
                                BusinessLogo(
                                  initials: business.initials,
                                  gradient: business.gradient,
                                  size: 36,
                                  radius: 12,
                                ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Row(
                                      children: [
                                        Expanded(
                                          child: Text(
                                            notification.title,
                                            overflow: TextOverflow.ellipsis,
                                            style: AppText.bodyBold.copyWith(
                                              color: palette.textPrimary,
                                            ),
                                          ),
                                        ),
                                        if (!notification.read)
                                          Container(
                                            width: 8,
                                            height: 8,
                                            margin: const EdgeInsets.only(
                                              left: 8,
                                              top: 4,
                                            ),
                                            decoration: const BoxDecoration(
                                              color: AppColors.primary600,
                                              shape: BoxShape.circle,
                                            ),
                                          ),
                                      ],
                                    ),
                                    const SizedBox(height: 2),
                                    Text(
                                      notification.body,
                                      style: AppText.small.copyWith(
                                        color: palette.textSecondary,
                                      ),
                                    ),
                                    const SizedBox(height: 6),
                                    Row(
                                      children: [
                                        Icon(
                                          _channelIcon(notification.channel),
                                          size: 12,
                                          color: palette.textMuted,
                                        ),
                                        const SizedBox(width: 6),
                                        Text(
                                          '${_channelLabel(notification.channel)}'
                                          ' · '
                                          '${formatDate(notification.sentAt)}',
                                          style: AppText.tiny.copyWith(
                                            color: palette.textMuted,
                                          ),
                                        ),
                                      ],
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
          ],
        ),
      ),
    );
  }
}
