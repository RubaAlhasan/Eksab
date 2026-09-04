import 'package:flutter/material.dart';

import 'app_colors.dart';

/// Spacing / radius / elevation tokens mirroring the prototype's Tailwind scale.
abstract final class AppRadius {
  static const sm = Radius.circular(8);
  static const md = Radius.circular(12); // rounded-xl
  static const lg = Radius.circular(16); // rounded-2xl
  static const xl = Radius.circular(24); // rounded-3xl
  static const xxl = Radius.circular(32); // rounded-4xl
  static const pill = Radius.circular(999);

  static const rSm = BorderRadius.all(sm);
  static const rMd = BorderRadius.all(md);
  static const rLg = BorderRadius.all(lg);
  static const rXl = BorderRadius.all(xl);
  static const rXxl = BorderRadius.all(xxl);
  static const rPill = BorderRadius.all(pill);
}

/// `--shadow-soft` / `--shadow-soft-lg` from the prototype design system.
abstract final class AppShadows {
  static List<BoxShadow> soft(bool isDark) => isDark
      ? const [
          BoxShadow(color: Color(0x4D000000), blurRadius: 2, offset: Offset(0, 1)),
          BoxShadow(
            color: Color(0x80000000),
            blurRadius: 24,
            spreadRadius: -8,
            offset: Offset(0, 8),
          ),
        ]
      : const [
          BoxShadow(color: Color(0x0A0F172A), blurRadius: 2, offset: Offset(0, 1)),
          BoxShadow(
            color: Color(0x1A0F172A),
            blurRadius: 24,
            spreadRadius: -8,
            offset: Offset(0, 8),
          ),
        ];

  static List<BoxShadow> softLg(bool isDark) => isDark
      ? const [
          BoxShadow(color: Color(0x4D000000), blurRadius: 4, offset: Offset(0, 2)),
          BoxShadow(
            color: Color(0xA6000000),
            blurRadius: 48,
            spreadRadius: -12,
            offset: Offset(0, 20),
          ),
        ]
      : const [
          BoxShadow(color: Color(0x0A0F172A), blurRadius: 4, offset: Offset(0, 2)),
          BoxShadow(
            color: Color(0x2E0F172A),
            blurRadius: 48,
            spreadRadius: -12,
            offset: Offset(0, 20),
          ),
        ];
}

/// Text scale. The prototype uses Inter; no font is bundled here yet, so these
/// fall back to the platform UI font. Dropping Inter*.ttf into `assets/fonts/`
/// and declaring it in pubspec.yaml is the only change needed to match exactly.
abstract final class AppText {
  static const _tight = -0.4;

  static const displayLg = TextStyle(
    fontSize: 30,
    fontWeight: FontWeight.w800,
    letterSpacing: _tight,
    height: 1.2,
  );
  static const display = TextStyle(
    fontSize: 24,
    fontWeight: FontWeight.w800,
    letterSpacing: _tight,
    height: 1.25,
  );
  static const h1 = TextStyle(
    fontSize: 20,
    fontWeight: FontWeight.w800,
    letterSpacing: _tight,
    height: 1.3,
  );
  static const h2 = TextStyle(fontSize: 18, fontWeight: FontWeight.w800, height: 1.3);
  static const title = TextStyle(fontSize: 16, fontWeight: FontWeight.w700, height: 1.35);
  static const body = TextStyle(fontSize: 14, fontWeight: FontWeight.w400, height: 1.5);
  static const bodySemi = TextStyle(fontSize: 14, fontWeight: FontWeight.w600, height: 1.4);
  static const bodyBold = TextStyle(fontSize: 14, fontWeight: FontWeight.w700, height: 1.4);
  static const small = TextStyle(fontSize: 12, fontWeight: FontWeight.w400, height: 1.45);
  static const smallSemi = TextStyle(fontSize: 12, fontWeight: FontWeight.w600, height: 1.4);
  static const smallBold = TextStyle(fontSize: 12, fontWeight: FontWeight.w700, height: 1.4);
  static const tiny = TextStyle(fontSize: 11, fontWeight: FontWeight.w500, height: 1.4);

  /// `text-xs font-bold uppercase tracking-wide` section labels.
  static const overline = TextStyle(
    fontSize: 11,
    fontWeight: FontWeight.w700,
    letterSpacing: 0.7,
    height: 1.4,
  );
}

/// The eight brand gradients the prototype assigns to demo businesses
/// (`from-amber-500 to-amber-700` and friends), resolved to real colours.
enum BrandGradient {
  amber(Color(0xFFF59E0B), Color(0xFFB45309)),
  rose(Color(0xFFF43F5E), Color(0xFFBE123C)),
  pinkFuchsia(Color(0xFFF472B6), Color(0xFFC026D3)),
  emerald(Color(0xFF10B981), Color(0xFF047857)),
  sky(Color(0xFF0EA5E9), Color(0xFF0369A1)),
  limeGreen(Color(0xFF65A30D), Color(0xFF15803D)),
  violetPurple(Color(0xFF8B5CF6), Color(0xFF7E22CE)),
  indigoPrimary(Color(0xFF6366F1), AppColors.primary700);

  const BrandGradient(this.from, this.to);

  final Color from;
  final Color to;

  /// Matches the prototype's `bg-gradient-to-br` direction.
  LinearGradient get gradient => LinearGradient(
    colors: [from, to],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );
}
