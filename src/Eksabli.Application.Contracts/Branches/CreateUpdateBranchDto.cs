using System.ComponentModel.DataAnnotations;

namespace Eksabli.Branches;

public class CreateUpdateBranchDto
{
    [Required]
    [StringLength(BranchConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [StringLength(BranchConsts.MaxAddressLength)]
    public string? Address { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    [StringLength(BranchConsts.MaxPhoneLength)]
    public string? Phone { get; set; }

    [StringLength(BranchConsts.MaxOpeningHoursJsonLength)]
    public string? OpeningHoursJson { get; set; }
}
