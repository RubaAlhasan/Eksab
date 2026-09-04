import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/providers/app_providers.dart';

/// Prototype: `customer/splash.html`.
///
/// Shown while the stored session is restored. Watching [sessionProvider] is
/// what kicks that off; the router's guard then redirects to Home or
/// Onboarding once it resolves, so this screen never navigates itself.
class SplashScreen extends ConsumerWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.watch(sessionProvider);

    return Scaffold(
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            colors: [AppColors.primary600, AppColors.primary800],
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
          ),
        ),
        child: SafeArea(
          child: Column(
            children: [
              const Spacer(),
              Container(
                width: 80,
                height: 80,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.15),
                  borderRadius: AppRadius.rXl,
                ),
                child: const Text(
                  'E',
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 36,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              const SizedBox(height: 24),
              Text(
                'Eksabli',
                style: AppText.displayLg.copyWith(color: Colors.white),
              ),
              const SizedBox(height: 8),
              Text(
                'Collect points everywhere you shop',
                style: AppText.body.copyWith(
                  color: Colors.white.withValues(alpha: 0.7),
                ),
              ),
              const Spacer(),
              const _PulsingDots(),
              const SizedBox(height: 16),
              Text(
                'Signing you in…',
                style: AppText.small.copyWith(
                  color: Colors.white.withValues(alpha: 0.5),
                ),
              ),
              const SizedBox(height: 48),
            ],
          ),
        ),
      ),
    );
  }
}

class _PulsingDots extends StatefulWidget {
  const _PulsingDots();

  @override
  State<_PulsingDots> createState() => _PulsingDotsState();
}

class _PulsingDotsState extends State<_PulsingDots>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1200),
  )..repeat();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, _) => Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: List.generate(3, (i) {
          final phase = (_controller.value + i * 0.15) % 1;
          final opacity = 0.3 + 0.7 * (1 - (phase - 0.5).abs() * 2);
          return Padding(
            padding: const EdgeInsets.symmetric(horizontal: 3),
            child: Container(
              width: 8,
              height: 8,
              decoration: BoxDecoration(
                color: Colors.white.withValues(alpha: opacity),
                shape: BoxShape.circle,
              ),
            ),
          );
        }),
      ),
    );
  }
}
