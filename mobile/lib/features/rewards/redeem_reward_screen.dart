import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_tabs.dart';
import '../../shared/widgets/qr_placeholder.dart';
import '../profile/error_screen.dart';

enum _RedemptionState { active, expired, success }

/// Prototype: `customer/redeem-reward.html`.
///
/// The token is short-lived by design — staff must validate it against the
/// server in real time, so an expired or offline code is never a valid
/// discount. The 6-second auto-confirm stands in for that staff scan until the
/// redemption endpoint is wired up.
class RedeemRewardScreen extends ConsumerStatefulWidget {
  const RedeemRewardScreen({super.key, required this.rewardId});

  final String rewardId;

  @override
  ConsumerState<RedeemRewardScreen> createState() => _RedeemRewardScreenState();
}

class _RedeemRewardScreenState extends ConsumerState<RedeemRewardScreen> {
  static const _tokenLifetime = Duration(minutes: 5);
  static const _staffConfirmDelay = Duration(seconds: 6);

  _RedemptionState _state = _RedemptionState.active;
  int _mode = 0; // 0 = QR, 1 = PIN
  int _secondsLeft = _tokenLifetime.inSeconds;
  Timer? _countdown;
  Timer? _staffConfirm;
  late String _pin;

  @override
  void initState() {
    super.initState();
    _start();
  }

  void _start() {
    _countdown?.cancel();
    _staffConfirm?.cancel();

    // Deterministic 4-digit PIN derived from the reward + the moment the token
    // was minted, so it stays stable for this token's lifetime.
    final seed = DateTime.now().millisecondsSinceEpoch ^ widget.rewardId.hashCode;
    _pin = (1000 + (seed.abs() % 9000)).toString();

    setState(() {
      _state = _RedemptionState.active;
      _secondsLeft = _tokenLifetime.inSeconds;
    });

    _countdown = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) return timer.cancel();
      setState(() => _secondsLeft--);
      if (_secondsLeft <= 0) {
        timer.cancel();
        _staffConfirm?.cancel();
        setState(() => _state = _RedemptionState.expired);
      }
    });

    _staffConfirm = Timer(_staffConfirmDelay, () {
      if (!mounted || _state != _RedemptionState.active) return;
      _countdown?.cancel();
      final reward = ref.read(rewardByIdProvider(widget.rewardId));
      if (reward != null) ref.read(couponsProvider.notifier).issue(reward);
      setState(() => _state = _RedemptionState.success);
    });
  }

  @override
  void dispose() {
    _countdown?.cancel();
    _staffConfirm?.cancel();
    super.dispose();
  }

  String get _formattedCountdown {
    final minutes = (_secondsLeft ~/ 60).toString().padLeft(2, '0');
    final seconds = (_secondsLeft % 60).toString().padLeft(2, '0');
    return '$minutes:$seconds';
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final reward = ref.watch(rewardByIdProvider(widget.rewardId));
    if (reward == null) return const ErrorScreen(kind: ErrorKind.notFound);

    return AppScaffold(
      backgroundColor: palette.surface,
      title: 'Redemption',
      body: switch (_state) {
        _RedemptionState.active => _buildActive(palette, reward.name),
        _RedemptionState.expired => _buildExpired(palette),
        _RedemptionState.success => _buildSuccess(palette),
      },
    );
  }

  Widget _buildActive(AppPalette palette, String rewardName) {
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(32, 16, 32, 32),
      child: Column(
        children: [
          Text(
            'Show this to staff at checkout',
            style: AppText.body.copyWith(color: palette.textMuted),
          ),
          const SizedBox(height: 4),
          Text(
            rewardName,
            textAlign: TextAlign.center,
            style: AppText.h2.copyWith(color: palette.textPrimary),
          ),
          const SizedBox(height: 24),

          PillTabs(
            labels: const ['QR Code', 'PIN'],
            selectedIndex: _mode,
            onChanged: (i) => setState(() => _mode = i),
          ),
          const SizedBox(height: 24),

          if (_mode == 0)
            QrPlaceholder(
              seed: 'redeem-${widget.rewardId}-$_pin',
              size: 224,
              modules: 7,
              borderRadius: 16,
            )
          else
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                for (final digit in _pin.split(''))
                  Container(
                    width: 48,
                    height: 56,
                    margin: const EdgeInsets.symmetric(horizontal: 6),
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: palette.isDark
                          ? AppColors.slate800
                          : AppColors.slate100,
                      borderRadius: AppRadius.rMd,
                    ),
                    child: Text(
                      digit,
                      style: AppText.display.copyWith(
                        color: palette.textPrimary,
                      ),
                    ),
                  ),
              ],
            ),
          const SizedBox(height: 24),

          AppCard(
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(
                  Icons.schedule_rounded,
                  size: 16,
                  color: palette.textSecondary,
                ),
                const SizedBox(width: 8),
                Text(
                  _formattedCountdown,
                  style: AppText.bodyBold.copyWith(color: palette.textPrimary),
                ),
                const SizedBox(width: 8),
                Flexible(
                  child: Text(
                    'until this token expires',
                    style: AppText.small.copyWith(color: palette.textMuted),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 32),

          AppButton(
            label: 'Cancel Redemption',
            variant: AppButtonVariant.secondary,
            expand: true,
            onPressed: () => context.canPop()
                ? context.pop()
                : context.go(Routes.reward(widget.rewardId)),
          ),
        ],
      ),
    );
  }

  Widget _buildExpired(AppPalette palette) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 32),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Container(
            width: 64,
            height: 64,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: AppColors.danger500.withValues(alpha: palette.isDark ? 0.12 : 0.1),
              borderRadius: AppRadius.rLg,
            ),
            child: const Icon(
              Icons.warning_amber_rounded,
              size: 26,
              color: AppColors.danger500,
            ),
          ),
          const SizedBox(height: 24),
          Text(
            'Token expired',
            style: AppText.h2.copyWith(color: palette.textPrimary),
          ),
          const SizedBox(height: 8),
          Text(
            'For security, redemption codes are single-use and expire quickly. '
            'Generate a new one to try again.',
            textAlign: TextAlign.center,
            style: AppText.body.copyWith(color: palette.textSecondary),
          ),
          const SizedBox(height: 32),
          AppButton(
            label: 'Generate New Code',
            size: AppButtonSize.lg,
            expand: true,
            onPressed: _start,
          ),
        ],
      ),
    );
  }

  Widget _buildSuccess(AppPalette palette) {
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
            'Redeemed!',
            style: AppText.h1.copyWith(color: palette.textPrimary),
          ),
          const SizedBox(height: 8),
          Text(
            'Staff confirmed your redemption. Enjoy!',
            textAlign: TextAlign.center,
            style: AppText.body.copyWith(color: palette.textSecondary),
          ),
          const SizedBox(height: 32),
          AppButton(
            label: 'View My Coupons',
            size: AppButtonSize.lg,
            expand: true,
            onPressed: () => context.go(Routes.coupons),
          ),
        ],
      ),
    );
  }
}
