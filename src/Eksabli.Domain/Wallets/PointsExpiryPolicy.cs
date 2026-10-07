using System;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace Eksabli.Wallets;

// Answers "when does a point awarded right now expire?" from the current business's own setting. Every award path
// (POS purchase, tier, campaign, referral) calls this instead of reading BusinessProfile itself, so the rule lives in one place.
public interface IPointsExpiryPolicy
{
    /// <summary>The ExpiresAt for an earn row awarded now; null when the current business never expires its points.</summary>
    Task<DateTime?> GetExpiresAtForEarnAsync();
}

public class PointsExpiryPolicy : IPointsExpiryPolicy, ITransientDependency
{
    // How far ahead the customer app warns about points about to expire.
    public static readonly TimeSpan ExpiringSoonWindow = TimeSpan.FromDays(30);

    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly IClock _clock;

    public PointsExpiryPolicy(IRepository<BusinessProfile, Guid> businessProfileRepository, IClock clock)
    {
        _businessProfileRepository = businessProfileRepository;
        _clock = clock;
    }

    public async Task<DateTime?> GetExpiresAtForEarnAsync()
    {
        // A tenant with no profile yet has no configured expiry, so its points do not expire.
        var profile = await _businessProfileRepository.FirstOrDefaultAsync(_ => true);
        return profile?.ComputeExpiresAt(_clock.Now);
    }
}
