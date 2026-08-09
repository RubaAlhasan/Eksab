import type { EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AuditLogDto extends EntityDto<string> {
  userId?: string | null;
  userName?: string | null;
  tenantId?: string | null;
  tenantName?: string | null;
  applicationName?: string | null;
  executionTime?: string;
  executionDuration?: number;
  clientIpAddress?: string | null;
  httpMethod?: string | null;
  url?: string | null;
  httpStatusCode?: number | null;
  hasException?: boolean;
}

export interface AdminAuditLogFilterDto extends PagedAndSortedResultRequestDto {
  startTime?: string | null;
  endTime?: string | null;
  httpMethod?: string | null;
  url?: string | null;
  userName?: string | null;
  hasException?: boolean | null;
  httpStatusCode?: number | null;
}
