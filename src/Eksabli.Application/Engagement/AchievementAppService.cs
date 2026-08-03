using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Features;
using Eksabli.Memberships;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Features;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Engagement;

[RemoteService(IsEnabled = false)]
public class AchievementAppService : ApplicationService, IAchievementAppService
{
    private readonly IAchievementRepository _repository;
    private readonly IRepository<AchievementAward, Guid> _awardRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly ICurrentTenant _currentTenant;

    public AchievementAppService(
        IAchievementRepository repository,
        IRepository<AchievementAward, Guid> awardRepository,
        IRepository<Membership, Guid> membershipRepository,
        ICurrentTenant currentTenant)
    {
        _repository = repository;
        _awardRepository = awardRepository;
        _membershipRepository = membershipRepository;
        _currentTenant = currentTenant;
    }

    public async Task<AchievementDto> GetAsync(Guid id)
    {
        var achievement = await GetVisibleAchievementAsync(id);
        return ObjectMapper.Map<Achievement, AchievementDto>(achievement);
    }

    public async Task<PagedResultDto<AchievementDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var (items, totalCount) = await _repository.GetListAsync(
            _currentTenant.Id,
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        return new PagedResultDto<AchievementDto>(totalCount, ObjectMapper.Map<List<Achievement>, List<AchievementDto>>(items));
    }

    public async Task<AchievementDto> CreateAsync(CreateUpdateAchievementDto input)
    {
        await CheckGamificationEnabledAsync();

        var achievement = Achievement.Create(GuidGenerator.Create(), _currentTenant.Id, input.Name);
        achievement.SetCriteria(input.CriteriaJson);
        await _repository.InsertAsync(achievement);
        return ObjectMapper.Map<Achievement, AchievementDto>(achievement);
    }

    public async Task<AchievementDto> UpdateAsync(Guid id, CreateUpdateAchievementDto input)
    {
        await CheckGamificationEnabledAsync();

        var achievement = await GetOwnAchievementAsync(id);
        achievement.SetName(input.Name);
        achievement.SetCriteria(input.CriteriaJson);
        await _repository.UpdateAsync(achievement);
        return ObjectMapper.Map<Achievement, AchievementDto>(achievement);
    }

    public async Task DeleteAsync(Guid id)
    {
        var achievement = await GetOwnAchievementAsync(id);
        await _repository.DeleteAsync(achievement);
    }

    public async Task<AchievementAwardDto> AwardAsync(AwardAchievementDto input)
    {
        await CheckGamificationEnabledAsync();

        await _membershipRepository.GetAsync(input.MembershipId); // 404s if not this tenant's member
        await GetVisibleAchievementAsync(input.AchievementId); // 404s if not visible to this tenant

        var existing = await _awardRepository.FirstOrDefaultAsync(a =>
            a.MembershipId == input.MembershipId && a.AchievementId == input.AchievementId);
        if (existing != null)
        {
            throw new UserFriendlyException("This customer already has this achievement.");
        }

        var award = AchievementAward.Create(GuidGenerator.Create(), input.MembershipId, input.AchievementId, Clock.Now);
        await _awardRepository.InsertAsync(award);
        return ObjectMapper.Map<AchievementAward, AchievementAwardDto>(award);
    }

    public async Task<List<AchievementAwardDto>> GetAwardsForMembershipAsync(Guid membershipId)
    {
        await _membershipRepository.GetAsync(membershipId); // 404s if not this tenant's member

        var awards = await _awardRepository.GetListAsync(a => a.MembershipId == membershipId);
        return ObjectMapper.Map<List<AchievementAward>, List<AchievementAwardDto>>(awards);
    }

    // Read access: platform-wide (TenantId == null) achievements are visible to every tenant.
    private async Task<Achievement> GetVisibleAchievementAsync(Guid id)
    {
        var achievement = await _repository.GetAsync(id);
        if (achievement.TenantId != null && achievement.TenantId != _currentTenant.Id)
        {
            throw new EntityNotFoundException(typeof(Achievement), id);
        }

        return achievement;
    }

    // Write access: only this tenant's own achievements — never someone else's, and never the
    // platform-wide catalog (that's host-side tooling, not built here).
    private async Task<Achievement> GetOwnAchievementAsync(Guid id)
    {
        var achievement = await _repository.GetAsync(id);
        if (achievement.TenantId != _currentTenant.Id)
        {
            throw new EntityNotFoundException(typeof(Achievement), id);
        }

        return achievement;
    }

    private async Task CheckGamificationEnabledAsync()
    {
        if (!await FeatureChecker.IsEnabledAsync(EksabliFeatures.Gamification))
        {
            throw new UserFriendlyException("Achievements aren't enabled for your plan.");
        }
    }
}
