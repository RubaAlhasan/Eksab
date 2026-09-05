import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/auth/auth_exception.dart';
import '../../core/auth/registration.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_form.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/edit-profile.html`.
class EditProfileScreen extends ConsumerStatefulWidget {
  const EditProfileScreen({super.key});

  @override
  ConsumerState<EditProfileScreen> createState() => _EditProfileScreenState();
}

class _EditProfileScreenState extends ConsumerState<EditProfileScreen> {
  late final TextEditingController _firstName;
  late final TextEditingController _lastName;
  late final TextEditingController _phone;
  late final TextEditingController _email;

  DateTime? _dob;
  CustomerGender? _gender;

  String? _firstNameError;
  String? _emailError;
  bool _saving = false;

  static final _emailPattern = RegExp(r'.+@.+\..+');

  @override
  void initState() {
    super.initState();
    final customer = ref.read(currentCustomerProvider);
    _firstName = TextEditingController(text: customer.firstName);
    _lastName = TextEditingController(text: customer.lastName);
    _phone = TextEditingController(text: customer.phone);
    _email = TextEditingController(text: customer.email);
    _dob = customer.dateOfBirth;
    _gender = CustomerGender.fromLabel(customer.gender);
  }

  @override
  void dispose() {
    for (final c in [_firstName, _lastName, _phone, _email]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _save() async {
    final email = _email.text.trim();
    setState(() {
      _firstNameError = _firstName.text.trim().isEmpty ? 'Required' : null;
      _emailError = email.isEmpty || _emailPattern.hasMatch(email)
          ? null
          : 'Enter a valid email.';
    });
    if (_firstNameError != null || _emailError != null) return;

    setState(() => _saving = true);

    try {
      // Phone and email live on the ABP identity user, which this endpoint
      // does not own — only name, date of birth and gender are persisted here.
      await ref
          .read(sessionProvider.notifier)
          .updateProfile(
            firstName: _firstName.text.trim(),
            lastName: _lastName.text.trim(),
            dateOfBirth: _dob,
            gender: _gender,
          );

      if (!mounted) return;
      showAppToast(context, title: 'Profile updated');
      context.canPop() ? context.pop() : context.go(Routes.profile);
    } on AuthException catch (error) {
      if (!mounted) return;
      setState(() {
        _saving = false;
        _emailError = null;
        _firstNameError = null;
      });
      showAppToast(
        context,
        title: 'Could not save',
        message: error.message,
        icon: Icons.error_outline_rounded,
        accent: AppColors.danger600,
      );
    }
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
    final customer = ref.watch(currentCustomerProvider);

    return AppScaffold(
      backgroundColor: palette.surface,
      title: 'Edit Profile',
      onBack: () =>
          context.canPop() ? context.pop() : context.go(Routes.profile),
      bottomBar: AppButton(
        label: 'Save Changes',
        loadingLabel: 'Saving…',
        loading: _saving,
        size: AppButtonSize.lg,
        expand: true,
        onPressed: _save,
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(24, 20, 24, 24),
        children: [
          Center(
            child: Column(
              children: [
                Stack(
                  clipBehavior: Clip.none,
                  children: [
                    AppAvatar(
                      initials: customer.initials,
                      size: AvatarSize.xl,
                    ),
                    Positioned(
                      bottom: -4,
                      right: -4,
                      child: AppIconButton(
                        icon: Icons.edit_outlined,
                        size: 12,
                        tooltip: 'Change photo',
                        foreground: Colors.white,
                        background: AppColors.primary600,
                        onPressed: () => showAppToast(
                          context,
                          title: 'Photo upload',
                          message: 'Avatar upload lands with the media '
                              'storage endpoint.',
                          icon: Icons.info_outline_rounded,
                          accent: AppColors.info600,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 10),
                Text(
                  'Tap to change photo',
                  style: AppText.small.copyWith(color: palette.textMuted),
                ),
              ],
            ),
          ),
          const SizedBox(height: 24),

          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: AppField(
                  label: 'First name',
                  controller: _firstName,
                  errorText: _firstNameError,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: AppField(label: 'Last name', controller: _lastName),
              ),
            ],
          ),
          const SizedBox(height: 16),
          AppField(
            label: 'Phone number',
            controller: _phone,
            keyboardType: TextInputType.phone,
            readOnly: true,
          ),
          const SizedBox(height: 16),
          AppField(
            label: 'Email',
            controller: _email,
            keyboardType: TextInputType.emailAddress,
            errorText: _emailError,
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
                        decoration: const InputDecoration(isDense: true),
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
                    DropdownButtonFormField<CustomerGender>(
                      initialValue: _gender,
                      isDense: true,
                      hint: Text(
                        'Select',
                        style: AppText.body.copyWith(color: AppColors.slate400),
                      ),
                      items: [
                        for (final g in CustomerGender.values)
                          DropdownMenuItem(value: g, child: Text(g.label)),
                      ],
                      onChanged: (v) => setState(() => _gender = v),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
