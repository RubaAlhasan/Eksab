import { mapEnumToOptions } from '@abp/ng.core';

export enum DashboardInsightSeverity {
  Info = 0,
  Warning = 1,
}

export const dashboardInsightSeverityOptions = mapEnumToOptions(DashboardInsightSeverity);
