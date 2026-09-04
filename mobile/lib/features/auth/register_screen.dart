import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_form.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/register.html`.
class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _firstName = TextEditingController();
  final _lastName = TextEditingController();
  final _phone = TextEditingController();
  final _email = TextEditingController();
  final _password = TextEditingController();

  DateTime? _dob;
  String? _gender;
  bool _acceptedTerms = false;
  bool _obscure = true;
  bool _submitting = false;
  bool _showFormAlert = false;

  final _errors = <String, String?>{};

  @override
  void dispose() {
    for (final c in [_firstName, _lastName, _phone, _email, _password]) {
      c.dispose();
    }
    super.dispose();
  }

  static final _phonePattern = RegExp(r'^[+\d][\d\s]{7,}$');
  static final _emailPattern = RegExp(r'.+@.+\..+');

  Future<void> _submit() async {
    final email = _email.text.trim();

    setState(() {
      _errors
        ..['firstName'] = _firstName.text.trim().isEmpty
            ? 'First name is required.'
            : null
        ..['lastName'] = _lastName.text.trim().isEmpty
            ? 'Last name is required.'
            : null
        ..['phone'] = _phonePattern.hasMatch(_phone.text.trim())
            ? null
            : 'Enter a valid phone number.'
        ..['email'] = email.isEmpty || _emailPattern.hasMatch(email)
            ? null
            : 'Enter a valid email address.'
        ..['dob'] = _dob == null ? 'Date of birth is required.' : null
        ..['password'] = _password.text.length >= 8
            ? null
            : 'Password must be at least 8 characters.'
        ..['terms'] = _acceptedTerms
            ? null
            : 'You must accept the terms to continue.';
      _showFormAlert = _errors.values.any((e) => e != null);
    });
    if (_showFormAlert) return;

    setState(() => _submitting = true);
    await Future<void>.delayed(const Duration(milliseconds: 1200));
    if (!mounted) return;
    context.push(Routes.otpVerify);
  }

  Future<void> _pickDob() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _dob ?? DateTime(now.year - 25),
      firstDate: DateTime(1920),
      lastDate: now,
    );
    if (picked != null) setState(() => _dob = picked);
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);

    return AppScaffold(
      backgroundColor: palette.surface,
      appBar: AppTopBar(
        title: '',
        onBack: () => context.canPop()
            ? context.pop()
            : context.go(Routes.onboarding),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(24, 8, 24, 32),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Create your account',
              style: AppText.display.copyWith(color: palette.textPrimary),
            ),
            const SizedBox(height: 4),
            Text(
              'One account, unlimited businesses. Takes less than a minute.',
              style: AppText.body.copyWith(color: palette.textSecondary),
            ),
            const SizedBox(height: 24),

            if (_showFormAlert) ...[
              const AppAlert(
                message: 'Please fix the highlighted fields and try again.',
                tone: AppTone.danger,
              ),
              const SizedBox(height: 16),
            ],

            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: AppField(
                    label: 'First name',
                    controller: _firstName,
                    hint: 'Layla',
                    errorText: _errors['firstName'],
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: AppField(
                    label: 'Last name',
                    controller: _lastName,
                    hint: 'Haddad',
                    errorText: _errors['lastName'],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            AppField(
              label: 'Phone number',
              controller: _phone,
              hint: '+971 50 123 4567',
              keyboardType: TextInputType.phone,
              errorText: _errors['phone'],
            ),
            const SizedBox(height: 16),
            AppField(
              label: 'Email',
              optional: true,
              controller: _email,
              hint: 'you@example.com',
              keyboardType: TextInputType.emailAddress,
              errorText: _errors['email'],
            ),
            const SizedBox(height: 16),

            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Date of birth',
                        style: AppText.smallSemi.copyWith(
                          fontSize: 13,
                          color: palette.textSecondary,
                        ),
                      ),
                      const SizedBox(height: 6),
                      InkWell(
                        onTap: _pickDob,
                        borderRadius: AppRadius.rMd,
                        child: InputDecorator(
                          decoration: InputDecoration(
                            isDense: true,
                            errorText: _errors['dob'],
                          ),
                          child: Text(
                            _dob == null
                                ? 'Select date'
                                : formatDate(_dob!, withYear: true),
                            style: AppText.body.copyWith(
                              color: _dob == null
                                  ? AppColors.slate400
                                  : palette.textPrimary,
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Gender',
                        style: AppText.smallSemi.copyWith(
                          fontSize: 13,
                          color: palette.textSecondary,
                        ),
                      ),
                      const SizedBox(height: 6),
                      DropdownButtonFormField<String>(
                        value: _gender,
                        isDense: true,
                        hint: Text(
                          'Select',
                          style: AppText.body.copyWith(color: AppColors.slate400),
                        ),
                        items: const [
                          DropdownMenuItem(value: 'Female', child: Text('Female')),
                          DropdownMenuItem(value: 'Male', child: Text('Male')),
                          DropdownMenuItem(
                            value: 'Prefer not to say',
                            child: Text('Prefer not to say'),
                          ),
                        ],
                        onChanged: (v) => setState(() => _gender = v),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),

            AppField(
              label: 'Password',
              controller: _password,
              hint: 'At least 8 characters',
              obscure: _obscure,
              errorText: _errors['password'],
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
            const SizedBox(height: 16),

            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SizedBox(
                  width: 24,
                  height: 24,
                  child: Checkbox(
                    value: _acceptedTerms,
                    onChanged: (v) =>
                        setState(() => _acceptedTerms = v ?? false),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Text.rich(
                    TextSpan(
                      style: AppText.small.copyWith(color: palette.textSecondary),
                      children: [
                        const TextSpan(text: 'I agree to the '),
                        TextSpan(
                          text: 'Terms of Service',
                          style: AppText.smallSemi.copyWith(
                            color: palette.primaryOnDarkAware,
                          ),
                        ),
                        const TextSpan(text: ' and '),
                        TextSpan(
                          text: 'Privacy Policy',
                          style: AppText.smallSemi.copyWith(
                            color: palette.primaryOnDarkAware,
                          ),
                        ),
                        const TextSpan(text: '.'),
                      ],
                    ),
                  ),
                ),
              ],
            ),
            if (_errors['terms'] != null) ...[
              const SizedBox(height: 6),
              Text(
                _errors['terms']!,
                style: AppText.small.copyWith(color: AppColors.danger600),
              ),
            ],
            const SizedBox(height: 20),

            AppButton(
              label: 'Create account',
              loadingLabel: 'Creating account…',
              loading: _submitting,
              size: AppButtonSize.lg,
              expand: true,
              onPressed: _submit,
            ),
            const SizedBox(height: 24),

            Center(
              child: Wrap(
                alignment: WrapAlignment.center,
                children: [
                  Text(
                    'Already have an account? ',
                    style: AppText.body.copyWith(color: palette.textSecondary),
                  ),
                  GestureDetector(
                    onTap: () => context.go(Routes.login),
                    child: Text(
                      'Log in',
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
