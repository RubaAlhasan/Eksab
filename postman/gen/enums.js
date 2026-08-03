'use strict';

// Ordinal -> name, exactly as declared in src/Eksabli.Domain.Shared/**/*.cs (0-based, sequential).
// The wire format is a plain integer (no JsonStringEnumConverter configured), so examples use ints;
// this map exists so descriptions/docs can spell out what each int means.
const ENUMS = {
  MembershipStatus: ['Active', 'Frozen'],
  CampaignType: ['Birthday', 'DoublePoints', 'SpendXGetY', 'WinBack', 'Vip', 'NewCustomer', 'Referral'],
  CampaignStatus: ['Draft', 'Active', 'Ended'],
  CampaignTargetRuleSegmentType: ['Tier', 'Inactive', 'NewCustomer', 'All'],
  RewardType: ['Discount', 'FreeProduct', 'GiftCard'],
  CouponStatus: ['Issued', 'Redeemed', 'Expired', 'Cancelled'],
  PointsTransactionType: ['Earn', 'Redeem', 'Expire', 'Adjust', 'Refund'],
  PointsTransactionSource: ['Purchase', 'Campaign', 'Referral', 'Birthday', 'Manual', 'Reward'],
  PointRuleType: ['PerCurrencyUnit', 'PerVisit'],
  NotificationChannel: ['Push', 'Email', 'Sms', 'InApp'],
  NotificationStatus: ['Queued', 'Sent', 'Failed'],
  ReferralStatus: ['Pending', 'Completed', 'Rewarded'],
  EmployeeRole: ['Owner', 'BranchManager', 'Cashier', 'MarketingManager'],
  DevicePlatform: ['iOS', 'Android', 'Web'],
  CustomerGender: ['Unspecified', 'Male', 'Female'],
  SupportTicketStatus: ['Open', 'InProgress', 'Resolved', 'Closed'],
  SupportTicketPriority: ['Low', 'Medium', 'High', 'Urgent'],
  InvoiceStatus: ['Draft', 'Sent', 'Paid', 'Overdue'],
  TenantSubscriptionStatus: ['Trialing', 'Active', 'PastDue', 'Cancelled'],
  TenantApprovalStatus: ['Pending', 'Approved', 'Suspended'],
};

function enumVal(enumName, memberName) {
  const idx = ENUMS[enumName].indexOf(memberName);
  if (idx === -1) throw new Error(`Unknown member ${memberName} for enum ${enumName}`);
  return idx;
}

function enumDoc(enumName) {
  return ENUMS[enumName].map((n, i) => `${i}=${n}`).join(', ');
}

module.exports = { ENUMS, enumVal, enumDoc };
