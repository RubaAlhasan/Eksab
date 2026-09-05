using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Businesses;

// Query for the customer-facing business directory (search + nearby).
public class CustomerBusinessFilterDto : PagedResultRequestDto
{
    // Matched against the tenant name, case-insensitively.
    public string? FilterText { get; set; }

    public Guid? CategoryId { get; set; }

    // When both are supplied, results carry DistanceKm (nearest branch) and are
    // ordered nearest-first. Without them, results are ordered by name.
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}
