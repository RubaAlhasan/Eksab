using System;

namespace Eksabli.Platform;

public class AdminUserDto
{
    public Guid Id { get; set; }

    public AdminUserType Type { get; set; }

    // Customer-only (from CustomerProfile.FirstName/LastName, itself optional) — always null for Staff.
    // IdentityUser.Name/Surname are never populated anywhere in this codebase for either realm, so
    // there is no real staff display name to put here; don't invent one.
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    // Staff-only (the real Volo.Abp.TenantManagement.Tenant.Name for their EmployeeAssignment's
    // TenantId) — always null for Customer (a customer isn't tied to one business).
    public string? BusinessName { get; set; }

    // Customer: IdentityUser.PhoneNumber. Staff: IdentityUser.Email. Whichever field the account was
    // actually reachable by, matching each realm's real registration flow.
    public string? Contact { get; set; }

    // IdentityUser's own real, framework-managed field — the same one the stock Identity > Users page
    // itself toggles. Not a fabricated status.
    public bool IsActive { get; set; }

    public DateTime CreationTime { get; set; }
}
