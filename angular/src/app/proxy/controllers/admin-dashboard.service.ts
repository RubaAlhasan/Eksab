import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { AdminActivityItemDto, AdminAlertsDto, AdminDashboardSummaryDto, AdminDashboardTrendPointDto, DashboardRangeDto, TopBusinessDto, TopOfferDto } from '../dashboards/models';

@Injectable({
  providedIn: 'root',
})
export class AdminDashboardService {
  private restService = inject(RestService);
  apiName = 'Default';


  getActivity = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, AdminActivityItemDto[]>({
      method: 'GET',
      url: '/api/app/admin-dashboard/activity',
    },
    { apiName: this.apiName,...config });


  getAlerts = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, AdminAlertsDto>({
      method: 'GET',
      url: '/api/app/admin-dashboard/alerts',
    },
    { apiName: this.apiName,...config });


  getSummary = (input: DashboardRangeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AdminDashboardSummaryDto>({
      method: 'GET',
      url: '/api/app/admin-dashboard/summary',
      params: { from: input.from, to: input.to },
    },
    { apiName: this.apiName,...config });


  getTopBusinesses = (input: DashboardRangeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TopBusinessDto[]>({
      method: 'GET',
      url: '/api/app/admin-dashboard/top-businesses',
      params: { from: input.from, to: input.to },
    },
    { apiName: this.apiName,...config });


  getTopOffers = (input: DashboardRangeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TopOfferDto[]>({
      method: 'GET',
      url: '/api/app/admin-dashboard/top-offers',
      params: { from: input.from, to: input.to },
    },
    { apiName: this.apiName,...config });


  getTrends = (input: DashboardRangeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AdminDashboardTrendPointDto[]>({
      method: 'GET',
      url: '/api/app/admin-dashboard/trends',
      params: { from: input.from, to: input.to },
    },
    { apiName: this.apiName,...config });

}
