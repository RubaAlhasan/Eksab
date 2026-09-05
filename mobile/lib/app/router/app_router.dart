import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../shared/models/models.dart';
import '../../shared/providers/app_providers.dart';

import '../../features/auth/forgot_password_screen.dart';
import '../../features/auth/login_screen.dart';
import '../../features/auth/otp_verify_screen.dart';
import '../../features/auth/register_screen.dart';
import '../../features/campaigns/birthday_rewards_screen.dart';
import '../../features/campaigns/campaigns_screen.dart';
import '../../features/discovery/nearby_stores_screen.dart';
import '../../features/discovery/search_screen.dart';
import '../../features/favorites/favorites_screen.dart';
import '../../features/home/home_screen.dart';
import '../../features/membership/join_store_screen.dart';
import '../../features/membership/my_memberships_screen.dart';
import '../../features/notifications/notifications_screen.dart';
import '../../features/onboarding/onboarding_screen.dart';
import '../../features/onboarding/splash_screen.dart';
import '../../features/profile/edit_profile_screen.dart';
import '../../features/profile/error_screen.dart';
import '../../features/profile/help_screen.dart';
import '../../features/profile/profile_screen.dart';
import '../../features/profile/settings_screen.dart';
import '../../features/referral/referral_screen.dart';
import '../../features/rewards/coupons_screen.dart';
import '../../features/rewards/redeem_reward_screen.dart';
import '../../features/rewards/reward_details_screen.dart';
import '../../features/rewards/rewards_screen.dart';
import '../../features/store/store_details_screen.dart';
import '../../features/wallet/my_points_screen.dart';
import '../../features/wallet/qr_code_screen.dart';
import '../../features/wallet/qr_scanner_screen.dart';
import '../../features/wallet/transaction_history_screen.dart';
import '../../features/wallet/wallet_screen.dart';
import 'app_shell.dart';

/// Route names, kept in one place so screens navigate by constant rather than
/// by hand-written path strings.
abstract final class Routes {
  static const splash = '/';
  static const onboarding = '/onboarding';
  static const login = '/login';
  static const register = '/register';
  static const otpVerify = '/otp-verify';
  static const forgotPassword = '/forgot-password';

  // Bottom-nav branches
  static const home = '/home';
  static const search = '/search';
  static const wallet = '/wallet';
  static const notifications = '/notifications';
  static const profile = '/profile';

  // Discovery / store
  static const nearby = '/nearby';
  static String store(String id) => '/store/$id';
  static String join(String id) => '/join/$id';

  // Points & rewards
  static String points(String businessId) => '/points/$businessId';
  static String history(String businessId) => '/points/$businessId/history';
  static String rewards(String businessId) => '/points/$businessId/rewards';
  // A reward is addressed by business + reward: the catalogue endpoint is
  // per-tenant, so the business id is needed to look one up.
  static String reward(String businessId, String rewardId) =>
      '/points/$businessId/rewards/$rewardId';
  static String redeem(String businessId, String rewardId) =>
      '/points/$businessId/rewards/$rewardId/redeem';
  static const coupons = '/coupons';

  // Wallet extras
  static const qrCode = '/qr-code';
  static const qrScanner = '/qr-scanner';

  // Profile sub-pages
  static const memberships = '/memberships';
  static const favorites = '/favorites';
  static const referral = '/referral';
  static const birthdayRewards = '/birthday-rewards';
  static const campaigns = '/campaigns';
  static const editProfile = '/edit-profile';
  static const settings = '/settings';
  static const help = '/help';
  static const error = '/error';
}

final _rootKey = GlobalKey<NavigatorState>();

/// Routes reachable without a session. Everything else sits behind the guard
/// in [routerProvider].
const _publicRoutes = <String>{
  Routes.splash,
  Routes.onboarding,
  Routes.login,
  Routes.register,
  Routes.otpVerify,
  Routes.forgotPassword,
};

/// The route table, exposed separately so tests can mount it in their own
/// router without the auth guard.
final appRoutes = <RouteBase>[
    GoRoute(
      path: Routes.splash,
      builder: (context, state) => const SplashScreen(),
    ),
    GoRoute(
      path: Routes.onboarding,
      builder: (context, state) => const OnboardingScreen(),
    ),
    GoRoute(
      path: Routes.login,
      builder: (context, state) => const LoginScreen(),
    ),
    GoRoute(
      path: Routes.register,
      builder: (context, state) => const RegisterScreen(),
    ),
    GoRoute(
      path: Routes.otpVerify,
      // ?phone= is set by both register and login; the `otp` grant needs the
      // number alongside the code.
      builder: (context, state) => OtpVerifyScreen(
        phoneNumber: state.uri.queryParameters['phone'] ?? '',
      ),
    ),
    GoRoute(
      path: Routes.forgotPassword,
      builder: (context, state) => const ForgotPasswordScreen(),
    ),

    // The five bottom-nav tabs keep independent navigation stacks, so pushing
    // Store Details from Search and returning to Wallet preserves both.
    StatefulShellRoute.indexedStack(
      builder: (context, state, navigationShell) =>
          AppShell(navigationShell: navigationShell),
      branches: [
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: Routes.home,
              builder: (context, state) => const HomeScreen(),
            ),
          ],
        ),
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: Routes.search,
              builder: (context, state) => const SearchScreen(),
            ),
          ],
        ),
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: Routes.wallet,
              builder: (context, state) => const WalletScreen(),
            ),
          ],
        ),
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: Routes.notifications,
              builder: (context, state) => const NotificationsScreen(),
            ),
          ],
        ),
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: Routes.profile,
              builder: (context, state) => const ProfileScreen(),
            ),
          ],
        ),
      ],
    ),

    GoRoute(
      path: Routes.nearby,
      builder: (context, state) => const NearbyStoresScreen(),
    ),
    GoRoute(
      path: '/store/:id',
      builder: (context, state) =>
          StoreDetailsScreen(businessId: state.pathParameters['id']!),
    ),
    GoRoute(
      path: '/join/:id',
      builder: (context, state) =>
          JoinStoreScreen(businessId: state.pathParameters['id']!),
    ),
    GoRoute(
      path: '/points/:id',
      builder: (context, state) =>
          MyPointsScreen(businessId: state.pathParameters['id']!),
      routes: [
        GoRoute(
          path: 'history',
          builder: (context, state) =>
              TransactionHistoryScreen(businessId: state.pathParameters['id']!),
        ),
        GoRoute(
          path: 'rewards',
          builder: (context, state) =>
              RewardsScreen(businessId: state.pathParameters['id']!),
          routes: [
            GoRoute(
              path: ':rewardId',
              builder: (context, state) => RewardDetailsScreen(
                businessId: state.pathParameters['id']!,
                rewardId: state.pathParameters['rewardId']!,
              ),
              routes: [
                GoRoute(
                  path: 'redeem',
                  builder: (context, state) => RedeemRewardScreen(
                    businessId: state.pathParameters['id']!,
                    rewardId: state.pathParameters['rewardId']!,
                  ),
                ),
              ],
            ),
          ],
        ),
      ],
    ),
    GoRoute(
      path: Routes.coupons,
      builder: (context, state) => const CouponsScreen(),
    ),
    GoRoute(
      path: Routes.qrCode,
      builder: (context, state) => const QrCodeScreen(),
    ),
    GoRoute(
      path: Routes.qrScanner,
      builder: (context, state) => const QrScannerScreen(),
    ),
    GoRoute(
      path: Routes.memberships,
      builder: (context, state) => const MyMembershipsScreen(),
    ),
    GoRoute(
      path: Routes.favorites,
      builder: (context, state) => const FavoritesScreen(),
    ),
    GoRoute(
      path: Routes.referral,
      builder: (context, state) => const ReferralScreen(),
    ),
    GoRoute(
      path: Routes.birthdayRewards,
      builder: (context, state) => const BirthdayRewardsScreen(),
    ),
    GoRoute(
      path: Routes.campaigns,
      builder: (context, state) => const CampaignsScreen(),
    ),
    GoRoute(
      path: Routes.editProfile,
      builder: (context, state) => const EditProfileScreen(),
    ),
    GoRoute(
      path: Routes.settings,
      builder: (context, state) => const SettingsScreen(),
    ),
    GoRoute(
      path: Routes.help,
      builder: (context, state) => const HelpScreen(),
    ),
    GoRoute(
      path: Routes.error,
      builder: (context, state) => const ErrorScreen(),
    ),
];

/// The app's router, with the auth guard attached.
///
/// Redirects are driven by [sessionProvider]: while the stored session is being
/// restored the user is held on the splash screen, so a returning user never
/// sees the login page flash before being sent home.
final routerProvider = Provider<GoRouter>((ref) {
  final authState = ValueNotifier<AsyncValue<Customer?>>(const AsyncLoading());
  ref.onDispose(authState.dispose);
  ref.listen<AsyncValue<Customer?>>(
    sessionProvider,
    (_, next) => authState.value = next,
    fireImmediately: true,
  );

  final router = GoRouter(
    navigatorKey: _rootKey,
    initialLocation: Routes.splash,
    refreshListenable: authState,
    routes: appRoutes,
    errorBuilder: (context, state) =>
        const ErrorScreen(kind: ErrorKind.notFound),
    redirect: (context, state) {
      final session = ref.read(sessionProvider);
      final location = state.matchedLocation;

      // Still restoring — hold on splash rather than guessing.
      if (session.isLoading) {
        return location == Routes.splash ? null : Routes.splash;
      }

      final isSignedIn = session.valueOrNull != null;
      final isPublic = _publicRoutes.contains(location);

      if (!isSignedIn) {
        if (location == Routes.splash) return Routes.onboarding;
        return isPublic ? null : Routes.login;
      }

      // Signed in: never leave the user sitting on splash or an auth screen.
      return isPublic ? Routes.home : null;
    },
  );

  ref.onDispose(router.dispose);
  return router;
});
