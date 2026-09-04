import 'package:dio/dio.dart';

import 'dev_certificate_stub.dart'
    if (dart.library.io) 'dev_certificate_io.dart' as impl;

/// Lets a **debug** build talk to the solution's self-signed dev certificate
/// (`openiddict.pfx`) on loopback hosts.
///
/// Split behind a conditional import because it needs `dart:io`, which does not
/// exist on the web. On the web the browser owns certificate trust, so there is
/// nothing to configure — the stub is a no-op.
void allowDevCertificate(Dio dio) => impl.allowDevCertificate(dio);
