using System;
using System.Threading.Tasks;
using Eksabli.Offers;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/offer")]
[Authorize(EksabliPermissions.Offers.Default)]
public class OffersController : EksabliController
{
    private readonly IOfferAppService _offerAppService;

    public OffersController(IOfferAppService offerAppService)
    {
        _offerAppService = offerAppService;
    }

    [HttpGet("{id}")]
    public Task<OfferDto> GetAsync(Guid id)
    {
        return _offerAppService.GetAsync(id);
    }

    [HttpGet]
    public Task<PagedResultDto<OfferDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _offerAppService.GetListAsync(input);
    }

    [Authorize(EksabliPermissions.Offers.Create)]
    [HttpPost]
    public Task<OfferDto> CreateAsync(CreateUpdateOfferDto input)
    {
        return _offerAppService.CreateAsync(input);
    }

    [Authorize(EksabliPermissions.Offers.Edit)]
    [HttpPut("{id}")]
    public Task<OfferDto> UpdateAsync(Guid id, CreateUpdateOfferDto input)
    {
        return _offerAppService.UpdateAsync(id, input);
    }

    [Authorize(EksabliPermissions.Offers.Delete)]
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id)
    {
        return _offerAppService.DeleteAsync(id);
    }
}
