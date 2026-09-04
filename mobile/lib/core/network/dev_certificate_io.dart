import 'dart:io';

import 'package:dio/dio.dart';
import 'package:dio/io.dart';
import 'package:flutter/foundation.dart';

import '../config/app_config.dart';

/// Native build: accept the self-signed dev certificate, but only in debug
/// builds and only for loopback hosts. Release builds always enforce normal
/// certificate validation.
void allowDevCertificate(Dio dio) {
  if (!kDebugMode || !AppConfig.isLocalDevHost) return;

  dio.httpClientAdapter = IOHttpClientAdapter(
    createHttpClient: () => HttpClient()
      ..badCertificateCallback = (cert, host, port) =>
          host == 'localhost' || host == '127.0.0.1' || host == '10.0.2.2',
  );
}
