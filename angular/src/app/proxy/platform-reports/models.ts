import type { SupportTicketPriority } from '../platform/support-ticket-priority.enum';
import type { SupportTicketStatus } from '../platform/support-ticket-status.enum';

export interface TenantGrowthPointDto {
  year: number;
  month: number;
  newTenants: number;
}

export interface SupportTicketMetricsDto {
  totalOpen: number;
  countByStatus: Record<SupportTicketStatus, number>;
  countByPriority: Record<SupportTicketPriority, number>;
}
