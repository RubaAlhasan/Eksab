using System.Linq;

namespace Eksabli.CustomerProfiles;

// Every place a phone number becomes (or looks up) a customer's identity has to treat the same
// real-world number as the exact same string, or a customer registered from one client silently
// becomes unfindable from another:
//   - OtpAppService.RequestOtpAsync / OtpLoginService.ValidateAndResolveUserAsync — the OTP cache key
//     AND, on first login, IdentityUser.UserName itself (see OtpLoginService's own comment: the phone
//     number IS the username by design, there's no separate structured field this could fall back on).
//   - PosAppService.LookupCustomerByPhoneAsync — a cashier typing a customer's number in by hand,
//     almost certainly on a different device/keyboard than whatever the customer's own phone sent at
//     registration time.
// Without this, "+966 50 111 2222" (spaces, as a human might type or a phone's contact-card export
// might format it) and "+966501112222" (no spaces, as the mobile app's own input mask might produce)
// are two different strings to an exact `NormalizedUserName` match, even though they're the same
// customer — that mismatch, not FindByNormalizedUserNameAsync being "the wrong method", is why a real
// customer's phone lookup can come back "No matching customer found."
//
// Deliberately conservative: strips whitespace/separator punctuation and de-duplicates a leading "+"
// down to one, but never guesses at inserting a missing country code. A wrong-country guess (e.g.
// assuming a bare "0501112222" means +966) is worse than leaving a genuinely ambiguous number
// ambiguous — that's a product decision (require the client to always send a full international
// number), not something this normalizer should paper over.
public static class PhoneNumberNormalizer
{
    public static string Normalize(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return phoneNumber;
        }

        var digitsAndPlus = new string(phoneNumber.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (digitsAndPlus.Length == 0)
        {
            return digitsAndPlus;
        }

        // "+" only means anything as the very first character — a real number never has one
        // mid-string, but a stray paste/typo could leave one there; strip those rather than let
        // "+9665+01112222" normalize to a different string than "+966501112222".
        var leadingPlus = digitsAndPlus[0] == '+';
        var digitsOnly = digitsAndPlus.Replace("+", "");
        return leadingPlus ? "+" + digitsOnly : digitsOnly;
    }
}
