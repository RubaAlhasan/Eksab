using System;
using System.Collections.Generic;
using Eksabli.Billing;

namespace Eksabli.Dashboards;

// Admin Portal Dashboard 360 payloads. Platform-wide, so every date is a UTC calendar day. The money fields are
// null when the caller lacks the permission that unlocks them: null means "hidden", never "zero". Points are summed
// across tenants; money never is, it stays one entry per currency.
public class AdminDashboardSummaryDto
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public int BusinessesApproved { get; set; }

    public int BusinessesPending { get; set; }

    public int BusinessesSuspended { get; set; }

    // Approved businesses that earned points in the trailing ActivityWindowDays.
    public int ActiveBusinesses { get; set; }

    // Every customer account on the platform, as of now.
    public int TotalCustomers { get; set; }

    // Customer accounts created in the window.
    public int NewCustomers { get; set; }

    public int PointsIssued { get; set; }

    public int PointsRedeemed { get; set; }

    public int Transactions { get; set; }

    public int LiveOffers { get; set; }

    public int BuyNowSales { get; set; }

    // Null unless the caller holds Eksabli.Billing.ManagePlatform.
    public List<CurrencyAmountDto>? RecordedValue { get; set; }

    // Null unless the caller holds Eksabli.Billing.ManagePlatform.
    public List<CurrencyAmountDto>? BuyNowValue { get; set; }
}

public class AdminDashboardTrendPointDto
{
    public DateOnly Date { get; set; }

    public int PointsIssued { get; set; }

    public int PointsRedeemed { get; set; }

    public int Transactions { get; set; }

    public int NewCustomers { get; set; }

    public int NewBusinesses { get; set; }

    // Null unless the caller holds Eksabli.Billing.ManagePlatform.
    public List<CurrencyAmountDto>? RecordedValue { get; set; }
}

public class TopBusinessDto
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int PointsIssued { get; set; }

    public int Transactions { get; set; }
}

public class TopOfferDto
{
    public Guid OfferId { get; set; }

    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public int Completed { get; set; }

    // Null unless the caller holds Eksabli.Billing.ManagePlatform.
    public List<CurrencyAmountDto>? CompletedValue { get; set; }
}

public class AdminAlertsDto
{
    public int PendingApprovals { get; set; }

    public int SuspendedBusinesses { get; set; }

    // Rewards at or below LowStockThreshold, across every business.
    public int LowStockRewards { get; set; }

    // Null unless the caller holds Eksabli.Billing.ManagePlatform.
    public int? PastDueSubscriptions { get; set; }

    // Null unless the caller holds Eksabli.SupportTickets.Manage.
    public int? OpenSupportTickets { get; set; }
}

public class AdminActivityItemDto
{
    public AdminActivityKind Kind { get; set; }

    public DateTime OccurredAt { get; set; }

    // The business name, the ticket subject, or null for a customer signup (customers are not named on the dashboard).
    public string? Subject { get; set; }
}

// Append new members, never renumber.
public enum AdminActivityKind
{
    BusinessRegistered = 0,
    CustomerJoined = 1,
    SupportTicketOpened = 2,
}
