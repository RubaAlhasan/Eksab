import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import { AdminUsersService } from '../../proxy/controllers/admin-users.service';
import type { AdminUserDto } from '../../proxy/platform/models';
import { AdminUserType } from '../../proxy/platform/admin-user-type.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SearchInputComponent } from '../../shared/components/search-input/search-input.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';

type TypeFilter = 'all' | 'customer' | 'staff';

/**
 * Admin Portal > Users — mirrors prototype/admin/users.html's cross-tenant directory, backed by a
 * brand-new endpoint added this session (`AdminUserAppService.GetListAsync`,
 * `GET /api/app/admin-users`, `Eksabli.Users.View`) — no existing endpoint combined these two realms.
 * Real fields only:
 * - Customers: Host-realm `CustomerProfile` (name) + `IdentityUser.PhoneNumber` (contact). Every real
 *   registered customer, not just this tenant's — there's no "this tenant's customers" concept, a
 *   customer isn't owned by one business.
 * - Staff: cross-tenant `EmployeeAssignment` (`Disable<IMultiTenant>()`) + each assignment's own
 *   tenant-scoped `IdentityUser.Email` (contact) + that tenant's real `Tenant.Name` (business) — a
 *   genuine per-row lookup, NOT the prototype's own hardcoded "Cedar & Bean Coffee" for every employee.
 * - `Realm` is derived client-side from `Type` (Customer → Host, Staff → Tenant) — architecturally
 *   fixed by construction (see the backend comment on `AdminUserAppService`), not a separate field.
 * - `Status` is `IdentityUser.IsActive` — the same real field the stock Identity > Users page itself
 *   toggles. The prototype's "Invited" status doesn't exist anywhere in the backend (employee accounts
 *   are created active immediately, no pending-invite state) and is **not** reproduced — only
 *   Active/Inactive are offered.
 * - Staff accounts have no real display name anywhere in this codebase (`IdentityUser.Name`/`.Surname`
 *   are never set, confirmed by reading every place an employee `IdentityUser` is created) — `Name` is
 *   always null for a Staff row; the template falls back to showing their email instead of inventing a
 *   name or a generic "Unnamed" label (which would be actively misleading — email IS their real
 *   identifier). Customers fall back to the localized "Unnamed customer" text, same convention as the
 *   Customers page's `customerName()` helper.
 */
@Component({
  selector: 'app-admin-users',
  templateUrl: './admin-users.component.html',
  styleUrls: ['./admin-users.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
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
export class AdminUsersComponent implements OnInit {
  private readonly usersService = inject(AdminUsersService);

  protected readonly Type = AdminUserType;
  private readonly pageSize = 10;

  protected readonly users = signal<AdminUserDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly filterText = signal('');
  protected readonly typeFilter = signal<TypeFilter>('all');
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  ngOnInit(): void {
    this.load();
  }

  /** Null when there's no real name to show (customer profile left blank, or a staff row — see this
   *  component's file comment for why staff never has one) — template decides the fallback per type. */
  protected displayName(user: AdminUserDto): string | null {
    const name = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
    return name || null;
  }

  protected initials(user: AdminUserDto): string {
    const name = this.displayName(user);
    if (name) {
      return name
        .split(' ')
        .filter(Boolean)
        .map((word) => word[0])
        .join('')
        .slice(0, 2)
        .toUpperCase();
    }
    return (user.contact ?? '?').slice(0, 2).toUpperCase();
  }

  protected typeLabelKey(type: AdminUserType | undefined): string {
    return type === AdminUserType.Staff ? '::AdminPanel:Users:TypeStaff' : '::AdminPanel:Users:TypeCustomer';
  }

  protected typeVariant(type: AdminUserType | undefined): StatusBadgeVariant {
    return type === AdminUserType.Staff ? 'neutral' : 'info';
  }

  protected realmLabelKey(type: AdminUserType | undefined): string {
    return type === AdminUserType.Staff ? '::AdminPanel:Users:RealmTenant' : '::AdminPanel:Users:RealmHost';
  }

  protected statusLabelKey(isActive: boolean): string {
    return isActive ? '::AdminPanel:Users:StatusActive' : '::AdminPanel:Users:StatusInactive';
  }

  protected statusVariant(isActive: boolean): StatusBadgeVariant {
    return isActive ? 'success' : 'neutral';
  }

  protected onSearchInput(value: string): void {
    this.filterText.set(value);
    this.pageIndex.set(0);
    this.load();
  }

  protected selectType(type: TypeFilter): void {
    this.typeFilter.set(type);
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

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    const type =
      this.typeFilter() === 'all' ? null : this.typeFilter() === 'customer' ? AdminUserType.Customer : AdminUserType.Staff;

    this.usersService
      .getList({
        filterText: this.filterText() || null,
        type,
        sorting: undefined,
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.users.set(result.items ?? []);
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
