import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/widgets/app_button.dart';

/// Prototype: `customer/onboarding.html` — three swipeable value-prop slides.
class OnboardingScreen extends StatefulWidget {
  const OnboardingScreen({super.key});

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen> {
  final _controller = PageController();
  int _index = 0;

  static const _slides = [
    (
      icon: Icons.groups_rounded,
      tone: AppColors.primary600,
      title: 'Join businesses',
      body:
          'Scan a QR code or search to join any business’s loyalty program '
          '— all from one account.',
    ),
    (
      icon: Icons.account_balance_wallet_rounded,
      tone: AppColors.success600,
      title: 'Collect points',
      body:
          'Earn points at checkout, from campaigns, and from referrals — '
          'every balance tracked independently.',
    ),
    (
      icon: Icons.card_giftcard_rounded,
      tone: AppColors.warning600,
      title: 'Redeem rewards',
      body:
          'Turn points into discounts, free products and gift cards — show '
          'a QR or PIN at the counter.',
    ),
  ];

  bool get _isLast => _index == _slides.length - 1;

  void _next() {
    if (_isLast) {
      context.go(Routes.register);
    } else {
      _controller.nextPage(
        duration: const Duration(milliseconds: 250),
        curve: Curves.easeOut,
      );
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);

    return Scaffold(
      backgroundColor: palette.surface,
      body: SafeArea(
        child: Column(
          children: [
            Align(
              alignment: Alignment.centerRight,
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
                child: GestureDetector(
                  onTap: () => context.go(Routes.register),
                  child: Text(
                    'Skip',
                    style: AppText.bodySemi.copyWith(color: palette.textMuted),
                  ),
                ),
              ),
            ),
            Expanded(
              child: PageView.builder(
                controller: _controller,
                itemCount: _slides.length,
                onPageChanged: (i) => setState(() => _index = i),
                itemBuilder: (context, i) {
                  final slide = _slides[i];
                  return Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 32),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Container(
                          width: 160,
                          height: 160,
                          alignment: Alignment.center,
                          decoration: BoxDecoration(
                            color: slide.tone.withValues(alpha: palette.isDark ? 0.12 : 0.08),
                            borderRadius: AppRadius.rXxl,
                          ),
                          child: Icon(slide.icon, size: 56, color: slide.tone),
                        ),
                        const SizedBox(height: 32),
                        Text(
                          slide.title,
                          textAlign: TextAlign.center,
                          style: AppText.display.copyWith(
                            color: palette.textPrimary,
                          ),
                        ),
                        const SizedBox(height: 12),
                        ConstrainedBox(
                          constraints: const BoxConstraints(maxWidth: 320),
                          child: Text(
                            slide.body,
                            textAlign: TextAlign.center,
                            style: AppText.body.copyWith(
                              color: palette.textSecondary,
                            ),
                          ),
                        ),
                      ],
                    ),
                  );
                },
              ),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(32, 0, 32, 40),
              child: Column(
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: List.generate(_slides.length, (i) {
                      final active = i == _index;
                      return AnimatedContainer(
                        duration: const Duration(milliseconds: 200),
                        margin: const EdgeInsets.symmetric(horizontal: 3),
                        width: active ? 20 : 8,
                        height: 8,
                        decoration: BoxDecoration(
                          color: active
                              ? AppColors.primary600
                              : (palette.isDark
                                    ? AppColors.slate700
                                    : AppColors.slate200),
                          borderRadius: BorderRadius.circular(999),
                        ),
                      );
                    }),
                  ),
                  const SizedBox(height: 24),
                  AppButton(
                    label: _isLast ? 'Get Started' : 'Next',
                    size: AppButtonSize.lg,
                    expand: true,
                    onPressed: _next,
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
