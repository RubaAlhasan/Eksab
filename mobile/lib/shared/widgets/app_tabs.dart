import 'package:flutter/material.dart';

import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';

/// `.tabs-pill` — the segmented control used on Wallet, Rewards, Nearby,
/// Redemption, and Settings.
class PillTabs extends StatelessWidget {
  const PillTabs({
    super.key,
    required this.labels,
    required this.selectedIndex,
    required this.onChanged,
    this.expand = false,
  });

  final List<String> labels;
  final int selectedIndex;
  final ValueChanged<int> onChanged;
  final bool expand;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final isDark = palette.isDark;

    Widget tab(int i) {
      final active = i == selectedIndex;
      return GestureDetector(
        onTap: () => onChanged(i),
        behavior: HitTestBehavior.opaque,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 150),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 7),
          decoration: BoxDecoration(
            color: active
                ? (isDark ? AppColors.slate700 : Colors.white)
                : Colors.transparent,
            borderRadius: AppRadius.rSm,
            boxShadow: active ? AppShadows.soft(isDark) : null,
          ),
          child: Text(
            labels[i],
            textAlign: TextAlign.center,
            style: AppText.smallSemi.copyWith(
              fontSize: 13,
              color: active
                  ? (isDark ? const Color(0xFFF1F5F9) : AppColors.primary900)
                  : palette.textMuted,
            ),
          ),
        ),
      );
    }

    return Container(
      padding: const EdgeInsets.all(4),
      decoration: BoxDecoration(
        color: isDark ? AppColors.slate800 : AppColors.slate100,
        borderRadius: AppRadius.rMd,
      ),
      child: Row(
        mainAxisSize: expand ? MainAxisSize.max : MainAxisSize.min,
        children: [
          for (var i = 0; i < labels.length; i++)
            expand ? Expanded(child: tab(i)) : tab(i),
        ],
      ),
    );
  }
}

/// `.tabs-list` / `.tab-trigger` — underline tabs used on Store Details and
/// My Coupons.
class UnderlineTabs extends StatelessWidget {
  const UnderlineTabs({
    super.key,
    required this.labels,
    required this.selectedIndex,
    required this.onChanged,
  });

  final List<String> labels;
  final int selectedIndex;
  final ValueChanged<int> onChanged;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return DecoratedBox(
      decoration: BoxDecoration(
        border: Border(bottom: BorderSide(color: palette.border)),
      ),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          children: [
            for (var i = 0; i < labels.length; i++)
              GestureDetector(
                onTap: () => onChanged(i),
                behavior: HitTestBehavior.opaque,
                child: Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 16,
                    vertical: 12,
                  ),
                  decoration: BoxDecoration(
                    border: Border(
                      bottom: BorderSide(
                        width: 2,
                        color: i == selectedIndex
                            ? palette.primaryOnDarkAware
                            : Colors.transparent,
                      ),
                    ),
                  ),
                  child: Text(
                    labels[i],
                    style: AppText.smallSemi.copyWith(
                      fontSize: 13,
                      color: i == selectedIndex
                          ? palette.primaryOnDarkAware
                          : palette.textMuted,
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

/// A horizontally scrolling row of filter chips (categories, transaction
/// types) — the `.tab-pill-trigger` variant laid out in a scroll view.
class FilterChipsRow extends StatelessWidget {
  const FilterChipsRow({
    super.key,
    required this.labels,
    required this.selected,
    required this.onChanged,
    this.padding = const EdgeInsets.symmetric(horizontal: 20),
  });

  final List<String> labels;
  final String selected;
  final ValueChanged<String> onChanged;
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final isDark = palette.isDark;
    return SizedBox(
      height: 36,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: padding,
        itemCount: labels.length,
        separatorBuilder: (_, __) => const SizedBox(width: 8),
        itemBuilder: (context, i) {
          final label = labels[i];
          final active = label == selected;
          return GestureDetector(
            onTap: () => onChanged(label),
            behavior: HitTestBehavior.opaque,
            child: Container(
              alignment: Alignment.center,
              padding: const EdgeInsets.symmetric(horizontal: 14),
              decoration: BoxDecoration(
                color: active
                    ? (isDark ? AppColors.slate700 : Colors.white)
                    : (isDark ? AppColors.slate800 : AppColors.slate100),
                borderRadius: AppRadius.rSm,
                boxShadow: active ? AppShadows.soft(isDark) : null,
              ),
              child: Text(
                label,
                style: AppText.smallSemi.copyWith(
                  fontSize: 13,
                  color: active
                      ? (isDark ? const Color(0xFFF1F5F9) : AppColors.primary900)
                      : palette.textMuted,
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}
