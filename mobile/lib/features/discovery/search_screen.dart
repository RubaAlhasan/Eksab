import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/search.html` — recent searches and category browse
/// until the field has a query, then live-filtered results.
class SearchScreen extends ConsumerStatefulWidget {
  const SearchScreen({super.key});

  @override
  ConsumerState<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends ConsumerState<SearchScreen> {
  final _controller = TextEditingController();
  String _query = '';

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _search(String value) {
    _controller.text = value;
    _controller.selection = TextSelection.collapsed(offset: value.length);
    setState(() => _query = value);
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final query = _query.trim();

    // The server does the filtering, so an empty query is the full directory —
    // which is what the "browse by category" view is built from.
    final results = ref.watch(
      businessSearchProvider(
        BusinessQuery(text: query.isEmpty ? null : query),
      ),
    );

    return Scaffold(
      backgroundColor: palette.scaffold,
      body: SafeArea(
        bottom: false,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(20, 12, 20, 12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Search',
                    style: AppText.h1.copyWith(color: palette.textPrimary),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _controller,
                    onChanged: (v) => setState(() => _query = v),
                    style: AppText.body.copyWith(color: palette.textPrimary),
                    decoration: InputDecoration(
                      isDense: true,
                      hintText: 'Search businesses, categories…',
                      prefixIcon: Icon(
                        Icons.search_rounded,
                        size: 20,
                        color: palette.textMuted,
                      ),
                      suffixIcon: _query.isEmpty
                          ? null
                          : IconButton(
                              icon: Icon(
                                Icons.close_rounded,
                                size: 18,
                                color: palette.textMuted,
                              ),
                              onPressed: () => _search(''),
                            ),
                    ),
                  ),
                ],
              ),
            ),
            Expanded(
              child: AsyncSection<List<Business>>(
                value: results,
                onRetry: () => ref.invalidate(businessSearchProvider),
                data: (list) => query.isEmpty
                    ? _buildBrowse(palette, list)
                    : (list.isEmpty
                          ? const EmptyState(
                              icon: Icons.search_rounded,
                              title: 'No results found',
                              message: 'Try a different name or category.',
                            )
                          : ListView.separated(
                              padding: const EdgeInsets.fromLTRB(20, 4, 20, 24),
                              itemCount: list.length,
                              separatorBuilder: (_, _) =>
                                  const SizedBox(height: 12),
                              itemBuilder: (context, i) {
                                final business = list[i];
                                return BusinessRow(
                                  business: business,
                                  meta: business.distanceKm == null
                                      ? business.category
                                      : '${business.category} · '
                                            '${business.distanceKm!.toStringAsFixed(1)} km',
                                  onTap: () =>
                                      context.push(Routes.store(business.id)),
                                );
                              },
                            )),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildBrowse(AppPalette palette, List<Business> all) {
    final categories = <String>[];
    for (final b in all) {
      if (b.category.isNotEmpty && !categories.contains(b.category)) {
        categories.add(b.category);
      }
    }

    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 4, 20, 24),
      children: [
        Text(
          'BROWSE BY CATEGORY',
          style: AppText.overline.copyWith(color: palette.textMuted),
        ),
        const SizedBox(height: 10),
        if (categories.isEmpty)
          const EmptyState(
            icon: Icons.storefront_outlined,
            title: 'No businesses yet',
            message: 'Approved businesses will appear here.',
          )
        else
          GridView.count(
            crossAxisCount: 2,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            crossAxisSpacing: 12,
            mainAxisSpacing: 12,
            childAspectRatio: 2.3,
            children: [
              for (final category in categories)
                AppCard(
                  onTap: () => _search(category),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        category,
                        overflow: TextOverflow.ellipsis,
                        style: AppText.bodyBold.copyWith(
                          color: palette.textPrimary,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        '${all.where((b) => b.category == category).length}'
                        ' businesses',
                        style: AppText.small.copyWith(color: palette.textMuted),
                      ),
                    ],
                  ),
                ),
            ],
          ),
      ],
    );
  }
}
