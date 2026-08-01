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
}
