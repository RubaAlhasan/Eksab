namespace Eksabli.Campaigns;

// Scheduled-segment-sweep types (Birthday/WinBack/Vip/NewCustomer) are evaluated by
// Campaigns.CampaignSweepWorker; real-time types (DoublePoints/SpendXGetY) are evaluated inline by
// Campaigns.ICampaignRulesEngine inside PosAppService.ComputePointsAsync — see
// docs/eksabli-loyalty-platform/features/05-campaigns-notifications/README.md#business-rules.
// Referral is defined here for schema parity with the DB design doc but has no evaluator yet — it
// triggers off Referral.Status == Completed, an entity that doesn't exist until Feature 06.
public enum CampaignType
{
    Birthday = 0,
    DoublePoints = 1,
    SpendXGetY = 2,
    WinBack = 3,
    Vip = 4,
    NewCustomer = 5,
    Referral = 6
}
