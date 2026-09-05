using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Businesses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

// Customer-facing business directory, used by the mobile app for search, nearby,
// store details, and for resolving the bare TenantIds that wallet/coupon/membership
// responses carry into something displayable.
//
// Separate from BusinessController, which is the tenant's view of *its own* profile
// and requires BusinessProfile permissions no customer holds.
[ApiController]
[Route("api/app/customer-business")]
[Authorize]
public class CustomerBusinessController : EksabliController
{
    private readonly ICustomerBusinessAppService _customerBusinessAppService;

    public CustomerBusinessController(ICustomerBusinessAppService customerBusinessAppService)
    {
        _customerBusinessAppService = customerBusinessAppService;
    }

    // Search / nearby. Pass latitude+longitude to get DistanceKm and nearest-first order.
    [HttpGet]
    public Task<PagedResultDto<CustomerBusinessDto>> GetListAsync([FromQuery] CustomerBusinessFilterDto input)
    {
        return _customerBusinessAppService.GetListAsync(input);
    }

    // Store details.
    [HttpGet("{tenantId}")]
    public Task<CustomerBusinessDto> GetAsync(Guid tenantId)
    {
        return _customerBusinessAppService.GetAsync(tenantId);
    }

    // Batch resolution — POST rather than GET so a long id list can't hit URL limits.
    [HttpPost("lookup")]
    public Task<List<CustomerBusinessDto>> GetManyAsync(CustomerBusinessLookupDto input)
    {
        return _customerBusinessAppService.GetManyAsync(input);
    }
}
