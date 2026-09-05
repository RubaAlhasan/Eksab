import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';
import '../../shared/widgets/app_tabs.dart';
import '../../shared/widgets/business_tiles.dart';

/// Prototype: `customer/transaction-history.html` — the full ledger for one
/// business, filterable by transaction type.
class TransactionHistoryScreen extends ConsumerStatefulWidget {
  const TransactionHistoryScreen({super.key, required this.businessId});

  final String businessId;

  @override
  ConsumerState<TransactionHistoryScreen> createState() =>
      _TransactionHistoryScreenState();
}

class _TransactionHistoryScreenState
    extends ConsumerState<TransactionHistoryScreen> {
  static const _all = 'All';

  String _filter = _all;

  @override
  Widget build(BuildContext context) {
    final business = ref.watch(businessByIdProvider(widget.businessId));
    final transactions = ref.watch(
      transactionsForBusinessProvider(widget.businessId),
    );

    return AppScaffold(
      title: business.valueOrNull == null
          ? 'History'
          : '${business.valueOrNull!.name} History',
      onBack: () => context.canPop()
          ? context.pop()
          : context.go(Routes.points(widget.businessId)),
      body: AsyncSection<List<PointTransaction>>(
        value: transactions,
        onRetry: () =>
            ref.invalidate(transactionsForBusinessProvider(widget.businessId)),
        data: (all) {
          // Only offer chips for types this ledger actually contains.
          final types = <String>{_all, ...all.map((t) => t.type.label)}.toList();
          final filtered = _filter == _all
              ? all
              : all.where((t) => t.type.label == _filter).toList();

          return Column(
            children: [
              const SizedBox(height: 12),
              FilterChipsRow(
                labels: types,
                selected: _filter,
                onChanged: (t) => setState(() => _filter = t),
              ),
              const SizedBox(height: 12),
              Expanded(
                child: filtered.isEmpty
                    ? EmptyState(
                        icon: Icons.schedule_rounded,
                        title: all.isEmpty
                            ? 'No activity yet'
                            : 'No transactions',
                        message: all.isEmpty
                            ? 'Points you earn here will show up in this list.'
                            : 'Nothing matches this filter yet.',
                      )
                    : ListView.separated(
                        padding: const EdgeInsets.fromLTRB(20, 4, 20, 24),
                        itemCount: filtered.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 8),
                        itemBuilder: (context, i) => TransactionRow(
                          transaction: filtered[i],
                          showType: true,
                        ),
                      ),
              ),
            ],
          );
        },
      ),
    );
  }
}
