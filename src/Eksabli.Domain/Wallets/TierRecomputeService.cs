using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Wallets;

public class TierRecomputeService : ITierRecomputeService, ITransientDependency
{
    private readonly IRepository<Tier, Guid> _tierRepository;

    public TierRecomputeService(IRepository<Tier, Guid> tierRepository)
    {
        _tierRepository = tierRepository;
    }

    public async Task RecomputeAsync(PointsWallet wallet)
    {
        var qualifying = (await _tierRepository.GetListAsync(t => t.MinLifetimePoints <= wallet.LifetimeEarned))
            .OrderByDescending(t => t.MinLifetimePoints)
            .FirstOrDefault();

        wallet.ChangeTier(qualifying?.Id);
    }
}
