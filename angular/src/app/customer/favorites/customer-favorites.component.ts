import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { LocalizedNamePipe } from '../../shared/pipes/localized-name.pipe';
import { environment } from '../../../environments/environment';
import { FollowsService } from '../../proxy/controllers/follows.service';
import { CustomerBusinessService } from '../../proxy/controllers/customer-business.service';
import type { CustomerBusinessDto } from '../../proxy/businesses/models';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';

/**
 * "Favorites" — businesses followed via `FollowsService`, independent of membership (you can follow a
 * business without joining its loyalty program, and vice versa). `FollowDto` only carries a bare
 * `tenantId` (confirmed by reading it), so names/logos are resolved in one batch call via
 * `CustomerBusinessService.getMany` rather than one request per row.
 */
@Component({
  selector: 'app-customer-favorites',
  templateUrl: './customer-favorites.component.html',
  styleUrls: ['./customer-favorites.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, LocalizationPipe, LocalizedNamePipe, SkeletonListComponent, EmptyStateComponent, ErrorStateComponent],
})
export class CustomerFavoritesComponent implements OnInit {
  private readonly followsService = inject(FollowsService);
  private readonly customerBusinessService = inject(CustomerBusinessService);

  protected readonly businesses = signal<CustomerBusinessDto[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly unfollowingIds = signal<Set<string>>(new Set());
  protected readonly logoFailedIds = signal<Set<string>>(new Set());

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected logoUrl(business: CustomerBusinessDto): string | null {
    if (!business.hasLogo || this.logoFailedIds().has(business.tenantId)) return null;
    return `${environment.apis.default.url}/api/app/business/${business.businessProfileId}/logo?v=${business.logoBlobName ?? ''}`;
  }

  protected markLogoFailed(tenantId: string): void {
    this.logoFailedIds.update(ids => new Set(ids).add(tenantId));
  }

  protected unfollow(tenantId: string): void {
    if (this.unfollowingIds().has(tenantId)) return;
    this.unfollowingIds.update(ids => new Set(ids).add(tenantId));
    this.followsService.unfollow(tenantId).subscribe({
      next: () => this.businesses.update(list => list.filter(b => b.tenantId !== tenantId)),
      error: () => {
        this.unfollowingIds.update(ids => {
          const next = new Set(ids);
          next.delete(tenantId);
          return next;
        });
      },
    });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    this.followsService.getMyFollows().subscribe({
      next: follows => {
        const tenantIds = follows.map(f => f.tenantId).filter((id): id is string => !!id);
        if (tenantIds.length === 0) {
          this.businesses.set([]);
          this.isLoading.set(false);
          return;
        }
        this.customerBusinessService.getMany({ tenantIds }).subscribe({
          next: businesses => {
            this.businesses.set(businesses);
            this.isLoading.set(false);
          },
          error: () => {
            this.isLoading.set(false);
            this.loadFailed.set(true);
          },
        });
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }
}
