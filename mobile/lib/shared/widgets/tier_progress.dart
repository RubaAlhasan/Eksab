import 'package:flutter/material.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../models/models.dart';
import 'business_tiles.dart' show formatPoints;

/// "Gold — 600 points to Platinum", with a bar.
///
/// This is the one thing a loyalty app has that a balance screen does not: a visible reason to come
/// back. Without it the app answers "how many points do I have" and stops there.
///
/// Renders nothing at all when the business defines no tiers, or when the customer is already on the
/// top rung and there is nothing left to climb — an empty or full bar with no target is noise. Callers
/// can therefore drop this in unconditionally.
class TierProgress extends StatelessWidget {
  const TierProgress({
    super.key,
    required this.membership,
    this.compact = false,
  });

  final Membership membership;

  /// Drops the tier labels and shrinks the bar, for use inside a small card.
  final bool compact;

  @override
  Widget build(BuildContext context) {
    if (!membership.hasTierProgress) return const SizedBox.shrink();

    final palette = AppPalette.of(context);
    final remaining = membership.pointsToNextTier;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (!compact) ...[
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                membership.tier!,
                style: AppText.smallBold.copyWith(color: palette.textPrimary),
              ),
              Text(
                membership.nextTier!,
                style: AppText.small.copyWith(color: palette.textMuted),
              ),
            ],
          ),
          const SizedBox(height: 6),
        ],

        ClipRRect(
          borderRadius: BorderRadius.circular(999),
          child: LinearProgressIndicator(
            value: membership.tierProgress,
            minHeight: compact ? 4 : 6,
            backgroundColor: palette.isDark
                ? AppColors.slate700
                : AppColors.slate200,
            valueColor: const AlwaysStoppedAnimation(AppColors.primary500),
          ),
        ),
        SizedBox(height: compact ? 4 : 6),

        Text(
          // Phrased as the remaining effort, not the total requirement: "600 points to Platinum"
          // is something a customer can act on, "2,600 / 5,000" is arithmetic homework.
          '${formatPoints(remaining)} points to ${membership.nextTier}',
          style: AppText.small.copyWith(color: palette.textMuted),
        ),
      ],
    );
  }
}
