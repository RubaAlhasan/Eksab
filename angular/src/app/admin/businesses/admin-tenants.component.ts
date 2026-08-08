import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { AdminTenantsService } from '../../proxy/controllers/admin-tenants.service';
import type { AdminTenantDto } from '../../proxy/businesses/models';
import { TenantApprovalStatus } from '../../proxy/business-profiles/tenant-approval-status.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SearchInputComponent } from '../../shared/components/search-input/search-input.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';

/**
 * Admin Portal > Businesses (Tenants) — Host-realm screen listing every business (ABP Tenant)
 * on the platform, with approve/suspend actions. Mirrors prototype/admin/businesses.html
 * (and business-details.html for the detail drill-down, not yet built) but wired against the
 * real, already-implemented backend (`AdminTenantsController` / `IAdminTenantAppService`) rather
 * than static demo data.
 *
 * Refactored to use the shared/components/* extracted from this page (see admin-portal-backend-
 * readiness.md's Shared Component Inventory) — this is the reference example every subsequent
 * Admin list page (Categories, Subscriptions, ...) should follow.
 */
@Component({
  selector: 'app-admin-tenants',
  templateUrl: './admin-tenants.component.html',
  styleUrls: ['./admin-tenants.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    LocalizationPipe,
    PageHeaderComponent,
    SearchInputComponent,
    StatusBadgeComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
  ],
})
export class AdminTenantsComponent implements OnInit {
  private readonly adminTenantsService = inject(AdminTenantsService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);

  protected readonly ApprovalStatus = TenantApprovalStatus;
  private readonly pageSize = 10;

  protected readonly tenants = signal<AdminTenantDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly filterText = signal('');
  /** Kept as the <select>'s raw string value ('' | '0' | '1' | '2') to avoid enum/select value-coercion friction. */
  protected readonly statusFilterValue = signal('');
  protected readonly pageIndex = signal(0);

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  ngOnInit(): void {
    this.load();
  }

  protected onSearchInput(value: string): void {
    this.filterText.set(value);
    this.pageIndex.set(0);
    this.load();
  }

  protected onStatusFilterChange(event: Event): void {
    this.statusFilterValue.set((event.target as HTMLSelectElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected statusLabelKey(status: TenantApprovalStatus | undefined): string {
    switch (status) {
      case TenantApprovalStatus.Approved:
        return '::AdminPanel:Businesses:StatusApproved';
      case TenantApprovalStatus.Suspended:
        return '::AdminPanel:Businesses:StatusSuspended';
      default:
        return '::AdminPanel:Businesses:StatusPending';
    }
  }

  protected statusVariant(status: TenantApprovalStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case TenantApprovalStatus.Approved:
        return 'success';
      case TenantApprovalStatus.Suspended:
        return 'danger';
      default:
        return 'warning';
    }
  }

  protected approve(tenant: AdminTenantDto): void {
    if (!tenant.tenantId) return;
    this.confirmation
      .warn('::AdminPanel:Businesses:ApproveConfirmMessage', '::AdminPanel:Businesses:ApproveConfirmTitle')
      .subscribe((status) => {
        if (status !== Confirmation.Status.confirm || !tenant.tenantId) return;
        this.adminTenantsService.approve(tenant.tenantId).subscribe(() => {
          this.toaster.success('::AdminPanel:Businesses:ApprovedMessage');
          this.load();
        });
      });
  }

  protected suspend(tenant: AdminTenantDto): void {
    if (!tenant.tenantId) return;
    this.confirmation
      .warn('::AdminPanel:Businesses:SuspendConfirmMessage', '::AdminPanel:Businesses:SuspendConfirmTitle')
      .subscribe((status) => {
        if (status !== Confirmation.Status.confirm || !tenant.tenantId) return;
        this.adminTenantsService.suspend(tenant.tenantId).subscribe(() => {
          this.toaster.success('::AdminPanel:Businesses:SuspendedMessage');
          this.load();
        });
      });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    const approvalStatus =
      this.statusFilterValue() === '' ? null : (Number(this.statusFilterValue()) as TenantApprovalStatus);

    this.adminTenantsService
      .getList({
        filterText: this.filterText() || null,
        approvalStatus,
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
        sorting: 'creationTime desc',
      })
      .subscribe({
        next: (result) => {
          this.tenants.set(result.items ?? []);
          this.totalCount.set(result.totalCount ?? 0);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.loadFailed.set(true);
        },
      });
  }
}
