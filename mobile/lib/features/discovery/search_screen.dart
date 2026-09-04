import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_badge.dart';
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
  List<String> _recent = ['Cedar & Bean Coffee', 'Fitness', 'Bookshop'];
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
    final businesses = ref.watch(businessesProvider);
    final categories = ref.watch(categoriesProvider);

    final query = _query.trim().toLowerCase();
    final results = query.isEmpty
        ? const <Business>[]
        : businesses
              .where(
                (b) =>
                    b.name.toLowerCase().contains(query) ||
                    b.category.toLowerCase().contains(query),
              )
              .toList();

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
              child: query.isEmpty
                  ? _buildBrowse(palette, categories)
                  : (results.isEmpty
                        ? const EmptyState(
                            icon: Icons.search_rounded,
                            title: 'No results found',
                            message: 'Try a different name or category.',
                          )
                        : ListView.separated(
                            padding: const EdgeInsets.fromLTRB(20, 4, 20, 24),
                            itemCount: results.length,
                            separatorBuilder: (_, __) => const SizedBox(height: 12),
                            itemBuilder: (context, i) {
                              final business = results[i];
                              return BusinessRow(
                                business: business,
                                logoSize: 48,
                                meta:
                                    '${business.category} · '
                                    '${business.distanceKm} km',
                                onTap: () =>
                                    context.push(Routes.store(business.id)),
                              );
                            },
                          )),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildBrowse(AppPalette palette, List<String> categories) {
    final businesses = ref.watch(businessesProvider);

    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 4, 20, 24),
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(
              'RECENT SEARCHES',
              style: AppText.overline.copyWith(color: palette.textMuted),
            ),
            if (_recent.isNotEmpty)
              GestureDetector(
                onTap: () => setState(() => _recent = []),
                child: Text(
                  'Clear',
                  style: AppText.smallSemi.copyWith(
                    color: palette.primaryOnDarkAware,
                  ),
                ),
              ),
          ],
        ),
        const SizedBox(height: 10),
        if (_recent.isEmpty)
          Text(
            'No recent searches.',
            style: AppText.small.copyWith(color: palette.textMuted),
          )
        else
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final term in _recent)
                GestureDetector(
                  onTap: () => _search(term),
                  child: AppBadge(term),
                ),
            ],
          ),
        const SizedBox(height: 24),

        Text(
          'BROWSE BY CATEGORY',
          style: AppText.overline.copyWith(color: palette.textMuted),
        ),
        const SizedBox(height: 10),
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
                      '${businesses.where((b) => b.category == category).length}'
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
