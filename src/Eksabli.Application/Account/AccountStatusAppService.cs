using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Eksabli.Account;

// Small, standalone concept rather than folded into EmployeeAssignmentAppService —
// IdentityUser.ShouldChangePasswordOnNextLogin is a general framework property, not specific to invited
// staff (EmployeeAssignmentAppService.InviteAsync is just the one place in this codebase that currently
// sets it), so this reads it generically for whoever is currently authenticated rather than assuming
// "employee". Self-service only — always reads the CALLER's own flag, never takes a target user id.
[RemoteService(IsEnabled = false)]
public class AccountStatusAppService : ApplicationService, IAccountStatusAppService
{
    private readonly IIdentityUserRepository _identityUserRepository;

    public AccountStatusAppService(IIdentityUserRepository identityUserRepository)
    {
        _identityUserRepository = identityUserRepository;
    }

    public async Task<bool> GetMustChangePasswordAsync()
    {
        var user = await _identityUserRepository.GetAsync(CurrentUser.GetId());
        return user.ShouldChangePasswordOnNextLogin;
    }
}
