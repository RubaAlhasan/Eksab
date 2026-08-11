import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { AdminTenantsService } from '../../proxy/controllers/admin-tenants.service';
import { CategoriesService } from '../../proxy/controllers/categories.service';
import { AdminSubscriptionsService } from '../../proxy/controllers/admin-subscriptions.service';
import { BusinessService } from '../../proxy/controllers/business.service';
import type { AdminTenantDto } from '../../proxy/businesses/models';
import type { CategoryDto } from '../../proxy/platform/models';
import { TenantApprovalStatus } from '../../proxy/business-profiles/tenant-approval-status.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SearchInputComponent } from '../../shared/components/search-input/search-input.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

/**
 * Admin Portal > Businesses — Host-realm screen listing every business on the platform, with
 * approve/suspend actions and a row link to admin-business-details.component.ts. Mirrors
 * prototype/admin/businesses.html's table shape (see below) but wired against the real,
 * already-implemented backend (`AdminTenantsController` / `IAdminTenantAppService`) rather than
 * static demo data.
 *
 * Refactored to use the shared/components/* extracted from this page (see admin-portal-backend-
 * readiness.md's Shared Component Inventory) — this is the reference example every subsequent
 * Admin list page (Categories, Subscriptions, ...) should follow.
 *
 * Category/Plan/Members columns added to match the prototype's table shape — but only where the data
 * is genuinely real, not the prototype's exact fabricated demo values:
 * - Category: real, resolved from `CategoryId` via `CategoriesService` ([AllowAnonymous] read, bulk
 *   loaded once — same "load a bounded batch, fall back gracefully" pattern as everywhere else).
 * - Members: real — `AdminTenantDto.MemberCount`, a new server-computed field (`AdminTenantAppService`,
 *   active-`Membership` count per tenant via the same `IDataFilter.Disable<IMultiTenant>()` pattern
 *   already used for the rest of this DTO — see AdminTenantAppService.cs).
 * - Plan: real, but best-effort — bulk `AdminSubscriptionsService.getList` mapped by `tenantId`.
 *   `AdminSubscriptionsController` has NO lesser read than `Eksabli.Billing.ManagePlatform` for the
 *   whole controller (confirmed — see admin-subscriptions.component.ts's own comment), so a viewer
 *   without that permission simply sees "—" for every row rather than the column erroring or a 403
 *   toast; the lookup call itself is skipped entirely if `canViewPlans` is false.
 * `[MISSING BACKEND CAPABILITY]`: no Plan **filter** dropdown — `AdminTenantFilterDto` has no `planId`
 * field, and filtering already-paginated server results client-side would silently show wrong counts;
 * not implemented. The prototype's "Trialing" status option is also not reproduced — that's really
 * `TenantSubscriptionStatus` (shown correctly on the Subscriptions page), not a real
 * `BusinessProfile.ApprovalStatus` value (only Pending/Approved/Suspended exist).
 *
 * "New Business" action wired to a REAL, already-implemented endpoint —
 * `BusinessController.RegisterAsync` (`[AllowAnonymous]`, the same one the public self-service signup
 * flow uses) — not a new API. One call provisions the Tenant, owner IdentityUser, BusinessProfile,
 * first Branch, and a trial TenantSubscription together (see BusinessAppService.RegisterAsync). Gated
 * client-side on `Eksabli.Tenants.Approve` rather than the page's own `Tenants.View` — the endpoint
 * itself has no server-side permission check at all (by design, for public signup), but minting a real
 * tenant + owner login is meaningfully more privileged than merely viewing the list, and there's no
 * dedicated `Tenants.Create` permission defined to reach for instead; `.Approve` is the closest real
 * "trusted with business lifecycle" signal already in EksabliPermissions.
 */
@Component({
  selector: 'app-admin-tenants',
  templateUrl: './admin-tenants.component.html',
  styleUrls: ['./admin-tenants.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    LocalizationPipe,
    PageHeaderComponent,
    SearchInputComponent,
    StatusBadgeComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
    ModalComponent,
  ],
})
export class AdminTenantsComponent implements OnInit {
  private readonly adminTenantsService = inject(AdminTenantsService);
  private readonly categoriesService = inject(CategoriesService);
  private readonly subscriptionsService = inject(AdminSubscriptionsService);
  private readonly businessService = inject(BusinessService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

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

  protected readonly canViewPlans = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Billing.ManagePlatform'));
  protected readonly canCreate = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Tenants.Approve'));

  /** Bulk lookups, loaded once — same "load a bounded batch, fall back on failure" shape used
   *  throughout the Admin Portal (see admin-subscriptions.component.ts's tenantNames). */
  protected readonly categories = signal<CategoryDto[]>([]);
  private readonly categoryNames = signal<Map<string, string>>(new Map());
  private readonly planNames = signal<Map<string, string>>(new Map());

  protected readonly createModalOpen = signal(false);
  protected readonly isCreating = signal(false);

  protected readonly createForm = new FormGroup({
    businessName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    categoryId: new FormControl('', { nonNullable: true }),
    descriptionEn: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    descriptionAr: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    website: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(256)] }),
    // Stored server-side as BusinessProfile.SocialLinksJson (a freeform blob, no fixed schema) —
    // these are just the two networks this form happens to collect, see RegisterBusinessDto.cs.
    instagramUrl: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(256)] }),
    facebookUrl: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(256)] }),
    branchName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    branchAddress: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(512)] }),
    branchPhone: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(32)] }),
    // Captured now so a future "nearest branch" feature has data to work with — nothing in the
    // backend computes distance from these yet (checked), this form only records the coordinates.
    branchLatitude: new FormControl('', { nonNullable: true, validators: [Validators.min(-90), Validators.max(90)] }),
    branchLongitude: new FormControl('', { nonNullable: true, validators: [Validators.min(-180), Validators.max(180)] }),
    ownerEmail: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    ownerPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(6), Validators.maxLength(128)] }),
  });

  ngOnInit(): void {
    this.loadCategoryNames();
    if (this.canViewPlans()) this.loadPlanNames();
    this.load();
  }

  protected initials(name: string | undefined): string {
    if (!name) return '?';
    return name
      .split(' ')
      .filter(Boolean)
      .map((word) => word[0])
      .join('')
      .slice(0, 2)
      .toUpperCase();
  }

  protected categoryName(categoryId: string | null | undefined): string {
    if (!categoryId) return '—';
    return this.categoryNames().get(categoryId) ?? '—';
  }

  protected planName(tenantId: string | undefined): string {
    if (!tenantId) return '—';
    return this.planNames().get(tenantId) ?? '—';
  }

  protected openCreateModal(): void {
    this.createForm.reset({
      businessName: '',
      categoryId: '',
      descriptionEn: '',
      descriptionAr: '',
      website: '',
      instagramUrl: '',
      facebookUrl: '',
      branchName: '',
      branchAddress: '',
      branchPhone: '',
      branchLatitude: '',
      branchLongitude: '',
      ownerEmail: '',
      ownerPassword: '',
    });
    this.createModalOpen.set(true);
  }

  protected closeCreateModal(): void {
    this.createModalOpen.set(false);
  }

  protected submitCreateForm(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }

    const value = this.createForm.getRawValue();
    this.isCreating.set(true);
    this.businessService
      .register({
        businessName: value.businessName,
        categoryId: value.categoryId || null,
        descriptionEn: value.descriptionEn || null,
        descriptionAr: value.descriptionAr || null,
        website: value.website || null,
        instagramUrl: value.instagramUrl || null,
        facebookUrl: value.facebookUrl || null,
        branchName: value.branchName,
        branchAddress: value.branchAddress || null,
        branchPhone: value.branchPhone || null,
        branchLatitude: value.branchLatitude === '' ? null : Number(value.branchLatitude),
        branchLongitude: value.branchLongitude === '' ? null : Number(value.branchLongitude),
        ownerEmail: value.ownerEmail,
        ownerPassword: value.ownerPassword,
      })
      .subscribe({
        next: () => {
          this.isCreating.set(false);
          this.createModalOpen.set(false);
          this.toaster.success('::AdminPanel:Businesses:CreatedMessage');
          this.pageIndex.set(0);
          this.load();
        },
        error: () => {
          this.isCreating.set(false);
          this.toaster.error('::AdminPanel:Businesses:CreateErrorMessage');
        },
      });
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

  // Also the reactivation path for a Suspended business — BusinessProfile.Approve() (backend) is
  // explicitly documented as doing double duty ("no separate Reactivate() method to keep in sync with
  // this one"), so the template's Approve button shows for anything !== Approved, not just Pending
  // (fixed here to match admin-business-details.component.html's already-correct condition — this
  // list page previously used `=== Pending`, which left a Suspended row with no action to undo it).
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

  private loadCategoryNames(): void {
    this.categoriesService.getList({ parentCategoryId: null, filterText: null, skipCount: 0, maxResultCount: 500 }).subscribe({
      next: (result) => {
        const items = result.items ?? [];
        const map = new Map<string, string>();
        for (const category of items) {
          if (category.id) map.set(category.id, category.nameEn ?? category.id);
        }
        this.categories.set(items);
        this.categoryNames.set(map);
      },
      // Best-effort — rows just fall back to the em dash, and the create form's category dropdown
      // just stays empty (category is optional on RegisterBusinessDto) — doesn't block the page.
      error: () => undefined,
    });
  }

  private loadPlanNames(): void {
    this.subscriptionsService.getList({ status: null, sorting: undefined, skipCount: 0, maxResultCount: 500 }).subscribe({
      next: (result) => {
        const map = new Map<string, string>();
        for (const subscription of result.items ?? []) {
          if (subscription.tenantId && subscription.planName) map.set(subscription.tenantId, subscription.planName);
        }
        this.planNames.set(map);
      },
      // Best-effort — a viewer without Billing.ManagePlatform (or a fetch failure) just sees "—" in
      // the Plan column for every row, doesn't block the businesses list itself.
      error: () => undefined,
    });
  }
}
