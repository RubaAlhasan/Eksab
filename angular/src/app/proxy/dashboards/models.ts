import type { CurrencyAmountDto } from '../billing/models';
import type { SmartOfferStatus } from '../smart-offers/smart-offer-status.enum';
import type { AdminActivityKind } from './admin-activity-kind.enum';
import type { BusinessInsightKind } from './business-insight-kind.enum';
import type { DashboardInsightSeverity } from './dashboard-insight-severity.enum';

export interface AdminActivityItemDto {
  kind?: AdminActivityKind;
  occurredAt?: string;
  subject?: string | null;
}

export interface AdminAlertsDto {
  pendingApprovals?: number;
  suspendedBusinesses?: number;
  lowStockRewards?: number;
  pastDueSubscriptions?: number | null;
  openSupportTickets?: number | null;
}

export interface AdminDashboardSummaryDto {
  from?: string;
  to?: string;
  businessesApproved?: number;
  businessesPending?: number;
  businessesSuspended?: number;
  activeBusinesses?: number;
  totalCustomers?: number;
  newCustomers?: number;
  pointsIssued?: number;
  pointsRedeemed?: number;
  transactions?: number;
  liveOffers?: number;
  buyNowSales?: number;
  recordedValue?: CurrencyAmountDto[] | null;
  buyNowValue?: CurrencyAmountDto[] | null;
}

export interface AdminDashboardTrendPointDto {
  date?: string;
  pointsIssued?: number;
  pointsRedeemed?: number;
  transactions?: number;
  newCustomers?: number;
  newBusinesses?: number;
  recordedValue?: CurrencyAmountDto[] | null;
}

export interface BusinessDashboardSummaryDto {
  timeZoneId?: string;
  today?: string;
  from?: string;
  to?: string;
  todaySummary?: BusinessTodaySummaryDto;
  activeMembers?: number;
  newMembers?: number;
  returningMembers?: number;
  pointsIssued?: number;
  pointsRedeemed?: number;
  redemptionRate?: number | null;
  transactions?: number;
  purchasesWithAmount?: number;
  valueCoveragePercent?: number | null;
  recordedValue?: CurrencyAmountDto[];
  buyNowSales?: number;
  buyNowValue?: CurrencyAmountDto[];
  pendingBuyNowOrders?: number;
  liveOffers?: number;
}

export interface BusinessDashboardTrendPointDto {
  date?: string;
  pointsIssued?: number;
  pointsRedeemed?: number;
  transactions?: number;
  recordedValue?: CurrencyAmountDto[];
  buyNowSales?: number;
  buyNowValue?: CurrencyAmountDto[];
}

export interface BusinessInsightDto {
  kind?: BusinessInsightKind;
  severity?: DashboardInsightSeverity;
  relatedId?: string | null;
  nameAr?: string | null;
  nameEn?: string | null;
  count?: number | null;
  hour?: number | null;
  percent?: number | null;
}

export interface BusinessOfferPerformanceDto {
  offerId?: string;
  titleAr?: string;
  titleEn?: string;
  status?: SmartOfferStatus;
  placed?: number;
  pending?: number;
  completed?: number;
  lapsed?: number;
  completionRate?: number | null;
  completedValue?: CurrencyAmountDto[];
  lastOrderAt?: string | null;
}

export interface BusinessTodaySummaryDto {
  transactions?: number;
  pointsIssued?: number;
  pointsRedeemed?: number;
  customersServed?: number;
  newCustomers?: number;
  returningCustomers?: number;
  buyNowSales?: number;
  recordedValue?: CurrencyAmountDto[];
  buyNowValue?: CurrencyAmountDto[];
}

export interface DashboardRangeDto {
  from?: string | null;
  to?: string | null;
}

export interface PeakHourCellDto {
  dayOfWeek?: number;
  hour?: number;
  activity?: number;
}

export interface TopBusinessDto {
  tenantId?: string;
  name?: string;
  pointsIssued?: number;
  transactions?: number;
}

export interface TopOfferDto {
  offerId?: string;
  tenantId?: string;
  tenantName?: string;
  titleAr?: string;
  titleEn?: string;
  completed?: number;
  completedValue?: CurrencyAmountDto[] | null;
}
