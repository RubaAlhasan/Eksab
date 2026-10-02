using System.Threading.Tasks;
using Eksabli.Shared;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Modularity;
using Xunit;

namespace Eksabli.Wallets;

public abstract class PointRuleAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IPointRuleAppService _pointRuleAppService;

    protected PointRuleAppService_Tests()
    {
        _pointRuleAppService = GetRequiredService<IPointRuleAppService>();
    }

    [Fact]
    public async Task Should_Create_List_Update_And_Delete_A_PointRule()
    {
        var created = await WithUnitOfWorkAsync(() => _pointRuleAppService.CreateAsync(new CreateUpdatePointRuleDto
        {
            RuleType = PointRuleType.PerCurrencyUnit,
            PointsPerUnit = 1m,
            Currency = Currency.Syp
        }));

        var list = await WithUnitOfWorkAsync(() => _pointRuleAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
        list.Items.ShouldContain(r => r.Id == created.Id);

        var updated = await WithUnitOfWorkAsync(() => _pointRuleAppService.UpdateAsync(created.Id, new CreateUpdatePointRuleDto
        {
            RuleType = PointRuleType.PerCurrencyUnit,
            PointsPerUnit = 2m,
            Currency = Currency.Syp
        }));
        updated.PointsPerUnit.ShouldBe(2m);

        await WithUnitOfWorkAsync(() => _pointRuleAppService.DeleteAsync(created.Id));
        var afterDelete = await WithUnitOfWorkAsync(() => _pointRuleAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
        afterDelete.Items.ShouldNotContain(r => r.Id == created.Id);
    }

    [Fact]
    public async Task Should_Not_Create_Duplicate_RuleType_For_The_Same_Tenant()
    {
        await WithUnitOfWorkAsync(() => _pointRuleAppService.CreateAsync(new CreateUpdatePointRuleDto
        {
            RuleType = PointRuleType.PerVisit,
            PointsPerUnit = 5m
        }));

        await Assert.ThrowsAsync<UserFriendlyException>(async () =>
        {
            await WithUnitOfWorkAsync(() => _pointRuleAppService.CreateAsync(new CreateUpdatePointRuleDto
            {
                RuleType = PointRuleType.PerVisit,
                PointsPerUnit = 10m
            }));
        });
    }

    [Fact]
    public async Task Should_Not_Create_Duplicate_PerCurrencyUnit_Rule_For_Same_Currency()
    {
        await WithUnitOfWorkAsync(() => _pointRuleAppService.CreateAsync(new CreateUpdatePointRuleDto
        {
            RuleType = PointRuleType.PerCurrencyUnit,
            PointsPerUnit = 1m,
            Currency = Currency.Syp
        }));

        await Assert.ThrowsAsync<UserFriendlyException>(async () =>
        {
            await WithUnitOfWorkAsync(() => _pointRuleAppService.CreateAsync(new CreateUpdatePointRuleDto
            {
                RuleType = PointRuleType.PerCurrencyUnit,
                PointsPerUnit = 2m,
                Currency = Currency.Syp
            }));
        });
    }

    [Fact]
    public async Task Should_Allow_PerCurrencyUnit_Rules_For_Different_Currencies()
    {
        var sypRule = await WithUnitOfWorkAsync(() => _pointRuleAppService.CreateAsync(new CreateUpdatePointRuleDto
        {
            RuleType = PointRuleType.PerCurrencyUnit,
            PointsPerUnit = 1m,
            Currency = Currency.Syp
        }));

        var usdRule = await WithUnitOfWorkAsync(() => _pointRuleAppService.CreateAsync(new CreateUpdatePointRuleDto
        {
            RuleType = PointRuleType.PerCurrencyUnit,
            PointsPerUnit = 5m,
            Currency = Currency.Usd
        }));

        sypRule.Id.ShouldNotBe(usdRule.Id);
        usdRule.PointsPerUnit.ShouldBe(5m);
    }

    [Fact]
    public async Task Should_Reject_PerCurrencyUnit_Rule_With_No_Currency()
    {
        await Assert.ThrowsAsync<UserFriendlyException>(async () =>
        {
            await WithUnitOfWorkAsync(() => _pointRuleAppService.CreateAsync(new CreateUpdatePointRuleDto
            {
                RuleType = PointRuleType.PerCurrencyUnit,
                PointsPerUnit = 1m,
                Currency = null
            }));
        });
    }

    [Fact]
    public async Task Should_Reject_PerVisit_Rule_With_A_Currency()
    {
        await Assert.ThrowsAsync<UserFriendlyException>(async () =>
        {
            await WithUnitOfWorkAsync(() => _pointRuleAppService.CreateAsync(new CreateUpdatePointRuleDto
            {
                RuleType = PointRuleType.PerVisit,
                PointsPerUnit = 10m,
                Currency = Currency.Syp
            }));
        });
    }
}
