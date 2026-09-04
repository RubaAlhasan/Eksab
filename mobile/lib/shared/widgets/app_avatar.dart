import 'package:flutter/material.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';

enum AvatarSize { xs, sm, md, lg, xl }

extension on AvatarSize {
  double get dimension => switch (this) {
    AvatarSize.xs => 28,
    AvatarSize.sm => 36,
    AvatarSize.md => 44,
    AvatarSize.lg => 60,
    AvatarSize.xl => 80,
  };

  double get fontSize => switch (this) {
    AvatarSize.xs => 10,
    AvatarSize.sm => 12,
    AvatarSize.md => 14,
    AvatarSize.lg => 18,
    AvatarSize.xl => 24,
  };
}

/// `.avatar` — initials on the primary gradient, always circular.
class AppAvatar extends StatelessWidget {
  const AppAvatar({
    super.key,
    required this.initials,
    this.size = AvatarSize.md,
    this.onTap,
  });

  final String initials;
  final AvatarSize size;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final avatar = Container(
      width: size.dimension,
      height: size.dimension,
      alignment: Alignment.center,
      decoration: const BoxDecoration(
        shape: BoxShape.circle,
        gradient: LinearGradient(
          colors: [AppColors.primary500, AppColors.primary700],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
      ),
      child: Text(
        initials,
        style: TextStyle(
          color: Colors.white,
          fontSize: size.fontSize,
          fontWeight: FontWeight.w700,
          letterSpacing: -0.3,
        ),
      ),
    );

    if (onTap == null) return avatar;
    return GestureDetector(onTap: onTap, child: avatar);
  }
}

/// The rounded-square business tile ("CB", "PF", …) on its brand gradient.
/// Used everywhere a business appears in a list, card, or header.
class BusinessLogo extends StatelessWidget {
  const BusinessLogo({
    super.key,
    required this.initials,
    required this.gradient,
    this.size = 48,
    this.radius = 16,
    this.fontSize,
    this.border,
  });

  final String initials;
  final BrandGradient gradient;
  final double size;
  final double radius;
  final double? fontSize;
  final BoxBorder? border;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        gradient: gradient.gradient,
        borderRadius: BorderRadius.circular(radius),
        border: border,
      ),
      child: Text(
        initials,
        style: TextStyle(
          color: Colors.white,
          fontSize: fontSize ?? size * 0.3,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

/// Soft tinted square behind a small icon — the `w-9 h-9 rounded-xl bg-*-50`
/// pattern used in quick actions, transaction rows, and settings groups.
class IconTile extends StatelessWidget {
  const IconTile({
    super.key,
    required this.icon,
    required this.tone,
    this.size = 36,
    this.iconSize = 18,
  });

  final IconData icon;
  final Color tone;
  final double size;
  final double iconSize;

  @override
  Widget build(BuildContext context) {
    final isDark = AppPalette.of(context).isDark;
    return Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: tone.withValues(alpha: isDark ? 0.14 : 0.1),
        borderRadius: BorderRadius.circular(size * 0.33),
      ),
      child: Icon(icon, size: iconSize, color: tone),
    );
  }
}
