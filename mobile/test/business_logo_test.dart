import 'package:eksabli_mobile/core/config/app_config.dart';
import 'package:eksabli_mobile/shared/models/models.dart';
import 'package:flutter_test/flutter_test.dart';

/// The logo path is plumbed end to end on the server — upload, storage and an [AllowAnonymous]
/// GET — but the app parsed `hasLogo` and never read it, so ten screens drew generated initials
/// over businesses that had real logos. These pin the client half down.
void main() {
  Business business({
    bool hasLogo = true,
    String? blob = 'tenant/abc123',
    String profileId = 'profile-1',
  }) => Business.fromJson({
    'tenantId': 'tenant-1',
    'name': 'Cedar & Bean Coffee',
    'businessProfileId': profileId,
    'hasLogo': hasLogo,
    'logoBlobName': blob,
  });

  test('builds a public logo URL keyed by profile id', () {
    final url = business().logoUrl!;
    expect(url, startsWith('${AppConfig.baseUrl}/api/app/business/profile-1/logo'));
  });

  test('cache-busts with the blob name, escaped', () {
    // The blob name contains a slash; leaving it raw would change the URL path rather than the
    // query, and point at an endpoint that does not exist.
    expect(business().logoUrl, endsWith('?v=tenant%2Fabc123'));
  });

  test('is null when no logo has been uploaded', () {
    // Not an empty string or a 404-bound URL: BusinessLogo keys its fallback off null, and this is
    // the case where generated initials are the intended presentation rather than a placeholder.
    expect(business(hasLogo: false).logoUrl, isNull);
  });

  test('is null when the profile id is missing', () {
    expect(business(profileId: '').logoUrl, isNull);
  });

  test('omits the version when the server sends no blob name', () {
    final url = business(blob: null).logoUrl!;
    expect(url, isNot(contains('?v=')));
    expect(url, endsWith('/logo'));
  });

  test('survives a copyWith', () {
    // follow/join both round-trip a Business through copyWith; dropping the logo there would make
    // it vanish the moment someone favourited a business.
    expect(business().copyWith(following: true).logoUrl, business().logoUrl);
  });
}
