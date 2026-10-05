using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.SmartOffers;

// Business Portal management surface. Exposed via SmartOffersController, gated per action by the Eksabli.SmartOffers.*
// permissions there.
[RemoteService(IsEnabled = false)]
public interface ISmartOfferAppService : IApplicationService
{
    Task<SmartOfferDto> GetAsync(Guid id);

    Task<PagedResultDto<SmartOfferDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    Task<SmartOfferDto> CreateAsync(CreateUpdateSmartOfferDto input);

    Task<SmartOfferDto> UpdateAsync(Guid id, CreateUpdateSmartOfferDto input);

    Task<SmartOfferDto> SetEnabledAsync(Guid id, SetSmartOfferEnabledDto input);

    Task DeleteAsync(Guid id);
}
