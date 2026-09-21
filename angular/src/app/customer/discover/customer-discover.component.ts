import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { environment } from '../../../environments/environment';
import { CategoriesService } from '../../proxy/controllers/categories.service';
import { CustomerBusinessService } from '../../proxy/controllers/customer-business.service';
import type { CategoryDto } from '../../proxy/platform/models';
import type { CustomerBusinessDto } from '../../proxy/businesses/models';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';

const PAGE_SIZE = 10;
const SEARCH_DEBOUNCE_MS = 350;

/**
 * Merges the prototype's separate search.html and nearby-stores.html into one screen — both are the
 * same `CustomerBusinessService.getList` query (search text + category + optional geolocation), just
 * with different starting emphasis, so building two near-duplicate pages against the identical endpoint
 * would be pure churn. No map view (list only, per product decision) — the prototype's own map is a
 * non-functional placeholder anyway; a real map SDK integration is a separate task if wanted later.
 *
 * Geolocation is opt-in via an explicit "Near Me" toggle, not requested automatically on load — the
 * browser's permission prompt firing the instant a customer opens Search would be a worse first
 * impression than a plain alphabetical list with a button to switch to distance-sorted.
 */
@Component({
  selector: 'app-customer-discover',
  templateUrl: './customer-discover.component.html',
  styleUrls: ['./customer-discover.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DecimalPipe, LocalizationPipe, LoadingSpinnerComponent, EmptyStateComponent, ErrorStateComponent, PaginationComponent],
})
export class CustomerDiscoverComponent implements OnInit {
  private readonly categoriesService = inject(CategoriesService);
  private readonly customerBusinessService = inject(CustomerBusinessService);

  protected readonly categories = signal<CategoryDto[]>([]);
  protected readonly selectedCategoryId = signal<string | null>(null);
  protected readonly searchText = signal('');
  protected readonly nearMeEnabled = signal(false);
  protected readonly locationDenied = signal(false);
  private coordinates: { latitude: number; longitude: number } | null = null;

  protected readonly businesses = signal<CustomerBusinessDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / PAGE_SIZE)));

  private searchDebounceHandle: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    this.categoriesService.getList({ parentCategoryId: null, filterText: null, sorting: 'nameEn asc', skipCount: 0, maxResultCount: 50 }).subscribe({
      next: result => this.categories.set(result.items ?? []),
      error: () => undefined,
    });
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected onSearchInput(event: Event): void {
    this.searchText.set((event.target as HTMLInputElement).value);
    if (this.searchDebounceHandle !== null) clearTimeout(this.searchDebounceHandle);
    this.searchDebounceHandle = setTimeout(() => {
      this.pageIndex.set(0);
      this.load();
    }, SEARCH_DEBOUNCE_MS);
  }

  protected selectCategory(categoryId: string | null): void {
    if (this.selectedCategoryId() === categoryId) return;
    this.selectedCategoryId.set(categoryId);
    this.pageIndex.set(0);
    this.load();
  }

  protected toggleNearMe(): void {
    if (this.nearMeEnabled()) {
      this.nearMeEnabled.set(false);
      this.coordinates = null;
      this.pageIndex.set(0);
      this.load();
      return;
    }

    if (!('geolocation' in navigator)) {
      this.locationDenied.set(true);
      return;
    }

    navigator.geolocation.getCurrentPosition(
      position => {
        this.coordinates = { latitude: position.coords.latitude, longitude: position.coords.longitude };
        this.locationDenied.set(false);
        this.nearMeEnabled.set(true);
        this.pageIndex.set(0);
        this.load();
      },
      () => this.locationDenied.set(true),
    );
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  // Per-card fallback state (a Set keyed by tenantId, not a single flag) — this is a list of many
  // businesses, unlike admin-business-details.component.ts's single-item `logoFailed` signal.
  protected readonly logoFailedIds = signal<Set<string>>(new Set());

  protected logoUrl(business: CustomerBusinessDto): string | null {
    if (!business.hasLogo || this.logoFailedIds().has(business.tenantId)) return null;
    return `${environment.apis.default.url}/api/app/business/${business.businessProfileId}/logo?v=${business.logoBlobName ?? ''}`;
  }

  protected markLogoFailed(tenantId: string): void {
    this.logoFailedIds.update(ids => new Set(ids).add(tenantId));
  }

  protected categoryName(business: CustomerBusinessDto): string | null {
    return business.categoryNameEn ?? null;
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    this.customerBusinessService
      .getList({
        filterText: this.searchText() || null,
        categoryId: this.selectedCategoryId(),
        latitude: this.coordinates?.latitude ?? null,
        longitude: this.coordinates?.longitude ?? null,
        skipCount: this.pageIndex() * PAGE_SIZE,
        maxResultCount: PAGE_SIZE,
      })
      .subscribe({
        next: result => {
          this.businesses.set(result.items ?? []);
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
