import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_scaffold.dart';

enum ErrorKind { notFound, serverError }

/// Prototype: `customer/error.html`.
///
/// Doubles as go_router's `errorBuilder`, so an unknown deep link lands on the
/// real 404 state rather than Flutter's default red screen.
class ErrorScreen extends StatefulWidget {
  const ErrorScreen({super.key, this.kind, this.onRetry});

  /// When null the screen shows the prototype's demo toggle between both
  /// states; when set it renders that state only.
  final ErrorKind? kind;
  final VoidCallback? onRetry;

  @override
  State<ErrorScreen> createState() => _ErrorScreenState();
}

class _ErrorScreenState extends State<ErrorScreen> {
  late ErrorKind _kind = widget.kind ?? ErrorKind.notFound;

  bool get _isDemo => widget.kind == null;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final notFound = _kind == ErrorKind.notFound;

    return AppScaffold(
      backgroundColor: palette.surface,
      title: 'Error',
      onBack: () => context.canPop() ? context.pop() : context.go(Routes.home),
      body: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 32),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            if (_isDemo) ...[
              Center(
                child: _StateToggle(
                  kind: _kind,
                  onChanged: (k) => setState(() => _kind = k),
                ),
              ),
              const SizedBox(height: 32),
            ],
            Container(
              width: 64,
              height: 64,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: (notFound ? AppColors.warning500 : AppColors.danger500)
                    .withValues(alpha: palette.isDark ? 0.12 : 0.1),
                borderRadius: AppRadius.rLg,
              ),
              child: Icon(
                notFound
                    ? Icons.warning_amber_rounded
                    : Icons.error_outline_rounded,
                size: 26,
                color: notFound
                    ? (palette.isDark
                          ? AppColors.warning300
                          : AppColors.warning600)
                    : (palette.isDark
                          ? AppColors.danger300
                          : AppColors.danger600),
              ),
            ),
            const SizedBox(height: 24),
            Text(
              notFound ? 'ERROR 404' : 'SOMETHING WENT WRONG',
              style: AppText.overline.copyWith(
                color: palette.textMuted,
                letterSpacing: 1.4,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              notFound ? "We couldn't find that" : "We couldn't load this",
              textAlign: TextAlign.center,
              style: AppText.h1.copyWith(color: palette.textPrimary),
            ),
            const SizedBox(height: 8),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 256),
              child: Text(
                notFound
                    ? "This screen doesn't exist, or the link you followed is "
                          'out of date.'
                    : 'Your points and account are safe — this screen just '
                          'failed to load. Try again in a moment.',
                textAlign: TextAlign.center,
                style: AppText.body.copyWith(color: palette.textSecondary),
              ),
            ),
            const SizedBox(height: 32),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 256),
              child: Column(
                children: [
                  AppButton(
                    label: 'Try Again',
                    size: AppButtonSize.lg,
                    expand: true,
                    onPressed:
                        widget.onRetry ??
                        () => showAppToast(
                          context,
                          title: 'Retrying…',
                          message: 'This would re-issue the failed request.',
                          icon: Icons.info_outline_rounded,
                          accent: AppColors.info600,
                        ),
                  ),
                  const SizedBox(height: 8),
                  AppButton(
                    label: 'Back to Home',
                    variant: AppButtonVariant.secondary,
                    size: AppButtonSize.lg,
                    expand: true,
                    onPressed: () => context.go(Routes.home),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _StateToggle extends StatelessWidget {
  const _StateToggle({required this.kind, required this.onChanged});

  final ErrorKind kind;
  final ValueChanged<ErrorKind> onChanged;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    Widget item(String label, ErrorKind value) {
      final active = value == kind;
      return GestureDetector(
        onTap: () => onChanged(value),
        behavior: HitTestBehavior.opaque,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 7),
          decoration: BoxDecoration(
            color: active
                ? (palette.isDark ? AppColors.slate700 : Colors.white)
                : Colors.transparent,
            borderRadius: AppRadius.rSm,
            boxShadow: active ? AppShadows.soft(palette.isDark) : null,
          ),
          child: Text(
            label,
            style: AppText.smallSemi.copyWith(
              fontSize: 13,
              color: active ? palette.textPrimary : palette.textMuted,
            ),
          ),
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: palette.isDark ? AppColors.slate800 : AppColors.slate100,
        borderRadius: AppRadius.rMd,
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          item('Not Found', ErrorKind.notFound),
          item('Server Error', ErrorKind.serverError),
        ],
      ),
    );
  }
}
