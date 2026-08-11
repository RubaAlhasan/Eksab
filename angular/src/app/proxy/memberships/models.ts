import type { AuditedEntityDto, EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { MembershipStatus } from './membership-status.enum';

export interface JoinBusinessDto {
  tenantId: string;
  referralCode?: string | null;
}

export interface MemberDto extends EntityDto<string> {
  customerId?: string;
  firstName?: string | null;
  lastName?: string | null;
  phoneNumber?: string | null;
  joinedAt?: string;
  status?: MembershipStatus;
  balance: number;
  tierId?: string | null;
  tierName?: string | null;
  lastActiveAt?: string | null;
}

export interface MemberFilterDto extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  tierId?: string | null;
  status?: MembershipStatus | null;
}

export interface MembershipDto extends AuditedEntityDto<string> {
  customerId?: string;
  tenantId?: string | null;
  joinedAt?: string;
  status?: MembershipStatus;
}

export interface WalletQrTokenResultDto {
  token?: string;
  expiresInSeconds?: number;
}
