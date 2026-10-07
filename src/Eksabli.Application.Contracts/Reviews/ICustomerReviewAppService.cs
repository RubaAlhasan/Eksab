using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Reviews;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/ReviewsController.cs). Reads are
// public (anonymous); writes are host-realm, customer-scoped, membership-gated in the app service
// itself (same shape as CustomerSmartOfferAppService.WatchPriceAsync) — plain customer create/update/
// delete-own needs no permission beyond authenticated + member, same reasoning as Follow/SupportTickets.
[RemoteService(IsEnabled = false)]
public interface ICustomerReviewAppService : IApplicationService
{
    // Public — a business's reviews and its average rating are shown to any visitor, not just members.
    Task<ReviewListResultDto> GetListAsync(Guid tenantId, PagedAndSortedResultRequestDto input);

    // Host-realm, customer-scoped: the caller's own review at this business, or null if they haven't left one.
    Task<ReviewDto?> GetMyReviewAsync(Guid tenantId);

    // Create-or-update: a customer gets at most one review per business (see Review's own comment).
    // Refused for a non-member, the same guard WatchPriceAsync applies.
    Task<ReviewDto> CreateOrUpdateMyReviewAsync(Guid tenantId, CreateUpdateReviewDto input);

    Task DeleteMyReviewAsync(Guid tenantId);

    // Business-side moderation (Eksabli.Reviews.Moderate) — ambient tenant, remove any review from
    // this business's own page.
    Task ModerateDeleteAsync(Guid reviewId);
}
