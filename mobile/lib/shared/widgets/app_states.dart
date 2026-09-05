import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/auth/auth_exception.dart';

/// `.empty-state` — tinted icon square, headline, hint, optional action.
class EmptyState extends StatelessWidget {
  const EmptyState({
    super.key,
    required this.icon,
    required this.title,
    this.message,
    this.action,
  });

  final IconData icon;
  final String title;
  final String? message;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 48),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 56,
            height: 56,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: palette.isDark
                  ? const Color(0x1F6248E3)
                  : AppColors.primary50,
              borderRadius: AppRadius.rLg,
            ),
            child: Icon(icon, size: 26, color: palette.primaryOnDarkAware),
          ),
          const SizedBox(height: 16),
          Text(
            title,
            textAlign: TextAlign.center,
            style: AppText.bodyBold.copyWith(color: palette.textPrimary),
          ),
          if (message != null) ...[
            const SizedBox(height: 4),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 256),
              child: Text(
                message!,
                textAlign: TextAlign.center,
                style: AppText.small.copyWith(color: palette.textMuted),
              ),
            ),
          ],
          if (action != null) ...[const SizedBox(height: 16), action!],
        ],
      ),
    );
  }
}

/// `.skeleton` — shimmering placeholder block shown while a screen's data
/// loads. The prototype fakes a ~500ms latency; screens here do the same so the
/// loading state stays exercised until real endpoints land.
class Skeleton extends StatefulWidget {
  const Skeleton({
    super.key,
    this.height = 16,
    this.width,
    this.radius = 12,
  });

  final double height;
  final double? width;
  final double radius;

  @override
  State<Skeleton> createState() => _SkeletonState();
}

class _SkeletonState extends State<Skeleton>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1600),
  )..repeat();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isDark = AppPalette.of(context).isDark;
    final base = isDark ? AppColors.slate800 : AppColors.slate200;
    final highlight = isDark
        ? Colors.white.withValues(alpha: 0.06)
        : Colors.white.withValues(alpha: 0.5);

    return ClipRRect(
      borderRadius: BorderRadius.circular(widget.radius),
      child: AnimatedBuilder(
        animation: _controller,
        builder: (context, _) {
          final t = _controller.value;
          return Container(
            height: widget.height,
            width: widget.width,
            decoration: BoxDecoration(
              color: base,
              gradient: LinearGradient(
                begin: Alignment(-1 + t * 2 - 0.3, 0),
                end: Alignment(-1 + t * 2 + 0.3, 0),
                colors: [base, highlight, base],
              ),
            ),
          );
        },
      ),
    );
  }
}

/// `.progress-track` / `.progress-fill` — tier progress bar.
class ProgressTrack extends StatelessWidget {
  const ProgressTrack({super.key, required this.percent, this.height = 8});

  /// 0–100, matching the prototype's `tierProgressPct`.
  final int percent;
  final double height;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return ClipRRect(
      borderRadius: BorderRadius.circular(999),
      child: Stack(
        children: [
          Container(
            height: height,
            color: palette.isDark ? AppColors.slate800 : AppColors.slate100,
          ),
          FractionallySizedBox(
            widthFactor: (percent.clamp(0, 100)) / 100,
            child: AnimatedContainer(
              duration: const Duration(milliseconds: 500),
              curve: Curves.easeOut,
              height: height,
              decoration: const BoxDecoration(
                gradient: LinearGradient(
                  colors: [AppColors.primary400, AppColors.primary600],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// The `text-xs font-bold uppercase tracking-wide text-slate-400` group label
/// used above card lists on Profile, Settings, and Help.
class SectionLabel extends StatelessWidget {
  const SectionLabel(this.label, {super.key, this.tone});

  final String label;
  final Color? tone;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return Padding(
      padding: const EdgeInsets.only(left: 4, bottom: 8),
      child: Text(
        label.toUpperCase(),
        style: AppText.overline.copyWith(color: tone ?? palette.textMuted),
      ),
    );
  }
}

/// A `Section header + "See all"` row, as used across Home and My Points.
class SectionHeader extends StatelessWidget {
  const SectionHeader({
    super.key,
    required this.title,
    this.actionLabel,
    this.onAction,
  });

  final String title;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            title,
            style: AppText.bodyBold.copyWith(color: palette.textPrimary),
          ),
          if (actionLabel != null)
            GestureDetector(
              onTap: onAction,
              behavior: HitTestBehavior.opaque,
              child: Text(
                actionLabel!,
                style: AppText.smallSemi.copyWith(
                  color: palette.primaryOnDarkAware,
                ),
              ),
            ),
        ],
      ),
    );
  }
}


/// Renders an [AsyncValue] with consistent loading, error and empty states so
/// every screen fails the same way instead of each inventing its own.
class AsyncSection<T> extends StatelessWidget {
  const AsyncSection({
    super.key,
    required this.value,
    required this.data,
    this.loading,
    this.onRetry,
    this.errorTitle = 'Could not load this',
  });

  final AsyncValue<T> value;
  final Widget Function(T value) data;

  /// Defaults to a shimmer block; pass a shaped skeleton where the screen has
  /// one so the layout does not jump when real data arrives.
  final Widget? loading;
  final VoidCallback? onRetry;
  final String errorTitle;

  @override
  Widget build(BuildContext context) {
    return value.when(
      data: data,
      loading: () =>
          loading ??
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 24),
            child: Skeleton(height: 88, radius: 16),
          ),
      error: (error, _) => ErrorState(
        title: errorTitle,
        // AuthException already carries a message written for a person; for
        // anything else, don't leak a raw stack trace into the UI.
        message: error is AuthException
            ? error.message
            : 'Something went wrong. Please try again.',
        onRetry: onRetry,
      ),
    );
  }
}

/// Failure state with an optional retry — the counterpart to [EmptyState].
class ErrorState extends StatelessWidget {
  const ErrorState({
    super.key,
    required this.title,
    this.message,
    this.onRetry,
  });

  final String title;
  final String? message;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 40),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 56,
            height: 56,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: AppColors.danger500.withValues(
                alpha: palette.isDark ? 0.14 : 0.1,
              ),
              borderRadius: AppRadius.rLg,
            ),
            child: const Icon(
              Icons.cloud_off_rounded,
              size: 26,
              color: AppColors.danger500,
            ),
          ),
          const SizedBox(height: 16),
          Text(
            title,
            textAlign: TextAlign.center,
            style: AppText.bodyBold.copyWith(color: palette.textPrimary),
          ),
          if (message != null) ...[
            const SizedBox(height: 4),
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 280),
              child: Text(
                message!,
                textAlign: TextAlign.center,
                style: AppText.small.copyWith(color: palette.textMuted),
              ),
            ),
          ],
          if (onRetry != null) ...[
            const SizedBox(height: 16),
            TextButton(onPressed: onRetry, child: const Text('Try again')),
          ],
        ],
      ),
    );
  }
}
