import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../shared/models/models.dart';
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

  @override
  Widget build(BuildContext context) {
    // Location is not requested yet, so the directory comes back alphabetically
    // and distanceKm is null. Passing coordinates here is all that's needed to
    // switch the server to nearest-first.
    final results = ref.watch(businessSearchProvider(const BusinessQuery()));
    final categories = ref.watch(categoriesProvider).valueOrNull ?? const [];

    return AppScaffold(
      title: 'Nearby Stores',
      onBack: () => context.canPop() ? context.pop() : context.go(Routes.home),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const SizedBox(height: 12),
          FilterChipsRow(
            labels: [_all, ...categories],
            selected: _category,
            onChanged: (c) => setState(() => _category = c),
          ),
          const SizedBox(height: 12),
          Expanded(
            child: AsyncSection<List<Business>>(
              value: results,
              onRetry: () => ref.invalidate(businessSearchProvider),
              loading: ListView.separated(
                padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
                itemCount: 4,
                separatorBuilder: (_, _) => const SizedBox(height: 12),
                itemBuilder: (_, _) => const Skeleton(height: 96, radius: 16),
              ),
              data: (all) {
                final filtered = _category == _all
                    ? all
                    : all.where((b) => b.category == _category).toList();

                if (filtered.isEmpty) {
                  return EmptyState(
                    icon: Icons.storefront_outlined,
                    title: _category == _all
                        ? 'No businesses yet'
                        : 'No stores in this category',
                    message: _category == _all
                        ? 'Approved businesses will appear here.'
                        : 'Try a different category, or clear the filter.',
                    action: _category == _all
                        ? null
                        : AppButton(
                            label: 'Clear filter',
                            variant: AppButtonVariant.outline,
                            size: AppButtonSize.sm,
                            onPressed: () => setState(() => _category = _all),
                          ),
                  );
                }

                return ListView.separated(
                  padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
                  itemCount: filtered.length,
                  separatorBuilder: (_, _) => const SizedBox(height: 12),
                  itemBuilder: (context, i) {
                    final business = filtered[i];
                    return BusinessRow(
                      business: business,
                      logoSize: 56,
                      meta: business.distanceKm == null
                          ? '${business.category} · ${business.branches} branches'
                          : '${business.category} · '
                                '${business.distanceKm!.toStringAsFixed(1)} km away',
                      onTap: () => context.push(Routes.store(business.id)),
                    );
                  },
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
