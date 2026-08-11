using System;
using System.ComponentModel.DataAnnotations;
using Eksabli.CustomerProfiles;

namespace Eksabli.Otp;

// Field set matches prototype/customer/register.html exactly (firstName, lastName, phone, optional
// email, dob, optional gender, password) — that screen submits this, then immediately routes to
// otp-verify.html, which is why this call also sends the OTP code itself (see OtpAppService.RegisterAsync)
// rather than requiring a separate POST /api/app/otp/request first. The account it creates is
// unverified (IdentityUser.PhoneNumberConfirmed = false) until that verify step completes via
// POST /connect/token (grant_type=otp) — see OtpLoginService.ValidateAndResolveUserAsync.
public class RegisterCustomerDto
{
    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(CustomerProfileConsts.MaxFirstNameLength)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(CustomerProfileConsts.MaxLastNameLength)]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public CustomerGender? Gender { get; set; }

    // Captured for parity with the prototype's password field and to leave the door open for a
    // future password-based login (prototype/customer/login.html) without a second migration.
    // Nothing currently authenticates with it — OTP (this same register -> verify round trip, or a
    // plain login) is the only login path actually wired up right now.
    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;
}
