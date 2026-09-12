import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/auth/auth_exception.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_tabs.dart';
import '../../shared/widgets/business_tiles.dart';
import '../profile/error_screen.dart';

/// Prototype: `customer/redeem-reward.html`.
///
/// **Two-phase redemption.** Confirming here RESERVES the customer's points and opens a `Pending`
/// coupon; it does not spend them. The reward becomes theirs only when a staff member approves the
/// code in the Business Portal's Redemption screen. Until then the points are held, and every exit
/// returns them — staff declining, the customer cancelling, or the reservation window lapsing
/// (`RedemptionReservationWorker`).
///
/// **The code shown IS the coupon code.** The QR encodes the same eight characters printed beneath
/// it, so staff can scan it or type it, and both paths hit the same server row. An earlier version of
/// this screen drew a locally-generated PIN that was never sent anywhere — staff scanning it got
/// "Invalid or already-used code", because it did not correspond to anything. The code now comes from
/// the server's response and nowhere else.
///
/// **Nothing here is on a timer except the display.** The previous implementation auto-confirmed six
/// seconds after the screen opened, standing in for a staff scan that had no UI to happen in; opening
/// the screen and walking away spent the points. Confirmation is now an explicit tap, the call is
/// awaited, and failures surface as themselves instead of a success screen.
class RedeemRewardScreen extends ConsumerStatefulWidget {
  const RedeemRewardScreen({
    super.key,
    required this.businessId,
    required this.rewardId,
  });

  final String businessId;
  final String rewardId;

  @override
  ConsumerState<RedeemRewardScreen> createState() => _RedeemRewardScreenState();
}

enum _Phase { confirm, submitting, pending, approved, declined, lapsed }

class _RedeemRewardScreenState extends ConsumerState<RedeemRewardScreen> {
  // Approvals arrive over the counter in seconds, but there is no push channel for them, so the
  // screen asks. Three seconds keeps "staff tapped Approve" and "customer sees it" close enough to
  // feel immediate without hammering a single-row endpoint.
  static const _pollInterval = Duration(seconds: 3);

  _Phase _phase = _Phase.confirm;
  int _mode = 0; // 0 = QR, 1 = code
  Coupon? _coupon;
  String? _error;
  bool _cancelling = false;

  Timer? _poll;
  Timer? _ticker;
  Duration? _remaining;

  @override
  void dispose() {
    _poll?.cancel();
    _ticker?.cancel();
    super.dispose();
  }

  Future<void> _confirm() async {
    setState(() {
      _phase = _Phase.submitting;
      _error = null;
    });

    try {
      final coupon = await ref
          .read(couponsProvider.notifier)
          .redeem(tenantId: widget.businessId, rewardId: widget.rewardId);

      if (!mounted) return;
      setState(() {
        _coupon = coupon;
        _phase = _resolvePhase(coupon);
      });
      _startWatching();
    } on AuthException catch (error) {
      if (!mounted) return;
      // Server-side rejections ("out of stock", "not enough points", "reward expired") are the
      // customer's answer, not an internal fault — show them rather than a generic failure.
      setState(() {
        _phase = _Phase.confirm;
        _error = error.message;
      });
    }
  }

  /// Starts the outcome poll and the countdown that runs beside it.
  void _startWatching() {
    _poll?.cancel();
    _ticker?.cancel();
    if (_phase != _Phase.pending) return;

    _tickCountdown();
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) => _tickCountdown());
    _poll = Timer.periodic(_pollInterval, (_) => _checkOutcome());
  }

  void _stopWatching() {
    _poll?.cancel();
    _ticker?.cancel();
    _poll = null;
    _ticker = null;
  }

  void _tickCountdown() {
    final coupon = _coupon;
    if (!mounted || coupon == null) return;

    final left = coupon.remaining(DateTime.now());
    setState(() => _remaining = left);

    if (left != null && left == Duration.zero) {
      // The server's own sweep is the authority on releasing the hold; this only stops presenting a
      // code that staff can no longer accept.
      _stopWatching();
      setState(() => _phase = _Phase.lapsed);
    }
  }

  Future<void> _checkOutcome() async {
    final coupon = _coupon;
    if (!mounted || coupon == null) return;

    try {
      final fresh = await ref
          .read(couponsProvider.notifier)
          .refreshOne(tenantId: widget.businessId, couponId: coupon.id);

      if (!mounted || fresh.status == coupon.status) return;

      setState(() {
        _coupon = fresh;
        _phase = _resolvePhase(fresh);
      });

      if (_phase != _Phase.pending) {
        _stopWatching();
        // The outcome moved points one way or the other, so balances are stale.
        ref.invalidate(membershipsProvider);
        ref.invalidate(couponsProvider);
      }
    } catch (_) {
      // A dropped poll is not worth surfacing — the next tick retries, and the countdown already
      // tells the customer how long this can go on.
    }
  }

  static _Phase _resolvePhase(Coupon coupon) => switch (coupon.status) {
    CouponStatus.pending || CouponStatus.issued => _Phase.pending,
    CouponStatus.redeemed => _Phase.approved,
    CouponStatus.cancelled => _Phase.declined,
    CouponStatus.expired => _Phase.lapsed,
  };

  Future<void> _cancel() async {
    final coupon = _coupon;
    if (coupon == null || _cancelling) return;

    setState(() => _cancelling = true);
    try {
      await ref
          .read(couponsProvider.notifier)
          .cancel(tenantId: widget.businessId, couponId: coupon.id);

      if (!mounted) return;
      _stopWatching();
      if (context.canPop()) {
        context.pop();
      } else {
        context.go(Routes.reward(widget.businessId, widget.rewardId));
      }
    } on AuthException catch (error) {
      if (!mounted) return;
      setState(() {
        _cancelling = false;
        _error = error.message;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final reward = ref
        .watch(
          rewardByIdProvider(
            RewardKey(businessId: widget.businessId, rewardId: widget.rewardId),
          ),
        )
        .valueOrNull;

    if (reward == null) return const ErrorScreen(kind: ErrorKind.notFound);

    return AppScaffold(
      backgroundColor: palette.surface,
      title: 'Redemption',
      body: switch (_phase) {
        _Phase.confirm || _Phase.submitting => _buildConfirm(palette, reward),
        _Phase.pending => _buildPending(palette, reward),
        _Phase.approved => _buildApproved(palette, reward),
        _Phase.declined => _buildDeclined(palette),
        _Phase.lapsed => _buildLapsed(palette),
      },
    );
  }

  // ---------------------------------------------------------------------------------------------
  // Confirm — the customer commits, nothing has happened yet
  // ---------------------------------------------------------------------------------------------

  Widget _buildConfirm(AppPalette palette, Reward reward) {
    final submitting = _phase == _Phase.submitting;

    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(24, 16, 24, 32),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(vertical: 24, horizontal: 20),
            decoration: BoxDecoration(
              color: palette.isDark ? AppColors.slate800 : AppColors.slate100,
              borderRadius: AppRadius.rLg,
            ),
            child: Column(
              children: [
                Icon(reward.type.icon, size: 32, color: reward.type.tone),
                const SizedBox(height: 12),
                Text(
                  reward.name,
                  textAlign: TextAlign.center,
                  style: AppText.h2.copyWith(color: palette.textPrimary),
                ),
                const SizedBox(height: 8),
                Text(
                  '${formatPoints(reward.pointsCost)} pts',
                  style: AppText.bodyBold.copyWith(color: AppColors.primary500),
                ),
              ],
            ),
          ),
          const SizedBox(height: 24),

          Text(
            'Confirm redemption',
            style: AppText.bodyBold.copyWith(color: palette.textPrimary),
          ),
          const SizedBox(height: 8),
          Text(
            'Your points are held now, not spent. Show the code to staff — they '
            'approve it and hand over your reward. If they decline, or you '
            'change your mind, the points come straight back.',
            style: AppText.body.copyWith(color: palette.textSecondary),
          ),
          const SizedBox(height: 24),

          if (_error != null) ...[
            AppAlert(tone: AppTone.danger, message: _error!),
            const SizedBox(height: 16),
          ],

          AppButton(
            label: submitting ? 'Getting your code…' : 'Get Redemption Code',
            size: AppButtonSize.lg,
            expand: true,
            loading: submitting,
            onPressed: submitting ? null : _confirm,
          ),
          const SizedBox(height: 8),
          AppButton(
            label: 'Not Now',
            variant: AppButtonVariant.secondary,
            expand: true,
            onPressed: submitting
                ? null
                : () => context.canPop()
                      ? context.pop()
                      : context.go(Routes.reward(widget.businessId, widget.rewardId)),
          ),
        ],
      ),
    );
  }

  // ---------------------------------------------------------------------------------------------
  // Pending — the code is live and staff have not acted yet
  // ---------------------------------------------------------------------------------------------

  Widget _buildPending(AppPalette palette, Reward reward) {
    final coupon = _coupon!;

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
            reward.name,
            textAlign: TextAlign.center,
            style: AppText.h2.copyWith(color: palette.textPrimary),
          ),
          const SizedBox(height: 24),

          PillTabs(
            labels: const ['QR Code', 'Code'],
            selectedIndex: _mode,
            onChanged: (i) => setState(() => _mode = i),
          ),
          const SizedBox(height: 24),

          if (_mode == 0)
            _QrCard(code: coupon.code, palette: palette)
          else
            _CodeCard(code: coupon.formattedCode, palette: palette),

          const SizedBox(height: 16),

          // Present under BOTH tabs. A counter with no working camera needs the characters without
          // the customer hunting for a second tab, and reading eight hex characters aloud across a
          // counter goes wrong often enough that copying is worth a tap — staff can be sent the code
          // directly, and the customer can paste it back if their own screen is hard to read.
          _CopyableCode(code: coupon.code, formatted: coupon.formattedCode, palette: palette),

          const SizedBox(height: 24),

          AppCard(
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
            child: Row(
              children: [
                SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(
                    strokeWidth: 2,
                    color: AppColors.primary500,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Waiting for staff to approve',
                        style: AppText.bodyBold.copyWith(color: palette.textPrimary),
                      ),
                      if (_remaining != null)
                        Text(
                          'Expires in ${_formatDuration(_remaining!)}',
                          style: AppText.small.copyWith(color: palette.textMuted),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),

          Text(
            '${formatPoints(coupon.pointsCost)} pts are held until staff decide. '
            'They have not left your balance.',
            textAlign: TextAlign.center,
            style: AppText.small.copyWith(color: palette.textMuted),
          ),

          if (_error != null) ...[
            const SizedBox(height: 16),
            AppAlert(tone: AppTone.danger, message: _error!),
          ],

          const SizedBox(height: 24),
          AppButton(
            label: 'Cancel & Get Points Back',
            variant: AppButtonVariant.secondary,
            expand: true,
            loading: _cancelling,
            onPressed: _cancelling ? null : _cancel,
          ),
        ],
      ),
    );
  }

  // ---------------------------------------------------------------------------------------------
  // Outcomes
  // ---------------------------------------------------------------------------------------------

  Widget _buildApproved(AppPalette palette, Reward reward) => _Outcome(
    palette: palette,
    icon: Icons.check_circle_outline_rounded,
    tint: AppColors.success500,
    iconColor: palette.isDark ? AppColors.success300 : AppColors.success600,
    title: 'Redeemed!',
    message: 'Staff approved your redemption. Enjoy your ${reward.name}!',
    primaryLabel: 'View My Coupons',
    onPrimary: () => context.go(Routes.coupons),
  );

  Widget _buildDeclined(AppPalette palette) {
    final reason = _coupon?.rejectionReason;
    return _Outcome(
      palette: palette,
      icon: Icons.undo_rounded,
      tint: AppColors.primary500,
      iconColor: AppColors.primary500,
      title: 'Not approved',
      message: reason != null && reason.isNotEmpty
          ? 'Staff declined this redemption: "$reason". '
                'Your ${formatPoints(_coupon?.pointsCost ?? 0)} points are back in your balance.'
          : 'Staff declined this redemption. Your '
                '${formatPoints(_coupon?.pointsCost ?? 0)} points are back in your balance.',
      primaryLabel: 'Back to Rewards',
      onPrimary: () => context.go(Routes.rewards(widget.businessId)),
    );
  }

  Widget _buildLapsed(AppPalette palette) => _Outcome(
    palette: palette,
    icon: Icons.schedule_rounded,
    tint: AppColors.warning500,
    iconColor: AppColors.warning500,
    title: 'Code expired',
    message: 'Nobody approved this in time, so your points have been returned. '
        'Start again when you are at the counter.',
    primaryLabel: 'Try Again',
    onPrimary: () {
      _stopWatching();
      setState(() {
        _phase = _Phase.confirm;
        _coupon = null;
        _remaining = null;
        _error = null;
      });
    },
  );

  static String _formatDuration(Duration d) {
    final minutes = d.inMinutes.toString().padLeft(2, '0');
    final seconds = (d.inSeconds % 60).toString().padLeft(2, '0');
    return '$minutes:$seconds';
  }
}

/// The scannable code. Always on a white ground with a dark foreground, in both themes — a QR
/// inverted for dark mode is unreadable to a good share of scanners, which assume dark-on-light.
class _QrCard extends StatelessWidget {
  const _QrCard({required this.code, required this.palette});

  final String code;
  final AppPalette palette;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: AppRadius.rLg,
        border: Border.all(color: palette.borderSubtle),
      ),
      child: QrImageView(
        // Keyed and labelled by the code itself: a screen reader announces something useful instead
        // of "qr code", and it makes the payload assertable — qr_flutter keeps `data` private, and
        // a QR encoding the wrong string is exactly the bug that shipped here once already.
        key: ValueKey('redemption-qr-$code'),
        semanticsLabel: 'Redemption code $code',
        data: code,
        size: 208,
        backgroundColor: Colors.white,
        eyeStyle: const QrEyeStyle(
          eyeShape: QrEyeShape.square,
          color: Color(0xFF0F172A),
        ),
        dataModuleStyle: const QrDataModuleStyle(
          dataModuleShape: QrDataModuleShape.square,
          color: Color(0xFF0F172A),
        ),
      ),
    );
  }
}

/// The code as text, with a one-tap copy.
///
/// Copies the RAW eight characters, not the spaced form shown on screen. The server normalizes
/// whitespace either way, but the Business Portal's own field is what this usually lands in, and a
/// clean `3B02543F` is what staff expect to see once it is pasted.
///
/// The whole row is tappable as well as the icon — the icon is what makes it discoverable, but a
/// target this small is easy to miss on a phone held at arm's length over a counter.
class _CopyableCode extends StatelessWidget {
  const _CopyableCode({
    required this.code,
    required this.formatted,
    required this.palette,
  });

  final String code;
  final String formatted;
  final AppPalette palette;

  Future<void> _copy(BuildContext context) async {
    await Clipboard.setData(ClipboardData(text: code));
    if (!context.mounted) return;
    showAppToast(context, title: 'Code copied');
  }

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: 'Redemption code $formatted, tap to copy',
      child: InkWell(
        onTap: () => _copy(context),
        borderRadius: AppRadius.rMd,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                formatted,
                style: AppText.bodyBold.copyWith(
                  color: palette.textSecondary,
                  letterSpacing: 3,
                  fontFeatures: const [FontFeature.tabularFigures()],
                ),
              ),
              const SizedBox(width: 4),
              AppIconButton(
                icon: Icons.copy_rounded,
                tooltip: 'Copy code',
                size: 16,
                onPressed: () => _copy(context),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The typed fallback, sized to be read across a counter.
class _CodeCard extends StatelessWidget {
  const _CodeCard({required this.code, required this.palette});

  final String code;
  final AppPalette palette;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(vertical: 32, horizontal: 16),
      decoration: BoxDecoration(
        color: palette.isDark ? AppColors.slate800 : AppColors.slate100,
        borderRadius: AppRadius.rLg,
      ),
      child: Text(
        code,
        textAlign: TextAlign.center,
        style: AppText.display.copyWith(
          color: palette.textPrimary,
          letterSpacing: 6,
          fontFeatures: const [FontFeature.tabularFigures()],
        ),
      ),
    );
  }
}

class _Outcome extends StatelessWidget {
  const _Outcome({
    required this.palette,
    required this.icon,
    required this.tint,
    required this.iconColor,
    required this.title,
    required this.message,
    required this.primaryLabel,
    required this.onPrimary,
  });

  final AppPalette palette;
  final IconData icon;
  final Color tint;
  final Color iconColor;
  final String title;
  final String message;
  final String primaryLabel;
  final VoidCallback onPrimary;

  @override
  Widget build(BuildContext context) {
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
              color: tint.withValues(alpha: palette.isDark ? 0.12 : 0.1),
              shape: BoxShape.circle,
            ),
            child: Icon(icon, size: 38, color: iconColor),
          ),
          const SizedBox(height: 24),
          Text(title, style: AppText.h1.copyWith(color: palette.textPrimary)),
          const SizedBox(height: 8),
          Text(
            message,
            textAlign: TextAlign.center,
            style: AppText.body.copyWith(color: palette.textSecondary),
          ),
          const SizedBox(height: 32),
          AppButton(
            label: primaryLabel,
            size: AppButtonSize.lg,
            expand: true,
            onPressed: onPrimary,
          ),
        ],
      ),
    );
  }
}
