using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Eksabli.Engagement;

public class ReferralAppService : ApplicationService, IReferralAppService
{
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IReferralRepository _referralRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public ReferralAppService(
        IRepository<Membership, Guid> membershipRepository,
        IReferralRepository referralRepository,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter)
    {
        _membershipRepository = membershipRepository;
        _referralRepository = referralRepository;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
    }

    public async Task<ReferralCodeDto> GetMyReferralCodeAsync(Guid tenantId)
    {
        var customerId = CurrentUser.GetId();

        using (_currentTenant.Change(tenantId))
        {
            var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId)
                ?? throw new UserFriendlyException("You haven't joined this business yet.");

            return new ReferralCodeDto { Code = membership.Id };
        }
    }

    public async Task<List<ReferralDto>> GetMyReferralsAsync()
    {
        var customerId = CurrentUser.GetId();

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var myMembershipIds = (await _membershipRepository.GetListAsync(m => m.CustomerId == customerId))
                .Select(m => m.Id)
                .ToList();

            var referrals = await _referralRepository.GetByReferrerMembershipIdsAsync(myMembershipIds);
            return ObjectMapper.Map<List<Referral>, List<ReferralDto>>(referrals);
        }
    }
}
