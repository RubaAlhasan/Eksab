using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Rewards;

public class Reward : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public string NameAr { get; private set; }

    public string NameEn { get; private set; }

    public RewardType Type { get; private set; }

    public int PointsCost { get; private set; }

    public int? StockRemaining { get; private set; }

    public DateTime? ValidFrom { get; private set; }

    public DateTime? ValidTo { get; private set; }

    public string? ImageBlobName { get; private set; }

    // Null = no approval escalation. Redemption confirmation for a reward whose PointsCost meets or
    // exceeds this threshold requires Manager+, not just Cashier — see PosAppService.ConfirmRedemptionAsync.
    public int? ApprovalThresholdPoints { get; private set; }

    protected Reward()
    {
        NameAr = string.Empty;
        NameEn = string.Empty;
    }

    private Reward(Guid id, string nameAr, string nameEn, RewardType type, int pointsCost)
        : base(id)
    {
        NameAr = Check.NotNullOrWhiteSpace(nameAr, nameof(nameAr), RewardConsts.MaxNameLength);
        NameEn = Check.NotNullOrWhiteSpace(nameEn, nameof(nameEn), RewardConsts.MaxNameLength);
        Type = type;
        PointsCost = pointsCost;
    }

    public static Reward Create(Guid id, string nameAr, string nameEn, RewardType type, int pointsCost)
    {
        return new Reward(id, nameAr, nameEn, type, pointsCost);
    }

    public void SetNames(string nameAr, string nameEn)
    {
        NameAr = Check.NotNullOrWhiteSpace(nameAr, nameof(nameAr), RewardConsts.MaxNameLength);
        NameEn = Check.NotNullOrWhiteSpace(nameEn, nameof(nameEn), RewardConsts.MaxNameLength);
    }

    public void SetType(RewardType type) => Type = type;

    public void SetPointsCost(int pointsCost) => PointsCost = pointsCost;

    public void SetStock(int? stockRemaining) => StockRemaining = stockRemaining;

    // No-op for unlimited stock (StockRemaining == null); clamped at 0 otherwise.
    public void DecrementStock()
    {
        if (StockRemaining.HasValue)
        {
            StockRemaining = Math.Max(0, StockRemaining.Value - 1);
        }
    }

    public void SetValidity(DateTime? validFrom, DateTime? validTo)
    {
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    public void SetImageBlobName(string? imageBlobName) => ImageBlobName = imageBlobName;

    public void SetApprovalThresholdPoints(int? approvalThresholdPoints) => ApprovalThresholdPoints = approvalThresholdPoints;
}
