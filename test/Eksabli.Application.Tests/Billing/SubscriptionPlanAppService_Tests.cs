using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Modularity;
using Volo.Abp.Validation;
using Xunit;

namespace Eksabli.Billing;

public abstract class SubscriptionPlanAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ISubscriptionPlanAppService _subscriptionPlanAppService;

    protected SubscriptionPlanAppService_Tests()
    {
        _subscriptionPlanAppService = GetRequiredService<ISubscriptionPlanAppService>();
    }

    [Fact]
    public async Task Should_Create_List_Update_And_Delete_A_Plan()
    {
        var created = await WithUnitOfWorkAsync(() => _subscriptionPlanAppService.CreateAsync(new CreateUpdateSubscriptionPlanDto
        {
            Name = "Custom Test Plan",
            MonthlyPrice = 99m,
            FeatureLimitsJson = "{}"
        }));

        var list = await WithUnitOfWorkAsync(() => _subscriptionPlanAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
        list.Items.ShouldContain(p => p.Id == created.Id);

        var updated = await WithUnitOfWorkAsync(() => _subscriptionPlanAppService.UpdateAsync(created.Id, new CreateUpdateSubscriptionPlanDto
        {
            Name = "Custom Test Plan",
            MonthlyPrice = 149m,
            FeatureLimitsJson = "{}"
        }));
        updated.MonthlyPrice.ShouldBe(149m);

        await WithUnitOfWorkAsync(() => _subscriptionPlanAppService.DeleteAsync(created.Id));
        var afterDelete = await WithUnitOfWorkAsync(() => _subscriptionPlanAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
        afterDelete.Items.ShouldNotContain(p => p.Id == created.Id);
    }

    [Fact]
    public async Task Should_Not_Create_A_Plan_Without_Name()
    {
        var exception = await Assert.ThrowsAsync<AbpValidationException>(async () =>
        {
            await WithUnitOfWorkAsync(() => _subscriptionPlanAppService.CreateAsync(new CreateUpdateSubscriptionPlanDto
            {
                Name = "",
                MonthlyPrice = 0m,
                FeatureLimitsJson = "{}"
            }));
        });

        exception.ValidationErrors.ShouldContain(err => err.MemberNames.Any(mem => mem == "Name"));
    }
}
