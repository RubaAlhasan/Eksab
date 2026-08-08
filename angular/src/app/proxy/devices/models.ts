import type { AuditedEntityDto } from '@abp/ng.core';
import type { DevicePlatform } from './device-platform.enum';

export interface DeviceDto extends AuditedEntityDto<string> {
  customerId?: string;
  platform?: DevicePlatform;
  pushToken?: string | null;
  lastActiveAt?: string;
  appVersion?: string | null;
}

export interface RegisterDeviceDto {
  platform: DevicePlatform;
  pushToken: string;
  appVersion?: string | null;
}
