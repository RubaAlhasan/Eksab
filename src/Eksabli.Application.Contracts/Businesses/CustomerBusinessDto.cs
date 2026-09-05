using System;

namespace Eksabli.Businesses;

// Customer-safe projection of a business: what the consumer app needs to render a
// business anywhere it appears (wallet row, search result, store header) without
// exposing anything tenant-internal.
//
// Deliberately NOT AdminTenantDto — that carries member counts, plan, MRR and
// approval status, none of which a customer should see.
public class CustomerBusinessDto
{
    public Guid TenantId { get; set; }

    // Lives on Volo.Abp.TenantManagement.Tenant, not on BusinessProfile.
    public string Name { get; set; } = string.Empty;

    public Guid? CategoryId { get; set; }

    public string? CategoryNameAr { get; set; }

    public string? CategoryNameEn { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string? Website { get; set; }

    // The logo is served by BusinessController.GetLogoAsync, which is keyed by
    // BusinessProfile id (not tenant id) and is AllowAnonymous — so the client can
    // use it directly as an image URL. Null LogoBlobName means "no logo uploaded".
    public Guid BusinessProfileId { get; set; }

    public bool HasLogo { get; set; }

    public int BranchCount { get; set; }

    // Straight-line distance to the nearest branch, populated only when the caller
    // supplies coordinates. Null when unknown — the client must not render "0 km".
    public double? DistanceKm { get; set; }
}
