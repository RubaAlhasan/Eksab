using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Memberships;

namespace Eksabli.Campaigns;

// Shared by CampaignAppService's target-segment preview and CampaignSweepWorker's actual fan-out, so
// "preview says 340 matching customers" and "the sweep that ran at 2am" can never silently diverge.
public interface ICampaignSegmentEvaluator
{
    Task<List<Membership>> EvaluateAsync(Campaign campaign);
}
