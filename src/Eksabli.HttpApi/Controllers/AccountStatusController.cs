using System.Threading.Tasks;
using Eksabli.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eksabli.Controllers;

// Self-service only — no EksabliPermissions gate, same shape as UserNotificationsController's own
// feed endpoints (any authenticated user reading their own state needs nothing beyond being logged in).
[ApiController]
[Route("api/app/account-status")]
[Authorize]
public class AccountStatusController : EksabliController
{
    private readonly IAccountStatusAppService _accountStatusAppService;

    public AccountStatusController(IAccountStatusAppService accountStatusAppService)
    {
        _accountStatusAppService = accountStatusAppService;
    }

    [HttpGet("must-change-password")]
    public Task<bool> GetMustChangePasswordAsync()
    {
        return _accountStatusAppService.GetMustChangePasswordAsync();
    }
}
