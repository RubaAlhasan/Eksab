using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Eksabli.CustomerProfiles;

public abstract class CustomerProfileAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICustomerProfileAppService _customerProfileAppService;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected CustomerProfileAppService_Tests()
    {
        _customerProfileAppService = GetRequiredService<ICustomerProfileAppService>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private IDisposable LoginAs(Guid userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, userId.ToString()));
        return _currentPrincipalAccessor.Change(new ClaimsPrincipal(identity));
    }

    [Fact]
    public async Task Should_Get_Or_Create_My_Profile_On_First_Access()
    {
        var userId = Guid.NewGuid();

        using (LoginAs(userId))
        {
            var profile = await WithUnitOfWorkAsync(() => _customerProfileAppService.GetMyProfileAsync());
            profile.UserId.ShouldBe(userId);
        }
    }

    [Fact]
    public async Task Should_Update_My_Profile()
    {
        var userId = Guid.NewGuid();

        using (LoginAs(userId))
        {
            var updated = await WithUnitOfWorkAsync(() => _customerProfileAppService.UpdateMyProfileAsync(new UpdateCustomerProfileDto
            {
                FirstName = "John",
                LastName = "Doe",
                Gender = CustomerGender.Male
            }));

            updated.FirstName.ShouldBe("John");
            updated.LastName.ShouldBe("Doe");
            updated.Gender.ShouldBe(CustomerGender.Male);
        }
    }
}
