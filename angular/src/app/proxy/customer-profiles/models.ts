import type { AuditedEntityDto } from '@abp/ng.core';
import type { CustomerGender } from './customer-gender.enum';

export interface CustomerProfileDto extends AuditedEntityDto<string> {
  userId?: string;
  firstName?: string | null;
  lastName?: string | null;
  dateOfBirth?: string | null;
  gender?: CustomerGender;
}

export interface UpdateCustomerProfileDto {
  firstName?: string | null;
  lastName?: string | null;
  dateOfBirth?: string | null;
  gender?: CustomerGender;
}
