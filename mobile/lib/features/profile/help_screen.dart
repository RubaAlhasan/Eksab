import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/router/app_router.dart';
import '../../app/theme/app_colors.dart';
import '../../app/theme/app_tokens.dart';
import '../../core/demo/demo_data.dart';
import '../../shared/widgets/app_avatar.dart';
import '../../shared/widgets/app_badge.dart';
import '../../shared/widgets/app_button.dart';
import '../../shared/widgets/app_card.dart';
import '../../shared/widgets/app_form.dart';
import '../../shared/widgets/app_scaffold.dart';
import '../../shared/widgets/app_states.dart';

/// Prototype: `customer/help.html` — contact shortcuts, an expandable FAQ, and
/// a support-ticket sheet.
class HelpScreen extends StatefulWidget {
  const HelpScreen({super.key});

  @override
  State<HelpScreen> createState() => _HelpScreenState();
}

class _HelpScreenState extends State<HelpScreen> {
  final _expanded = <int>{};

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);

    return AppScaffold(
      title: 'Help & Support',
      onBack: () =>
          context.canPop() ? context.pop() : context.go(Routes.profile),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 16, 20, 24),
        children: [
          Row(
            children: [
              Expanded(
                child: _ContactCard(
                  icon: Icons.mail_outline_rounded,
                  tone: AppColors.primary600,
                  label: 'Email Support',
                  onTap: () => showAppToast(
                    context,
                    title: 'support@eksabli.app',
                    message: 'Copy this address into your mail app.',
                    icon: Icons.info_outline_rounded,
                    accent: AppColors.info600,
                  ),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _ContactCard(
                  icon: Icons.assignment_outlined,
                  tone: AppColors.success600,
                  label: 'Submit a Ticket',
                  onTap: () => _openTicketSheet(context),
                ),
              ),
            ],
          ),
          const SizedBox(height: 24),

          const SectionLabel('Frequently Asked Questions'),
          AppCardList(
            children: [
              for (var i = 0; i < DemoData.faqs.length; i++)
                _FaqRow(
                  faq: DemoData.faqs[i],
                  expanded: _expanded.contains(i),
                  onToggle: () => setState(() {
                    _expanded.contains(i)
                        ? _expanded.remove(i)
                        : _expanded.add(i);
                  }),
                ),
            ],
          ),
          const SizedBox(height: 20),

          const AppAlert(
            message:
                "Can't find an answer? Reach us at support@eksabli.app — "
                'average response time is under 4 hours.',
          ),
          const SizedBox(height: 8),
          Center(
            child: Text(
              'Eksabli Support',
              style: AppText.small.copyWith(color: palette.textMuted),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _openTicketSheet(BuildContext context) async {
    final subject = TextEditingController();
    final message = TextEditingController();

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) {
        final palette = AppPalette.of(sheetContext);
        return Padding(
          padding: EdgeInsets.fromLTRB(
            24,
            20,
            24,
            24 + MediaQuery.viewInsetsOf(sheetContext).bottom,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Submit a support ticket',
                style: AppText.title.copyWith(color: palette.textPrimary),
              ),
              const SizedBox(height: 16),
              AppField(
                label: 'Subject',
                controller: subject,
                hint: 'e.g. Missing points from a purchase',
              ),
              const SizedBox(height: 12),
              AppField(
                label: 'Message',
                controller: message,
                hint: 'Describe the issue…',
                keyboardType: TextInputType.multiline,
              ),
              const SizedBox(height: 20),
              Row(
                children: [
                  Expanded(
                    child: AppButton(
                      label: 'Cancel',
                      variant: AppButtonVariant.secondary,
                      expand: true,
                      onPressed: () => Navigator.of(sheetContext).pop(),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: AppButton(
                      label: 'Submit',
                      expand: true,
                      onPressed: () {
                        Navigator.of(sheetContext).pop();
                        showAppToast(
                          context,
                          title: 'Ticket submitted',
                          message: "We'll reply to your email shortly.",
                        );
                      },
                    ),
                  ),
                ],
              ),
            ],
          ),
        );
      },
    );

    subject.dispose();
    message.dispose();
  }
}

class _ContactCard extends StatelessWidget {
  const _ContactCard({
    required this.icon,
    required this.tone,
    required this.label,
    required this.onTap,
  });

  final IconData icon;
  final Color tone;
  final String label;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return AppCard(
      onTap: onTap,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          IconTile(icon: icon, tone: tone),
          const SizedBox(height: 8),
          Text(
            label,
            textAlign: TextAlign.center,
            style: AppText.smallSemi.copyWith(color: palette.textPrimary),
          ),
        ],
      ),
    );
  }
}

class _FaqRow extends StatelessWidget {
  const _FaqRow({
    required this.faq,
    required this.expanded,
    required this.onToggle,
  });

  final ({String question, String answer}) faq;
  final bool expanded;
  final VoidCallback onToggle;

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        InkWell(
          onTap: onToggle,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    faq.question,
                    style: AppText.bodySemi.copyWith(
                      color: palette.textPrimary,
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                AnimatedRotation(
                  turns: expanded ? 0.5 : 0,
                  duration: const Duration(milliseconds: 180),
                  child: Icon(
                    Icons.expand_more_rounded,
                    size: 18,
                    color: palette.textMuted,
                  ),
                ),
              ],
            ),
          ),
        ),
        AnimatedCrossFade(
          duration: const Duration(milliseconds: 180),
          crossFadeState: expanded
              ? CrossFadeState.showSecond
              : CrossFadeState.showFirst,
          firstChild: const SizedBox(width: double.infinity),
          secondChild: Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
            child: Text(
              faq.answer,
              style: AppText.small.copyWith(
                color: palette.textSecondary,
                height: 1.6,
              ),
            ),
          ),
        ),
      ],
    );
  }
}
