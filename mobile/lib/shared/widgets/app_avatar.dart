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
    this.logoUrl,
    this.size = 48,
    this.radius = 16,
    this.fontSize,
    this.border,
  });

  final String initials;
  final BrandGradient gradient;

  /// The business's real logo. Null for a business that has not uploaded one, which is when the
  /// generated initials-on-gradient below is the intended presentation rather than a placeholder.
  final String? logoUrl;

  final double size;
  final double radius;
  final double? fontSize;
  final BoxBorder? border;

  @override
  Widget build(BuildContext context) {
    final url = logoUrl;

    return Container(
      width: size,
      height: size,
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        // A real logo sits on a neutral card, not on the generated brand gradient. Logos are rarely
        // square (the seeded one is 108x115), so `contain` leaves bars either side — filling those
        // with an unrelated colour picked by hashing the tenant id reads as an accident, and fights
        // whatever palette the business actually uses. White is what wallet apps use for the same
        // reason, and it is what a transparent PNG needs behind it.
        color: url == null ? null : Colors.white,
        gradient: url == null ? gradient.gradient : null,
        borderRadius: BorderRadius.circular(radius),
        border: border,
      ),
      child: url == null
          ? _initials()
          : Padding(
              // Breathing room so a logo with no built-in margin does not run into the corners.
              padding: EdgeInsets.all(size * 0.12),
              child: Image.network(
                url,
                fit: BoxFit.contain,
                // A broken or unreachable image degrades to exactly what this widget drew before
                // logos existed, rather than to a broken-image glyph.
                errorBuilder: (_, _, _) => _initials(),
                loadingBuilder: (context, child, progress) =>
                    progress == null ? child : _initials(),
              ),
            ),
    );
  }

  /// Generated fallback. Paints its own gradient so it still looks right inside the neutral
  /// container used when a logo was expected but failed to load.
  Widget _initials() => DecoratedBox(
    decoration: BoxDecoration(gradient: gradient.gradient),
    child: Center(
      child: Text(
        initials,
        style: TextStyle(
          color: Colors.white,
          fontSize: fontSize ?? size * 0.3,
          fontWeight: FontWeight.w700,
        ),
      ),
    ),
  );
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
