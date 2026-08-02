using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Billing;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/SubscriptionPlansController.cs).
[RemoteService(IsEnabled = false)]
public interface ISubscriptionPlanAppService : IApplicationService
{
    Task<SubscriptionPlanDto> GetAsync(Guid id);

    Task<PagedResultDto<SubscriptionPlanDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    Task<SubscriptionPlanDto> CreateAsync(CreateUpdateSubscriptionPlanDto input);

    Task<SubscriptionPlanDto> UpdateAsync(Guid id, CreateUpdateSubscriptionPlanDto input);

    Task DeleteAsync(Guid id);
}
