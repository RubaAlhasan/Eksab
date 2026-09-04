using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Eksabli.Account;

public abstract class AccountStatusAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAccountStatusAppService _accountStatusAppService;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected AccountStatusAppService_Tests()
    {
        _accountStatusAppService = GetRequiredService<IAccountStatusAppService>();
        _identityUserManager = GetRequiredService<IdentityUserManager>();
        _identityUserRepository = GetRequiredService<IIdentityUserRepository>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private IDisposable LoginAs(Guid userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, userId.ToString()));
        return _currentPrincipalAccessor.Change(new ClaimsPrincipal(identity));
    }

    private async Task<Guid> CreateUserAsync(bool mustChangePassword)
    {
        Guid userId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            var email = $"user-{Guid.NewGuid():N}@example.com";
            var user = new IdentityUser(Guid.NewGuid(), email, email, null);
            user.SetShouldChangePasswordOnNextLogin(mustChangePassword);
            (await _identityUserManager.CreateAsync(user, "P@ssw0rd!2026")).CheckErrors();
            userId = user.Id;
        });
        return userId;
    }

    [Fact]
    public async Task Should_Report_False_When_The_Flag_Is_Not_Set()
    {
        var userId = await CreateUserAsync(mustChangePassword: false);

        using (LoginAs(userId))
        {
            (await WithUnitOfWorkAsync(() => _accountStatusAppService.GetMustChangePasswordAsync())).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Should_Report_True_When_The_Flag_Is_Set()
    {
        var userId = await CreateUserAsync(mustChangePassword: true);

        using (LoginAs(userId))
        {
            (await WithUnitOfWorkAsync(() => _accountStatusAppService.GetMustChangePasswordAsync())).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Should_Invalidate_The_Cached_Flag_The_Moment_The_User_Changes()
    {
        var userId = await CreateUserAsync(mustChangePassword: true);

        using (LoginAs(userId))
        {
            // Primes the 30s cache with "true" — the same path MustChangePasswordFilter goes through
            // on every request.
            (await WithUnitOfWorkAsync(() => _accountStatusAppService.GetMustChangePasswordAsync())).ShouldBeTrue();

            // Simulates what ABP's own ProfileAppService.ChangePasswordAsync does after a successful
            // password change: clear the flag and save.
            await WithUnitOfWorkAsync(async () =>
            {
                var user = await _identityUserRepository.GetAsync(userId);
                user.SetShouldChangePasswordOnNextLogin(false);
                await _identityUserRepository.UpdateAsync(user);
            });

            // Without MustChangePasswordCacheInvalidator reacting to the entity-changed event, this
            // would still read the stale cached "true" for up to 30 more seconds.
            (await WithUnitOfWorkAsync(() => _accountStatusAppService.GetMustChangePasswordAsync())).ShouldBeFalse();
        }
    }
}
