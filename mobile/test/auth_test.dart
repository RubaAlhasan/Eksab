import 'package:dio/dio.dart';
import 'package:eksabli_mobile/app/router/app_router.dart';
import 'package:eksabli_mobile/app/theme/app_theme.dart';
import 'package:eksabli_mobile/core/auth/auth_exception.dart';
import 'package:eksabli_mobile/core/auth/auth_tokens.dart';
import 'package:eksabli_mobile/core/demo/demo_data.dart';
import 'package:eksabli_mobile/shared/models/models.dart';
import 'package:eksabli_mobile/shared/providers/app_providers.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

class _SessionStub extends SessionNotifier {
  _SessionStub(this.user);

  final Customer? user;

  @override
  Future<Customer?> build() async => user;
}

DioException _badResponse(int status, Map<String, dynamic> body) {
  final options = RequestOptions(path: '/connect/token');
  return DioException(
    requestOptions: options,
    type: DioExceptionType.badResponse,
    response: Response<dynamic>(
      requestOptions: options,
      statusCode: status,
      data: body,
    ),
  );
}

void main() {
  group('AuthException mapping', () {
    test('invalid_grant is a credential error, not a generic failure', () {
      final error = AuthException.fromDio(
        _badResponse(400, {
          'error': 'invalid_grant',
          'error_description': 'Invalid username or password!',
        }),
      );

      expect(error.kind, AuthFailure.invalidCredentials);
      expect(error.isCredentialError, isTrue);
      // The server's own wording is surfaced rather than replaced.
      expect(error.message, 'Invalid username or password!');
    });

    test('invalid_grant without a description still reads sensibly', () {
      final error = AuthException.fromDio(
        _badResponse(400, {'error': 'invalid_grant'}),
      );

      expect(error.kind, AuthFailure.invalidCredentials);
      expect(error.message, 'Incorrect phone number or password.');
    });

    test('invalid_client is reported as a configuration problem', () {
      final error = AuthException.fromDio(
        _badResponse(400, {'error': 'invalid_client'}),
      );

      expect(error.kind, AuthFailure.configuration);
      expect(error.isCredentialError, isFalse);
    });

    test('a connection failure is not mistaken for bad credentials', () {
      final error = AuthException.fromDio(
        DioException(
          requestOptions: RequestOptions(path: '/connect/token'),
          type: DioExceptionType.connectionError,
        ),
      );

      expect(error.kind, AuthFailure.network);
      expect(error.isCredentialError, isFalse);
    });

    test('a 5xx is reported as a server problem', () {
      final error = AuthException.fromDio(_badResponse(500, {}));
      expect(error.kind, AuthFailure.server);
    });
  });

  group('AuthTokens', () {
    test('treats a token expiring within 30s as already expired', () {
      final almostExpired = AuthTokens(
        accessToken: 'a',
        refreshToken: 'r',
        expiresAt: DateTime.now().add(const Duration(seconds: 10)),
      );
      expect(almostExpired.isExpired, isTrue);
      expect(almostExpired.canRefresh, isTrue);
    });

    test('a token with real headroom is not expired', () {
      final fresh = AuthTokens(
        accessToken: 'a',
        refreshToken: null,
        expiresAt: DateTime.now().add(const Duration(minutes: 5)),
      );
      expect(fresh.isExpired, isFalse);
      expect(fresh.canRefresh, isFalse);
    });

    test('parses expires_in from the token response', () {
      final tokens = AuthTokens.fromResponse({
        'access_token': 'abc',
        'refresh_token': 'def',
        'expires_in': 3600,
      });

      expect(tokens.accessToken, 'abc');
      expect(tokens.refreshToken, 'def');
      expect(tokens.isExpired, isFalse);
    });
  });

  group('route guard', () {
    Future<ProviderContainer> pumpWithSession(
      WidgetTester tester,
      Customer? user,
    ) async {
      final container = ProviderContainer(
        overrides: [sessionProvider.overrideWith(() => _SessionStub(user))],
      );
      addTearDown(container.dispose);

      await tester.pumpWidget(
        UncontrolledProviderScope(
          container: container,
          child: MaterialApp.router(
            routerConfig: container.read(routerProvider),
            theme: AppTheme.light(),
          ),
        ),
      );
      await tester.pumpAndSettle();
      return container;
    }

    String locationOf(ProviderContainer container) => container
        .read(routerProvider)
        .routerDelegate
        .currentConfiguration
        .uri
        .path;

    testWidgets('a signed-out user is sent from splash to onboarding', (
      tester,
    ) async {
      final container = await pumpWithSession(tester, null);
      expect(locationOf(container), Routes.onboarding);
    });

    testWidgets('a signed-out user cannot reach a protected route', (
      tester,
    ) async {
      final container = await pumpWithSession(tester, null);

      container.read(routerProvider).go(Routes.wallet);
      await tester.pumpAndSettle();

      expect(locationOf(container), Routes.login);
    });

    testWidgets('a signed-in user is sent from splash to home', (tester) async {
      final container = await pumpWithSession(
        tester,
        DemoData.currentCustomer,
      );
      expect(locationOf(container), Routes.home);
    });

    testWidgets('a signed-in user is bounced off the login screen', (
      tester,
    ) async {
      final container = await pumpWithSession(
        tester,
        DemoData.currentCustomer,
      );

      container.read(routerProvider).go(Routes.login);
      await tester.pumpAndSettle();

      expect(locationOf(container), Routes.home);
    });
  });
}
