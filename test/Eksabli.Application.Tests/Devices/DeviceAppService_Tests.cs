using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Eksabli.Devices;

public abstract class DeviceAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IDeviceAppService _deviceAppService;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected DeviceAppService_Tests()
    {
        _deviceAppService = GetRequiredService<IDeviceAppService>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private IDisposable LoginAs(Guid userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, userId.ToString()));
        return _currentPrincipalAccessor.Change(new ClaimsPrincipal(identity));
    }

    [Fact]
    public async Task Should_Register_And_List_My_Device()
    {
        var userId = Guid.NewGuid();
        var pushToken = Guid.NewGuid().ToString("N");

        using (LoginAs(userId))
        {
            var registered = await WithUnitOfWorkAsync(() => _deviceAppService.RegisterAsync(new RegisterDeviceDto
            {
                Platform = DevicePlatform.Android,
                PushToken = pushToken
            }));

            registered.CustomerId.ShouldBe(userId);

            var list = await WithUnitOfWorkAsync(() => _deviceAppService.GetListAsync());
            list.ShouldContain(d => d.Id == registered.Id);
        }
    }

    [Fact]
    public async Task Should_Not_Remove_Another_Customers_Device()
    {
        var ownerId = Guid.NewGuid();
        var pushToken = Guid.NewGuid().ToString("N");
        Guid deviceId;

        using (LoginAs(ownerId))
        {
            var device = await WithUnitOfWorkAsync(() => _deviceAppService.RegisterAsync(new RegisterDeviceDto
            {
                Platform = DevicePlatform.iOS,
                PushToken = pushToken
            }));
            deviceId = device.Id;
        }

        using (LoginAs(Guid.NewGuid()))
        {
            await Assert.ThrowsAsync<Volo.Abp.Authorization.AbpAuthorizationException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _deviceAppService.RemoveAsync(deviceId));
            });
        }
    }
}
