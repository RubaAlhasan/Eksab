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

[RemoteService(IsEnabled = false)]
public class ReferralAppService : ApplicationService, IReferralAppService
{
    // Same shape as Rewards.CouponAppService.MaxCodeGenerationAttempts — a retry cap on the
    // vanishingly unlikely event two fresh GUIDs' first 8 hex chars collide within the same tenant.
    private const int MaxCodeGenerationAttempts = 5;

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

    // Generates the code lazily, on this member's first request for it, and persists it — every
    // subsequent call for the same membership returns that same code rather than minting a new one.
    // See Membership.ReferralCode's own comment for why this replaced handing out the raw
    // Membership.Id (functionally fine, but not something a person can actually read out or type).
    public async Task<ReferralCodeDto> GetMyReferralCodeAsync(Guid tenantId)
    {
        var customerId = CurrentUser.GetId();

        using (_currentTenant.Change(tenantId))
        {
            var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId)
                ?? throw new UserFriendlyException("You haven't joined this business yet.");

            if (membership.ReferralCode == null)
            {
                membership.SetReferralCode(await GenerateUniqueCodeAsync());
                await _membershipRepository.UpdateAsync(membership);
            }

            return new ReferralCodeDto { Code = membership.ReferralCode! };
        }
    }

    // Must be called with the target tenant already the ambient CurrentTenant — the uniqueness check
    // relies on Membership's own IMultiTenant filter to scope it to just this business, matching
    // Membership.ReferralCode's own comment on why per-tenant uniqueness (not global, unlike
    // Rewards.Coupon.Code) is enough here.
    private async Task<string> GenerateUniqueCodeAsync()
    {
        for (var attempt = 0; attempt < MaxCodeGenerationAttempts; attempt++)
        {
            // Guid.NewGuid() (not GuidGenerator.Create()) — same reasoning as
            // CouponAppService.GenerateUniqueCodeAsync: uniform randomness across the whole code,
            // not ABP's time-prefixed sequential id.
            var code = Guid.NewGuid().ToString("N")[..ReferralConsts.CodeLength].ToUpperInvariant();

            if (!await _membershipRepository.AnyAsync(m => m.ReferralCode == code))
            {
                return code;
            }
        }

        throw new UserFriendlyException("Couldn't generate a referral code. Please try again.");
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
