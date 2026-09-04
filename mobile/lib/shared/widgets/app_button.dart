import 'package:flutter/material.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';

enum AppButtonVariant { primary, secondary, outline, ghost, danger, success }

enum AppButtonSize { sm, md, lg }

/// `.btn` and its variants. Handles the prototype's inline "submitting" state
/// (spinner + label swap) via [loading], which also disables the button.
class AppButton extends StatelessWidget {
  const AppButton({
    super.key,
    required this.label,
    this.onPressed,
    this.variant = AppButtonVariant.primary,
    this.size = AppButtonSize.md,
    this.icon,
    this.expand = false,
    this.loading = false,
    this.loadingLabel,
    this.foregroundOverride,
  });

  final String label;
  final VoidCallback? onPressed;
  final AppButtonVariant variant;
  final AppButtonSize size;
  final IconData? icon;
  final bool expand;
  final bool loading;
  final String? loadingLabel;

  /// Used by the "Log Out" button, which is a secondary button with danger text.
  final Color? foregroundOverride;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final isDark = palette.isDark;
    final enabled = onPressed != null && !loading;

    final (Color bg, Color fg, Color? borderColor) = switch (variant) {
      AppButtonVariant.primary => (AppColors.primary600, Colors.white, null),
      AppButtonVariant.secondary => (
        isDark ? AppColors.slate800 : Colors.white,
        isDark ? const Color(0xFFF1F5F9) : AppColors.slate800,
        isDark ? AppColors.slate700 : AppColors.slate200,
      ),
      AppButtonVariant.outline => (
        Colors.transparent,
        isDark ? AppColors.primary300 : AppColors.primary600,
        isDark ? AppColors.primary800 : AppColors.primary200,
      ),
      AppButtonVariant.ghost => (
        Colors.transparent,
        isDark ? AppColors.slate300 : AppColors.slate600,
        null,
      ),
      AppButtonVariant.danger => (AppColors.danger600, Colors.white, null),
      AppButtonVariant.success => (AppColors.success600, Colors.white, null),
    };

    final (EdgeInsets padding, TextStyle textStyle) = switch (size) {
      AppButtonSize.sm => (
        const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
        AppText.smallBold.copyWith(fontSize: 13),
      ),
      AppButtonSize.md => (
        const EdgeInsets.symmetric(horizontal: 18, vertical: 11),
        AppText.bodySemi,
      ),
      AppButtonSize.lg => (
        const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
        AppText.bodySemi.copyWith(fontSize: 15),
      ),
    };

    final effectiveFg = foregroundOverride ?? fg;
    final elevated =
        variant == AppButtonVariant.primary ||
        variant == AppButtonVariant.danger ||
        variant == AppButtonVariant.success ||
        variant == AppButtonVariant.secondary;

    final content = Row(
      mainAxisSize: expand ? MainAxisSize.max : MainAxisSize.min,
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        if (loading)
          Padding(
            padding: const EdgeInsets.only(right: 8),
            child: SizedBox(
              width: 16,
              height: 16,
              child: CircularProgressIndicator(
                strokeWidth: 2.5,
                valueColor: AlwaysStoppedAnimation(effectiveFg),
              ),
            ),
          )
        else if (icon != null)
          Padding(
            padding: const EdgeInsets.only(right: 8),
            child: Icon(icon, size: 18, color: effectiveFg),
          ),
        Flexible(
          child: Text(
            loading ? (loadingLabel ?? label) : label,
            style: textStyle.copyWith(color: effectiveFg),
            overflow: TextOverflow.ellipsis,
          ),
        ),
      ],
    );

    return Opacity(
      opacity: enabled ? 1 : 0.5,
      child: Material(
        color: bg,
        borderRadius: AppRadius.rMd,
        elevation: 0,
        child: Ink(
          decoration: BoxDecoration(
            color: bg,
            borderRadius: AppRadius.rMd,
            border: borderColor == null ? null : Border.all(color: borderColor),
            boxShadow: elevated && enabled ? AppShadows.soft(isDark) : null,
          ),
          child: InkWell(
            onTap: enabled ? onPressed : null,
            borderRadius: AppRadius.rMd,
            child: Padding(padding: padding, child: content),
          ),
        ),
      ),
    );
  }
}

/// `.btn-icon` — square icon-only button, used in top bars and rows.
class AppIconButton extends StatelessWidget {
  const AppIconButton({
    super.key,
    required this.icon,
    this.onPressed,
    this.variant = AppButtonVariant.ghost,
    this.size = 18,
    this.tooltip,
    this.foreground,
    this.background,
  });

  final IconData icon;
  final VoidCallback? onPressed;
  final AppButtonVariant variant;
  final double size;
  final String? tooltip;
  final Color? foreground;
  final Color? background;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final isDark = palette.isDark;

    final (Color bg, Color fg, Color? borderColor) = switch (variant) {
      AppButtonVariant.secondary => (
        isDark ? AppColors.slate800 : Colors.white,
        isDark ? const Color(0xFFF1F5F9) : AppColors.slate800,
        isDark ? AppColors.slate700 : AppColors.slate200,
      ),
      _ => (
        Colors.transparent,
        isDark ? AppColors.slate300 : AppColors.slate600,
        null,
      ),
    };

    final button = Material(
      color: background ?? bg,
      borderRadius: AppRadius.rMd,
      child: Ink(
        decoration: BoxDecoration(
          color: background ?? bg,
          borderRadius: AppRadius.rMd,
          border: borderColor == null ? null : Border.all(color: borderColor),
        ),
        child: InkWell(
          onTap: onPressed,
          borderRadius: AppRadius.rMd,
          child: Padding(
            padding: const EdgeInsets.all(9),
            child: Icon(icon, size: size, color: foreground ?? fg),
          ),
        ),
      ),
    );

    return tooltip == null ? button : Tooltip(message: tooltip!, child: button);
  }
}
