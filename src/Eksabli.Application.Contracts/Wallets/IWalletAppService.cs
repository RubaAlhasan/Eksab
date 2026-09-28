using System;
using System.Threading.Tasks;
using Eksabli.Reports;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Wallets;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/WalletController.cs).
[RemoteService(IsEnabled = false)]
public interface IWalletAppService : IApplicationService
{
    Task<PagedResultDto<TransactionListItemDto>> GetMyTransactionHistoryAsync(Guid tenantId, GetMyTransactionHistoryInput input);
}
