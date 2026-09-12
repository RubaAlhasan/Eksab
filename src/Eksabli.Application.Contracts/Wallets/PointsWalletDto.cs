using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Wallets;

public class PointsWalletDto : AuditedEntityDto<Guid>
{
    public Guid MembershipId { get; set; }

    public Guid? TenantId { get; set; }

    public int Balance { get; set; }

    public int LifetimeEarned { get; set; }

    public int LifetimeRedeemed { get; set; }

    public Guid? CurrentTierId { get; set; }

    public string? CurrentTierName { get; set; }

    // Resolved only by MembershipAppService.GetMyWalletsAsync (the customer's own cross-business
    // wallet list — Tenant.Name, cross-tenant, safe here because a customer's own wallet already
    // proves membership in that business) — null for any other caller of this DTO, same "only set
    // where it's actually resolved" shape as CurrentTierName above.
    public string? BusinessName { get; set; }
}
