import type { AuditedEntityDto } from '@abp/ng.core';
import type { MembershipStatus } from './membership-status.enum';

export interface JoinBusinessDto {
  tenantId: string;
  referralCode?: string | null;
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
