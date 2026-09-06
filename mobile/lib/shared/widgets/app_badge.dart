import 'package:flutter/material.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';

/// Tone shared by badges and alerts.
///
/// [reward] is the brand SECONDARY (magenta) and exists so that celebratory moments —
/// points earned, a reward unlocked, a tier-up, a referral paid out — have a sanctioned
/// colour of their own. Before it existed the only options were a status tone (which
/// means something else) or [primary] (which means "actionable"), so those moments were
/// borrowing colours that already had jobs.
///
/// It is NOT a second [primary]: a button stays [primary]. And it is not a status —
/// nothing about it means good/bad, so it never replaces [success] or [danger].
enum AppTone { primary, secondary, success, warning, danger, info, neutral }

/// Reads better at call sites than `AppTone.secondary` for what this tone is actually for.
const AppTone reward = AppTone.secondary;

extension AppToneColors on AppTone {
  Color background(bool isDark) => switch (this) {
    AppTone.primary => isDark ? AppColors.primary600.withValues(alpha: 0.18) : AppColors.primary100,
    AppTone.secondary => isDark ? AppColors.secondary600.withValues(alpha: 0.22) : AppColors.secondary100,
    AppTone.success => isDark ? AppColors.success500.withValues(alpha: 0.15) : AppColors.success100,
    AppTone.warning => isDark ? AppColors.warning500.withValues(alpha: 0.15) : AppColors.warning100,
    AppTone.danger => isDark ? AppColors.danger500.withValues(alpha: 0.15) : AppColors.danger100,
    AppTone.info => isDark ? AppColors.info500.withValues(alpha: 0.15) : AppColors.info100,
    AppTone.neutral => isDark ? AppColors.slate800 : AppColors.slate100,
  };

  Color foreground(bool isDark) => switch (this) {
    AppTone.primary => isDark ? AppColors.primary300 : AppColors.primary700,
    AppTone.secondary => isDark ? AppColors.secondary300 : AppColors.secondary800,
    AppTone.success => isDark ? AppColors.success300 : AppColors.success700,
    AppTone.warning => isDark ? AppColors.warning300 : AppColors.warning700,
    AppTone.danger => isDark ? AppColors.danger300 : AppColors.danger700,
    AppTone.info => isDark ? AppColors.info300 : AppColors.info700,
    AppTone.neutral => isDark ? AppColors.slate300 : AppColors.slate600,
  };

  /// Softer fill used by the alert component (`alert-*`).
  Color alertBackground(bool isDark) => switch (this) {
    AppTone.primary => isDark ? AppColors.primary600.withValues(alpha: 0.08) : AppColors.primary50,
    AppTone.secondary => isDark ? AppColors.secondary600.withValues(alpha: 0.10) : AppColors.secondary50,
    AppTone.success => isDark ? AppColors.success500.withValues(alpha: 0.08) : AppColors.success50,
    AppTone.warning => isDark ? AppColors.warning500.withValues(alpha: 0.08) : AppColors.warning50,
    AppTone.danger => isDark ? AppColors.danger500.withValues(alpha: 0.08) : AppColors.danger50,
    AppTone.info => isDark ? AppColors.info500.withValues(alpha: 0.08) : AppColors.info50,
    AppTone.neutral => isDark ? AppColors.slate900 : AppColors.slate50,
  };

  Color alertBorder(bool isDark) => switch (this) {
    AppTone.primary => isDark ? AppColors.primary600.withValues(alpha: 0.30) : AppColors.primary200,
    AppTone.secondary => isDark ? AppColors.secondary600.withValues(alpha: 0.30) : AppColors.secondary200,
    AppTone.success => isDark ? AppColors.success500.withValues(alpha: 0.30) : AppColors.success200,
    AppTone.warning => isDark ? AppColors.warning500.withValues(alpha: 0.30) : AppColors.warning200,
    AppTone.danger => isDark ? AppColors.danger500.withValues(alpha: 0.30) : AppColors.danger200,
    AppTone.info => isDark ? AppColors.info500.withValues(alpha: 0.30) : AppColors.info200,
    AppTone.neutral => isDark ? AppColors.slate700 : AppColors.slate200,
  };

  IconData get icon => switch (this) {
    AppTone.primary => Icons.info_outline_rounded,
    AppTone.secondary => Icons.auto_awesome_rounded,
    AppTone.success => Icons.check_circle_outline_rounded,
    AppTone.warning => Icons.warning_amber_rounded,
    AppTone.danger => Icons.error_outline_rounded,
    AppTone.info => Icons.info_outline_rounded,
    AppTone.neutral => Icons.info_outline_rounded,
  };
}

/// `.badge` — pill label, 11px bold uppercase-ish, tone-tinted.
class AppBadge extends StatelessWidget {
  const AppBadge(this.label, {super.key, this.tone = AppTone.neutral, this.dot = false});

  final String label;
  final AppTone tone;
  final bool dot;

  @override
  Widget build(BuildContext context) {
    final isDark = AppPalette.of(context).isDark;
    final fg = tone.foreground(isDark);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: tone.background(isDark),
        borderRadius: AppRadius.rPill,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (dot) ...[
            Container(
              width: 6,
              height: 6,
              decoration: BoxDecoration(color: fg, shape: BoxShape.circle),
            ),
            const SizedBox(width: 5),
          ],
          Text(
            label,
            style: AppText.tiny.copyWith(
              color: fg,
              fontWeight: FontWeight.w700,
              letterSpacing: 0.2,
            ),
          ),
        ],
      ),
    );
  }
}

/// `.alert` — icon + message block used for inline form and info messaging.
class AppAlert extends StatelessWidget {
  const AppAlert({
    super.key,
    required this.message,
    this.tone = AppTone.info,
    this.icon,
  });

  final String message;
  final AppTone tone;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final isDark = AppPalette.of(context).isDark;
    final fg = tone.foreground(isDark);
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: tone.alertBackground(isDark),
        border: Border.all(color: tone.alertBorder(isDark)),
        borderRadius: const BorderRadius.all(Radius.circular(14)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon ?? tone.icon, size: 18, color: fg),
          const SizedBox(width: 12),
          Expanded(
            child: Text(message, style: AppText.small.copyWith(color: fg)),
          ),
        ],
      ),
    );
  }
}
