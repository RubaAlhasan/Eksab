using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Rewards;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/CouponsController.cs).
[RemoteService(IsEnabled = false)]
public interface ICouponAppService : IApplicationService
{
    Task<PagedResultDto<RewardDto>> GetCatalogAsync(Guid tenantId, PagedAndSortedResultRequestDto input);

    // Across every business the customer has an active membership at, approved only — same
    // business-selection shape as CustomerSmartOfferAppService.GetFeedAsync /
    // CustomerCampaignAppService.GetMyFeedAsync. Capped, not paginated — a feed, not a report.
    Task<CustomerRewardListDto> GetMyFeedAsync(int maxResultCount = 50);

    // Creates a PENDING coupon and RESERVES the points — it does not debit them. The reward is not
    // the customer's until staff approve the returned code at the till (PosAppService).
    Task<CouponDto> RedeemAsync(RedeemRewardDto input);

    // Single coupon by id, scoped to the caller's own membership. The customer app polls this while a
    // redemption is Pending to learn whether staff approved, declined, or let it lapse.
    Task<CouponDto> GetMyCouponAsync(Guid tenantId, Guid couponId);

    // Customer-initiated withdrawal of their own Pending redemption — releases the hold immediately
    // instead of making them wait out the window. Only valid while Pending.
    Task<CouponDto> CancelMyCouponAsync(Guid tenantId, Guid couponId);

    Task<List<CouponDto>> GetMyCouponsAsync(Guid? tenantId = null);
}
