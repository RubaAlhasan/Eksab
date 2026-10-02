import type { AuditedEntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { InvoiceStatus } from './invoice-status.enum';
import type { PaymentStatus } from './payment-status.enum';
import type { TenantSubscriptionStatus } from './tenant-subscription-status.enum';
import type { Currency } from '../shared/currency.enum';

export interface AdminInvoiceFilterDto extends PagedAndSortedResultRequestDto {
  status?: InvoiceStatus | null;
  tenantSubscriptionId?: string | null;
}

export interface AdminPaymentFilterDto extends PagedAndSortedResultRequestDto {
  invoiceId?: string | null;
  status?: PaymentStatus | null;
}

export interface AdminSubscriptionFilterDto extends PagedAndSortedResultRequestDto {
  status?: TenantSubscriptionStatus | null;
  tenantId?: string | null;
}

export interface AdminSubscriptionStatsDto {
  activeCount: number;
  trialingCount: number;
  // Never summed across currencies — the platform applies no SYP<->USD conversion anywhere.
  approxMrrByCurrency: CurrencyAmountDto[];
}

export interface ChangePlanDto {
  planId: string;
}

export interface CreateUpdateSubscriptionPlanDto {
  name: string;
  monthlyPriceSyp?: number;
  monthlyPriceUsd?: number;
  featureLimitsJson?: string;
  isTrialDefault?: boolean;
}

// One currency's worth of a money aggregate — see AdminSubscriptionStatsDto.approxMrrByCurrency /
// MrrTrendPointDto.amountsByCurrency for why this is never combined across currencies.
export interface CurrencyAmountDto {
  currency?: Currency;
  amount?: number;
}

export interface InvoiceDto extends AuditedEntityDto<string> {
  tenantSubscriptionId?: string;
  amount?: number;
  currency?: Currency;
  status?: InvoiceStatus;
  dueDate?: string;
  paidAt?: string | null;
}

export interface MrrTrendPointDto {
  year: number;
  month: number;
  // Only lists currencies with at least one paid invoice that month — never a forced zero entry.
  amountsByCurrency: CurrencyAmountDto[];
}

export interface PaymentDto extends AuditedEntityDto<string> {
  invoiceId?: string;
  provider?: string;
  providerTransactionRef?: string | null;
  status?: PaymentStatus;
}

export interface RecordManualPaymentDto {
  invoiceId: string;
  providerTransactionRef?: string | null;
}

export interface SubscriptionPlanDto extends FullAuditedEntityDto<string> {
  name?: string;
  monthlyPriceSyp?: number;
  monthlyPriceUsd?: number;
  featureLimitsJson?: string;
  isTrialDefault?: boolean;
}

export interface TenantSubscriptionDto extends AuditedEntityDto<string> {
  tenantId?: string | null;
  planId?: string;
  planName?: string | null;
  currency?: Currency;
  startDate?: string;
  renewalDate?: string;
  status?: TenantSubscriptionStatus;
  pendingPlanId?: string | null;
  pendingPlanName?: string | null;
  planChangeRequestedAt?: string | null;
}

export interface UsageDto {
  branchCount?: number;
  maxBranches?: number;
}
