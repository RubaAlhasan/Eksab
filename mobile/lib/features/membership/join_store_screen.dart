import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_form.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_tabs.dart';
import '../../shared/widgets/qr_placeholder.dart';
import '../profile/error_screen.dart';

/// Prototype: `customer/join-store.html` — scan-QR or tap-to-join, with an
/// optional referral code, then a success state.
class JoinStoreScreen extends ConsumerStatefulWidget {
  const JoinStoreScreen({super.key, required this.businessId});

  final String businessId;

  @override
  ConsumerState<JoinStoreScreen> createState() => _JoinStoreScreenState();
}

class _JoinStoreScreenState extends ConsumerState<JoinStoreScreen> {
  final _referral = TextEditingController();
  int _mode = 0; // 0 = scan QR, 1 = just join
  bool _submitting = false;
  bool _joined = false;

  @override
  void dispose() {
    _referral.dispose();
    super.dispose();
  }

  Future<void> _join() async {
    setState(() => _submitting = true);
    await Future<void>.delayed(const Duration(milliseconds: 1200));
    if (!mounted) return;

    final business = ref.read(businessByIdProvider(widget.businessId));
    if (business != null) {
      ref.read(membershipsProvider.notifier).join(business);
      ref.read(businessesProvider.notifier).markJoined(business.id);
    }
    setState(() {
      _submitting = false;
      _joined = true;
    });
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final business = ref.watch(businessByIdProvider(widget.businessId));
    if (business == null) return const ErrorScreen(kind: ErrorKind.notFound);

    return AppScaffold(
      backgroundColor: palette.surface,
      title: 'Join Business',
      body: _joined
          ? Padding(
              padding: const EdgeInsets.symmetric(horizontal: 32),
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Container(
                    width: 80,
                    height: 80,
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: AppColors.success500.withValues(alpha: palette.isDark ? 0.12 : 0.1),
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
                      style: AppText.body.copyWith(
                        color: palette.textSecondary,
                      ),
                      children: [
                        const TextSpan(text: "You've joined "),
                        TextSpan(
                          text: business.name,
                          style: AppText.bodySemi.copyWith(
                            color: palette.textPrimary,
                          ),
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
                    onPressed: () => context.go(Routes.points(business.id)),
                  ),
                ],
              ),
            )
          : SingleChildScrollView(
              padding: const EdgeInsets.fromLTRB(24, 16, 24, 32),
              child: Column(
                children: [
                  BusinessLogo(
                    initials: business.initials,
                    gradient: business.gradient,
                    size: 64,
                    radius: 24,
                    fontSize: 20,
                  ),
                  const SizedBox(height: 16),
                  Text(
                    business.name,
                    textAlign: TextAlign.center,
                    style: AppText.h2.copyWith(color: palette.textPrimary),
                  ),
                  Text(
                    business.category,
                    style: AppText.body.copyWith(color: palette.textMuted),
                  ),
                  const SizedBox(height: 24),

                  PillTabs(
                    labels: const ['Scan QR', 'Just Join'],
                    selectedIndex: _mode,
                    onChanged: (i) => setState(() => _mode = i),
                  ),
                  const SizedBox(height: 24),

                  if (_mode == 0) ...[
                    QrPlaceholder(
                      seed: 'join-${business.id}',
                      size: 192,
                      modules: 6,
                      padding: 16,
                      borderRadius: 16,
                    ),
                    const SizedBox(height: 16),
                    ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 256),
                      child: Text(
                        'Point your camera at the QR code displayed at any '
                        'branch counter to join instantly.',
                        textAlign: TextAlign.center,
                        style: AppText.small.copyWith(color: palette.textMuted),
                      ),
                    ),
                  ] else
                    const AppAlert(
                      message:
                          'You can also join without scanning — just confirm '
                          'below.',
                    ),
                  const SizedBox(height: 24),

                  AppField(
                    label: 'Referral code',
                    optional: true,
                    controller: _referral,
                    hint: 'e.g. LAYLA25',
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
            ),
    );
  }
}
