using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Engagement;

public interface IReferralRepository : IRepository<Referral, Guid>
{
    // "Referral status list" — as referrer, across every tenant the customer has referred someone
    // into. Caller must disable the IMultiTenant data filter first (same convention as
    // MembershipAppService.GetMyMembershipsAsync) since referrerMembershipIds spans multiple tenants.
    Task<List<Referral>> GetByReferrerMembershipIdsAsync(List<Guid> referrerMembershipIds, CancellationToken cancellationToken = default);

    // Referral completion check — is this customer someone's pending referee in the ambient tenant?
    Task<Referral?> FindPendingByRefereeAsync(Guid refereeCustomerId, CancellationToken cancellationToken = default);
}
