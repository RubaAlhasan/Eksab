import 'package:flutter/material.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';

/// The prototype's `.card`: white/slate-900 surface, 1px border, 16px radius,
/// `--shadow-soft`. Pass [onTap] for the `.card-interactive` variant.
class AppCard extends StatelessWidget {
  const AppCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.onTap,
    this.gradient,
    this.color,
    this.borderRadius = AppRadius.rLg,
    this.clipContent = false,
    this.border,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final VoidCallback? onTap;

  /// Set for the accent cards (wallet total, referral hero, campaign banners),
  /// which drop the border and render white-on-gradient content.
  final Gradient? gradient;
  final Color? color;
  final BorderRadius borderRadius;
  final bool clipContent;
  final BoxBorder? border;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final hasAccent = gradient != null || color != null;

    final decorated = Container(
      decoration: BoxDecoration(
        color: gradient == null ? (color ?? palette.surface) : null,
        gradient: gradient,
        borderRadius: borderRadius,
        border:
            border ??
            (hasAccent ? null : Border.all(color: palette.border)),
        boxShadow: AppShadows.soft(palette.isDark),
      ),
      clipBehavior: clipContent ? Clip.antiAlias : Clip.none,
      child: clipContent ? child : Padding(padding: padding, child: child),
    );

    if (onTap == null) return decorated;

    return Material(
      color: Colors.transparent,
      borderRadius: borderRadius,
      child: InkWell(
        onTap: onTap,
        borderRadius: borderRadius,
        child: decorated,
      ),
    );
  }
}

/// A `.card` used as a settings/profile group: rows separated by hairlines.
class AppCardList extends StatelessWidget {
  const AppCardList({super.key, required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final rows = <Widget>[];
    for (var i = 0; i < children.length; i++) {
      if (i > 0) {
        rows.add(Divider(height: 1, thickness: 1, color: palette.borderSubtle));
      }
      rows.add(children[i]);
    }
    return AppCard(
      padding: EdgeInsets.zero,
      clipContent: true,
      child: Column(mainAxisSize: MainAxisSize.min, children: rows),
    );
  }
}

/// A tappable row inside an [AppCardList] — icon, label, trailing chevron.
class AppListRow extends StatelessWidget {
  const AppListRow({
    super.key,
    required this.label,
    this.icon,
    this.onTap,
    this.trailing,
    this.labelColor,
    this.iconColor,
  });

  final String label;
  final IconData? icon;
  final VoidCallback? onTap;
  final Widget? trailing;
  final Color? labelColor;
  final Color? iconColor;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        child: Row(
          children: [
            if (icon != null) ...[
              Icon(icon, size: 18, color: iconColor ?? palette.textMuted),
              const SizedBox(width: 12),
            ],
            Expanded(
              child: Text(
                label,
                style: AppText.bodySemi.copyWith(
                  color: labelColor ?? palette.textPrimary,
                ),
              ),
            ),
            trailing ??
                (onTap == null
                    ? const SizedBox.shrink()
                    : Icon(
                        Icons.chevron_right_rounded,
                        size: 20,
                        color: palette.isDark
                            ? AppColors.slate600
                            : AppColors.slate300,
                      )),
          ],
        ),
      ),
    );
  }
}
