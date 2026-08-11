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

export interface AuditLogActionDto {
  serviceName?: string | null;
  methodName?: string | null;
  parameters?: string | null;
  executionTime?: string;
  executionDuration?: number;
}

export interface AuditLogEntityPropertyChangeDto {
  propertyName?: string | null;
  originalValue?: string | null;
  newValue?: string | null;
}

export interface AuditLogEntityChangeDto {
  entityTypeFullName?: string | null;
  entityId?: string | null;
  changeType?: number;
  changeTime?: string;
  propertyChanges: AuditLogEntityPropertyChangeDto[];
}

export interface AuditLogDetailDto extends AuditLogDto {
  comments?: string | null;
  exceptions?: string | null;
  actions: AuditLogActionDto[];
  entityChanges: AuditLogEntityChangeDto[];
}
