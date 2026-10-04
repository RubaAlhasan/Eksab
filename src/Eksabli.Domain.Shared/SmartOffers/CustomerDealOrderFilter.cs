namespace Eksabli.SmartOffers;

// Filter on a customer's own deal orders. Server-side on purpose: the history is paged, so a client-side filter
// would only ever see the page it already has.
public enum CustomerDealOrderFilter
{
    All = 0,

    // Still on hold and not yet lapsed: the customer can show the code at the counter.
    Active = 1,

    Completed = 2,

    // Cancelled, rejected, expired, or held but lapsed.
    Closed = 3
}
