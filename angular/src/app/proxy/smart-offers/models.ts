import type { AuditedEntityDto, EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { CustomerDealOrderFilter } from './customer-deal-order-filter.enum';
import type { Currency } from '../shared/currency.enum';
import type { SmartOfferOrderStatus } from './smart-offer-order-status.enum';
import type { SmartOfferStatus } from './smart-offer-status.enum';
import type { SmartPricingStrategy } from './smart-pricing-strategy.enum';

export interface CreateUpdateSmartOfferDto {
  titleAr: string;
  titleEn: string;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  strategy: SmartPricingStrategy;
  currency: Currency;
  basePrice: number;
  minimumPrice?: number | null;
  dailyQuantity?: number | null;
  timeZoneId: string;
  validFrom?: string | null;
  validTo?: string | null;
  isEnabled: boolean;
  stages: CreateUpdateSmartOfferStageDto[];
}

export interface CreateUpdateSmartOfferStageDto {
  id?: string | null;
  startTime: string;
  endTime: string;
  price: number;
  currency: Currency;
  quantityLimit?: number | null;
}

export interface SetSmartOfferEnabledDto {
  isEnabled: boolean;
}

export interface SmartOfferDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  titleAr?: string;
  titleEn?: string;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  strategy?: SmartPricingStrategy;
  currency?: Currency;
  basePrice?: number;
  minimumPrice?: number | null;
  dailyQuantity?: number | null;
  timeZoneId?: string;
  validFrom?: string | null;
  validTo?: string | null;
  isEnabled?: boolean;
  status?: SmartOfferStatus;
  stages?: SmartOfferStageDto[];
  currentPrice?: number | null;
  currentStageId?: string | null;
  remainingNow?: number | null;
  serverNowUtc?: string;
  nextChangeAtUtc?: string | null;
  nextPrice?: number | null;
}

export interface SmartOfferStageDto extends EntityDto<string> {
  startTime?: string;
  endTime?: string;
  price?: number;
  currency?: Currency;
  quantityLimit?: number | null;
  remainingToday?: number | null;
}

export interface CustomerSmartOfferDto extends EntityDto<string> {
  tenantId?: string | null;
  titleAr?: string;
  titleEn?: string;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  strategy?: SmartPricingStrategy;
  currency?: Currency;
  basePrice?: number;
  status?: SmartOfferStatus;
  isAvailableNow?: boolean;
  currentPrice?: number | null;
  discountPercent?: number | null;
  currentStageStartTime?: string | null;
  currentStageEndTime?: string | null;
  currentStageEndsAtUtc?: string | null;
  remainingNow?: number | null;
  maxOrderQuantity?: number;
  nextChangeAtUtc?: string | null;
  nextChangeLocalTime?: string | null;
  nextChangeIsTomorrow?: boolean;
  nextPrice?: number | null;
  validTo?: string | null;
  serverNowUtc?: string;
  businessName?: string | null;
  isMember?: boolean;
  myPendingOrder?: SmartOfferOrderDto | null;
}

export interface CustomerSmartOfferOrderDto extends SmartOfferOrderDto {
  tenantId?: string | null;
  businessName?: string | null;
  offerDescriptionAr?: string | null;
  offerDescriptionEn?: string | null;
}

export interface CustomerSmartOfferDetailsDto extends EntityDto<string> {
  tenantId?: string;
  businessName?: string;
  titleAr?: string;
  titleEn?: string;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  currency?: Currency;
  basePrice?: number;
}

export interface GetMySmartOfferOrdersInput extends PagedAndSortedResultRequestDto {
  filter?: CustomerDealOrderFilter;
}

export interface CustomerSmartOfferListDto {
  items: CustomerSmartOfferDto[];
}

export interface PlaceSmartOfferOrderDto {
  tenantId: string;
  smartOfferId: string;
  quantity: number;
}

export interface SmartOfferOrderDto extends AuditedEntityDto<string> {
  smartOfferId?: string;
  offerTitleAr?: string;
  offerTitleEn?: string;
  code?: string;
  quantity?: number;
  unitPrice?: number;
  basePrice?: number;
  totalAmount?: number;
  currency?: Currency;
  status?: SmartOfferOrderStatus;
  serviceDate?: string;
  placedAt?: string;
  reservationExpiresAt?: string;
  completedAt?: string | null;
  rejectionReason?: string | null;
  serverNowUtc?: string;
}

export interface SmartOfferOrderStaffDto extends SmartOfferOrderDto {
  customerName?: string | null;
  customerPhone?: string | null;
  offerDescriptionAr?: string | null;
  offerDescriptionEn?: string | null;
}

export interface SmartOfferOrderCodeDto {
  code: string;
}

export interface RejectSmartOfferOrderDto extends SmartOfferOrderCodeDto {
  reason?: string | null;
}
