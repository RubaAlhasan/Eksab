import 'package:flutter/material.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';

/// Tone shared by badges and alerts — matches the prototype's
/// `badge-primary / -success / -warning / -danger / -info / -neutral`.
enum AppTone { primary, success, warning, danger, info, neutral }

extension AppToneColors on AppTone {
  Color background(bool isDark) => switch (this) {
    AppTone.primary => isDark ? const Color(0x2E6248E3) : AppColors.primary100,
    AppTone.success => isDark ? const Color(0x2610B981) : AppColors.success100,
    AppTone.warning => isDark ? const Color(0x26F59E0B) : AppColors.warning100,
    AppTone.danger => isDark ? const Color(0x26EF4444) : AppColors.danger100,
    AppTone.info => isDark ? const Color(0x260EA5E9) : AppColors.info100,
    AppTone.neutral => isDark ? AppColors.slate800 : AppColors.slate100,
  };

  Color foreground(bool isDark) => switch (this) {
    AppTone.primary => isDark ? AppColors.primary300 : AppColors.primary700,
    AppTone.success => isDark ? AppColors.success300 : AppColors.success700,
    AppTone.warning => isDark ? AppColors.warning300 : AppColors.warning700,
    AppTone.danger => isDark ? AppColors.danger300 : AppColors.danger700,
    AppTone.info => isDark ? AppColors.info300 : AppColors.info700,
    AppTone.neutral => isDark ? AppColors.slate300 : AppColors.slate600,
  };

  /// Softer fill used by the alert component (`alert-*`).
  Color alertBackground(bool isDark) => switch (this) {
    AppTone.primary => isDark ? const Color(0x146248E3) : AppColors.primary50,
    AppTone.success => isDark ? const Color(0x1410B981) : AppColors.success50,
    AppTone.warning => isDark ? const Color(0x14F59E0B) : AppColors.warning50,
    AppTone.danger => isDark ? const Color(0x14EF4444) : AppColors.danger50,
    AppTone.info => isDark ? const Color(0x140EA5E9) : AppColors.info50,
    AppTone.neutral => isDark ? AppColors.slate900 : AppColors.slate50,
  };

  Color alertBorder(bool isDark) => switch (this) {
    AppTone.primary => isDark ? const Color(0x4D6248E3) : AppColors.primary200,
    AppTone.success => isDark ? const Color(0x4D10B981) : AppColors.success200,
    AppTone.warning => isDark ? const Color(0x4DF59E0B) : AppColors.warning200,
    AppTone.danger => isDark ? const Color(0x4DEF4444) : AppColors.danger200,
    AppTone.info => isDark ? const Color(0x4D0EA5E9) : AppColors.info200,
    AppTone.neutral => isDark ? AppColors.slate700 : AppColors.slate200,
  };

  IconData get icon => switch (this) {
    AppTone.primary => Icons.info_outline_rounded,
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
