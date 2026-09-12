
export interface AwardPointsByCustomerIdDto {
  purchaseAmount?: number | null;
}

export interface AwardPointsByQrDto {
  qrToken: string;
  purchaseAmount?: number | null;
}

export interface AwardPointsResultDto {
  transactionId?: string;
  pointsAwarded?: number;
  newBalance?: number;
  newTierId?: string | null;
  newTierName?: string | null;
}

export interface ConfirmRedemptionDto {
  code: string;
  branchId?: string | null;
}

export interface CustomerLookupResultDto {
  customerId?: string;
  membershipId?: string;
  walletId?: string;
  balance?: number;
  firstName?: string | null;
  lastName?: string | null;
}

export interface LookupRedemptionDto {
  code: string;
}

export interface ManualAdjustDto {
  customerId: string;
  points: number;
  reason?: string | null;
}

export interface PhoneLookupDto {
  phoneNumber: string;
}

export interface RedemptionConfirmationDto {
  couponId?: string;
  rewardNameAr?: string | null;
  rewardNameEn?: string | null;
  redeemedAt?: string;
  pointsDebited?: number;
  newBalance?: number;
  customerName?: string | null;
}

export interface RedemptionLookupDto {
  couponId?: string;
  code?: string;
  rewardNameAr?: string | null;
  rewardNameEn?: string | null;
  pointsCost?: number;
  customerName?: string | null;
  customerPhone?: string | null;
  balanceAfterRedemption?: number;
  issuedAt?: string;
  reservationExpiresAt?: string | null;
  requiresManagerApproval?: boolean;
  canCurrentEmployeeApprove?: boolean;
}

export interface RedemptionRejectionDto {
  couponId?: string;
  rewardNameAr?: string | null;
  rewardNameEn?: string | null;
  pointsReleased?: number;
  newAvailableBalance?: number;
}

export interface RejectRedemptionDto {
  code: string;
  reason?: string | null;
}
