import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_form.dart';
import '../../shared/widgets/app_scaffold.dart';

/// Prototype: `customer/otp-verify.html` — 6-digit code with a resend timer.
///
/// Still simulated: the code is checked locally against [_demoCode] and no
/// tokens are minted, so API calls made afterwards will 401. The client is
/// already registered for an `otp` grant, so wiring this to `OtpAppService`
/// later is a change in [SessionNotifier.signIn], not in this screen.
class OtpVerifyScreen extends ConsumerStatefulWidget {
  const OtpVerifyScreen({super.key});

  @override
  ConsumerState<OtpVerifyScreen> createState() => _OtpVerifyScreenState();
}

class _OtpVerifyScreenState extends ConsumerState<OtpVerifyScreen> {
  static const _demoCode = '123456';
  static const _resendSeconds = 30;

  String _code = '';
  bool _showError = false;
  bool _submitting = false;
  int _secondsLeft = _resendSeconds;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _startTimer();
  }

  void _startTimer() {
    _timer?.cancel();
    setState(() => _secondsLeft = _resendSeconds);
    _timer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) return timer.cancel();
      setState(() => _secondsLeft--);
      if (_secondsLeft <= 0) timer.cancel();
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _verify() async {
    if (_code.length < 6) {
      setState(() => _showError = true);
      return;
    }
    if (_code != _demoCode) {
      setState(() => _showError = true);
      return;
    }

    setState(() {
      _showError = false;
      _submitting = true;
    });
    await Future<void>.delayed(const Duration(seconds: 1));
    if (!mounted) return;

    // Simulated sign-in — the router's guard sends us to Home once the
    // session state flips.
    ref.read(sessionProvider.notifier).signInSimulated();
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);

    return AppScaffold(
      backgroundColor: palette.surface,
      appBar: const AppTopBar(title: ''),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(24, 8, 24, 32),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: 64,
              height: 64,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: AppColors.primary600.withValues(alpha: palette.isDark ? 0.12 : 0.08),
                borderRadius: AppRadius.rLg,
              ),
              child: Icon(
                Icons.smartphone_rounded,
                size: 28,
                color: palette.primaryOnDarkAware,
              ),
            ),
            const SizedBox(height: 24),
            Text(
              'Verify your number',
              style: AppText.display.copyWith(color: palette.textPrimary),
            ),
            const SizedBox(height: 4),
            Text.rich(
              TextSpan(
                style: AppText.body.copyWith(color: palette.textSecondary),
                children: [
                  const TextSpan(text: 'We sent a 6-digit code to '),
                  TextSpan(
                    text: '+971 50 123 4567',
                    style: AppText.bodySemi.copyWith(
                      color: palette.textPrimary,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 4),
            Text(
              'Simulated verification — demo code: 123456',
              style: AppText.small.copyWith(color: palette.textMuted),
            ),
            const SizedBox(height: 24),

            if (_showError) ...[
              const AppAlert(
                message: 'Incorrect code. Please check and try again.',
                tone: AppTone.danger,
              ),
              const SizedBox(height: 16),
            ],

            OtpInput(
              length: 6,
              hasError: _showError,
              onChanged: (v) => _code = v,
              onCompleted: (v) => _code = v,
            ),
            const SizedBox(height: 24),

            AppButton(
              label: 'Verify',
              loadingLabel: 'Verifying…',
              loading: _submitting,
              size: AppButtonSize.lg,
              expand: true,
              onPressed: _verify,
            ),
            const SizedBox(height: 16),

            Center(
              child: Wrap(
                alignment: WrapAlignment.center,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(
                    "Didn't get a code? ",
                    style: AppText.body.copyWith(color: palette.textSecondary),
                  ),
                  if (_secondsLeft > 0)
                    Text(
                      'Resend in ${_secondsLeft}s',
                      style: AppText.bodySemi.copyWith(
                        color: palette.textMuted,
                      ),
                    )
                  else
                    GestureDetector(
                      onTap: () {
                        _startTimer();
                        showAppToast(
                          context,
                          title: 'Code resent',
                          message: 'Check your messages for the new code.',
                        );
                      },
                      child: Text(
                        'Resend code',
                        style: AppText.bodySemi.copyWith(
                          color: palette.primaryOnDarkAware,
                        ),
                      ),
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
