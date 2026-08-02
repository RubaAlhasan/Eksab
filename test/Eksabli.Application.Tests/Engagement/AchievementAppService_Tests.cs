using System;
using System.Threading.Tasks;
using Eksabli.Features;
using Eksabli.Memberships;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Engagement;

public abstract class AchievementAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAchievementAppService _achievementAppService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IFeatureManager _featureManager;
    private readonly ICurrentTenant _currentTenant;

    protected AchievementAppService_Tests()
    {
        _achievementAppService = GetRequiredService<IAchievementAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _featureManager = GetRequiredService<IFeatureManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private async Task<Guid> CreateTenantWithGamificationAsync(bool enabled)
    {
        Guid tenantId = default;

        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });

        using (_currentTenant.Change(tenantId))
        {
            await WithUnitOfWorkAsync(() => _featureManager.SetForTenantAsync(tenantId, EksabliFeatures.Gamification, enabled ? "true" : "false"));
        }

        return tenantId;
    }

    [Fact]
    public async Task Should_Create_List_Update_And_Delete_An_Achievement_When_Gamification_Is_Enabled()
    {
        var tenantId = await CreateTenantWithGamificationAsync(enabled: true);

        using (_currentTenant.Change(tenantId))
        {
            var created = await WithUnitOfWorkAsync(() => _achievementAppService.CreateAsync(new CreateUpdateAchievementDto
            {
                Name = "Frequent Visitor",
                CriteriaJson = "{\"visits\":10}"
            }));
            created.TenantId.ShouldBe(tenantId);

            var list = await WithUnitOfWorkAsync(() => _achievementAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
            list.Items.ShouldContain(a => a.Id == created.Id);

            var updated = await WithUnitOfWorkAsync(() => _achievementAppService.UpdateAsync(created.Id, new CreateUpdateAchievementDto
            {
                Name = "Super Visitor",
                CriteriaJson = "{\"visits\":20}"
            }));
            updated.Name.ShouldBe("Super Visitor");

            await WithUnitOfWorkAsync(() => _achievementAppService.DeleteAsync(created.Id));
            var afterDelete = await WithUnitOfWorkAsync(() => _achievementAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
            afterDelete.Items.ShouldNotContain(a => a.Id == created.Id);
        }
    }

    [Fact]
    public async Task Should_Not_Create_An_Achievement_When_Gamification_Is_Disabled()
    {
        var tenantId = await CreateTenantWithGamificationAsync(enabled: false);

        using (_currentTenant.Change(tenantId))
        {
            await Should.ThrowAsync<UserFriendlyException>(() => WithUnitOfWorkAsync(() =>
                _achievementAppService.CreateAsync(new CreateUpdateAchievementDto { Name = "Frequent Visitor" })));
        }
    }

    [Fact]
    public async Task Should_Award_An_Achievement_Once_Per_Membership()
    {
        var tenantId = await CreateTenantWithGamificationAsync(enabled: true);

        Guid achievementId = default, membershipId = default;
        using (_currentTenant.Change(tenantId))
        {
            var achievement = await WithUnitOfWorkAsync(() => _achievementAppService.CreateAsync(new CreateUpdateAchievementDto { Name = "Frequent Visitor" }));
            achievementId = achievement.Id;

            await WithUnitOfWorkAsync(async () =>
            {
                var membership = Membership.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
                await _membershipRepository.InsertAsync(membership, autoSave: true);
                membershipId = membership.Id;
            });

            var award = await WithUnitOfWorkAsync(() => _achievementAppService.AwardAsync(new AwardAchievementDto
            {
                MembershipId = membershipId,
                AchievementId = achievementId
            }));
            award.MembershipId.ShouldBe(membershipId);

            await Should.ThrowAsync<UserFriendlyException>(() => WithUnitOfWorkAsync(() =>
                _achievementAppService.AwardAsync(new AwardAchievementDto { MembershipId = membershipId, AchievementId = achievementId })));

            var awards = await WithUnitOfWorkAsync(() => _achievementAppService.GetAwardsForMembershipAsync(membershipId));
            awards.ShouldHaveSingleItem();
        }
    }
}
