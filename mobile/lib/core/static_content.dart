import '../shared/models/models.dart';

/// Content that is genuinely static (or not yet backed by an endpoint), kept
/// out of the API layer so it is obvious what is and isn't server data.
abstract final class StaticContent {
  /// Help copy. No CMS or FAQ endpoint exists; these are product copy, not data.
  static const List<({String question, String answer})> faqs = [
    (
      question: 'How do I earn points?',
      answer:
          'Points are awarded automatically by staff at checkout — either by '
          'scanning your wallet QR or looking up your phone number.',
    ),
    (
      question: 'Do points expire?',
      answer:
          'Each business sets its own expiration policy. Expired points appear '
          'as a separate line in your transaction history.',
    ),
    (
      question: 'Can I transfer points to a friend?',
      answer:
          'Not yet — point transfer between customers is not available in this '
          'version.',
    ),
    (
      question: 'What happens if a business closes?',
      answer:
          'Your membership and balance are frozen, not deleted, so your history '
          'at other businesses is unaffected.',
    ),
    (
      question: 'How do I delete my account?',
      answer:
          'Go to Settings → Danger Zone → Delete my account. Your data is '
          'retained for 90 days before permanent deletion.',
    ),
  ];

  /// Placeholder until `DevicesController` is wired into the app — the endpoint
  /// exists but session/device management is not part of this pass.
  static const List<LinkedDevice> devices = [
    LinkedDevice(name: 'This device', location: '', isCurrent: true),
  ];
}
