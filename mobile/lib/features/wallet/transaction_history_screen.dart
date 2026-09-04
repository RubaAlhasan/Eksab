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
import '../profile/error_screen.dart';

/// Prototype: `customer/transaction-history.html` — full ledger for one
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

  static String _label(TransactionType type) => switch (type) {
    TransactionType.earn => 'Earn',
    TransactionType.redeem => 'Redeem',
    TransactionType.adjust => 'Adjust',
    TransactionType.expire => 'Expire',
    TransactionType.refund => 'Refund',
  };

  @override
  Widget build(BuildContext context) {
    final business = ref.watch(businessByIdProvider(widget.businessId));
    if (business == null) return const ErrorScreen(kind: ErrorKind.notFound);

    final all = ref.watch(transactionsForBusinessProvider(widget.businessId));

    // Only offer chips for types this business actually has, as the prototype
    // does — an empty "Refund" filter is noise.
    final types = <String>{_all, ...all.map((t) => _label(t.type))}.toList();

    final filtered = _filter == _all
        ? all
        : all.where((t) => _label(t.type) == _filter).toList();

    return AppScaffold(
      title: '${business.name} History',
      onBack: () => context.canPop()
          ? context.pop()
          : context.go(Routes.points(widget.businessId)),
      body: Column(
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
                ? const EmptyState(
                    icon: Icons.schedule_rounded,
                    title: 'No transactions',
                    message: 'Nothing matches this filter yet.',
                  )
                : ListView.separated(
                    padding: const EdgeInsets.fromLTRB(20, 4, 20, 24),
                    itemCount: filtered.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 8),
                    itemBuilder: (context, i) =>
                        TransactionRow(transaction: filtered[i], showType: true),
                  ),
          ),
        ],
      ),
    );
  }
}
