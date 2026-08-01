using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Wallets;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/TiersController.cs).
[RemoteService(IsEnabled = false)]
public interface ITierAppService : IApplicationService
{
    Task<TierDto> GetAsync(Guid id);

    Task<PagedResultDto<TierDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    Task<TierDto> CreateAsync(CreateUpdateTierDto input);

    Task<TierDto> UpdateAsync(Guid id, CreateUpdateTierDto input);

    Task DeleteAsync(Guid id);
}
