using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.CustomerProfiles;

public class UpdateCustomerProfileDto
{
    [StringLength(CustomerProfileConsts.MaxFirstNameLength)]
    public string? FirstName { get; set; }

    [StringLength(CustomerProfileConsts.MaxLastNameLength)]
    public string? LastName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public CustomerGender Gender { get; set; }
}
