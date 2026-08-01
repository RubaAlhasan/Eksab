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

    Task<CouponDto> RedeemAsync(RedeemRewardDto input);

    Task<List<CouponDto>> GetMyCouponsAsync(Guid? tenantId = null);
}
