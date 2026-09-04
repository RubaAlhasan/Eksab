import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../shared/providers/app_providers.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/qr_placeholder.dart';

/// Prototype: `customer/qr-code.html` — the customer's wallet identity code
/// shown to staff at checkout.
///
/// Displaying a cached code is fine; awarding or redeeming points against it is
/// always validated server-side, so an offline code is not a valid discount.
class QrCodeScreen extends ConsumerWidget {
  const QrCodeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final palette = AppPalette.of(context);
    final customer = ref.watch(currentCustomerProvider);

    return AppScaffold(
      backgroundColor: palette.surface,
      title: 'My Wallet QR',
      actionIcon: Icons.qr_code_scanner_rounded,
      actionTooltip: 'Scan a check-in code',
      onAction: () => context.push(Routes.qrScanner),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(32, 24, 32, 32),
        child: Column(
          children: [
            AppAvatar(initials: customer.initials, size: AvatarSize.lg),
            const SizedBox(height: 16),
            Text(
              customer.fullName,
              style: AppText.title.copyWith(color: palette.textPrimary),
            ),
            const SizedBox(height: 4),
            Text(
              'Show this to staff to identify your account',
              textAlign: TextAlign.center,
              style: AppText.small.copyWith(color: palette.textMuted),
            ),
            const SizedBox(height: 24),
            QrPlaceholder(seed: 'wallet-${customer.id}', size: 240),
            const SizedBox(height: 24),
            const AppAlert(
              message:
                  'This code refreshes automatically and only works when '
                  "scanned by a business you're a member of.",
            ),
          ],
        ),
      ),
    );
  }
}
