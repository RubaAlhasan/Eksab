using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Billing;

public class SubscriptionPlanDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = string.Empty;

    public decimal MonthlyPrice { get; set; }

    public string FeatureLimitsJson { get; set; } = "{}";

    public bool IsTrialDefault { get; set; }
}
