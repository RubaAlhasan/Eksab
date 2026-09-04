import type { AuditedEntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { InvoiceStatus } from './invoice-status.enum';
import type { PaymentStatus } from './payment-status.enum';
import type { TenantSubscriptionStatus } from './tenant-subscription-status.enum';

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
  approxMrr: number;
}

export interface ChangePlanDto {
  planId: string;
}

export interface CreateUpdateSubscriptionPlanDto {
  name: string;
  monthlyPrice?: number;
  featureLimitsJson?: string;
  isTrialDefault?: boolean;
}

export interface InvoiceDto extends AuditedEntityDto<string> {
  tenantSubscriptionId?: string;
  amount?: number;
  status?: InvoiceStatus;
  dueDate?: string;
  paidAt?: string | null;
}

export interface MrrTrendPointDto {
  year: number;
  month: number;
  amount: number;
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
  monthlyPrice?: number;
  featureLimitsJson?: string;
  isTrialDefault?: boolean;
}

export interface TenantSubscriptionDto extends AuditedEntityDto<string> {
  tenantId?: string | null;
  planId?: string;
  planName?: string | null;
  startDate?: string;
  renewalDate?: string;
  status?: TenantSubscriptionStatus;
}

export interface UsageDto {
  branchCount?: number;
  maxBranches?: number;
}
