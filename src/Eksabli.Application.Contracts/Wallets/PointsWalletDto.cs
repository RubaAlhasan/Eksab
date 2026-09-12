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

    // ---------------------------------------------------------------------------------------------
    // Tier progress — everything the customer app needs to draw "Gold, 600 points to Platinum"
    // without being handed the tenant's whole tier ladder.
    //
    // Multiplier is deliberately NOT exposed. It is the business's own earn-rate configuration, and a
    // customer needs to know where they stand, not what each rung is worth to the merchant.
    //
    // The floors are both here because a progress bar needs a start as well as an end: filling from
    // zero would show a customer who has just reached Gold (2,000) as nearly full on the way to
    // Platinum (5,000), when they have in fact only just begun that stretch.
    // ---------------------------------------------------------------------------------------------

    public int? CurrentTierMinLifetimePoints { get; set; }

    // Null when the customer is already on the highest tier the business defines — the UI shows
    // "top tier" rather than an unreachable target.
    public string? NextTierName { get; set; }

    public int? NextTierMinLifetimePoints { get; set; }
}
