using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.CustomerProfiles;
using Eksabli.Memberships;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Eksabli.Reviews;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/ReviewsController.cs). Writes are
// scoped to the caller's own review by switching the ambient tenant, the same shape
// FollowAppService.FollowAsync and CustomerSmartOfferAppService.WatchPriceAsync already use — no
// IDataFilter.Disable<IMultiTenant> needed here since every call operates on exactly one tenant.
[RemoteService(IsEnabled = false)]
public class CustomerReviewAppService : ApplicationService, ICustomerReviewAppService
{
    // A page of reviews is rendered on a store's details screen, not a report — capped the same way
    // CustomerSmartOfferAppService caps its own orders page.
    private const int MaxPageSize = 50;

    private readonly IRepository<Review, Guid> _reviewRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly ICurrentTenant _currentTenant;

    public CustomerReviewAppService(
        IRepository<Review, Guid> reviewRepository,
        IRepository<Membership, Guid> membershipRepository,
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        ICurrentTenant currentTenant)
    {
        _reviewRepository = reviewRepository;
        _membershipRepository = membershipRepository;
        _customerProfileRepository = customerProfileRepository;
        _currentTenant = currentTenant;
    }

    public async Task<ReviewListResultDto> GetListAsync(Guid tenantId, PagedAndSortedResultRequestDto input)
    {
        using (_currentTenant.Change(tenantId))
        {
            var totalCount = await _reviewRepository.CountAsync();

            // The average is over every review at this business, not just the page being returned —
            // see ReviewListResultDto's own comment. Bounded in-memory, same convention
            // CategoryAppService's cross-tenant counts already use for a "good enough at this scale" read.
            var allRatings = (await _reviewRepository.GetListAsync()).Select(r => r.Rating).ToList();
            var averageRating = allRatings.Count == 0 ? 0 : allRatings.Average();

            var reviews = await _reviewRepository.GetPagedListAsync(
                input.SkipCount,
                Math.Clamp(input.MaxResultCount, 1, MaxPageSize),
                input.Sorting.IsNullOrWhiteSpace() ? "CreationTime desc" : input.Sorting);

            var dtos = ObjectMapper.Map<List<Review>, List<ReviewDto>>(reviews);
            await AttachReviewerNamesAsync(dtos, reviews);

            return new ReviewListResultDto(totalCount, dtos, averageRating);
        }
    }

    public async Task<ReviewDto?> GetMyReviewAsync(Guid tenantId)
    {
        var customerId = CurrentUser.GetId();
        using (_currentTenant.Change(tenantId))
        {
            var review = await _reviewRepository.FirstOrDefaultAsync(r => r.CustomerId == customerId);
            if (review == null) return null;

            var dto = ObjectMapper.Map<Review, ReviewDto>(review);
            await AttachReviewerNamesAsync(new List<ReviewDto> { dto }, new List<Review> { review });
            return dto;
        }
    }

    public async Task<ReviewDto> CreateOrUpdateMyReviewAsync(Guid tenantId, CreateUpdateReviewDto input)
    {
        var customerId = CurrentUser.GetId();
        using (_currentTenant.Change(tenantId))
        {
            var isMember = await _membershipRepository.AnyAsync(m => m.CustomerId == customerId && m.Status == MembershipStatus.Active);
            if (!isMember)
            {
                throw new UserFriendlyException("Join this business before leaving a review.");
            }

            var review = await _reviewRepository.FirstOrDefaultAsync(r => r.CustomerId == customerId);
            if (review == null)
            {
                review = Review.Create(GuidGenerator.Create(), customerId, input.Rating, input.Comment);
                await _reviewRepository.InsertAsync(review);
            }
            else
            {
                review.SetRating(input.Rating, input.Comment);
                await _reviewRepository.UpdateAsync(review);
            }

            var dto = ObjectMapper.Map<Review, ReviewDto>(review);
            await AttachReviewerNamesAsync(new List<ReviewDto> { dto }, new List<Review> { review });
            return dto;
        }
    }

    public async Task DeleteMyReviewAsync(Guid tenantId)
    {
        var customerId = CurrentUser.GetId();
        using (_currentTenant.Change(tenantId))
        {
            await _reviewRepository.DeleteAsync(r => r.CustomerId == customerId, autoSave: true);
        }
    }

    // Ambient tenant (the business's own session) — ReviewsController gates the action itself with
    // [Authorize(EksabliPermissions.Reviews.Moderate)], the same split FollowsController.GetFollowersAsync
    // uses: the permission lives on the controller action, not re-checked in here.
    public async Task ModerateDeleteAsync(Guid reviewId)
    {
        var review = await _reviewRepository.FindAsync(reviewId);
        if (review != null)
        {
            await _reviewRepository.DeleteAsync(review);
        }
    }

    private async Task AttachReviewerNamesAsync(List<ReviewDto> dtos, List<Review> reviews)
    {
        var customerIds = reviews.Select(r => r.CustomerId).Distinct().ToList();
        var profiles = (await _customerProfileRepository.GetListAsync(p => customerIds.Contains(p.UserId)))
            .ToDictionary(p => p.UserId);

        for (var i = 0; i < reviews.Count; i++)
        {
            var profile = profiles.GetValueOrDefault(reviews[i].CustomerId);
            dtos[i].ReviewerName = FormatReviewerName(profile?.FirstName, profile?.LastName);
        }
    }

    // "First name + last initial" (e.g. "Sara K.") — never the full last name, since this is shown
    // publicly on the business's own page, unlike FollowAppService.GetFollowersAsync's full name
    // (business-realm-only, a different trust boundary). Null when there's no first name on file.
    private static string? FormatReviewerName(string? firstName, string? lastName)
    {
        if (firstName.IsNullOrWhiteSpace()) return null;

        var initial = lastName.IsNullOrWhiteSpace() ? string.Empty : $" {lastName!.Trim()[0]}.";
        return $"{firstName!.Trim()}{initial}";
    }
}
