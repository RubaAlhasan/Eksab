using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Eksabli.CustomerProfiles;

public class CustomerProfile : AuditedAggregateRoot<Guid>
{
    // Host-realm entity — no IMultiTenant. UserId is a soft reference to IdentityUser.Id
    // (same convention as Membership.CustomerId — no EF FK).
    public Guid UserId { get; private set; }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public DateTime? DateOfBirth { get; private set; }

    public CustomerGender Gender { get; private set; }

    protected CustomerProfile()
    {
        /* Required by the ORM */
    }

    private CustomerProfile(Guid id, Guid userId)
        : base(id)
    {
        UserId = userId;
        Gender = CustomerGender.Unspecified;
    }

    public static CustomerProfile Create(Guid id, Guid userId)
    {
        return new CustomerProfile(id, userId);
    }

    public void SetName(string? firstName, string? lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    public void SetDateOfBirth(DateTime? dateOfBirth) => DateOfBirth = dateOfBirth;

    public void SetGender(CustomerGender gender) => Gender = gender;
}
