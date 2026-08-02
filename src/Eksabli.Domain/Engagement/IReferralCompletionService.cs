using System;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Eksabli.Wallets;

namespace Eksabli.Engagement;

// Hooks into Feature 02's point-award pipeline (PosAppService.AwardPointsCoreAsync) — see
// docs/eksabli-loyalty-platform/features/06-engagement-gamification/README.md#implementation-checklist.
public interface IReferralCompletionService
{
    // isFirstEarn = the wallet had no prior Earn transactions before this one — the qualifying action
    // for referral completion. No-op if the membership isn't a pending referee.
    Task TryCompleteAsync(Membership refereeMembership, PointsWallet refereeWallet, bool isFirstEarn);
}
