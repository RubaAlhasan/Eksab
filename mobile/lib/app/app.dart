import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../shared/providers/app_providers.dart';
import 'router/app_router.dart';
import 'theme/app_theme.dart';

class EksabliApp extends ConsumerWidget {
  const EksabliApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final prefs = ref.watch(preferencesProvider);

    return MaterialApp.router(
      title: 'Eksabli',
      debugShowCheckedModeBanner: false,
      routerConfig: ref.watch(routerProvider),
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      themeMode: prefs.themeMode,
      // Arabic ships alongside English from day one — the target market is
      // bilingual, and RTL layout is far cheaper to honour now than to retrofit.
      // See docs/eksabli-loyalty-platform/05-flutter-architecture.md#localization.
      locale: prefs.locale,
      supportedLocales: const [Locale('en'), Locale('ar')],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
    );
  }
}
