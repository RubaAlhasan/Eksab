using System.ComponentModel.DataAnnotations;

namespace Eksabli.Pos;

public class PhoneLookupDto
{
    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;
}
