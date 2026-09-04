import 'package:flutter/material.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';

/// `.field-label` + `.input` + `.field-error`, wired as one labelled field so
/// every form on the app lays out identically.
class AppField extends StatelessWidget {
  const AppField({
    super.key,
    required this.label,
    required this.controller,
    this.hint,
    this.optional = false,
    this.keyboardType,
    this.obscure = false,
    this.suffix,
    this.errorText,
    this.readOnly = false,
    this.onTap,
    this.textInputAction,
    this.onChanged,
  });

  final String label;
  final TextEditingController controller;
  final String? hint;

  /// Renders the `(optional)` hint the prototype puts next to optional labels.
  final bool optional;
  final TextInputType? keyboardType;
  final bool obscure;
  final Widget? suffix;
  final String? errorText;
  final bool readOnly;
  final VoidCallback? onTap;
  final TextInputAction? textInputAction;
  final ValueChanged<String>? onChanged;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Text(
              label,
              style: AppText.smallSemi.copyWith(
                fontSize: 13,
                color: palette.textSecondary,
              ),
            ),
            if (optional) ...[
              const SizedBox(width: 4),
              Text(
                '(optional)',
                style: AppText.small.copyWith(color: palette.textMuted),
              ),
            ],
          ],
        ),
        const SizedBox(height: 6),
        TextField(
          controller: controller,
          keyboardType: keyboardType,
          obscureText: obscure,
          readOnly: readOnly,
          onTap: onTap,
          onChanged: onChanged,
          textInputAction: textInputAction,
          style: AppText.body.copyWith(color: palette.textPrimary),
          decoration: InputDecoration(
            hintText: hint,
            suffixIcon: suffix,
            errorText: errorText,
            isDense: true,
          ),
        ),
      ],
    );
  }
}

/// A single OTP digit box (`.otp-box`), sized to the prototype's 56px height.
class OtpInput extends StatefulWidget {
  const OtpInput({
    super.key,
    required this.length,
    required this.onCompleted,
    required this.onChanged,
    this.hasError = false,
  });

  final int length;
  final ValueChanged<String> onCompleted;
  final ValueChanged<String> onChanged;
  final bool hasError;

  @override
  State<OtpInput> createState() => _OtpInputState();
}

class _OtpInputState extends State<OtpInput> {
  late final List<TextEditingController> _controllers = List.generate(
    widget.length,
    (_) => TextEditingController(),
  );
  late final List<FocusNode> _nodes = List.generate(
    widget.length,
    (_) => FocusNode(),
  );

  @override
  void dispose() {
    for (final c in _controllers) {
      c.dispose();
    }
    for (final n in _nodes) {
      n.dispose();
    }
    super.dispose();
  }

  String get _value => _controllers.map((c) => c.text).join();

  void _handleChanged(int index, String value) {
    if (value.length > 1) {
      // Paste / autofill of the whole code.
      final digits = value.replaceAll(RegExp(r'[^0-9]'), '');
      for (var i = 0; i < widget.length; i++) {
        _controllers[i].text = i < digits.length ? digits[i] : '';
      }
      _nodes[(digits.length - 1).clamp(0, widget.length - 1)].requestFocus();
    } else if (value.isNotEmpty && index < widget.length - 1) {
      _nodes[index + 1].requestFocus();
    }

    widget.onChanged(_value);
    if (_value.length == widget.length) widget.onCompleted(_value);
    setState(() {});
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return Row(
      children: [
        for (var i = 0; i < widget.length; i++) ...[
          if (i > 0) const SizedBox(width: 8),
          Expanded(
            child: SizedBox(
              height: 56,
              child: TextField(
                controller: _controllers[i],
                focusNode: _nodes[i],
                textAlign: TextAlign.center,
                keyboardType: TextInputType.number,
                style: AppText.h2.copyWith(color: palette.textPrimary),
                onChanged: (v) => _handleChanged(i, v),
                decoration: InputDecoration(
                  counterText: '',
                  contentPadding: EdgeInsets.zero,
                  enabledBorder: widget.hasError
                      ? const OutlineInputBorder(
                          borderRadius: AppRadius.rMd,
                          borderSide: BorderSide(color: AppColors.danger600),
                        )
                      : null,
                ),
              ),
            ),
          ),
        ],
      ],
    );
  }
}
