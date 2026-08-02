using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eksabli.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Eksabli.Engagement;

public class EfCoreReferralRepository : EfCoreRepository<EksabliDbContext, Referral, Guid>, IReferralRepository
{
    public EfCoreReferralRepository(IDbContextProvider<EksabliDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    // Caller is expected to have already disabled the IMultiTenant data filter (same convention as
    // MembershipAppService.GetMyMembershipsAsync) — this just applies the predicate.
    public async Task<List<Referral>> GetByReferrerMembershipIdsAsync(List<Guid> referrerMembershipIds, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await AsyncExecuter.ToListAsync(
            queryable.Where(r => referrerMembershipIds.Contains(r.ReferrerMembershipId)),
            GetCancellationToken(cancellationToken));
    }

    public async Task<Referral?> FindPendingByRefereeAsync(Guid refereeCustomerId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await AsyncExecuter.FirstOrDefaultAsync(
            queryable.Where(r => r.RefereeCustomerId == refereeCustomerId && r.Status == ReferralStatus.Pending),
            GetCancellationToken(cancellationToken));
    }
}
