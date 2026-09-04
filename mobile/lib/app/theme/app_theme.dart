import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'app_colors.dart';
import 'app_tokens.dart';

abstract final class AppTheme {
  static ThemeData light() => _build(AppPalette.light);
  static ThemeData dark() => _build(AppPalette.dark);

  static ThemeData _build(AppPalette p) {
    final scheme =
        ColorScheme.fromSeed(
          seedColor: AppColors.primary600,
          brightness: p.isDark ? Brightness.dark : Brightness.light,
        ).copyWith(
          primary: AppColors.primary600,
          onPrimary: Colors.white,
          surface: p.surface,
          onSurface: p.textPrimary,
          error: AppColors.danger600,
        );

    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: p.scaffold,
      extensions: [p],
      splashFactory: InkSparkle.splashFactory,
      textTheme: _textTheme(p),
      appBarTheme: AppBarTheme(
        backgroundColor: p.surface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        titleTextStyle: AppText.title.copyWith(color: p.textPrimary),
        iconTheme: IconThemeData(color: p.textSecondary),
        systemOverlayStyle: p.isDark
            ? SystemUiOverlayStyle.light
            : SystemUiOverlayStyle.dark,
      ),
      dividerTheme: DividerThemeData(
        color: p.borderSubtle,
        thickness: 1,
        space: 1,
      ),
      // Inputs mirror `.input` in the prototype design system: 12px radius,
      // slate border, primary focus ring.
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: p.isDark ? AppColors.slate800 : Colors.white,
        contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        hintStyle: AppText.body.copyWith(color: AppColors.slate400),
        border: _inputBorder(p.border),
        enabledBorder: _inputBorder(p.isDark ? AppColors.slate700 : AppColors.slate200),
        focusedBorder: _inputBorder(AppColors.primary600, width: 1.5),
        errorBorder: _inputBorder(AppColors.danger600),
        focusedErrorBorder: _inputBorder(AppColors.danger600, width: 1.5),
        errorStyle: AppText.small.copyWith(
          color: p.isDark ? AppColors.danger300 : AppColors.danger600,
        ),
      ),
      switchTheme: SwitchThemeData(
        thumbColor: const WidgetStatePropertyAll(Colors.white),
        trackColor: WidgetStateProperty.resolveWith(
          (s) => s.contains(WidgetState.selected)
              ? AppColors.primary600
              : (p.isDark ? AppColors.slate700 : AppColors.slate300),
        ),
        trackOutlineColor: const WidgetStatePropertyAll(Colors.transparent),
      ),
      checkboxTheme: CheckboxThemeData(
        shape: const RoundedRectangleBorder(borderRadius: BorderRadius.all(Radius.circular(5))),
        side: BorderSide(color: p.isDark ? AppColors.slate600 : AppColors.slate300, width: 1.5),
        fillColor: WidgetStateProperty.resolveWith(
          (s) => s.contains(WidgetState.selected) ? AppColors.primary600 : Colors.transparent,
        ),
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: p.surface,
        surfaceTintColor: Colors.transparent,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: AppRadius.xl),
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: p.surface,
        surfaceTintColor: Colors.transparent,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.all(Radius.circular(20)),
        ),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: p.isDark ? AppColors.slate800 : Colors.white,
        contentTextStyle: AppText.bodySemi.copyWith(color: p.textPrimary),
        shape: const RoundedRectangleBorder(borderRadius: AppRadius.rMd),
        elevation: 6,
      ),
    );
  }

  static OutlineInputBorder _inputBorder(Color color, {double width = 1}) =>
      OutlineInputBorder(
        borderRadius: AppRadius.rMd,
        borderSide: BorderSide(color: color, width: width),
      );

  static TextTheme _textTheme(AppPalette p) {
    final base = TextTheme(
      displayLarge: AppText.displayLg,
      displayMedium: AppText.display,
      headlineSmall: AppText.h1,
      titleLarge: AppText.h2,
      titleMedium: AppText.title,
      bodyLarge: AppText.body,
      bodyMedium: AppText.body,
      bodySmall: AppText.small,
      labelLarge: AppText.bodySemi,
      labelMedium: AppText.smallSemi,
      labelSmall: AppText.tiny,
    );
    return base.apply(bodyColor: p.textPrimary, displayColor: p.textPrimary);
  }
}
