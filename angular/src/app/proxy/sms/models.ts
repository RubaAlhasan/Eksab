import type { EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface SmsLogDto extends EntityDto<string> {
  phoneNumber: string;
  message: string;
  creationTime: string;
}

export interface AdminSmsLogFilterDto extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
}
