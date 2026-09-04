import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/demo/demo_data.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_avatar.dart';

/// Prototype: `customer/qr-scanner.html` — branch check-in scanner.
///
/// The camera preview is stubbed; wiring a scanner package (e.g.
/// `mobile_scanner`) replaces [_ScannerFrame] and calls [_showResult] with the
/// decoded branch token. Check-in itself stays a server-side mutation.
class QrScannerScreen extends StatelessWidget {
  const QrScannerScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.slate950,
      body: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  AppIconButton(
                    icon: Icons.arrow_back_rounded,
                    tooltip: 'Back',
                    foreground: Colors.white,
                    background: Colors.white.withValues(alpha: 0.1),
                    onPressed: () => context.canPop()
                        ? context.pop()
                        : context.go(Routes.home),
                  ),
                  const Text(
                    'Scan Check-in QR',
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  AppIconButton(
                    icon: Icons.qr_code_rounded,
                    tooltip: 'My QR',
                    foreground: Colors.white,
                    background: Colors.white.withValues(alpha: 0.1),
                    onPressed: () => context.push(Routes.qrCode),
                  ),
                ],
              ),
            ),
            const Spacer(),
            const _ScannerFrame(),
            const SizedBox(height: 32),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 256),
              child: Text(
                'Point your camera at a branch check-in QR code',
                textAlign: TextAlign.center,
                style: AppText.body.copyWith(
                  color: Colors.white.withValues(alpha: 0.6),
                ),
              ),
            ),
            const Spacer(),
            Padding(
              padding: const EdgeInsets.fromLTRB(32, 0, 32, 40),
              child: AppButton(
                label: 'Simulate Scan',
                size: AppButtonSize.lg,
                expand: true,
                onPressed: () => _showResult(context),
              ),
            ),
          ],
        ),
      ),
    );
  }

  void _showResult(BuildContext context) {
    final business = DemoData.businesses.first;

    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) {
        final palette = AppPalette.of(sheetContext);
        return Padding(
          padding: const EdgeInsets.fromLTRB(24, 12, 24, 32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 48,
                height: 4,
                decoration: BoxDecoration(
                  color: palette.isDark
                      ? AppColors.slate700
                      : AppColors.slate200,
                  borderRadius: BorderRadius.circular(999),
                ),
              ),
              const SizedBox(height: 20),
              Row(
                children: [
                  BusinessLogo(
                    initials: business.initials,
                    gradient: business.gradient,
                    size: 56,
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(
                          business.name,
                          style: AppText.title.copyWith(
                            color: palette.textPrimary,
                          ),
                        ),
                        Text(
                          'Downtown Branch check-in',
                          style: AppText.small.copyWith(
                            color: palette.textMuted,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 20),
              const AppAlert(
                message: "Checked in! Staff has been notified you're here.",
                tone: AppTone.success,
              ),
              const SizedBox(height: 20),
              AppButton(
                label: 'View My Points',
                size: AppButtonSize.lg,
                expand: true,
                onPressed: () {
                  Navigator.of(sheetContext).pop();
                  context.push(Routes.points(business.id));
                },
              ),
              const SizedBox(height: 8),
              AppButton(
                label: 'Close',
                variant: AppButtonVariant.secondary,
                size: AppButtonSize.lg,
                expand: true,
                onPressed: () => Navigator.of(sheetContext).pop(),
              ),
            ],
          ),
        );
      },
    );
  }
}

/// The viewfinder: dimmed border, primary corner brackets, sweeping scan line.
class _ScannerFrame extends StatefulWidget {
  const _ScannerFrame();

  @override
  State<_ScannerFrame> createState() => _ScannerFrameState();
}

class _ScannerFrameState extends State<_ScannerFrame>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 2000),
  )..repeat(reverse: true);

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    const size = 256.0;
    return SizedBox(
      width: size,
      height: size,
      child: Stack(
        children: [
          Container(
            decoration: BoxDecoration(
              border: Border.all(color: Colors.white.withValues(alpha: 0.3), width: 2),
              borderRadius: AppRadius.rXl,
            ),
          ),
          const _Corner(top: true, left: true),
          const _Corner(top: true, left: false),
          const _Corner(top: false, left: true),
          const _Corner(top: false, left: false),
          AnimatedBuilder(
            animation: _controller,
            builder: (context, _) => Positioned(
              top: 16 + (size - 32) * _controller.value,
              left: 16,
              right: 16,
              child: Container(
                height: 2,
                decoration: BoxDecoration(
                  color: AppColors.primary400,
                  borderRadius: BorderRadius.circular(999),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _Corner extends StatelessWidget {
  const _Corner({required this.top, required this.left});

  final bool top;
  final bool left;

  @override
  Widget build(BuildContext context) {
    const side = BorderSide(color: AppColors.primary400, width: 4);
    return Positioned(
      top: top ? -1 : null,
      bottom: top ? null : -1,
      left: left ? -1 : null,
      right: left ? null : -1,
      child: Container(
        width: 40,
        height: 40,
        decoration: BoxDecoration(
          border: Border(
            top: top ? side : BorderSide.none,
            bottom: top ? BorderSide.none : side,
            left: left ? side : BorderSide.none,
            right: left ? BorderSide.none : side,
          ),
          borderRadius: BorderRadius.only(
            topLeft: top && left ? AppRadius.lg : Radius.zero,
            topRight: top && !left ? AppRadius.lg : Radius.zero,
            bottomLeft: !top && left ? AppRadius.lg : Radius.zero,
            bottomRight: !top && !left ? AppRadius.lg : Radius.zero,
          ),
        ),
      ),
    );
  }
}
