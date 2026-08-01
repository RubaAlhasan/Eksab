using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Modularity;
using Volo.Abp.Validation;
using Xunit;

namespace Eksabli.Rewards;

public abstract class RewardAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IRewardAppService _rewardAppService;

    protected RewardAppService_Tests()
    {
        _rewardAppService = GetRequiredService<IRewardAppService>();
    }

    [Fact]
    public async Task Should_Create_List_Update_And_Delete_A_Reward()
    {
        var created = await WithUnitOfWorkAsync(() => _rewardAppService.CreateAsync(new CreateUpdateRewardDto
        {
            NameAr = "قهوة مجانية",
            NameEn = "Free Coffee",
            Type = RewardType.FreeProduct,
            PointsCost = 100
        }));

        var list = await WithUnitOfWorkAsync(() => _rewardAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
        list.Items.ShouldContain(r => r.Id == created.Id);

        var updated = await WithUnitOfWorkAsync(() => _rewardAppService.UpdateAsync(created.Id, new CreateUpdateRewardDto
        {
            NameAr = "قهوة مجانية",
            NameEn = "Free Coffee",
            Type = RewardType.FreeProduct,
            PointsCost = 150
        }));
        updated.PointsCost.ShouldBe(150);

        await WithUnitOfWorkAsync(() => _rewardAppService.DeleteAsync(created.Id));
        var afterDelete = await WithUnitOfWorkAsync(() => _rewardAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
        afterDelete.Items.ShouldNotContain(r => r.Id == created.Id);
    }

    [Fact]
    public async Task Should_Not_Create_A_Reward_Without_PointsCost()
    {
        var exception = await Assert.ThrowsAsync<AbpValidationException>(async () =>
        {
            await WithUnitOfWorkAsync(() => _rewardAppService.CreateAsync(new CreateUpdateRewardDto
            {
                NameAr = "قهوة مجانية",
                NameEn = "Free Coffee",
                Type = RewardType.FreeProduct,
                PointsCost = 0
            }));
        });

        exception.ValidationErrors.ShouldContain(err => err.MemberNames.Any(mem => mem == "PointsCost"));
    }
}
