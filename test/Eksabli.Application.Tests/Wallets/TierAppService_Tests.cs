using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Modularity;
using Volo.Abp.Validation;
using Xunit;

namespace Eksabli.Wallets;

public abstract class TierAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ITierAppService _tierAppService;

    protected TierAppService_Tests()
    {
        _tierAppService = GetRequiredService<ITierAppService>();
    }

    [Fact]
    public async Task Should_Create_List_Update_And_Delete_A_Tier()
    {
        var created = await WithUnitOfWorkAsync(() => _tierAppService.CreateAsync(new CreateUpdateTierDto
        {
            Name = "Silver",
            MinLifetimePoints = 0,
            Multiplier = 1.0m
        }));

        var list = await WithUnitOfWorkAsync(() => _tierAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
        list.Items.ShouldContain(t => t.Id == created.Id);

        var updated = await WithUnitOfWorkAsync(() => _tierAppService.UpdateAsync(created.Id, new CreateUpdateTierDto
        {
            Name = "Silver",
            MinLifetimePoints = 0,
            Multiplier = 1.25m
        }));
        updated.Multiplier.ShouldBe(1.25m);

        await WithUnitOfWorkAsync(() => _tierAppService.DeleteAsync(created.Id));
        var afterDelete = await WithUnitOfWorkAsync(() => _tierAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
        afterDelete.Items.ShouldNotContain(t => t.Id == created.Id);
    }

    [Fact]
    public async Task Should_Not_Create_A_Tier_Without_Name()
    {
        var exception = await Assert.ThrowsAsync<AbpValidationException>(async () =>
        {
            await WithUnitOfWorkAsync(() => _tierAppService.CreateAsync(new CreateUpdateTierDto
            {
                Name = "",
                MinLifetimePoints = 0,
                Multiplier = 1.0m
            }));
        });

        exception.ValidationErrors.ShouldContain(err => err.MemberNames.Any(mem => mem == "Name"));
    }
}
