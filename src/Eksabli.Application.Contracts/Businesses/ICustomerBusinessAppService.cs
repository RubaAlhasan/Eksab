using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Businesses;

// Customer-facing business directory. Only Approved businesses are ever returned —
// Pending/Suspended tenants must not be discoverable, matching the guard
// MembershipAppService.JoinAsync already applies at join time.
//
// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/CustomerBusinessController.cs).
[RemoteService(IsEnabled = false)]
public interface ICustomerBusinessAppService : IApplicationService
{
    Task<PagedResultDto<CustomerBusinessDto>> GetListAsync(CustomerBusinessFilterDto input);

    Task<CustomerBusinessDto> GetAsync(Guid tenantId);

    Task<List<CustomerBusinessDto>> GetManyAsync(CustomerBusinessLookupDto input);
}
