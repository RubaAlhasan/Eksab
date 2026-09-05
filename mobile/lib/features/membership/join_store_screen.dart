import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../core/auth/auth_exception.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';

/// Prototype: `customer/join-store.html`.
///
/// `POST /api/app/memberships/join` is the real call; the server rejects joins
/// for businesses that are not Approved, and that message is shown verbatim.
class JoinStoreScreen extends ConsumerStatefulWidget {
  const JoinStoreScreen({super.key, required this.businessId});

  final String businessId;

  @override
  ConsumerState<JoinStoreScreen> createState() => _JoinStoreScreenState();
}

class _JoinStoreScreenState extends ConsumerState<JoinStoreScreen> {
  bool _submitting = false;
  bool _joined = false;
  String? _error;

  Future<void> _join() async {
    setState(() {
      _submitting = true;
      _error = null;
    });

    try {
      await ref
          .read(membershipsProvider.notifier)
          .join(widget.businessId);
      ref.invalidate(businessByIdProvider(widget.businessId));
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _joined = true;
      });
    } on AuthException catch (error) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _error = error.message;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final business = ref.watch(businessByIdProvider(widget.businessId));

    return AppScaffold(
      backgroundColor: palette.surface,
      title: 'Join Business',
      body: AsyncSection<Business>(
        value: business,
        onRetry: () => ref.invalidate(businessByIdProvider(widget.businessId)),
        data: (biz) => _joined
            ? _success(context, palette, biz)
            : _form(context, palette, biz),
      ),
    );
  }

  Widget _form(BuildContext context, AppPalette palette, Business biz) {
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(24, 16, 24, 32),
      child: Column(
        children: [
          BusinessLogo(
            initials: biz.initials,
            gradient: biz.gradient,
            size: 64,
            radius: 24,
            fontSize: 20,
          ),
          const SizedBox(height: 16),
          Text(
            biz.name,
            textAlign: TextAlign.center,
            style: AppText.h2.copyWith(color: palette.textPrimary),
          ),
          Text(
            biz.category,
            style: AppText.body.copyWith(color: palette.textMuted),
          ),
          const SizedBox(height: 24),

          if (_error != null) ...[
            AppAlert(message: _error!, tone: AppTone.danger),
            const SizedBox(height: 16),
          ],

          const AppAlert(
            message:
                'Joining is free. You can start collecting points on your '
                'next visit.',
          ),
          const SizedBox(height: 32),

          AppButton(
            label: 'Join Business',
            loadingLabel: 'Joining…',
            loading: _submitting,
            size: AppButtonSize.lg,
            expand: true,
            onPressed: _join,
          ),
        ],
      ),
    );
  }

  Widget _success(BuildContext context, AppPalette palette, Business biz) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 32),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Container(
            width: 80,
            height: 80,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: AppColors.success500.withValues(
                alpha: palette.isDark ? 0.12 : 0.1,
              ),
              shape: BoxShape.circle,
            ),
            child: Icon(
              Icons.check_circle_outline_rounded,
              size: 40,
              color: palette.isDark
                  ? AppColors.success300
                  : AppColors.success600,
            ),
          ),
          const SizedBox(height: 24),
          Text(
            "You're in!",
            style: AppText.h1.copyWith(color: palette.textPrimary),
          ),
          const SizedBox(height: 8),
          Text.rich(
            textAlign: TextAlign.center,
            TextSpan(
              style: AppText.body.copyWith(color: palette.textSecondary),
              children: [
                const TextSpan(text: "You've joined "),
                TextSpan(
                  text: biz.name,
                  style: AppText.bodySemi.copyWith(color: palette.textPrimary),
                ),
                const TextSpan(
                  text: '. Start earning points on your next visit.',
                ),
              ],
            ),
          ),
          const SizedBox(height: 32),
          AppButton(
            label: 'View My Points',
            size: AppButtonSize.lg,
            expand: true,
            onPressed: () => context.go(Routes.points(biz.id)),
          ),
        ],
      ),
    );
  }
}
