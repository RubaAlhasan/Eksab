using System;
using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.SmartOffers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

// Business Portal management of Smart Offers. Same permission split as OffersController: reading is the group permission,
// and each write is its own child permission.
[ApiController]
[Route("api/app/smart-offer")]
[Authorize(EksabliPermissions.SmartOffers.Default)]
public class SmartOffersController : EksabliController
{
    private readonly ISmartOfferAppService _smartOfferAppService;

    public SmartOffersController(ISmartOfferAppService smartOfferAppService)
    {
        _smartOfferAppService = smartOfferAppService;
    }

    [HttpGet("{id}")]
    public Task<SmartOfferDto> GetAsync(Guid id)
    {
        return _smartOfferAppService.GetAsync(id);
    }

    [HttpGet]
    public Task<PagedResultDto<SmartOfferDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _smartOfferAppService.GetListAsync(input);
    }

    [Authorize(EksabliPermissions.SmartOffers.Create)]
    [HttpPost]
    public Task<SmartOfferDto> CreateAsync(CreateUpdateSmartOfferDto input)
    {
        return _smartOfferAppService.CreateAsync(input);
    }

    [Authorize(EksabliPermissions.SmartOffers.Edit)]
    [HttpPut("{id}")]
    public Task<SmartOfferDto> UpdateAsync(Guid id, CreateUpdateSmartOfferDto input)
    {
        return _smartOfferAppService.UpdateAsync(id, input);
    }

    [Authorize(EksabliPermissions.SmartOffers.Edit)]
    [HttpPut("{id}/enabled")]
    public Task<SmartOfferDto> SetEnabledAsync(Guid id, SetSmartOfferEnabledDto input)
    {
        return _smartOfferAppService.SetEnabledAsync(id, input);
    }

    [Authorize(EksabliPermissions.SmartOffers.Delete)]
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id)
    {
        return _smartOfferAppService.DeleteAsync(id);
    }
}
