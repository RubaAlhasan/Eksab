import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AdminUserType } from './admin-user-type.enum';
import type { SupportTicketPriority } from './support-ticket-priority.enum';
import type { SupportTicketStatus } from './support-ticket-status.enum';

export interface AddSupportTicketMessageDto {
  body: string;
}

export interface AdminUserDto {
  id?: string;
  type?: AdminUserType;
  firstName?: string | null;
  lastName?: string | null;
  businessName?: string | null;
  contact?: string | null;
  isActive: boolean;
  creationTime?: string;
}

export interface AdminUserFilterDto extends PagedAndSortedResultRequestDto {
  filterText?: string | null;
  type?: AdminUserType | null;
}

export interface CategoryDto extends FullAuditedEntityDto<string> {
  nameAr?: string;
  nameEn?: string;
  iconBlobName?: string | null;
  parentCategoryId?: string | null;
  businessCount: number;
}

export interface CategoryListFilterDto extends PagedAndSortedResultRequestDto {
  parentCategoryId?: string | null;
  filterText?: string | null;
}

export interface CreateSupportTicketDto {
  subject: string;
  body: string;
  priority?: SupportTicketPriority;
}

export interface CreateUpdateCategoryDto {
  nameAr: string;
  nameEn: string;
  iconBlobName?: string | null;
  parentCategoryId?: string | null;
}

export interface SupportTicketDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  customerId?: string | null;
  subject?: string;
  status?: SupportTicketStatus;
  priority?: SupportTicketPriority;
  messages?: SupportTicketMessageDto[];
}

export interface SupportTicketFilterDto extends PagedAndSortedResultRequestDto {
  status?: SupportTicketStatus | null;
  priority?: SupportTicketPriority | null;
  tenantId?: string | null;
}

export interface SupportTicketMessageDto {
  id?: string;
  ticketId?: string;
  senderId?: string;
  body?: string;
  createdAt?: string;
}
