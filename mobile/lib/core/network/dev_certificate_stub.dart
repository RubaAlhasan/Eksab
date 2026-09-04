import 'package:dio/dio.dart';

/// Web build: the browser decides which certificates to trust, so there is
/// nothing for the app to override. To use the local dev host from a browser
/// you must trust `https://localhost:44330` in the browser itself.
void allowDevCertificate(Dio dio) {}
