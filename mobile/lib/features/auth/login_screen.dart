import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/auth/auth_exception.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_form.dart';

/// Prototype: `customer/login.html`.
///
/// Real login via the OTP path: `POST /api/app/otp/request` sends a code, then
/// the OTP screen exchanges it through OpenIddict's custom `otp` grant.
///
/// The password grant is deliberately not used here. `Eksabli_App` permits it,
/// but `RegisterCustomerDto` states the stored password authenticates nothing
/// today — OTP is the only login path wired server-side. The password field is
/// kept (collected at registration) so this can switch over without UI churn
/// once the server supports it.
class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _identifier = TextEditingController();

  String? _identifierError;

  /// The server's message for a rejected login, shown verbatim.
  String? _formError;
  bool _submitting = false;

  @override
  void dispose() {
    _identifier.dispose();
    super.dispose();
  }

  static final _phonePattern = RegExp(r'^[+\d][\d\s-]{7,}$');

  Future<void> _submit() async {
    final phone = _identifier.text.trim();

    setState(() {
      _formError = null;
      _identifierError = _phonePattern.hasMatch(phone)
          ? null
          : 'Enter the phone number on your account.';
    });
    if (_identifierError != null) return;

    setState(() => _submitting = true);

    try {
      await ref.read(sessionProvider.notifier).requestOtp(phone);
      if (!mounted) return;
      setState(() => _submitting = false);
      context.push('${Routes.otpVerify}?phone=${Uri.encodeComponent(phone)}');
    } on AuthException catch (error) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _formError = error.message;
      });
    } catch (error) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _formError = 'Unexpected error: $error';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);

    return Scaffold(
      backgroundColor: palette.surface,
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(24, 24, 24, 32),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                width: 56,
                height: 56,
                alignment: Alignment.center,
                decoration: const BoxDecoration(
                  gradient: LinearGradient(
                    colors: [AppColors.primary500, AppColors.primary700],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                  borderRadius: AppRadius.rLg,
                ),
                child: const Text(
                  'E',
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 20,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              const SizedBox(height: 24),
              Text(
                'Welcome back',
                style: AppText.display.copyWith(color: palette.textPrimary),
              ),
              const SizedBox(height: 4),
              Text(
                'Log in to see your points across every business.',
                style: AppText.body.copyWith(color: palette.textSecondary),
              ),
              const SizedBox(height: 24),

              if (_formError != null) ...[
                AppAlert(message: _formError!, tone: AppTone.danger),
                const SizedBox(height: 16),
              ],

              AppField(
                label: 'Phone number',
                controller: _identifier,
                hint: '+971 50 123 4567',
                keyboardType: TextInputType.phone,
                errorText: _identifierError,
              ),
              const SizedBox(height: 6),
              Text(
                "We'll text you a 6-digit code to sign in.",
                style: AppText.small.copyWith(color: palette.textMuted),
              ),
              const SizedBox(height: 20),

              AppButton(
                label: 'Send code',
                loadingLabel: 'Sending…',
                loading: _submitting,
                size: AppButtonSize.lg,
                expand: true,
                onPressed: _submit,
              ),
              const SizedBox(height: 20),

              Center(
                child: Wrap(
                  alignment: WrapAlignment.center,
                  children: [
                    Text(
                      'New to Eksabli? ',
                      style: AppText.body.copyWith(
                        color: palette.textSecondary,
                      ),
                    ),
                    GestureDetector(
                      onTap: () => context.push(Routes.register),
                      child: Text(
                        'Create an account',
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
      ),
    );
  }
}
