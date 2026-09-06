import 'package:flutter/material.dart';

/// Colour tokens. The source of truth is now `angular/src/styles/_tokens.scss`
/// (the shipping design system) — NOT the prototype, which is frozen on the older
/// cool-slate palette and is no longer tracked here. Keep this file and that one in
/// sync; every value below has a named counterpart there.
///
/// Not ported: the `--eks-chart-*` categorical ramp and the `--eks-tier-*` ordinal
/// ramp. Neither has a consumer in this app yet (no charts, no tier colouring), and
/// unused tokens rot. Port them from _tokens.scss when the first screen needs them.
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

  // Secondary (magenta) — the one brand hue that still fits beside the violet without
  // colliding with a status colour. Reserved for brand moments that are neither an
  // action nor a status: rewards earned, tier-up, referral, campaign highlights.
  // NOT a second primary — a button is still primary600. See the long note in
  // angular/src/styles/_tokens.scss for the gamut search behind hue 333deg.
  static const secondary50 = Color(0xFFFEF0FB);
  static const secondary100 = Color(0xFFFFE1F8);
  static const secondary200 = Color(0xFFF9BFEE);
  static const secondary300 = Color(0xFFEC94DE);
  static const secondary400 = Color(0xFFDA64CA);
  static const secondary500 = Color(0xFFC73BB7);
  static const secondary600 = Color(0xFFAF1DA0);
  static const secondary700 = Color(0xFF8D1281);
  static const secondary800 = Color(0xFF6F0C66);
  static const secondary900 = Color(0xFF560B4E);

  // Neutral ramp every surface/border/text token is built from. Still named `slate`
  // so the ~9 call sites outside this file keep working, but the values are now a
  // WARM gray, lightness-matched step-for-step to the cool slate they replace (max
  // drift 1.9% relative luminance) — a pure hue shift, so nothing got heavier or
  // lighter. A blue-gray fights the violet accent; this one doesn't.
  //
  // Chroma held at ~0.005 OKLCH (Tailwind `stone` territory). Above ~0.010 a warm ramp
  // stops reading as "gray" and starts reading as BEIGE.
  static const slate50 = Color(0xFFFAF9F8);
  static const slate100 = Color(0xFFF4F3F1);
  static const slate200 = Color(0xFFE9E7E5);
  static const slate300 = Color(0xFFD5D3D0);
  static const slate400 = Color(0xFFA4A19E);
  static const slate500 = Color(0xFF726F6D);
  static const slate600 = Color(0xFF585654);
  static const slate700 = Color(0xFF403E3C);
  static const slate800 = Color(0xFF2A2827);
  static const slate900 = Color(0xFF191817);
  static const slate950 = Color(0xFF0C0B0A);
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
    required this.secondary,
    required this.secondaryOnDarkAware,
    required this.surfaceInverse,
    required this.textOnInverse,
    required this.textOnInverseMuted,
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

  /// Brand accent for moments that are neither an action nor a status — reward
  /// earned, tier-up, referral. Never use it for a button; that's [primary].
  final Color secondary;

  /// [secondary] as text/icons — lifted a step in dark mode to stay legible.
  final Color secondaryOnDarkAware;

  /// A deliberately inverted surface: near-black in light mode, near-white in dark.
  /// For hero bands, a stats strip, the membership card's reward shelf — the thing
  /// that gives the app depth without spending another hue on it.
  final Color surfaceInverse;
  final Color textOnInverse;
  final Color textOnInverseMuted;
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
    // slate500, not slate400: on the light ground slate400 is 2.7:1 — below WCAG AA,
    // and this is the app's most-used text colour. slate500 is 4.73:1.
    textMuted: AppColors.slate500,
    primary: AppColors.primary600,
    primaryOnDarkAware: AppColors.primary600,
    secondary: AppColors.secondary600,
    secondaryOnDarkAware: AppColors.secondary600,
    surfaceInverse: AppColors.slate900,
    textOnInverse: AppColors.slate100,
    textOnInverseMuted: AppColors.slate400,
    shadow: Color(0x1A191817),
  );

  static const dark = AppPalette(
    isDark: true,
    scaffold: AppColors.slate950,
    surface: AppColors.slate900,
    surfaceMuted: AppColors.slate900,
    border: AppColors.slate800,
    borderSubtle: AppColors.slate800,
    textPrimary: AppColors.slate100,
    textSecondary: AppColors.slate300,
    // slate400 IS AA here — 6.89:1 on the dark ground — so unlike light mode it stays.
    textMuted: AppColors.slate400,
    primary: AppColors.primary600,
    primaryOnDarkAware: AppColors.primary400,
    secondary: AppColors.secondary600,
    secondaryOnDarkAware: AppColors.secondary400,
    // Inverts, or it disappears into the dark ground.
    surfaceInverse: AppColors.slate50,
    textOnInverse: AppColors.slate900,
    textOnInverseMuted: AppColors.slate500,
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
