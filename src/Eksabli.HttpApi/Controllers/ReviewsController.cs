using System;
using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/review")]
[Authorize]
public class ReviewsController : EksabliController
{
    private readonly ICustomerReviewAppService _reviewAppService;

    public ReviewsController(ICustomerReviewAppService reviewAppService)
    {
        _reviewAppService = reviewAppService;
    }

    // Public — a business's reviews and its average rating are shown to any visitor.
    [AllowAnonymous]
    [HttpGet("{tenantId}")]
    public Task<ReviewListResultDto> GetListAsync(Guid tenantId, [FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _reviewAppService.GetListAsync(tenantId, input);
    }

    [HttpGet("{tenantId}/my")]
    public Task<ReviewDto?> GetMyReviewAsync(Guid tenantId)
    {
        return _reviewAppService.GetMyReviewAsync(tenantId);
    }

    [HttpPut("{tenantId}/my")]
    public Task<ReviewDto> CreateOrUpdateMyReviewAsync(Guid tenantId, CreateUpdateReviewDto input)
    {
        return _reviewAppService.CreateOrUpdateMyReviewAsync(tenantId, input);
    }

    [HttpDelete("{tenantId}/my")]
    public Task DeleteMyReviewAsync(Guid tenantId)
    {
        return _reviewAppService.DeleteMyReviewAsync(tenantId);
    }

    [Authorize(EksabliPermissions.Reviews.Moderate)]
    [HttpDelete("moderate/{reviewId}")]
    public Task ModerateDeleteAsync(Guid reviewId)
    {
        return _reviewAppService.ModerateDeleteAsync(reviewId);
    }
}
