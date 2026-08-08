import { mapEnumToOptions } from '@abp/ng.core';

export enum DevicePlatform {
  iOS = 0,
  Android = 1,
  Web = 2,
}

export const devicePlatformOptions = mapEnumToOptions(DevicePlatform);
