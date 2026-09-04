import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import 'app_button.dart';

/// The prototype's shared inner-page top bar (`CustomerShell.topBarHtml`):
/// back button, title, optional trailing action, hairline bottom border.
class AppTopBar extends StatelessWidget implements PreferredSizeWidget {
  const AppTopBar({
    super.key,
    required this.title,
    this.showBack = true,
    this.onBack,
    this.actionIcon,
    this.onAction,
    this.actionTooltip,
  });

  final String title;
  final bool showBack;
  final VoidCallback? onBack;
  final IconData? actionIcon;
  final VoidCallback? onAction;
  final String? actionTooltip;

  @override
  Size get preferredSize => const Size.fromHeight(56);

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return Container(
      height: 56,
      padding: const EdgeInsets.symmetric(horizontal: 12),
      decoration: BoxDecoration(
        color: palette.surface,
        border: Border(bottom: BorderSide(color: palette.borderSubtle)),
      ),
      child: Row(
        children: [
          if (showBack)
            AppIconButton(
              icon: Icons.arrow_back_rounded,
              tooltip: 'Back',
              onPressed: onBack ?? () => _pop(context),
            )
          else
            const SizedBox(width: 8),
          const SizedBox(width: 4),
          Expanded(
            child: Text(
              title,
              overflow: TextOverflow.ellipsis,
              style: AppText.title.copyWith(color: palette.textPrimary),
            ),
          ),
          if (actionIcon != null)
            AppIconButton(
              icon: actionIcon!,
              tooltip: actionTooltip,
              onPressed: onAction,
            ),
        ],
      ),
    );
  }

  static void _pop(BuildContext context) {
    if (context.canPop()) {
      context.pop();
    } else {
      context.go('/home');
    }
  }
}

/// Standard page shell: themed background + [AppTopBar] + safe-area body.
/// Screens that need a custom header (Home, Search, Profile, QR Scanner) build
/// their own chrome and use [AppScaffold] with `appBar: null`.
class AppScaffold extends StatelessWidget {
  const AppScaffold({
    super.key,
    required this.body,
    this.title,
    this.showBack = true,
    this.onBack,
    this.actionIcon,
    this.onAction,
    this.actionTooltip,
    this.backgroundColor,
    this.bottomBar,
    this.appBar,
    this.resizeToAvoidBottomInset = true,
  });

  final Widget body;
  final String? title;
  final bool showBack;
  final VoidCallback? onBack;
  final IconData? actionIcon;
  final VoidCallback? onAction;
  final String? actionTooltip;
  final Color? backgroundColor;

  /// Pinned footer (e.g. the "Redeem Now" / "Save Changes" action bars).
  final Widget? bottomBar;
  final PreferredSizeWidget? appBar;
  final bool resizeToAvoidBottomInset;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return Scaffold(
      backgroundColor: backgroundColor ?? palette.scaffold,
      resizeToAvoidBottomInset: resizeToAvoidBottomInset,
      appBar:
          appBar ??
          (title == null
              ? null
              : AppTopBar(
                  title: title!,
                  showBack: showBack,
                  onBack: onBack,
                  actionIcon: actionIcon,
                  onAction: onAction,
                  actionTooltip: actionTooltip,
                )),
      body: SafeArea(top: title == null && appBar == null, child: body),
      bottomNavigationBar: bottomBar == null
          ? null
          : SafeArea(
              top: false,
              child: Container(
                padding: const EdgeInsets.fromLTRB(24, 12, 24, 20),
                decoration: BoxDecoration(
                  color: palette.surface,
                  border: Border(top: BorderSide(color: palette.borderSubtle)),
                ),
                child: bottomBar,
              ),
            ),
    );
  }
}

/// Toast equivalent of the prototype's `Eksabli.showToast`.
void showAppToast(
  BuildContext context, {
  required String title,
  String? message,
  IconData icon = Icons.check_circle_outline_rounded,
  Color? accent,
}) {
  final palette = AppPalette.of(context);
  final color = accent ?? AppColors.success600;

  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(
      SnackBar(
        duration: const Duration(seconds: 3),
        margin: const EdgeInsets.all(16),
        padding: const EdgeInsets.all(14),
        content: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, size: 20, color: color),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    title,
                    style: AppText.bodySemi.copyWith(color: palette.textPrimary),
                  ),
                  if (message != null) ...[
                    const SizedBox(height: 2),
                    Text(
                      message,
                      style: AppText.small.copyWith(color: palette.textMuted),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
}
