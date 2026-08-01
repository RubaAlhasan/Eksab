using System.ComponentModel.DataAnnotations;

namespace Eksabli.Otp;

public class RequestOtpDto
{
    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;
}
