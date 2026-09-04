import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/app_tabs.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/nearby-stores.html` — list/map toggle plus a category
/// chip filter, sorted by distance.
class NearbyStoresScreen extends ConsumerStatefulWidget {
  const NearbyStoresScreen({super.key});

  @override
  ConsumerState<NearbyStoresScreen> createState() => _NearbyStoresScreenState();
}

class _NearbyStoresScreenState extends ConsumerState<NearbyStoresScreen> {
  static const _all = 'All';

  String _category = _all;
  int _view = 0; // 0 = list, 1 = map
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    Future<void>.delayed(const Duration(milliseconds: 500), () {
      if (mounted) setState(() => _loading = false);
    });
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final businesses = ref.watch(businessesProvider);
    final categories = [_all, ...ref.watch(categoriesProvider)];

    final filtered =
        businesses
            .where((b) => _category == _all || b.category == _category)
            .toList()
          ..sort((a, b) => a.distanceKm.compareTo(b.distanceKm));

    return AppScaffold(
      title: 'Nearby Stores',
      onBack: () => context.canPop() ? context.pop() : context.go(Routes.home),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 16, 20, 8),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                PillTabs(
                  labels: const ['List', 'Map'],
                  selectedIndex: _view,
                  onChanged: (i) => setState(() => _view = i),
                ),
                AppIconButton(
                  icon: Icons.tune_rounded,
                  variant: AppButtonVariant.secondary,
                  tooltip: 'Filter',
                  onPressed: () => showAppToast(
                    context,
                    title: 'Filters',
                    message: 'Distance and rating filters land with the '
                        'PostGIS discovery endpoint.',
                    icon: Icons.info_outline_rounded,
                    accent: AppColors.info600,
                  ),
                ),
              ],
            ),
          ),
          FilterChipsRow(
            labels: categories,
            selected: _category,
            onChanged: (c) => setState(() => _category = c),
          ),
          const SizedBox(height: 12),
          Expanded(
            child: _loading
                ? ListView.separated(
                    padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
                    itemCount: 4,
                    separatorBuilder: (_, __) => const SizedBox(height: 12),
                    itemBuilder: (_, __) =>
                        const Skeleton(height: 96, radius: 16),
                  )
                : ListView(
                    padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
                    children: [
                      if (_view == 1) ...[
                        _MapPlaceholder(palette: palette),
                        const SizedBox(height: 16),
                      ],
                      if (filtered.isEmpty)
                        EmptyState(
                          icon: Icons.storefront_outlined,
                          title: 'No stores in this category',
                          message:
                              'Try a different category, or clear the filter '
                              'to see everything nearby.',
                          action: AppButton(
                            label: 'Clear filter',
                            variant: AppButtonVariant.outline,
                            size: AppButtonSize.sm,
                            onPressed: () => setState(() => _category = _all),
                          ),
                        )
                      else
                        for (final business in filtered) ...[
                          BusinessRow(
                            business: business,
                            showRating: true,
                            logoSize: 56,
                            onTap: () => context.push(Routes.store(business.id)),
                          ),
                          const SizedBox(height: 12),
                        ],
                    ],
                  ),
          ),
        ],
      ),
    );
  }
}

/// Stands in for the real map. The prototype shows the same placeholder — the
/// live version renders results from the PostGIS nearby-search endpoint.
class _MapPlaceholder extends StatelessWidget {
  const _MapPlaceholder({required this.palette});

  final AppPalette palette;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 256,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        borderRadius: AppRadius.rLg,
        gradient: LinearGradient(
          colors: palette.isDark
              ? [AppColors.primary900, AppColors.primary950]
              : [AppColors.primary100, AppColors.primary200],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            Icons.location_on_rounded,
            size: 40,
            color: palette.isDark
                ? AppColors.primary300
                : AppColors.primary600,
          ),
          const SizedBox(height: 8),
          Text(
            'Map view (placeholder)',
            style: AppText.smallSemi.copyWith(
              color: palette.isDark
                  ? AppColors.primary300
                  : AppColors.primary700,
            ),
          ),
        ],
      ),
    );
  }
}
