import type { AuditedEntityDto } from '@abp/ng.core';
import type { DayOfWeek } from './day-of-week.enum';

export interface DayOpeningHoursDto {
  dayOfWeek: DayOfWeek;
  isClosed: boolean;
  // "HH:mm", e.g. "09:00". Required when isClosed is false. "24:00" is a valid close time (open until
  // midnight) — the same convention the backend's SmartOffers stage times use.
  openTime?: string | null;
  closeTime?: string | null;
}

export interface BranchDto extends AuditedEntityDto<string> {
  name?: string;
  address?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  phone?: string | null;
  openingHours: DayOpeningHoursDto[];
}

export interface CreateUpdateBranchDto {
  name: string;
  address?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  phone?: string | null;
  // At most one entry per day of week — the server validates and does the actual HH:mm checking.
  openingHours?: DayOpeningHoursDto[] | null;
}
