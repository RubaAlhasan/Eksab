import 'package:flutter/material.dart';

/// Colour tokens ported 1:1 from the prototype's Tailwind config
/// (`prototype/assets/js/tailwind-config.js`). Keep the two in sync — the
/// prototype remains the visual source of truth for the customer app.
abstract final class AppColors {
  // Primary (violet)
  static const primary50 = Color(0xFFF4F3FF);
  static const primary100 = Color(0xFFEBE9FE);
  static const primary200 = Color(0xFFD9D6FD);
  static const primary300 = Color(0xFFBEB8FB);
  static const primary400 = Color(0xFF9D8FF8);
  static const primary500 = Color(0xFF7C6AF0);
  static const primary600 = Color(0xFF6248E3);
  static const primary700 = Color(0xFF4F37C4);
  static const primary800 = Color(0xFF422F9E);
  static const primary900 = Color(0xFF392A7D);
  static const primary950 = Color(0xFF241A52);

  static const success50 = Color(0xFFECFDF5);
  static const success100 = Color(0xFFD1FAE5);
  static const success200 = Color(0xFFA7F3D0);
  static const success300 = Color(0xFF6EE7B7);
  static const success500 = Color(0xFF10B981);
  static const success600 = Color(0xFF059669);
  static const success700 = Color(0xFF047857);

  static const warning50 = Color(0xFFFFFBEB);
  static const warning100 = Color(0xFFFEF3C7);
  static const warning200 = Color(0xFFFDE68A);
  static const warning300 = Color(0xFFFCD34D);
  static const warning400 = Color(0xFFFBBF24);
  static const warning500 = Color(0xFFF59E0B);
  static const warning600 = Color(0xFFD97706);
  static const warning700 = Color(0xFFB45309);

  static const danger50 = Color(0xFFFEF2F2);
  static const danger100 = Color(0xFFFEE2E2);
  static const danger200 = Color(0xFFFECACA);
  static const danger300 = Color(0xFFFCA5A5);
  static const danger500 = Color(0xFFEF4444);
  static const danger600 = Color(0xFFDC2626);
  static const danger700 = Color(0xFFB91C1C);

  static const info50 = Color(0xFFF0F9FF);
  static const info100 = Color(0xFFE0F2FE);
  static const info200 = Color(0xFFBAE6FD);
  static const info300 = Color(0xFF7DD3FC);
  static const info500 = Color(0xFF0EA5E9);
  static const info600 = Color(0xFF0284C7);
  static const info700 = Color(0xFF0369A1);

  // Slate — the neutral ramp every surface/border/text token is built from.
  static const slate50 = Color(0xFFF8FAFC);
  static const slate100 = Color(0xFFF1F5F9);
  static const slate200 = Color(0xFFE2E8F0);
  static const slate300 = Color(0xFFCBD5E1);
  static const slate400 = Color(0xFF94A3B8);
  static const slate500 = Color(0xFF64748B);
  static const slate600 = Color(0xFF475569);
  static const slate700 = Color(0xFF334155);
  static const slate800 = Color(0xFF1E293B);
  static const slate900 = Color(0xFF0F172A);
  static const slate950 = Color(0xFF020617);
}

/// Semantic surface/text tokens resolved per brightness. Widgets read these via
/// `AppPalette.of(context)` instead of branching on `Theme.of(context).brightness`
/// at every call site.
@immutable
class AppPalette extends ThemeExtension<AppPalette> {
  const AppPalette({
    required this.isDark,
    required this.scaffold,
    required this.surface,
    required this.surfaceMuted,
    required this.border,
    required this.borderSubtle,
    required this.textPrimary,
    required this.textSecondary,
    required this.textMuted,
    required this.primary,
    required this.primaryOnDarkAware,
    required this.shadow,
  });

  final bool isDark;

  /// Page background (`bg-slate-50` / `dark:bg-slate-950`).
  final Color scaffold;

  /// Card + sheet background (`bg-white` / `dark:bg-slate-900`).
  final Color surface;

  /// Inset rows inside a card (`bg-slate-50` / `dark:bg-slate-900`).
  final Color surfaceMuted;
  final Color border;
  final Color borderSubtle;
  final Color textPrimary;
  final Color textSecondary;
  final Color textMuted;
  final Color primary;

  /// Primary tint used for text/icons — lighter in dark mode so it stays legible.
  final Color primaryOnDarkAware;
  final Color shadow;

  /// Resolves the palette for [context].
  ///
  /// Falls back to the brightness-matched default when the ambient theme has
  /// no [AppPalette] extension — a bare `MaterialApp`, a widget previewed in
  /// isolation, or a test that supplies its own theme. Asserting here instead
  /// would turn a cosmetic gap into a crash for every widget in the tree.
  static AppPalette of(BuildContext context) {
    final theme = Theme.of(context);
    return theme.extension<AppPalette>() ??
        (theme.brightness == Brightness.dark ? dark : light);
  }

  static const light = AppPalette(
    isDark: false,
    scaffold: AppColors.slate50,
    surface: Colors.white,
    surfaceMuted: AppColors.slate50,
    border: AppColors.slate200,
    borderSubtle: AppColors.slate100,
    textPrimary: AppColors.slate900,
    textSecondary: AppColors.slate600,
    textMuted: AppColors.slate400,
    primary: AppColors.primary600,
    primaryOnDarkAware: AppColors.primary600,
    shadow: Color(0x1A0F172A),
  );

  static const dark = AppPalette(
    isDark: true,
    scaffold: AppColors.slate950,
    surface: AppColors.slate900,
    surfaceMuted: AppColors.slate900,
    border: AppColors.slate800,
    borderSubtle: AppColors.slate800,
    textPrimary: Color(0xFFF1F5F9),
    textSecondary: AppColors.slate300,
    textMuted: AppColors.slate400,
    primary: AppColors.primary600,
    primaryOnDarkAware: AppColors.primary400,
    shadow: Color(0x800A0A0A),
  );

  @override
  AppPalette copyWith({bool? isDark}) => isDark == null || isDark == this.isDark
      ? this
      : (isDark ? AppPalette.dark : AppPalette.light);

  @override
  AppPalette lerp(ThemeExtension<AppPalette>? other, double t) =>
      t < 0.5 ? this : (other as AppPalette? ?? this);
}
