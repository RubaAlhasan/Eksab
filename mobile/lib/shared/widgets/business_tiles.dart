import 'package:flutter/material.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../models/models.dart';
import 'app_avatar.dart';
import 'app_badge.dart';
import 'app_card.dart';

/// Row used by Nearby Stores, Search results, Favorites, and My Memberships:
/// logo, name (+ optional badge), meta line, optional trailing widget.
class BusinessRow extends StatelessWidget {
  const BusinessRow({
    super.key,
    required this.business,
    this.onTap,
    this.meta,
    this.showRating = false,
    this.trailing,
    this.logoSize = 48,
  });

  final Business business;
  final VoidCallback? onTap;
  final String? meta;
  final bool showRating;
  final Widget? trailing;
  final double logoSize;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return AppCard(
      onTap: onTap,
      child: Row(
        children: [
          BusinessLogo(
            initials: business.initials,
            gradient: business.gradient,
            size: logoSize,
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  children: [
                    Flexible(
                      child: Text(
                        business.name,
                        overflow: TextOverflow.ellipsis,
                        style: AppText.bodyBold.copyWith(
                          color: palette.textPrimary,
                        ),
                      ),
                    ),
                    if (business.member) ...[
                      const SizedBox(width: 8),
                      const AppBadge('Member', tone: AppTone.primary),
                    ],
                  ],
                ),
                const SizedBox(height: 2),
                Text(
                  meta ??
                      '${business.category} · ${business.branches} branches',
                  overflow: TextOverflow.ellipsis,
                  style: AppText.small.copyWith(color: palette.textMuted),
                ),
                // No rating exists server-side. Show what does: branch
                // count, and distance when the directory supplied it.
                if (showRating) ...[
                  const SizedBox(height: 6),
                  Text(
                    business.distanceKm == null
                        ? '${business.branches} '
                              '${business.branches == 1 ? 'branch' : 'branches'}'
                        : '${business.branches} · '
                              '${business.distanceKm!.toStringAsFixed(1)} km away',
                    style: AppText.small.copyWith(color: palette.textSecondary),
                  ),
                ],
              ],
            ),
          ),
          if (trailing != null) ...[const SizedBox(width: 8), trailing!],
        ],
      ),
    );
  }
}

/// A wallet entry: business, balance, tier badge, and tier progress bar.
class WalletRow extends StatelessWidget {
  const WalletRow({
    super.key,
    required this.business,
    required this.membership,
    this.onTap,
  });

  final Business business;
  final Membership membership;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return AppCard(
      onTap: onTap,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          BusinessLogo(
            initials: business.initials,
            gradient: business.gradient,
            size: 48,
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        business.name,
                        overflow: TextOverflow.ellipsis,
                        style: AppText.bodyBold.copyWith(
                          color: palette.textPrimary,
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Text(
                      formatPoints(membership.balance),
                      style: AppText.bodyBold.copyWith(
                        color: palette.primaryOnDarkAware,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 6),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    // Tier thresholds are not exposed to customers, so there is
                    // no "x pts to next tier" to show — only the current tier.
                    if (membership.tier != null)
                      AppBadge(membership.tier!)
                    else
                      const SizedBox.shrink(),
                    Text(
                      '${formatPoints(membership.lifetimeEarned)} earned',
                      style: AppText.small.copyWith(color: palette.textMuted),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Transaction row shared by My Points and Transaction History.
class TransactionRow extends StatelessWidget {
  const TransactionRow({
    super.key,
    required this.transaction,
    this.showType = false,
  });

  final PointTransaction transaction;

  /// History shows `Earn · Aug 4, 2026`; My Points shows just `Aug 4`.
  final bool showType;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final meta = _meta(transaction.type);

    return AppCard(
      padding: const EdgeInsets.all(14),
      child: Row(
        children: [
          IconTile(icon: meta.icon, tone: meta.color, iconSize: 16),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  transaction.description,
                  overflow: TextOverflow.ellipsis,
                  style: AppText.bodySemi.copyWith(color: palette.textPrimary),
                ),
                const SizedBox(height: 2),
                Text(
                  showType
                      ? '${meta.label} · '
                            '${formatDate(transaction.date, withYear: true)}'
                      : formatDate(transaction.date),
                  style: AppText.small.copyWith(color: palette.textMuted),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Text(
            '${transaction.isCredit ? '+' : ''}${transaction.points}',
            style: AppText.bodyBold.copyWith(
              color: transaction.isCredit
                  ? (palette.isDark
                        ? AppColors.success300
                        : AppColors.success600)
                  : (palette.isDark ? AppColors.danger300 : AppColors.danger600),
            ),
          ),
        ],
      ),
    );
  }

  static ({IconData icon, Color color, String label}) _meta(
    TransactionType type,
  ) => switch (type) {
    TransactionType.earn => (
      icon: Icons.add_rounded,
      color: AppColors.success600,
      label: 'Earn',
    ),
    TransactionType.redeem => (
      icon: Icons.remove_rounded,
      color: AppColors.danger600,
      label: 'Redeem',
    ),
    TransactionType.adjust => (
      icon: Icons.edit_outlined,
      color: AppColors.info600,
      label: 'Adjust',
    ),
    TransactionType.expire => (
      icon: Icons.schedule_rounded,
      color: AppColors.slate500,
      label: 'Expire',
    ),
    TransactionType.refund => (
      icon: Icons.refresh_rounded,
      color: AppColors.warning600,
      label: 'Refund',
    ),
  };
}

// ---------------------------------------------------------------------------
// Formatting helpers
// ---------------------------------------------------------------------------

const _months = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
];

const _monthsLong = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];

/// `Aug 4` / `Aug 4, 2026` — matches the prototype's `toLocaleDateString` use.
String formatDate(DateTime date, {bool withYear = false}) {
  final base = '${_months[date.month - 1]} ${date.day}';
  return withYear ? '$base, ${date.year}' : base;
}

/// `August 4, 2026`, used on reward terms.
String formatDateLong(DateTime date) =>
    '${_monthsLong[date.month - 1]} ${date.day}, ${date.year}';

/// `Aug 2026`, used for "Member since".
String formatMonthYear(DateTime date) =>
    '${_months[date.month - 1]} ${date.year}';

/// Thousands separator, mirroring JS `Number.toLocaleString()`.
String formatPoints(int value) {
  final digits = value.abs().toString();
  final buffer = StringBuffer(value < 0 ? '-' : '');
  for (var i = 0; i < digits.length; i++) {
    if (i > 0 && (digits.length - i) % 3 == 0) buffer.write(',');
    buffer.write(digits[i]);
  }
  return buffer.toString();
}
