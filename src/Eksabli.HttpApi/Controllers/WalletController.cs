using System;
using System.Threading.Tasks;
using Eksabli.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/wallet")]
[Authorize]
public class WalletController : EksabliController
{
    private readonly IWalletAppService _walletAppService;

    public WalletController(IWalletAppService walletAppService)
    {
        _walletAppService = walletAppService;
    }

    [HttpGet("{tenantId}/transactions")]
    public Task<PagedResultDto<PointsTransactionDto>> GetMyTransactionHistoryAsync(Guid tenantId, [FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _walletAppService.GetMyTransactionHistoryAsync(tenantId, input);
    }
}
