using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Wallets;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/PointRulesController.cs).
[RemoteService(IsEnabled = false)]
public interface IPointRuleAppService : IApplicationService
{
    Task<PointRuleDto> GetAsync(Guid id);

    Task<PagedResultDto<PointRuleDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    Task<PointRuleDto> CreateAsync(CreateUpdatePointRuleDto input);

    Task<PointRuleDto> UpdateAsync(Guid id, CreateUpdatePointRuleDto input);

    Task DeleteAsync(Guid id);
}
