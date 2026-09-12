using System;
using System.Threading.Tasks;
using Eksabli.Wallets;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Platform;

// Host-realm, platform operations — exposed via an explicit controller
// (src/Eksabli.HttpApi/Controllers/AdminUsersController.cs). Spans both realms (Host-realm customers +
// every tenant's business staff), same cross-cutting shape as Businesses.IAdminTenantAppService.
[RemoteService(IsEnabled = false)]
public interface IAdminUserAppService : IApplicationService
{
    Task<PagedResultDto<AdminUserDto>> GetListAsync(AdminUserFilterDto input);

    // Customer-only (see AdminUserAppService's own comment on why a Staff row has no equivalent
    // detail view here) — every business membership this customer has joined, each with its own
    // independent wallet, resolved cross-tenant via IDataFilter.Disable<IMultiTenant>().
    Task<AdminCustomerDetailDto> GetCustomerDetailAsync(Guid customerId);

    // Read-only ledger for ONE of that customer's memberships, at the business it belongs to — same
    // "switch ambient tenant, then query normally" shape as Wallets.WalletAppService
    // .GetMyTransactionHistoryAsync, just parameterized by an explicit membershipId/tenantId instead of
    // the caller's own CustomerId (this runs as Host-realm staff looking at an arbitrary customer).
    Task<PagedResultDto<PointsTransactionDto>> GetCustomerTransactionsAsync(Guid membershipId, Guid tenantId, PagedAndSortedResultRequestDto input);
}
