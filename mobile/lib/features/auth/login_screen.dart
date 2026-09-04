import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/auth/auth_exception.dart';
import '../../core/config/app_config.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_form.dart';

/// Prototype: `customer/login.html`.
///
/// This is a real login: it performs an OpenIddict password grant against the
/// same auth server and the same `Eksabli_App` client the Angular app uses,
/// then loads the profile. Failures show the server's own message rather than
/// a generic one.
class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _identifier = TextEditingController();
  final _password = TextEditingController();

  String? _identifierError;
  String? _passwordError;

  /// The server's message for a rejected login, shown verbatim.
  String? _formError;
  bool _credentialsRejected = false;
  bool _obscure = true;
  bool _remember = true;
  bool _submitting = false;

  @override
  void dispose() {
    _identifier.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final identifier = _identifier.text.trim();
    final password = _password.text;

    setState(() {
      _formError = null;
      _credentialsRejected = false;
      _identifierError = identifier.isEmpty ? 'Enter your phone or email.' : null;
      _passwordError = password.isEmpty ? 'Enter your password.' : null;
    });
    if (_identifierError != null || _passwordError != null) return;

    setState(() => _submitting = true);

    try {
      await ref
          .read(sessionProvider.notifier)
          .signIn(username: identifier, password: password);

      // The router's guard moves us to Home the moment the session resolves,
      // so there is nothing to navigate to here.
    } on AuthException catch (error) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _formError = error.message;
        _credentialsRejected = error.isCredentialError;
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
                label: 'Phone or email',
                controller: _identifier,
                hint: '+971 50 123 4567',
                keyboardType: TextInputType.emailAddress,
                errorText: _identifierError,
              ),
              const SizedBox(height: 16),
              AppField(
                label: 'Password',
                controller: _password,
                hint: 'Your password',
                obscure: _obscure,
                errorText:
                    _passwordError ?? (_credentialsRejected ? '' : null),
                suffix: IconButton(
                  icon: Icon(
                    _obscure
                        ? Icons.visibility_outlined
                        : Icons.visibility_off_outlined,
                    size: 18,
                    color: palette.textMuted,
                  ),
                  onPressed: () => setState(() => _obscure = !_obscure),
                ),
              ),
              const SizedBox(height: 6),
              Text(
                'Signing in to ${AppConfig.baseUrl}',
                style: AppText.small.copyWith(color: palette.textMuted),
              ),
              const SizedBox(height: 12),

              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Row(
                    children: [
                      SizedBox(
                        width: 24,
                        height: 24,
                        child: Checkbox(
                          value: _remember,
                          onChanged: (v) =>
                              setState(() => _remember = v ?? false),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(
                        'Remember me',
                        style: AppText.body.copyWith(
                          color: palette.textSecondary,
                        ),
                      ),
                    ],
                  ),
                  GestureDetector(
                    onTap: () => context.push(Routes.forgotPassword),
                    child: Text(
                      'Forgot password?',
                      style: AppText.bodySemi.copyWith(
                        color: palette.primaryOnDarkAware,
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 20),

              AppButton(
                label: 'Log in',
                loadingLabel: 'Logging in…',
                loading: _submitting,
                size: AppButtonSize.lg,
                expand: true,
                onPressed: _submit,
              ),
              const SizedBox(height: 20),

              Row(
                children: [
                  Expanded(child: Divider(color: palette.borderSubtle)),
                  Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    child: Text(
                      'or',
                      style: AppText.small.copyWith(color: palette.textMuted),
                    ),
                  ),
                  Expanded(child: Divider(color: palette.borderSubtle)),
                ],
              ),
              const SizedBox(height: 20),

              AppButton(
                label: 'Log in with OTP instead',
                icon: Icons.smartphone_rounded,
                variant: AppButtonVariant.secondary,
                size: AppButtonSize.lg,
                expand: true,
                onPressed: () => context.push(Routes.otpVerify),
              ),
              const SizedBox(height: 24),

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
