using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Campaigns;

public class Campaign : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public string NameAr { get; private set; }

    public string NameEn { get; private set; }

    public CampaignType Type { get; private set; }

    // Freeform effect parameters (Multiplier/SpendThreshold/BonusPoints/DaysBefore) — see
    // Campaigns.CampaignRules for the shape both CampaignRulesEngine and CampaignSweepWorker parse it
    // with. Targeting criteria live on TargetRules instead, kept separate so "who" and "what happens"
    // can evolve independently.
    public string? RulesJson { get; private set; }

    public DateTime StartDate { get; private set; }

    public DateTime EndDate { get; private set; }

    public CampaignStatus Status { get; private set; }

    private readonly List<CampaignTargetRule> _targetRules = new();

    // Child collection — no separate repository (see
    // docs/eksabli-loyalty-platform/03-database-design.md#cross-cutting-notes). Only reachable through
    // this aggregate; ICampaignRepository.WithDetailsAsync is what actually populates it from the DB.
    public IReadOnlyCollection<CampaignTargetRule> TargetRules => _targetRules;

    protected Campaign()
    {
        NameAr = string.Empty;
        NameEn = string.Empty;
    }

    private Campaign(Guid id, string nameAr, string nameEn, CampaignType type, DateTime startDate, DateTime endDate)
        : base(id)
    {
        NameAr = Check.NotNullOrWhiteSpace(nameAr, nameof(nameAr), CampaignConsts.MaxNameLength);
        NameEn = Check.NotNullOrWhiteSpace(nameEn, nameof(nameEn), CampaignConsts.MaxNameLength);
        Type = type;

        // Only checked at creation, not inside SetDateRange itself — SetDateRange is also called from
        // UpdateAsync, and an already-Active/Ended campaign legitimately has a StartDate that's now in
        // the past; re-validating "not in the past" there would break editing anything else about it.
        // A brand-new Draft, on the other hand, should never start out already-expired-looking.
        if (startDate.Date < DateTime.UtcNow.Date)
        {
            throw new UserFriendlyException("The campaign's start date can't be in the past.");
        }

        SetDateRange(startDate, endDate);
        Status = CampaignStatus.Draft;
    }

    public static Campaign Create(Guid id, string nameAr, string nameEn, CampaignType type, DateTime startDate, DateTime endDate)
    {
        return new Campaign(id, nameAr, nameEn, type, startDate, endDate);
    }

    public void SetNames(string nameAr, string nameEn)
    {
        NameAr = Check.NotNullOrWhiteSpace(nameAr, nameof(nameAr), CampaignConsts.MaxNameLength);
        NameEn = Check.NotNullOrWhiteSpace(nameEn, nameof(nameEn), CampaignConsts.MaxNameLength);
    }

    public void SetType(CampaignType type) => Type = type;

    public void SetRules(string? rulesJson) => RulesJson = rulesJson;

    public void SetDateRange(DateTime startDate, DateTime endDate)
    {
        if (endDate <= startDate)
        {
            throw new UserFriendlyException("The campaign's end date must be after its start date.");
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    public void Activate()
    {
        if (Status != CampaignStatus.Draft)
        {
            throw new UserFriendlyException("Only a draft campaign can be activated.");
        }

        // Mirrors the wizard's own required-field validators (business-campaigns.component.ts,
        // applyRuleFieldValidators) — without this, a campaign could go Active with a real 0 for
        // Threshold/Bonus, which CampaignRulesEngine's own "candidateBonus > bonusPoints" comparison
        // (which starts at 0) then treats identically to "this campaign didn't apply", silently earning
        // nothing. Caught live: a real "Spend X Get Y" campaign, Active and inside its date range,
        // awarded 0 bonus points on a $200 sale because Bonus Points had been left blank at creation.
        // Checked only here, not in SetRules — a still-being-configured Draft can legitimately have no
        // rules yet; only going live half-configured is the actual problem.
        var rules = CampaignRules.Parse(RulesJson);
        switch (Type)
        {
            case CampaignType.SpendXGetY:
                if (rules.SpendThreshold is null or <= 0 || rules.BonusPoints is null or <= 0)
                {
                    throw new UserFriendlyException("A \"Spend X, Get Y\" campaign needs a spend threshold and bonus points that are both greater than zero before it can be activated.");
                }
                break;
            case CampaignType.Birthday:
            case CampaignType.WinBack:
            case CampaignType.Vip:
            case CampaignType.NewCustomer:
                if (rules.BonusPoints is null or <= 0)
                {
                    throw new UserFriendlyException("This campaign type needs bonus points greater than zero before it can be activated.");
                }
                break;
        }

        Status = CampaignStatus.Active;
    }

    // No-op once already Ended — called both from staff action (future use) and unconditionally by
    // CampaignSweepWorker's daily housekeeping pass over expired campaigns.
    public void End()
    {
        if (Status == CampaignStatus.Active)
        {
            Status = CampaignStatus.Ended;
        }
    }

    public CampaignTargetRule AddTargetRule(Guid id, CampaignTargetRuleSegmentType segmentType, string? parametersJson)
    {
        var rule = CampaignTargetRule.Create(id, Id, segmentType, parametersJson);
        _targetRules.Add(rule);
        return rule;
    }

    public void ClearTargetRules() => _targetRules.Clear();
}
