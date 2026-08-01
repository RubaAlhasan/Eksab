using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Wallets;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/WalletController.cs).
[RemoteService(IsEnabled = false)]
public interface IWalletAppService : IApplicationService
{
    Task<PagedResultDto<PointsTransactionDto>> GetMyTransactionHistoryAsync(Guid tenantId, PagedAndSortedResultRequestDto input);
}
