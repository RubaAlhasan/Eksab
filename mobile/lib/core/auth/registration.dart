/// Payload for `POST /api/app/otp/register`.
///
/// Mirrors `RegisterCustomerDto` in
/// `src/Eksabli.Application.Contracts/Otp/RegisterCustomerDto.cs`. That call
/// creates the `IdentityUser` + `CustomerProfile` immediately but leaves the
/// account `PhoneNumberConfirmed = false` — unusable until the OTP step proves
/// the number — and sends the verification code itself, so no separate
/// `otp/request` is needed after registering.
class RegisterRequest {
  const RegisterRequest({
    required this.phoneNumber,
    required this.firstName,
    required this.lastName,
    required this.password,
    this.email,
    this.dateOfBirth,
    this.gender,
  });

  final String phoneNumber;
  final String firstName;
  final String lastName;

  /// Server requires at least 8 characters. Stored against the identity user
  /// for a future password login; OTP is the only path wired today.
  final String password;
  final String? email;
  final DateTime? dateOfBirth;
  final CustomerGender? gender;

  Map<String, dynamic> toJson() => {
    'phoneNumber': phoneNumber,
    'firstName': firstName,
    'lastName': lastName,
    'password': password,
    if (email != null && email!.isNotEmpty) 'email': email,
    // The server binds DateTime?; send date-only ISO to avoid timezone drift
    // shifting someone's birthday by a day.
    if (dateOfBirth != null)
      'dateOfBirth': dateOfBirth!.toIso8601String().split('T').first,
    if (gender != null) 'gender': gender!.value,
  };
}

/// Mirrors `Eksabli.CustomerProfiles.CustomerGender` — the server serialises
/// this enum by its integer value.
enum CustomerGender {
  unspecified(0, 'Prefer not to say'),
  male(1, 'Male'),
  female(2, 'Female');

  const CustomerGender(this.value, this.label);

  final int value;
  final String label;

  static CustomerGender fromValue(Object? raw) {
    final v = raw is int ? raw : int.tryParse('$raw');
    return switch (v) {
      1 => CustomerGender.male,
      2 => CustomerGender.female,
      _ => CustomerGender.unspecified,
    };
  }

  static CustomerGender? fromLabel(String? label) {
    if (label == null) return null;
    for (final g in CustomerGender.values) {
      if (g.label == label) return g;
    }
    return null;
  }
}
