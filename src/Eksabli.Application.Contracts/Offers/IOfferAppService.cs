using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Offers;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/OffersController.cs).
[RemoteService(IsEnabled = false)]
public interface IOfferAppService : IApplicationService
{
    Task<OfferDto> GetAsync(Guid id);

    Task<PagedResultDto<OfferDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    Task<OfferDto> CreateAsync(CreateUpdateOfferDto input);

    Task<OfferDto> UpdateAsync(Guid id, CreateUpdateOfferDto input);

    Task DeleteAsync(Guid id);
}
