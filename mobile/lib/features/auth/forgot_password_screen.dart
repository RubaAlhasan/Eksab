import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_form.dart';
import '../../shared/widgets/app_scaffold.dart';

/// Prototype: `customer/forgot-password.html` — request form, then a
/// confirmation state that deliberately does not reveal whether the account
/// exists.
class ForgotPasswordScreen extends StatefulWidget {
  const ForgotPasswordScreen({super.key});

  @override
  State<ForgotPasswordScreen> createState() => _ForgotPasswordScreenState();
}

class _ForgotPasswordScreenState extends State<ForgotPasswordScreen> {
  final _identifier = TextEditingController();
  String? _error;
  bool _submitting = false;
  bool _sent = false;

  @override
  void dispose() {
    _identifier.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final value = _identifier.text.trim();
    if (value.isEmpty) {
      setState(() => _error = 'Enter your phone or email.');
      return;
    }

    setState(() {
      _error = null;
      _submitting = true;
    });
    await Future<void>.delayed(const Duration(seconds: 1));
    if (!mounted) return;
    setState(() {
      _submitting = false;
      _sent = true;
    });
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);

    return AppScaffold(
      backgroundColor: palette.surface,
      appBar: AppTopBar(
        title: '',
        onBack: () =>
            context.canPop() ? context.pop() : context.go(Routes.login),
      ),
      body: Padding(
        padding: const EdgeInsets.fromLTRB(24, 8, 24, 32),
        child: _sent ? _buildSent(palette) : _buildRequest(palette),
      ),
    );
  }

  Widget _buildRequest(AppPalette palette) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          width: 64,
          height: 64,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: AppColors.warning500.withValues(alpha: palette.isDark ? 0.12 : 0.1),
            borderRadius: AppRadius.rLg,
          ),
          child: Icon(
            Icons.lock_outline_rounded,
            size: 28,
            color: palette.isDark ? AppColors.warning300 : AppColors.warning600,
          ),
        ),
        const SizedBox(height: 24),
        Text(
          'Reset your password',
          style: AppText.display.copyWith(color: palette.textPrimary),
        ),
        const SizedBox(height: 4),
        Text(
          "Enter the phone number or email on your account and we'll send "
          'reset instructions.',
          style: AppText.body.copyWith(color: palette.textSecondary),
        ),
        const SizedBox(height: 24),
        AppField(
          label: 'Phone or email',
          controller: _identifier,
          hint: '+971 50 123 4567',
          errorText: _error,
        ),
        const SizedBox(height: 20),
        AppButton(
          label: 'Send reset instructions',
          loadingLabel: 'Sending…',
          loading: _submitting,
          size: AppButtonSize.lg,
          expand: true,
          onPressed: _submit,
        ),
      ],
    );
  }

  Widget _buildSent(AppPalette palette) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Container(
          width: 64,
          height: 64,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: AppColors.success500.withValues(alpha: palette.isDark ? 0.12 : 0.1),
            borderRadius: AppRadius.rLg,
          ),
          child: Icon(
            Icons.check_circle_outline_rounded,
            size: 28,
            color: palette.isDark ? AppColors.success300 : AppColors.success600,
          ),
        ),
        const SizedBox(height: 24),
        Text(
          'Check your messages',
          textAlign: TextAlign.center,
          style: AppText.h1.copyWith(color: palette.textPrimary),
        ),
        const SizedBox(height: 8),
        Text.rich(
          textAlign: TextAlign.center,
          TextSpan(
            style: AppText.body.copyWith(color: palette.textSecondary),
            children: [
              const TextSpan(text: 'We sent reset instructions to '),
              TextSpan(
                text: _identifier.text.trim(),
                style: AppText.bodySemi.copyWith(color: palette.textPrimary),
              ),
              const TextSpan(text: '. It may take a minute to arrive.'),
            ],
          ),
        ),
        const SizedBox(height: 32),
        AppButton(
          label: 'Back to login',
          size: AppButtonSize.lg,
          expand: true,
          onPressed: () => context.go(Routes.login),
        ),
        const SizedBox(height: 16),
        GestureDetector(
          onTap: () => showAppToast(
            context,
            title: 'Instructions resent',
            message: 'Check your messages again shortly.',
          ),
          child: Text(
            "Didn't get it? Resend",
            style: AppText.bodySemi.copyWith(
              color: palette.primaryOnDarkAware,
            ),
          ),
        ),
      ],
    );
  }
}
