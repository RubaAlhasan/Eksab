using System;

namespace Eksabli.SmartOffers;

// One deal the customer is watching for a price drop. Only the two keys the app needs to show the watch state.
public class SmartOfferWatchDto
{
    public Guid TenantId { get; set; }

    public Guid SmartOfferId { get; set; }
}
