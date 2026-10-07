using System;
using System.Collections.Generic;

namespace Eksabli.Businesses;

// One branch's customer-visible contact info. Deliberately just Name + Phone — no address/
// lat-long/opening-hours here; those stay staff-only (see CustomerBusinessDto's own file comment
// on why this projection is intentionally narrow, and customer-store-details.component.ts's file
// comment on why a full branch locator isn't built). A business's "phone numbers" are exactly its
// branches' phones — branches already are a real add/remove collection with one phone each, so a
// business with 3 branches already has 3 phones; this just makes the existing data visible to
// members instead of adding a new, separate phone-list concept to maintain.
public class CustomerBusinessBranchDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    // Free text, as the business wrote it. Shown as-is: opening hours are not structured data on the branch.
    public string? Address { get; set; }

    public string? OpeningHours { get; set; }

    // Set together or not at all; used for a "open in maps" link.
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}

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

    // The business's own social profiles, read from BusinessProfile.SocialLinksJson under the "instagram" and
    // "facebook" keys the business settings screen writes. Null when not set.
    public string? Instagram { get; set; }

    public string? Facebook { get; set; }

    // The logo is served by BusinessController.GetLogoAsync, which is keyed by
    // BusinessProfile id (not tenant id) and is AllowAnonymous — so the client can
    // use it directly as an image URL. Null LogoBlobName means "no logo uploaded".
    public Guid BusinessProfileId { get; set; }

    public bool HasLogo { get; set; }

    // Opaque version token for the logo URL, not a path the client resolves itself — the blob store
    // is server-side and this only ever appears as `?v=...`. Without it a business that changes its
    // logo keeps serving the old one out of the HTTP cache, since the URL is keyed by profile id and
    // never otherwise changes. Already public in the same form the Business Portal uses.
    public string? LogoBlobName { get; set; }

    public int BranchCount { get; set; }

    // Straight-line distance to the nearest branch, populated only when the caller
    // supplies coordinates. Null when unknown — the client must not render "0 km".
    public double? DistanceKm { get; set; }

    // Every branch's name + phone, so a member can see all of a business's numbers (one per
    // location) rather than just the aggregate BranchCount above. Empty list, never null.
    public List<CustomerBusinessBranchDto> Branches { get; set; } = new();
}
