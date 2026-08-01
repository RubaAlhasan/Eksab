using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Branches;

public class Branch : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public string Name { get; private set; }

    public string? Address { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public string? Phone { get; private set; }

    public string? OpeningHoursJson { get; private set; }

    protected Branch()
    {
        Name = string.Empty;
    }

    private Branch(Guid id, string name)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), BranchConsts.MaxNameLength);
    }

    public static Branch Create(Guid id, string name)
    {
        return new Branch(id, name);
    }

    public void SetName(string name) => Name = Check.NotNullOrWhiteSpace(name, nameof(name), BranchConsts.MaxNameLength);

    public void SetAddress(string? address) => Address = address;

    public void SetLocation(double? latitude, double? longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public void SetPhone(string? phone) => Phone = phone;

    public void SetOpeningHours(string? openingHoursJson) => OpeningHoursJson = openingHoursJson;
}
